using HabitTracker.Application.DTOs;
using HabitTracker.Application.Interfaces;
using HabitTracker.Application.Services;
using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Planes;
using HabitTracker.Infrastructure.Persistence;
using HabitTracker.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Integration.Tests;

/// <summary>
/// Los conteos de las métricas del Administrador con EF Core y SQLite: personas distintas con registro, retención, registros por
/// correo y planes más iniciados. Solo números: ninguna de estas consultas devuelve un identificador (Fase 6, RD-09, RD-12).
/// </summary>
public class MetricasHabitosPersistenciaTests
{
    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly Guid Beto = Guid.NewGuid();
    private static readonly Guid Carla = Guid.NewGuid();

    private sealed class SinPermisos : IPermisoService
    {
        public Task<bool> TienePermisoEfectivoAsync(string solicitante, string permisoSolicitado, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private static HabitoService Servicio(HabitTrackerDbContext contexto) =>
        new(new HabitoRepository(contexto), new HabitTrackerUnitOfWork(contexto), new SinPermisos(), new MetaRepository(contexto));

    private static async Task<(SqliteConnection Conexion, DbContextOptions<HabitTrackerDbContext> Opciones)> PrepararAsync()
    {
        var conexion = new SqliteConnection("Data Source=:memory:");
        await conexion.OpenAsync();
        var opciones = new DbContextOptionsBuilder<HabitTrackerDbContext>().UseSqlite(conexion).Options;
        await using var contexto = new HabitTrackerDbContext(opciones);
        await contexto.Database.EnsureCreatedAsync();
        return (conexion, opciones);
    }

    private static async Task<Guid> HabitoActivoAsync(DbContextOptions<HabitTrackerDbContext> opciones, Guid persona)
    {
        Guid id;
        await using (var contexto = new HabitTrackerDbContext(opciones))
            id = await Servicio(contexto).CrearHabitoAsync(new CrearHabitoCommand("Beber agua", Periodicidad.Diaria, 1, "vaso"), persona);
        await using (var contexto = new HabitTrackerDbContext(opciones))
            await Servicio(contexto).CambiarEstadoAsync(id, EstadoHabito.Activo, persona);
        return id;
    }

    private static async Task RegistrarAsync(
        DbContextOptions<HabitTrackerDbContext> opciones, Guid habito, Guid persona, int hace, OrigenRegistro origen = OrigenRegistro.App)
    {
        await using var contexto = new HabitTrackerDbContext(opciones);
        await Servicio(contexto).RegistrarAvanceAsync(
            habito, 1, DateTime.UtcNow.AddDays(-hace), persona, false, origen, origen == OrigenRegistro.Correo ? $"correo:{hace}" : null);
    }

    private static MetricasHabitosRepository Repo(HabitTrackerDbContext contexto) => new(contexto);

    /// <summary>
    /// Ana: registros hace 45 y hace 10 días (en la cohorte y retenida). Beto: hace 40 (en la cohorte, no retenido).
    /// Carla: hace 5 y hace 2 días, este último por correo (activa, fuera de la cohorte).
    /// </summary>
    private static async Task<DbContextOptions<HabitTrackerDbContext>> SembrarAsync(SqliteConnection _, DbContextOptions<HabitTrackerDbContext> opciones)
    {
        var ana = await HabitoActivoAsync(opciones, Ana);
        var beto = await HabitoActivoAsync(opciones, Beto);
        var carla = await HabitoActivoAsync(opciones, Carla);
        await RegistrarAsync(opciones, ana, Ana, 45);
        await RegistrarAsync(opciones, ana, Ana, 10);
        await RegistrarAsync(opciones, beto, Beto, 40);
        await RegistrarAsync(opciones, carla, Carla, 5);
        await RegistrarAsync(opciones, carla, Carla, 2, OrigenRegistro.Correo);
        return opciones;
    }

    [Fact]
    [Trait("Requirement", "RF-REP-04")]
    [Trait("Requirement", "RD-09")]
    public async Task RF_REP_04_PersonasConRegistro_CuentaPersonasDistintas_NoRegistros()
    {
        var (conexion, opciones) = await PrepararAsync();
        await using var _ = conexion;
        await SembrarAsync(conexion, opciones);
        var ahora = DateTime.UtcNow.AddMinutes(1);

        await using var contexto = new HabitTrackerDbContext(opciones);
        var r = Repo(contexto);

        // Ana (hace 10) y Carla (hace 5 y 2): 2 personas aunque haya 3 registros; Beto (hace 40) queda fuera de 30 días.
        Assert.Equal(2, await r.ContarPersonasConRegistroAsync(ahora.AddDays(-30), ahora));
        Assert.Equal(3, await r.ContarPersonasConRegistroAsync(ahora.AddDays(-50), ahora));
        Assert.Equal(0, await r.ContarPersonasConRegistroAsync(ahora.AddDays(-3000), ahora.AddDays(-2000)));
    }

    [Fact]
    [Trait("Requirement", "RF-REP-04")]
    public async Task RF_REP_04_Retencion_CohorteDeHace30A60Dias_YCuantasSiguenEnLosUltimos30()
    {
        var (conexion, opciones) = await PrepararAsync();
        await using var _ = conexion;
        await SembrarAsync(conexion, opciones);
        var ahora = DateTime.UtcNow.AddMinutes(1);

        await using var contexto = new HabitTrackerDbContext(opciones);
        var (cohorte, retenidas) = await Repo(contexto).ContarRetencionAsync(ahora.AddDays(-60), ahora.AddDays(-30), ahora.AddDays(-30), ahora);

        Assert.Equal((2, 1), (cohorte, retenidas)); // Ana y Beto estaban; solo Ana volvió. Carla no estaba en la cohorte.
    }

    [Fact]
    [Trait("Requirement", "RF-REP-04")]
    public async Task RF_REP_04_Retencion_SinNadieEnLaCohorte_DaCeroCero()
    {
        var (conexion, opciones) = await PrepararAsync();
        await using var _ = conexion;

        await using var contexto = new HabitTrackerDbContext(opciones);
        var (cohorte, retenidas) = await Repo(contexto).ContarRetencionAsync(
            DateTime.UtcNow.AddDays(-60), DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

        Assert.Equal((0, 0), (cohorte, retenidas));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public async Task RF_NEG_07_RegistrosPorCorreo_SoloCuentaLosDelCorreo_DesdeLaFecha()
    {
        var (conexion, opciones) = await PrepararAsync();
        await using var _ = conexion;
        await SembrarAsync(conexion, opciones);

        await using var contexto = new HabitTrackerDbContext(opciones);
        var r = Repo(contexto);

        Assert.Equal(1, await r.ContarRegistrosPorCorreoDesdeAsync(DateTime.UtcNow.AddDays(-7)));
        Assert.Equal(0, await r.ContarRegistrosPorCorreoDesdeAsync(DateTime.UtcNow.AddDays(-1))); // el de correo fue hace 2 días
    }

    private static PlanPlantilla Plantilla(string slug) =>
        new(new ContenidoPlan(
            slug, "Plan", "Resumen.", "Descripción.", AreaPlan.Estres, NivelPlan.Basico, IntensidadPlan.Baja, SexoPlan.Todos, 16, 100, 3, [], null,
            false, [], [], [], ["Uno", "Dos", "Tres"], "Aviso.", [], false, 1,
            [new TareaPlantilla(1, "Respirar", "Instrucciones.", Periodicidad.Diaria, 1, "veces", "Versión mínima.", Ancla.AMediaTarde)]));

    [Fact]
    [Trait("Requirement", "RF-REP-04")]
    public async Task RF_REP_04_PlanesMasIniciados_AgrupaPorPlanDelCatalogo_DeMasAMenos_YRespetaElMaximo()
    {
        var (conexion, opciones) = await PrepararAsync();
        await using var _ = conexion;
        await using (var contexto = new HabitTrackerDbContext(opciones))
        {
            var ahora = DateTime.UtcNow;
            foreach (var (slug, veces) in new[] { ("caminar", 3), ("agua", 1), ("yoga", 2) })
                for (var i = 0; i < veces; i++)
                    contexto.PlanesUsuario.Add(PlanUsuario.Iniciar(Guid.NewGuid(), Plantilla(slug), null, ahora));
            await contexto.SaveChangesAsync();
        }

        await using var lectura = new HabitTrackerDbContext(opciones);
        var todos = await Repo(lectura).PlanesMasIniciadosAsync(5);
        var dos = await Repo(lectura).PlanesMasIniciadosAsync(2);

        Assert.Equal([("caminar", 3), ("yoga", 2), ("agua", 1)], todos.Select(p => (p.Slug, p.Cantidad)));
        Assert.Equal(["caminar", "yoga"], dos.Select(p => p.Slug));
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_LosConteosSonSoloNumeros_NingunaConsultaDevuelveIdentificadores()
    {
        // El contrato del repositorio es el de privacidad: números y slugs del catálogo, nunca un Guid de una persona.
        var devueltos = typeof(IMetricasHabitosRepository).GetMethods()
            .Select(m => m.ReturnType.GenericTypeArguments.Single())
            .SelectMany(t => new[] { t }.Concat(t.GenericTypeArguments))
            .ToList();
        Assert.DoesNotContain(devueltos, t => t == typeof(Guid));
        Assert.DoesNotContain(typeof(PlanIniciadoConteo).GetProperties(), p => p.PropertyType == typeof(Guid));
        await Task.CompletedTask;
    }
}
