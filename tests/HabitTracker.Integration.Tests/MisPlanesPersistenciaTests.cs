using HabitTracker.Application.DTOs;
using HabitTracker.Application.Interfaces;
using HabitTracker.Application.Services;
using HabitTracker.Domain.Adherencia;
using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Perfil;
using HabitTracker.Domain.Planes;
using HabitTracker.Domain.Salud;
using HabitTracker.Infrastructure.Contenido;
using HabitTracker.Infrastructure.Persistence;
using HabitTracker.Infrastructure.Repositories;
using HabitTracker.Infrastructure.Seguridad;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Integration.Tests;

/// <summary>
/// Fase 4 de HabitTracker v2: planes, pausas, logros y hábitos de plan contra EF Core + SQLite con los repositorios y
/// servicios reales y el catálogo de lanzamiento de verdad (RD-09, RD-12).
/// </summary>
public class MisPlanesPersistenciaTests
{
    private static readonly DateTime Lunes = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);
    private readonly IDataProtectionProvider claves = new EphemeralDataProtectionProvider();
    private readonly Guid usuario = Guid.NewGuid();

    private sealed class RelojFijo(DateTime utc) : TimeProvider
    {
        public DateTime Ahora { get; set; } = utc;
        public override DateTimeOffset GetUtcNow() => new(Ahora, TimeSpan.Zero);
    }

    private sealed class ConConsentimiento : IConsentimientoSalud
    {
        public Task<bool> TieneConsentimientoDatosSaludAsync(Guid usuarioId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class PermisoNulo : IPermisoService
    {
        public Task<bool> TienePermisoEfectivoAsync(string correo, string permiso, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private static async Task<(SqliteConnection Conexion, DbContextOptions<HabitTrackerDbContext> Opciones)> PrepararBaseAsync()
    {
        var conexion = new SqliteConnection("Data Source=:memory:");
        await conexion.OpenAsync();
        var opciones = new DbContextOptionsBuilder<HabitTrackerDbContext>().UseSqlite(conexion).Options;
        await using var contexto = new HabitTrackerDbContext(opciones);
        await contexto.Database.EnsureCreatedAsync();
        await new SembradorCatalogoPlanes(contexto).SembrarAsync(); // los 13 planes de lanzamiento, tal como en producción
        return (conexion, opciones);
    }

    /// <summary>Todos los servicios sobre un mismo contexto (como una petición HTTP).</summary>
    private sealed record Servicios(
        PlanUsuarioService Planes, HoyService Hoy, ResumenProgresoService Resumen, DatosPlanesService Datos, HabitoService Habitos);

    private Servicios Crear(HabitTrackerDbContext contexto, RelojFijo reloj)
    {
        var planes = new PlanUsuarioRepository(contexto);
        var habitos = new HabitoRepository(contexto);
        var plantillas = new PlanPlantillaRepository(contexto);
        var perfiles = new PerfilUsuarioRepository(contexto);
        var cuestionarios = new CuestionarioSaludRepository(contexto, new CifradorDataProtection(claves));
        var logrosRepo = new LogroUsuarioRepository(contexto);
        var unidad = new HabitTrackerUnitOfWork(contexto);
        var logros = new LogrosService(logrosRepo, reloj);
        var mensajes = new CatalogoMensajes();

        return new Servicios(
            new PlanUsuarioService(planes, plantillas, habitos, perfiles, cuestionarios, new ConConsentimiento(), unidad, reloj),
            new HoyService(planes, plantillas, habitos, perfiles, logros, mensajes, reloj),
            new ResumenProgresoService(planes, habitos, perfiles, logros, logrosRepo, mensajes, unidad, reloj),
            new DatosPlanesService(planes, logrosRepo),
            new HabitoService(habitos, unidad, new PermisoNulo(), new MetaRepository(contexto), reloj));
    }

    /// <summary>Una persona adulta con perfil y el cuestionario respondido, sin condiciones: apta para cualquier plan.</summary>
    private async Task PrepararPersonaAsync(DbContextOptions<HabitTrackerDbContext> opciones, Guid? id = null)
    {
        var quien = id ?? usuario;
        await using var contexto = new HabitTrackerDbContext(opciones);
        var perfiles = new PerfilUsuarioRepository(contexto);
        await perfiles.AgregarAsync(PerfilUsuario.Crear(quien,
            new DatosPerfil(PerfilLaboral.OficinaDiurna, "America/Santo_Domingo", true, Edad: 30, Sexo: Sexo.Mujer), Lunes));
        var c = new CuestionarioSalud(quien);
        c.GuardarObjetivos(new BloqueObjetivos([Objetivo.MasEnergia], [], Experiencia.Principiante, Lugar.Gimnasio), Lunes);
        c.GuardarSeguridad(new BloqueSeguridad(false, false, false, false, false, false, false), Lunes);
        c.GuardarCondiciones(new BloqueCondiciones([CondicionSalud.Ninguna], [Alergia.Ninguna]), Lunes);
        await new CuestionarioSaludRepository(contexto, new CifradorDataProtection(claves)).GuardarAsync(c);
        await contexto.SaveChangesAsync();
    }

    // ---------- El catálogo de verdad se puede inscribir ----------

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    [Trait("Requirement", "RD-12")]
    public async Task RF_NEG_02_LosTreceDePlanesDeLanzamiento_SeInscribenParaUnaPersonaApta()
    {
        var reloj = new RelojFijo(Lunes);
        foreach (var slug in LectorCatalogoPlanes.Leer().Select(c => c.Slug))
        {
            var (conexion, opciones) = await PrepararBaseAsync();
            await using var _ = conexion;
            await PrepararPersonaAsync(opciones);

            PlanUsuarioDto plan;
            await using (var contexto = new HabitTrackerDbContext(opciones))
                plan = await Crear(contexto, reloj).Planes.InscribirseAsync(usuario, new InscribirseCommand(slug, null, null));

            Assert.Equal(EstadoPlanUsuario.Activo, plan.Estado);
            Assert.NotEmpty(plan.Habitos);
            Assert.All(plan.Habitos, h =>
            {
                Assert.Equal(EstadoHabito.Activo, h.Estado);
                Assert.False(string.IsNullOrWhiteSpace(h.VersionMinima));
                Assert.NotNull(h.Ancla);
                Assert.NotNull(h.HoraSugerida);
            });
        }
    }

    // ---------- Persistencia ----------

    [Fact]
    [Trait("Requirement", "RD-09")]
    public async Task RD_09_Plan_PersisteConSusHabitosYLasPausasDeUnaPeticionAOtra()
    {
        var (conexion, opciones) = await PrepararBaseAsync();
        await using var _ = conexion;
        await PrepararPersonaAsync(opciones);
        var reloj = new RelojFijo(Lunes);
        PlanUsuarioDto plan;
        await using (var contexto = new HabitTrackerDbContext(opciones))
            plan = await Crear(contexto, reloj).Planes.InscribirseAsync(usuario, new InscribirseCommand("mi-dia-oficina", null, null));

        // Una pausa que se abre en una petición y se cierra en otra.
        reloj.Ahora = Lunes.AddDays(2);
        await using (var contexto = new HabitTrackerDbContext(opciones))
            await Crear(contexto, reloj).Planes.CambiarEstadoAsync(usuario, plan.Id, new CambiarEstadoPlanCommand(EstadoPlanUsuario.Pausado, MotivoPausa.Viaje, false));
        reloj.Ahora = Lunes.AddDays(5);
        await using (var contexto = new HabitTrackerDbContext(opciones))
            await Crear(contexto, reloj).Planes.CambiarEstadoAsync(usuario, plan.Id, new CambiarEstadoPlanCommand(EstadoPlanUsuario.Activo, null, false));

        await using var lectura = new HabitTrackerDbContext(opciones);
        var guardado = await new PlanUsuarioRepository(lectura).ObtenerAsync(plan.Id, usuario);
        var pausa = Assert.Single(guardado!.Pausas);
        Assert.Equal(Lunes.AddDays(2), pausa.InicioUtc);
        Assert.Equal(Lunes.AddDays(5), pausa.FinUtc);
        Assert.Equal(MotivoPausa.Viaje, pausa.Motivo);
        Assert.Equal(DateTimeKind.Utc, pausa.InicioUtc.Kind);
        Assert.Equal(EstadoPlanUsuario.Activo, guardado.Estado);
        // Los 3 días de pausa se agregan al final del plan.
        Assert.Equal(Lunes.AddDays(28 + 3), guardado.FechaFinPrevista);
        Assert.Equal(3, await lectura.Habitos.CountAsync(h => h.PlanUsuarioId == plan.Id));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_LaVersionMinimaYLosCamposDeLaTareaViajanALaBase()
    {
        var (conexion, opciones) = await PrepararBaseAsync();
        await using var _ = conexion;
        await PrepararPersonaAsync(opciones);
        var reloj = new RelojFijo(Lunes);
        PlanUsuarioDto plan;
        await using (var contexto = new HabitTrackerDbContext(opciones))
            plan = await Crear(contexto, reloj).Planes.InscribirseAsync(usuario, new InscribirseCommand("mi-dia-oficina", null, null));
        var habitoId = plan.Habitos.First(h => h.Nombre.StartsWith("Pausa activa", StringComparison.Ordinal)).HabitoId;

        await using (var contexto = new HabitTrackerDbContext(opciones))
            await Crear(contexto, reloj).Habitos.RegistrarAvanceAsync(habitoId, 1, Lunes, usuario, esVersionMinima: true);

        await using var lectura = new HabitTrackerDbContext(opciones);
        var habito = await new HabitoRepository(lectura).GetByIdAsync(habitoId);
        Assert.True(Assert.Single(habito!.Registros).EsVersionMinima);
        Assert.Equal(plan.Id, habito.PlanUsuarioId);
        Assert.Equal(Ancla.AMediaManana, habito.Ancla);
        Assert.False(string.IsNullOrEmpty(habito.Descripcion));
        Assert.False(string.IsNullOrEmpty(habito.VersionMinima));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_HoyYResumen_ConDatosReales_MideElDiaLocalYGanaLosLogros()
    {
        var (conexion, opciones) = await PrepararBaseAsync();
        await using var _ = conexion;
        await PrepararPersonaAsync(opciones);
        var reloj = new RelojFijo(Lunes);
        PlanUsuarioDto plan;
        await using (var contexto = new HabitTrackerDbContext(opciones))
            plan = await Crear(contexto, reloj).Planes.InscribirseAsync(usuario, new InscribirseCommand("mi-dia-oficina", null, null));

        // Tres días seguidos, todas las tareas con su cantidad completa, a las 10:00 locales (14:00 UTC).
        foreach (var dia in new[] { 0, 1, 2 })
            foreach (var h in plan.Habitos)
            {
                reloj.Ahora = Lunes.Date.AddDays(dia).AddHours(14);
                await using var contexto = new HabitTrackerDbContext(opciones);
                await Crear(contexto, reloj).Habitos.RegistrarAvanceAsync(h.HabitoId, h.CantidadObjetivo, reloj.Ahora, usuario);
            }

        reloj.Ahora = Lunes.Date.AddDays(2).AddHours(20);
        HoyDto hoy;
        ResumenProgresoDto resumen;
        await using (var contexto = new HabitTrackerDbContext(opciones))
            hoy = await Crear(contexto, reloj).Hoy.ObtenerHoyAsync(usuario);
        await using (var contexto = new HabitTrackerDbContext(opciones))
            resumen = await Crear(contexto, reloj).Resumen.ObtenerResumenAsync(usuario);

        Assert.All(hoy.Tareas, t => Assert.Equal(EstadoTareaHoy.Hecho, t.Estado));
        Assert.Equal(3, Assert.Single(hoy.Planes).Racha);
        Assert.Equal([ClaveLogro.PrimerAvance], hoy.LogrosNuevos);
        Assert.Equal(3, resumen.Racha);
        Assert.Equal(3, resumen.Mes.DiasCumplidos);
        Assert.Single(resumen.Logros);
    }

    // ---------- Baja de cuenta ----------

    [Fact]
    [Trait("Requirement", "RD-09")]
    [Trait("Requirement", "RF-NEG-06")]
    public async Task RD_09_BajaDeCuenta_BorraPlanesPausasYLogros_SinTocarALosDemas()
    {
        var (conexion, opciones) = await PrepararBaseAsync();
        await using var _ = conexion;
        var otro = Guid.NewGuid();
        await PrepararPersonaAsync(opciones);
        await PrepararPersonaAsync(opciones, otro);
        var reloj = new RelojFijo(Lunes);
        PlanUsuarioDto plan;
        await using (var contexto = new HabitTrackerDbContext(opciones))
        {
            var servicios = Crear(contexto, reloj);
            plan = await servicios.Planes.InscribirseAsync(usuario, new InscribirseCommand("mi-dia-oficina", null, null));
            await servicios.Planes.InscribirseAsync(otro, new InscribirseCommand("mi-dia-oficina", null, null));
        }

        await using (var contexto = new HabitTrackerDbContext(opciones))
        {
            var servicios = Crear(contexto, reloj);
            await servicios.Planes.CambiarEstadoAsync(usuario, plan.Id, new CambiarEstadoPlanCommand(EstadoPlanUsuario.Pausado, MotivoPausa.Otro, false));
            await servicios.Habitos.EliminarDatosDeUsuarioAsync(usuario); // la baja borra los hábitos aparte
        }

        await using (var contexto = new HabitTrackerDbContext(opciones))
            await Crear(contexto, reloj).Datos.EliminarAsync(usuario);

        await using var lectura = new HabitTrackerDbContext(opciones);
        Assert.Empty(await lectura.PlanesUsuario.Where(p => p.UsuarioId == usuario).ToListAsync());
        await using (var comando = conexion.CreateCommand())
        {
            // Las pausas caen en cascada con su plan.
            comando.CommandText = $"SELECT COUNT(*) FROM PeriodosPausa WHERE PlanUsuarioId = '{plan.Id.ToString().ToUpperInvariant()}'";
            Assert.Equal(0L, await comando.ExecuteScalarAsync());
        }

        Assert.Single(await lectura.PlanesUsuario.Where(p => p.UsuarioId == otro).ToListAsync());
        Assert.Equal(3, await lectura.Habitos.CountAsync(h => h.UsuarioId == otro));
        // Idempotente.
        await using var otraVez = new HabitTrackerDbContext(opciones);
        await Crear(otraVez, reloj).Datos.EliminarAsync(usuario);
    }

    [Fact]
    [Trait("Requirement", "RD-09")]
    public async Task RD_09_BorrarUnPlan_DejaSusHabitosSueltosEnVezDeBorrarlos()
    {
        var (conexion, opciones) = await PrepararBaseAsync();
        await using var _ = conexion;
        await PrepararPersonaAsync(opciones);
        var reloj = new RelojFijo(Lunes);
        await using (var contexto = new HabitTrackerDbContext(opciones))
            await Crear(contexto, reloj).Planes.InscribirseAsync(usuario, new InscribirseCommand("mi-dia-oficina", null, null));

        await using (var contexto = new HabitTrackerDbContext(opciones))
            await Crear(contexto, reloj).Datos.EliminarAsync(usuario);

        await using var lectura = new HabitTrackerDbContext(opciones);
        var habitos = await lectura.Habitos.Where(h => h.UsuarioId == usuario).ToListAsync();
        Assert.Equal(3, habitos.Count);
        Assert.All(habitos, h => Assert.Null(h.PlanUsuarioId));
    }

    [Fact]
    [Trait("Requirement", "RD-09")]
    public async Task RD_09_UnLogroNoPuedeEstarDosVecesParaLaMismaPersona()
    {
        var (conexion, opciones) = await PrepararBaseAsync();
        await using var _ = conexion;
        await using var contexto = new HabitTrackerDbContext(opciones);
        contexto.LogrosUsuario.Add(new LogroUsuario(usuario, ClaveLogro.PrimerAvance, Lunes));
        await contexto.SaveChangesAsync();

        contexto.LogrosUsuario.Add(new LogroUsuario(usuario, ClaveLogro.PrimerAvance, Lunes.AddDays(1)));
        await Assert.ThrowsAsync<DbUpdateException>(() => contexto.SaveChangesAsync());

        // Otra persona sí puede tenerlo.
        await using var otro = new HabitTrackerDbContext(opciones);
        otro.LogrosUsuario.Add(new LogroUsuario(Guid.NewGuid(), ClaveLogro.PrimerAvance, Lunes));
        await otro.SaveChangesAsync();
    }

    [Fact]
    [Trait("Requirement", "RD-09")]
    public async Task RD_09_DosPeticionesParaElMismoPlan_LaBaseDejaPasarSoloUna()
    {
        var (conexion, opciones) = await PrepararBaseAsync();
        await using var _ = conexion;
        await using var a = new HabitTrackerDbContext(opciones);
        await using var b = new HabitTrackerDbContext(opciones);
        var plantillaA = (await new PlanPlantillaRepository(a).ObtenerPorSlugsAsync(["mi-dia-oficina"])).Single();
        var plantillaB = (await new PlanPlantillaRepository(b).ObtenerPorSlugsAsync(["mi-dia-oficina"])).Single();
        // Las dos peticiones leyeron "no tiene este plan" antes de que cualquiera guardara.
        await new PlanUsuarioRepository(a).AgregarAsync(PlanUsuario.Iniciar(usuario, plantillaA, null, Lunes));
        await new PlanUsuarioRepository(b).AgregarAsync(PlanUsuario.Iniciar(usuario, plantillaB, null, Lunes));

        await new HabitTrackerUnitOfWork(a).SaveChangesAsync();

        await Assert.ThrowsAsync<HabitTracker.Application.Exceptions.ConflictoDeUnicidadException>(
            () => new HabitTrackerUnitOfWork(b).SaveChangesAsync());
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_UnPlanTerminado_NoImpideEmpezarElMismoOtraVez()
    {
        var (conexion, opciones) = await PrepararBaseAsync();
        await using var _ = conexion;
        await PrepararPersonaAsync(opciones);
        var reloj = new RelojFijo(Lunes);
        PlanUsuarioDto primero;
        await using (var contexto = new HabitTrackerDbContext(opciones))
            primero = await Crear(contexto, reloj).Planes.InscribirseAsync(usuario, new InscribirseCommand("mi-dia-oficina", null, null));
        await using (var contexto = new HabitTrackerDbContext(opciones))
            await Crear(contexto, reloj).Planes.CambiarEstadoAsync(
                usuario, primero.Id, new CambiarEstadoPlanCommand(EstadoPlanUsuario.Cancelado, null, false));

        await using var nuevo = new HabitTrackerDbContext(opciones);
        var otra = await Crear(nuevo, reloj).Planes.InscribirseAsync(usuario, new InscribirseCommand("mi-dia-oficina", null, null));

        Assert.NotEqual(primero.Id, otra.Id);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_CompletarConservandoLosHabitos_LosDejaActivosYConSuVinculoAlPlan()
    {
        var (conexion, opciones) = await PrepararBaseAsync();
        await using var _ = conexion;
        await PrepararPersonaAsync(opciones);
        var reloj = new RelojFijo(Lunes);
        PlanUsuarioDto plan;
        await using (var contexto = new HabitTrackerDbContext(opciones))
            plan = await Crear(contexto, reloj).Planes.InscribirseAsync(usuario, new InscribirseCommand("mi-dia-oficina", null, null));
        reloj.Ahora = Lunes.AddDays(plan.Semanas * 7);
        await using (var contexto = new HabitTrackerDbContext(opciones))
            await Crear(contexto, reloj).Planes.CambiarEstadoAsync(
                usuario, plan.Id, new CambiarEstadoPlanCommand(EstadoPlanUsuario.Completado, null, true));

        await using var lectura = new HabitTrackerDbContext(opciones);
        var habitos = await lectura.Habitos.Where(h => h.UsuarioId == usuario).ToListAsync();
        Assert.Equal(3, habitos.Count);
        Assert.All(habitos, h =>
        {
            Assert.Equal(EstadoHabito.Activo, h.Estado);
            Assert.Equal(plan.Id, h.PlanUsuarioId);
        });
    }

    [Fact]
    [Trait("Requirement", "RD-09")]
    public async Task RD_09_AgregarUnLogroQueYaExiste_NoFallaNiLoDuplica()
    {
        var (conexion, opciones) = await PrepararBaseAsync();
        await using var _ = conexion;
        await using var contexto = new HabitTrackerDbContext(opciones);
        var repositorio = new LogroUsuarioRepository(contexto);

        var primera = await repositorio.AgregarSiNoExisteAsync(new LogroUsuario(usuario, ClaveLogro.PrimerAvance, Lunes));
        var segunda = await repositorio.AgregarSiNoExisteAsync(new LogroUsuario(usuario, ClaveLogro.PrimerAvance, Lunes.AddDays(1)));

        Assert.True(primera);
        Assert.False(segunda);
        Assert.Equal(1, await contexto.LogrosUsuario.CountAsync(l => l.UsuarioId == usuario));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-06")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_06_UnPlanSoloSeLeeConLaIdentidadDeSuDueno()
    {
        var (conexion, opciones) = await PrepararBaseAsync();
        await using var _ = conexion;
        await PrepararPersonaAsync(opciones);
        PlanUsuarioDto plan;
        await using (var contexto = new HabitTrackerDbContext(opciones))
            plan = await Crear(contexto, new RelojFijo(Lunes)).Planes.InscribirseAsync(usuario, new InscribirseCommand("mi-dia-oficina", null, null));

        await using var lectura = new HabitTrackerDbContext(opciones);
        var repositorio = new PlanUsuarioRepository(lectura);

        Assert.NotNull(await repositorio.ObtenerAsync(plan.Id, usuario));
        Assert.Null(await repositorio.ObtenerAsync(plan.Id, Guid.NewGuid()));
        Assert.Empty(await repositorio.ListarDeUsuarioAsync(Guid.NewGuid()));
    }
}
