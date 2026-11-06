using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Infrastructure.Persistence;
using AccessControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AccessControl.Infrastructure.Tests;

/// <summary>
/// Persistencia real de la baja de cuenta con EF Core sobre SQLite en memoria: estado de la cuenta, cuentas vencidas y
/// borrado definitivo de todo lo que depende de ella en Control de Acceso (RD-09).
/// </summary>
public sealed class BajaDeCuentaRepositoryTests : IDisposable
{
    private static readonly DateTime Ahora = DateTime.UtcNow;

    private readonly SqliteConnection _conexion = new("DataSource=:memory:");

    public BajaDeCuentaRepositoryTests()
    {
        _conexion.Open();
        using var contexto = NuevoContexto();
        contexto.Database.EnsureCreated();
    }

    public void Dispose() => _conexion.Dispose();

    private ApplicationDbContext NuevoContexto() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_conexion).Options);

    private async Task<Usuario> Guardar(string correo, Rol rol = Rol.Estandar, Action<Usuario>? ajustar = null)
    {
        var usuario = new Usuario("Persona", new Email(correo), "hash-guardado", rol);
        ajustar?.Invoke(usuario);
        using var contexto = NuevoContexto();
        await new UsuarioRepository(contexto).SaveAsync(usuario);
        return usuario;
    }

    private int Contar<T>(Func<ApplicationDbContext, IQueryable<T>> consulta)
    {
        using var contexto = NuevoContexto();
        return consulta(contexto).Count();
    }

    // ---------- El estado se guarda ----------

    [Fact]
    public async Task RF_CA_20_LaBajaProgramadaYLaDesactivacionDelDuenoSePersisten()
    {
        var programada = await Guardar("a@ejemplo.com", ajustar: u => { u.Activar(); u.ProgramarBaja(Ahora, 30); });
        var desactivada = await Guardar("b@ejemplo.com", ajustar: u => { u.Activar(); u.SolicitarDesactivacion(Ahora); });

        using var contexto = NuevoContexto();
        var repositorio = new UsuarioRepository(contexto);
        var a = (await repositorio.GetByIdAsync(programada.Id))!;
        var b = (await repositorio.GetByIdAsync(desactivada.Id))!;

        Assert.True(a.TieneBajaProgramada);
        Assert.Equal(Ahora.AddDays(30), a.BajaProgramadaPara);
        Assert.NotNull(a.BajaSolicitadaEn);
        Assert.True(a.Activo);
        Assert.True(b.DesactivadaPorElUsuario);
        Assert.False(b.Activo);
    }

    [Fact]
    public async Task RF_CA_20_CancelarLaBajaSeGuardaConElMismoContextoQueLaLeyo()
    {
        var usuario = await Guardar("a@ejemplo.com", ajustar: u => { u.Activar(); u.ProgramarBaja(Ahora, 30); });

        using (var contexto = NuevoContexto())
        {
            var repositorio = new UsuarioRepository(contexto);
            var cargado = (await repositorio.GetByIdAsync(usuario.Id))!;
            cargado.CancelarBaja();
            await repositorio.UpdateAsync(cargado);
        }

        using var lectura = NuevoContexto();
        var final = (await new UsuarioRepository(lectura).GetByIdAsync(usuario.Id))!;
        Assert.False(final.TieneBajaProgramada);
        Assert.Null(final.BajaSolicitadaEn);
    }

    // ---------- Cuentas vencidas ----------

    [Fact]
    public async Task RF_CA_20_SoloSeListanLasCuentasCuyoPlazoYaTermino_LasMasAntiguasPrimero()
    {
        var vieja = await Guardar("vieja@ejemplo.com", ajustar: u => u.ProgramarBaja(Ahora.AddDays(-40), 30));
        var reciente = await Guardar("reciente@ejemplo.com", ajustar: u => u.ProgramarBaja(Ahora.AddDays(-32), 30));
        await Guardar("enGracia@ejemplo.com", ajustar: u => u.ProgramarBaja(Ahora.AddDays(-5), 30));
        await Guardar("sinBaja@ejemplo.com");

        using var contexto = NuevoContexto();
        var vencidas = await new UsuarioRepository(contexto).ObtenerConBajaVencidaAsync(Ahora, 50);

        Assert.Equal([vieja.Id, reciente.Id], vencidas.Select(u => u.Id).ToArray());
    }

    [Fact]
    public async Task RD_06_UnAdministradorNuncaSeBorraSolo_AunqueSuPlazoHayaTerminado()
    {
        await Guardar("admin@ejemplo.com", Rol.Administrador, u => u.ProgramarBaja(Ahora.AddDays(-40), 30));
        var persona = await Guardar("persona@ejemplo.com", ajustar: u => u.ProgramarBaja(Ahora.AddDays(-40), 30));

        using var contexto = NuevoContexto();
        var vencidas = await new UsuarioRepository(contexto).ObtenerConBajaVencidaAsync(Ahora, 50);

        Assert.Equal(persona.Id, Assert.Single(vencidas).Id);
    }

    [Fact]
    public async Task RD_09_ElLoteTieneUnTope()
    {
        for (var i = 0; i < 5; i++)
        {
            await Guardar($"p{i}@ejemplo.com", ajustar: u => u.ProgramarBaja(Ahora.AddDays(-40), 30));
        }

        using var contexto = NuevoContexto();
        var vencidas = await new UsuarioRepository(contexto).ObtenerConBajaVencidaAsync(Ahora, 3);

        Assert.Equal(3, vencidas.Count);
    }

    // ---------- Borrado definitivo ----------

    [Fact]
    public async Task RD_09_EliminarDefinitivamenteBorraLaCuentaYTodoLoQueDependeDeEllaEnControlDeAcceso()
    {
        var persona = await Guardar("persona@ejemplo.com", ajustar: u => u.Activar());
        var otra = await Guardar("otra@ejemplo.com", ajustar: u => u.Activar());

        using (var contexto = NuevoContexto())
        {
            contexto.ConsentimientosUsuario.Add(ConsentimientoUsuario.DelRegistro(persona.Id, "v2.1", "v1.4", false, Ahora));
            contexto.ConsentimientosUsuario.Add(ConsentimientoUsuario.DelRegistro(otra.Id, "v2.1", "v1.4", false, Ahora));
            contexto.SesionesUsuario.Add(new SesionUsuario(persona.Id, new string('a', 64), Ahora, TimeSpan.FromDays(14), "Firefox", "10.0.0.1"));
            contexto.SesionesUsuario.Add(new SesionUsuario(otra.Id, new string('b', 64), Ahora, TimeSpan.FromDays(14), "Chrome", null));
            contexto.LoginsExternos.Add(new LoginExterno(persona.Id, "google", "sub-persona", Ahora));
            contexto.LoginsExternos.Add(new LoginExterno(otra.Id, "google", "sub-otra", Ahora));
            contexto.RevocacionesSesion.Add(new RevocacionSesion(persona.Id, Ahora, Ahora.AddMinutes(15)));
            contexto.RevocacionesSesion.Add(new RevocacionSesion(otra.Id, Ahora, Ahora.AddMinutes(15)));
            contexto.CorreosEnCola.Add(new CorreoEnCola("persona@ejemplo.com", "Recuperación", "Código: 123456"));
            contexto.CorreosEnCola.Add(new CorreoEnCola("otra@ejemplo.com", "Recuperación", "Código: 654321"));
            await contexto.SaveChangesAsync();
        }

        using (var contexto = NuevoContexto())
        {
            await new UsuarioRepository(contexto).EliminarDefinitivamenteAsync(persona.Id, "persona@ejemplo.com");
        }

        // Nada de la persona.
        Assert.Equal(0, Contar(c => c.usuarios.Where(u => u.Id == persona.Id)));
        Assert.Equal(0, Contar(c => c.ConsentimientosUsuario.Where(x => x.UsuarioId == persona.Id)));
        Assert.Equal(0, Contar(c => c.SesionesUsuario.Where(x => x.UsuarioId == persona.Id)));
        Assert.Equal(0, Contar(c => c.LoginsExternos.Where(x => x.UsuarioId == persona.Id)));
        Assert.Equal(0, Contar(c => c.RevocacionesSesion.Where(x => x.UsuarioId == persona.Id)));
        Assert.Equal(0, Contar(c => c.CorreosEnCola.Where(x => x.Destinatario == "persona@ejemplo.com")));
        // Todo lo de la otra persona sigue ahí.
        Assert.Equal(1, Contar(c => c.usuarios.Where(u => u.Id == otra.Id)));
        Assert.Equal(1, Contar(c => c.ConsentimientosUsuario.Where(x => x.UsuarioId == otra.Id)));
        Assert.Equal(1, Contar(c => c.SesionesUsuario.Where(x => x.UsuarioId == otra.Id)));
        Assert.Equal(1, Contar(c => c.LoginsExternos.Where(x => x.UsuarioId == otra.Id)));
        Assert.Equal(1, Contar(c => c.RevocacionesSesion.Where(x => x.UsuarioId == otra.Id)));
        Assert.Equal(1, Contar(c => c.CorreosEnCola.Where(x => x.Destinatario == "otra@ejemplo.com")));
    }

    [Fact]
    public async Task RD_09_EliminarDefinitivamenteDosVecesEsInofensivo()
    {
        var persona = await Guardar("persona@ejemplo.com");

        using var contexto = NuevoContexto();
        var repositorio = new UsuarioRepository(contexto);
        await repositorio.EliminarDefinitivamenteAsync(persona.Id, "persona@ejemplo.com");
        await repositorio.EliminarDefinitivamenteAsync(persona.Id, "persona@ejemplo.com");

        Assert.Equal(0, Contar(c => c.usuarios));
    }

    [Fact]
    public async Task RD_09_UnaCuentaBorradaYaNoApareceEntreLasVencidas()
    {
        var persona = await Guardar("persona@ejemplo.com", ajustar: u => u.ProgramarBaja(Ahora.AddDays(-40), 30));

        using (var contexto = NuevoContexto())
        {
            await new UsuarioRepository(contexto).EliminarDefinitivamenteAsync(persona.Id, "persona@ejemplo.com");
        }

        using var lectura = NuevoContexto();
        Assert.Empty(await new UsuarioRepository(lectura).ObtenerConBajaVencidaAsync(Ahora, 50));
    }

    // ---------- Historial de consentimientos (descarga de datos) ----------

    [Fact]
    public async Task RF_CA_07_ElHistorialCompletoSeListaDelMasAntiguoAlMasReciente_SoloDeEsaPersona()
    {
        var persona = await Guardar("persona@ejemplo.com");
        var otra = await Guardar("otra@ejemplo.com");

        using (var contexto = NuevoContexto())
        {
            var repositorio = new ConsentimientoRepository(contexto);
            await repositorio.AgregarAsync(ConsentimientoUsuario.DelRegistro(persona.Id, "v2.0", "v1.3", false, Ahora.AddDays(-10)));
            await repositorio.AgregarAsync(ConsentimientoUsuario.DelRegistro(persona.Id, "v2.1", "v1.4", true, Ahora));
            await repositorio.AgregarAsync(ConsentimientoUsuario.DelRegistro(otra.Id, "v2.1", "v1.4", false, Ahora));
        }

        using var lectura = NuevoContexto();
        var historial = await new ConsentimientoRepository(lectura).ListarHistorialAsync(persona.Id);

        Assert.Equal(["v2.0", "v2.1"], historial.Select(h => h.TerminosVersion).ToArray());
        Assert.All(historial, h => Assert.Equal(persona.Id, h.UsuarioId));
    }
}
