using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Domain.Excepciones;
using AccessControl.Infrastructure.Persistence;
using AccessControl.Infrastructure.Repositories;
using AccessControl.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AccessControl.Infrastructure.Tests;

/// <summary>Persistencia real del consentimiento con EF Core sobre SQLite en memoria (sin la API ni SQL Server).</summary>
public sealed class ConsentimientoRepositoryTests : IDisposable
{
    private readonly SqliteConnection _conexion = new("DataSource=:memory:");

    public ConsentimientoRepositoryTests()
    {
        _conexion.Open();
        using var contexto = NuevoContexto();
        contexto.Database.EnsureCreated();
    }

    public void Dispose() => _conexion.Dispose();

    private ApplicationDbContext NuevoContexto() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_conexion).Options);

    private static Usuario NuevoUsuario(string correo = "persona@ejemplo.com") =>
        new("Persona de prueba", new Email(correo), "hash-guardado", Rol.Estandar);

    private static ConsentimientoUsuario Consentimiento(Guid usuarioId, DateTime ahora, string terminos = "v2.1", bool marketing = false) =>
        new(usuarioId, terminos, "v1.4", false, marketing, false, false, ahora, ahora, ConsentimientoUsuario.OrigenActualizacion);

    private int ContarConsentimientos()
    {
        using var contexto = NuevoContexto();
        return contexto.ConsentimientosUsuario.Count();
    }

    // ---------- Registro atómico: el usuario y su constancia de aceptación viajan juntos ----------

    [Fact]
    public async Task RF_CA_01_ElRegistroGuardaElUsuarioYSuConsentimientoEnUnaSolaOperacion()
    {
        var usuario = NuevoUsuario();
        var consentimiento = ConsentimientoUsuario.DelRegistro(usuario.Id, "v2.1", "v1.4", marketing: true, DateTime.UtcNow);

        using (var contexto = NuevoContexto())
        {
            await new UsuarioRepository(contexto).SaveAsync(usuario, consentimiento);
        }

        using var lectura = NuevoContexto();
        var guardado = await new ConsentimientoRepository(lectura).ObtenerVigenteAsync(usuario.Id);
        Assert.NotNull(guardado);
        Assert.Equal("v2.1", guardado!.TerminosVersion);
        Assert.True(guardado.Marketing);
        Assert.Equal(ConsentimientoUsuario.OrigenRegistro, guardado.Origen);
    }

    [Fact]
    public async Task RD_09_SiElCorreoYaExiste_NoQuedaNingunConsentimientoHuerfano()
    {
        using (var contexto = NuevoContexto())
        {
            var primero = NuevoUsuario();
            await new UsuarioRepository(contexto).SaveAsync(primero, ConsentimientoUsuario.DelRegistro(primero.Id, "v2.1", "v1.4", false, DateTime.UtcNow));
        }

        // Segundo registro con el mismo correo: choca con el índice único y NO debe dejar su consentimiento guardado.
        var duplicado = NuevoUsuario();
        using (var contexto = NuevoContexto())
        {
            await Assert.ThrowsAsync<CorreoYaRegistradoException>(() =>
                new UsuarioRepository(contexto).SaveAsync(duplicado, ConsentimientoUsuario.DelRegistro(duplicado.Id, "v2.1", "v1.4", true, DateTime.UtcNow)));
        }

        Assert.Equal(1, ContarConsentimientos());
        using var verificacion = NuevoContexto();
        Assert.Equal(1, await verificacion.usuarios.CountAsync());
    }

    [Fact]
    public async Task RD_09_ElContextoQuedaUsableTrasUnRegistroDuplicado()
    {
        using (var contexto = NuevoContexto())
        {
            var primero = NuevoUsuario();
            await new UsuarioRepository(contexto).SaveAsync(primero, ConsentimientoUsuario.DelRegistro(primero.Id, "v2.1", "v1.4", false, DateTime.UtcNow));
        }

        using var compartido = NuevoContexto();
        var repo = new UsuarioRepository(compartido);
        var duplicado = NuevoUsuario();
        await Assert.ThrowsAsync<CorreoYaRegistradoException>(
            () => repo.SaveAsync(duplicado, ConsentimientoUsuario.DelRegistro(duplicado.Id, "v2.1", "v1.4", true, DateTime.UtcNow)));

        // Con el mismo contexto, un registro distinto sigue funcionando (el consentimiento fallido no quedó "pendiente").
        var otro = NuevoUsuario("otra@ejemplo.com");
        await repo.SaveAsync(otro, ConsentimientoUsuario.DelRegistro(otro.Id, "v2.1", "v1.4", false, DateTime.UtcNow));

        Assert.Equal(2, ContarConsentimientos());
    }

    // ---------- Historial de solo inserción ----------

    [Fact]
    public async Task RF_CA_01_ElConsentimientoVigenteEsLaUltimaFilaDelHistorial()
    {
        var usuario = NuevoUsuario();
        var hace = DateTime.UtcNow.AddDays(-5);
        using (var contexto = NuevoContexto())
        {
            await new UsuarioRepository(contexto).SaveAsync(usuario);
            var repo = new ConsentimientoRepository(contexto);
            // Se insertan desordenadas a propósito: manda ActualizadoEn, no el orden de inserción.
            await repo.AgregarAsync(Consentimiento(usuario.Id, hace.AddDays(2), terminos: "v2.1", marketing: true));
            await repo.AgregarAsync(Consentimiento(usuario.Id, hace, terminos: "v2.0", marketing: false));
            await repo.AgregarAsync(Consentimiento(usuario.Id, hace.AddDays(1), terminos: "v2.1", marketing: false));
        }

        using var lectura = NuevoContexto();
        var vigente = await new ConsentimientoRepository(lectura).ObtenerVigenteAsync(usuario.Id);

        Assert.Equal("v2.1", vigente!.TerminosVersion);
        Assert.True(vigente.Marketing);          // la fila de hace 3 días (la más reciente)
        Assert.Equal(3, ContarConsentimientos());  // nada se borró ni se sobrescribió
    }

    [Fact]
    public async Task RF_CA_01_UnUsuarioSinConsentimientosOConOtroUsuario_DevuelveNull()
    {
        var conConsentimiento = NuevoUsuario("uno@ejemplo.com");
        var sinConsentimiento = NuevoUsuario("dos@ejemplo.com");
        using (var contexto = NuevoContexto())
        {
            var repoUsuarios = new UsuarioRepository(contexto);
            await repoUsuarios.SaveAsync(sinConsentimiento);
            await repoUsuarios.SaveAsync(conConsentimiento, ConsentimientoUsuario.DelRegistro(conConsentimiento.Id, "v2.1", "v1.4", true, DateTime.UtcNow));
        }

        using var lectura = NuevoContexto();
        var repo = new ConsentimientoRepository(lectura);
        Assert.Null(await repo.ObtenerVigenteAsync(sinConsentimiento.Id));
        Assert.Null(await repo.ObtenerVigenteAsync(Guid.NewGuid()));
        Assert.NotNull(await repo.ObtenerVigenteAsync(conConsentimiento.Id));
    }

    [Fact]
    public async Task RD_09_LasFechasVuelvenIntactasDeLaBaseDeDatos()
    {
        var usuario = NuevoUsuario();
        var aceptado = new DateTime(2026, 11, 5, 14, 30, 0, DateTimeKind.Utc);
        using (var contexto = NuevoContexto())
        {
            await new UsuarioRepository(contexto).SaveAsync(usuario);
            await new ConsentimientoRepository(contexto).AgregarAsync(Consentimiento(usuario.Id, aceptado));
        }

        using var lectura = NuevoContexto();
        var guardado = await new ConsentimientoRepository(lectura).ObtenerVigenteAsync(usuario.Id);

        Assert.Equal(aceptado, guardado!.AceptadoEn);
    }

    // ---------- Baja de cuenta (fase 6): borrar el usuario borra su historial ----------

    [Fact]
    public async Task RF_CA_01_AlEliminarElUsuario_SeEliminaSuHistorialDeConsentimientos()
    {
        var usuario = NuevoUsuario();
        using (var contexto = NuevoContexto())
        {
            await new UsuarioRepository(contexto).SaveAsync(usuario, ConsentimientoUsuario.DelRegistro(usuario.Id, "v2.1", "v1.4", false, DateTime.UtcNow));
            await new ConsentimientoRepository(contexto).AgregarAsync(Consentimiento(usuario.Id, DateTime.UtcNow));
        }

        using (var contexto = NuevoContexto())
        {
            var aBorrar = await contexto.usuarios.SingleAsync();
            contexto.usuarios.Remove(aBorrar);
            await contexto.SaveChangesAsync();
        }

        Assert.Equal(0, ContarConsentimientos());
    }

    // ---------- Versiones vigentes desde la configuración ----------

    private static IConfiguration Config(params (string Clave, string? Valor)[] valores) =>
        new ConfigurationBuilder().AddInMemoryCollection(valores.ToDictionary(v => v.Clave, v => v.Valor)).Build();

    [Fact]
    public void RF_CA_01_SinConfiguracion_LasVersionesVigentesSonLasDelFrontend()
    {
        var versiones = new VersionesLegalesConfiguracion(Config());

        Assert.Equal("v2.1", versiones.Terminos);
        Assert.Equal("v1.4", versiones.Privacidad);
    }

    [Fact]
    public void RF_CA_01_LasVersionesVigentesSePuedenCambiarPorConfiguracion()
    {
        var versiones = new VersionesLegalesConfiguracion(Config(("Legal:VersionTerminos", " v3.0 "), ("Legal:VersionPrivacidad", "v2.0")));

        Assert.Equal("v3.0", versiones.Terminos);
        Assert.Equal("v2.0", versiones.Privacidad);
    }

    [Theory]
    [InlineData("Legal:VersionTerminos", "")]
    [InlineData("Legal:VersionTerminos", "   ")]
    [InlineData("Legal:VersionPrivacidad", "0123456789012345678901")]
    public void RD_07_UnaVersionVaciaOMasLargaQueLaColumna_DetieneElArranque(string clave, string valor)
    {
        var error = Assert.Throws<InvalidOperationException>(() => new VersionesLegalesConfiguracion(Config((clave, valor))));

        Assert.Contains(clave, error.Message);
    }
}
