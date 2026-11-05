using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Infrastructure.Persistence;
using AccessControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AccessControl.Infrastructure.Tests;

/// <summary>Persistencia real de las sesiones con EF Core sobre SQLite en memoria (sin la API ni SQL Server).</summary>
public sealed class SesionRepositoryTests : IDisposable
{
    private static readonly TimeSpan Duracion = TimeSpan.FromDays(14);

    private readonly SqliteConnection _conexion = new("DataSource=:memory:");
    private readonly Usuario _usuario = new("Persona", new Email("persona@ejemplo.com"), "hash-guardado", Rol.Estandar);
    private readonly Usuario _otro = new("Otra", new Email("otra@ejemplo.com"), "hash-guardado", Rol.Estandar);

    public SesionRepositoryTests()
    {
        _conexion.Open();
        using var contexto = NuevoContexto();
        contexto.Database.EnsureCreated();
        contexto.usuarios.AddRange(_usuario, _otro);
        contexto.SaveChanges();
    }

    public void Dispose() => _conexion.Dispose();

    private ApplicationDbContext NuevoContexto() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_conexion).Options);

    private static string Hash(char c) => new(c, SesionUsuario.LongitudHash);

    private SesionUsuario Sesion(char hash, Usuario? dueno = null, DateTime? ahora = null) =>
        new((dueno ?? _usuario).Id, Hash(hash), ahora ?? DateTime.UtcNow, Duracion, "Firefox", "10.0.0.1");

    private async Task<SesionUsuario> Guardar(SesionUsuario sesion)
    {
        using var contexto = NuevoContexto();
        await new SesionRepository(contexto).AgregarAsync(sesion);
        return sesion;
    }

    private SesionUsuario? Leer(Guid id)
    {
        using var contexto = NuevoContexto();
        return contexto.SesionesUsuario.AsNoTracking().SingleOrDefault(s => s.Id == id);
    }

    // ---------- Guardar y buscar ----------

    [Fact]
    public async Task RF_CA_03_SeGuardaYSeBuscaPorElHashDelToken()
    {
        var sesion = await Guardar(Sesion('a'));

        using var contexto = NuevoContexto();
        var leida = await new SesionRepository(contexto).ObtenerPorHashAsync(Hash('a'));

        Assert.NotNull(leida);
        Assert.Equal(sesion.Id, leida!.Id);
        Assert.Equal("Firefox", leida.Dispositivo);
        Assert.Equal("10.0.0.1", leida.IpCreacion);
        Assert.Null(await new SesionRepository(contexto).ObtenerPorHashAsync(Hash('z')));
    }

    [Fact]
    public async Task RD_09_NoPuedenExistirDosSesionesConElMismoHash()
    {
        await Guardar(Sesion('a'));

        await Assert.ThrowsAsync<DbUpdateException>(() => Guardar(Sesion('a')));
    }

    [Fact]
    public async Task RD_06_ObtenerPorIdSoloDevuelveLaSesionDelUsuarioIndicado()
    {
        var sesion = await Guardar(Sesion('a'));

        using var contexto = NuevoContexto();
        var repositorio = new SesionRepository(contexto);

        Assert.NotNull(await repositorio.ObtenerAsync(_usuario.Id, sesion.Id));
        Assert.Null(await repositorio.ObtenerAsync(_otro.Id, sesion.Id));
    }

    // ---------- Rotación atómica ----------

    [Fact]
    public async Task RF_CA_12_RotarRevocaLaActualYGuardaLaSiguienteEnLaMismaFamilia()
    {
        var actual = await Guardar(Sesion('a'));
        var siguiente = actual.SiguienteEnLaFamilia(Hash('b'), DateTime.UtcNow, Duracion);
        var ahora = DateTime.UtcNow;

        bool rotada;
        using (var contexto = NuevoContexto())
        {
            rotada = await new SesionRepository(contexto).RotarAsync(actual.Id, siguiente, ahora);
        }

        Assert.True(rotada);
        var vieja = Leer(actual.Id)!;
        Assert.NotNull(vieja.RevocadaEn);
        Assert.Equal(siguiente.Id, vieja.ReemplazadaPorId);
        var nueva = Leer(siguiente.Id)!;
        Assert.Null(nueva.RevocadaEn);
        Assert.Equal(actual.FamiliaId, nueva.FamiliaId);
    }

    [Fact]
    public async Task RF_CA_18_UnTokenSoloSeCanjeaUnaVez_LaSegundaRotacionPierdeYNoDejaFilasHuerfanas()
    {
        var actual = await Guardar(Sesion('a'));
        var primera = actual.SiguienteEnLaFamilia(Hash('b'), DateTime.UtcNow, Duracion);
        var segunda = actual.SiguienteEnLaFamilia(Hash('c'), DateTime.UtcNow, Duracion);

        bool ganada, perdida;
        using (var contexto = NuevoContexto())
        {
            ganada = await new SesionRepository(contexto).RotarAsync(actual.Id, primera, DateTime.UtcNow);
        }

        using (var contexto = NuevoContexto())
        {
            perdida = await new SesionRepository(contexto).RotarAsync(actual.Id, segunda, DateTime.UtcNow);
        }

        Assert.True(ganada);
        Assert.False(perdida);
        Assert.Null(Leer(segunda.Id));   // la sesión del perdedor no quedó guardada
        Assert.NotNull(Leer(primera.Id));
        Assert.Equal(primera.Id, Leer(actual.Id)!.ReemplazadaPorId);
    }

    [Fact]
    public async Task RF_CA_18_NoSeRotaUnaSesionQueYaEstabaRevocada()
    {
        var actual = await Guardar(Sesion('a'));
        using (var contexto = NuevoContexto())
        {
            await new SesionRepository(contexto).RevocarFamiliaAsync(actual.FamiliaId, DateTime.UtcNow);
        }

        var siguiente = actual.SiguienteEnLaFamilia(Hash('b'), DateTime.UtcNow, Duracion);
        bool rotada;
        using (var contexto = NuevoContexto())
        {
            rotada = await new SesionRepository(contexto).RotarAsync(actual.Id, siguiente, DateTime.UtcNow);
        }

        Assert.False(rotada);
        Assert.Null(Leer(siguiente.Id));
    }

    // ---------- Revocación ----------

    [Fact]
    public async Task RF_CA_18_RevocarLaFamiliaCortaTodasSusSesiones_PeroNoLasDeOtraFamilia()
    {
        var a = await Guardar(Sesion('a'));
        var b = await Guardar(a.SiguienteEnLaFamilia(Hash('b'), DateTime.UtcNow, Duracion));
        var ajena = await Guardar(Sesion('c'));

        int filas;
        using (var contexto = NuevoContexto())
        {
            filas = await new SesionRepository(contexto).RevocarFamiliaAsync(a.FamiliaId, DateTime.UtcNow);
        }

        Assert.Equal(2, filas);
        Assert.NotNull(Leer(a.Id)!.RevocadaEn);
        Assert.NotNull(Leer(b.Id)!.RevocadaEn);
        Assert.Null(Leer(ajena.Id)!.RevocadaEn);
    }

    [Fact]
    public async Task RF_CA_12_RevocarTodasCortaSoloLasSesionesDeEseUsuario()
    {
        var uno = await Guardar(Sesion('a'));
        var dos = await Guardar(Sesion('b'));
        var deOtro = await Guardar(Sesion('c', _otro));

        using (var contexto = NuevoContexto())
        {
            await new SesionRepository(contexto).RevocarTodasAsync(_usuario.Id, DateTime.UtcNow);
        }

        Assert.NotNull(Leer(uno.Id)!.RevocadaEn);
        Assert.NotNull(Leer(dos.Id)!.RevocadaEn);
        Assert.Null(Leer(deOtro.Id)!.RevocadaEn);
    }

    [Fact]
    public async Task RF_CA_18_RevocarNoCambiaLaFechaDeUnaSesionQueYaEstabaRevocada()
    {
        var sesion = await Guardar(Sesion('a'));
        var primera = DateTime.UtcNow.AddMinutes(-10);
        using (var contexto = NuevoContexto())
        {
            await new SesionRepository(contexto).RevocarTodasAsync(_usuario.Id, primera);
            await new SesionRepository(contexto).RevocarTodasAsync(_usuario.Id, DateTime.UtcNow);
        }

        Assert.Equal(primera, Leer(sesion.Id)!.RevocadaEn);
    }

    // ---------- Listar y limpiar ----------

    [Fact]
    public async Task RF_CA_18_ListarSoloMuestraLasActivasDelUsuario_LaMasRecienteUsadaPrimero()
    {
        var ahora = DateTime.UtcNow;
        var vieja = await Guardar(Sesion('a', ahora: ahora.AddHours(-2)));
        var reciente = await Guardar(Sesion('b', ahora: ahora.AddMinutes(-1)));
        var revocada = await Guardar(Sesion('c'));
        var vencida = await Guardar(Sesion('d', ahora: ahora - Duracion - TimeSpan.FromHours(1)));
        await Guardar(Sesion('e', _otro));
        using (var contexto = NuevoContexto())
        {
            await new SesionRepository(contexto).RevocarFamiliaAsync(revocada.FamiliaId, ahora);
        }

        IReadOnlyList<SesionUsuario> activas;
        using (var contexto = NuevoContexto())
        {
            activas = await new SesionRepository(contexto).ListarActivasAsync(_usuario.Id, ahora);
        }

        Assert.Equal([reciente.Id, vieja.Id], activas.Select(s => s.Id).ToArray());
        Assert.DoesNotContain(activas, s => s.Id == vencida.Id);
    }

    [Fact]
    public async Task RD_09_EliminarVencidasBorraSoloLasQueVencieronAntesDeLaFecha()
    {
        var ahora = DateTime.UtcNow;
        var muyVieja = await Guardar(Sesion('a', ahora: ahora - Duracion - TimeSpan.FromDays(40)));
        var vencidaReciente = await Guardar(Sesion('b', ahora: ahora - Duracion - TimeSpan.FromDays(1)));
        var vigente = await Guardar(Sesion('c', ahora: ahora));

        int borradas;
        using (var contexto = NuevoContexto())
        {
            borradas = await new SesionRepository(contexto).EliminarVencidasAsync(ahora.AddDays(-30));
        }

        Assert.Equal(1, borradas);
        Assert.Null(Leer(muyVieja.Id));
        Assert.NotNull(Leer(vencidaReciente.Id));   // se conserva un tiempo para detectar su reutilización
        Assert.NotNull(Leer(vigente.Id));
    }

    // ---------- Baja de cuenta (fase 6) ----------

    [Fact]
    public async Task RD_09_AlEliminarElUsuarioSeEliminanSusSesiones()
    {
        var sesion = await Guardar(Sesion('a'));

        using (var contexto = NuevoContexto())
        {
            contexto.usuarios.Remove(contexto.usuarios.Single(u => u.Id == _usuario.Id));
            await contexto.SaveChangesAsync();
        }

        Assert.Null(Leer(sesion.Id));
    }
}
