using HabitTracker.Application.Services;
using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Planes;
using HabitTracker.Infrastructure.Persistence;
using HabitTracker.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Integration.Tests;

/// <summary>
/// Reporte de constancia (RF-REP-02 a 05) con los repositorios y el servicio reales sobre EF Core + SQLite: una fila por plan,
/// agregación sin identificar a nadie, solo lo propio para una persona y rangos que cambian el resultado (RD-12).
/// </summary>
public class ReporteConstanciaPersistenciaTests : IDisposable
{
    private static readonly DateTime Inicio = new(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc);   // viernes
    private static readonly DateTime HoyUtc = new(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);  // miércoles: el día 18 aún no cuenta ayer
    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly Guid Beto = Guid.NewGuid();

    private sealed class RelojFijo(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc, TimeSpan.Zero);
    }

    private readonly SqliteConnection _conexion = new("DataSource=:memory:");

    public ReporteConstanciaPersistenciaTests()
    {
        _conexion.Open();
        using var contexto = NuevoContexto();
        contexto.Database.EnsureCreated();
    }

    public void Dispose() => _conexion.Dispose();

    private HabitTrackerDbContext NuevoContexto() =>
        new(new DbContextOptionsBuilder<HabitTrackerDbContext>().UseSqlite(_conexion).Options);

    private static PlanPlantilla Plantilla(string nombre) =>
        new(new ContenidoPlan(
            nombre.ToLowerInvariant(), nombre, "Resumen.", "Descripción.", AreaPlan.Estres, NivelPlan.Basico, IntensidadPlan.Baja, SexoPlan.Todos, 16, 100, 3, [], null,
            false, [], [], [], ["Uno", "Dos", "Tres"], "Aviso.", [], false, 1,
            [new TareaPlantilla(1, "Respirar", "Instrucciones.", Periodicidad.Diaria, 1, "veces", "Versión mínima.", Ancla.AMediaTarde)]));

    /// <summary>Un plan de la persona con un hábito y un avance por cada día indicado (días desde el 13 de noviembre).</summary>
    private async Task<PlanUsuario> Sembrar(Guid usuario, string nombrePlan, int[] diasConAvance, int[]? soloMinimo = null, bool cancelado = false)
    {
        var plan = PlanUsuario.Iniciar(usuario, Plantilla(nombrePlan), null, Inicio);
        var habito = Habito.DesdeTareaDePlan(usuario, plan.Id, new TareaPlantilla(1, "Respirar", "Instrucciones.", Periodicidad.Diaria, 1, "veces", "Mínima.", Ancla.AMediaTarde));
        foreach (var dia in diasConAvance)
        {
            habito.RegistrarAvance(usuario, 1, Inicio.AddDays(dia), esVersionMinima: soloMinimo?.Contains(dia) == true);
        }

        if (cancelado) plan.Cancelar(Inicio.AddDays(1));

        await using var contexto = NuevoContexto();
        contexto.PlanesUsuario.Add(plan);
        contexto.Habitos.Add(habito);
        await contexto.SaveChangesAsync();
        return plan;
    }

    private ReporteConstanciaService Servicio(HabitTrackerDbContext contexto) =>
        new(new PlanUsuarioRepository(contexto), new HabitoRepository(contexto), new PerfilUsuarioRepository(contexto),
            new ReporteConstanciaRepository(contexto), new RelojFijo(HoyUtc));

    private static DateOnly D(int dia) => new(2026, 11, dia);

    [Fact]
    [Trait("Requirement", "RF-REP-02")]
    public async Task RF_REP_02_Personal_RespondeQueTanConstanteFuiEnCadaPlan()
    {
        await Sembrar(Ana, "Calma", diasConAvance: [0, 1, 3], soloMinimo: [1]); // 13, 14 (mínima), 16

        await using var contexto = NuevoContexto();
        var fila = Assert.Single(await Servicio(contexto).PersonalAsync(Ana, D(13), D(17)));

        Assert.Equal("Calma", fila.Plan);
        Assert.Equal(1, fila.Personas);
        Assert.Equal(3, fila.DiasConAvance);
        Assert.Equal(1, fila.DiasSoloMinimo);
        Assert.Equal(5, fila.DiasEnRango);
        Assert.Equal(60m, fila.PorcentajeConstancia);
    }

    [Fact]
    [Trait("Requirement", "RF-REP-03")]
    public async Task RF_REP_03_ElReporteTieneUnaFilaPorPlan_MuchasMenosQueLosAvancesQueResume()
    {
        await Sembrar(Ana, "Calma", [0, 1, 2, 3, 4]);
        await Sembrar(Ana, "Descanso", [0, 2, 4]);

        await using var contexto = NuevoContexto();
        var filas = await Servicio(contexto).PersonalAsync(Ana, D(13), D(17));

        Assert.Equal(2, filas.Count);
        Assert.Equal(8, filas.Sum(f => f.DiasConAvance)); // 8 avances resumidos en 2 filas
    }

    [Fact]
    [Trait("Requirement", "RF-REP-05")]
    public async Task RF_REP_05_DosRangosDistintosDanResultadosDistintosSobreLosMismosDatos()
    {
        await Sembrar(Ana, "Calma", [0, 1, 3]);
        await using var contexto = NuevoContexto();
        var servicio = Servicio(contexto);

        var primera = Assert.Single(await servicio.PersonalAsync(Ana, D(13), D(14)));
        var segunda = Assert.Single(await servicio.PersonalAsync(Ana, D(15), D(17)));

        Assert.Equal(100m, primera.PorcentajeConstancia); // 2 de 2
        Assert.Equal(33.3m, segunda.PorcentajeConstancia); // 1 de 3
        Assert.NotEqual(primera, segunda);
    }

    [Fact]
    [Trait("Requirement", "RF-REP-05")]
    public async Task RF_REP_05_UnRangoEnElQueElPlanNoEstabaEnMarchaNoDaFilas()
    {
        await Sembrar(Ana, "Calma", [0, 1]);
        await using var contexto = NuevoContexto();

        Assert.Empty(await Servicio(contexto).PersonalAsync(Ana, D(1), D(10))); // antes de que empezara
    }

    [Fact]
    [Trait("Requirement", "RF-REP-04")]
    public async Task RF_REP_04_UnaPersonaSoloVeSusPropiosPlanes_NuncaLosDeOtra()
    {
        await Sembrar(Ana, "Calma", [0, 1, 2]);
        await Sembrar(Beto, "Descanso", [0, 1, 2, 3, 4]);

        await using var contexto = NuevoContexto();
        var deAna = await Servicio(contexto).PersonalAsync(Ana, D(13), D(17));
        var deBeto = await Servicio(contexto).PersonalAsync(Beto, D(13), D(17));

        Assert.Equal("Calma", Assert.Single(deAna).Plan);
        Assert.Equal("Descanso", Assert.Single(deBeto).Plan);
    }

    // Asignación 3 — hallazgo 1: un plan cancelado desaparecía del reporte aunque estuvo en curso dentro del rango (RF-REP-02).
    // Esta prueba reemplaza a «UnPlanCancelado_NoEntraEnElReporte», que daba por buena esa omisión.
    // El plan empieza el 13 y se cancela el 14: estuvo en marcha 2 días y en los 2 hubo avance.
    [Fact]
    [Trait("Requirement", "RF-REP-02")]
    public async Task Hallazgo1_UnPlanCanceladoCuentaHastaElDiaQueSeCancelo_EnElReportePersonal()
    {
        await Sembrar(Ana, "Calma", [0, 1], cancelado: true);

        await using var contexto = NuevoContexto();
        var fila = Assert.Single(await Servicio(contexto).PersonalAsync(Ana, D(13), D(17)));

        Assert.Equal("Calma", fila.Plan);
        Assert.Equal(2, fila.DiasEnRango);
        Assert.Equal(2, fila.DiasConAvance);
        Assert.Equal(100m, fila.PorcentajeConstancia);
    }

    // Asignación 3 — hallazgo 1: la misma omisión estaba repetida en el agregado del Administrador (otro filtro, otra consulta).
    [Fact]
    [Trait("Requirement", "RF-REP-02")]
    public async Task Hallazgo1_UnPlanCanceladoCuentaHastaElDiaQueSeCancelo_EnElReporteDelAdministrador()
    {
        await Sembrar(Ana, "Calma", [0, 1], cancelado: true);

        await using var contexto = NuevoContexto();
        var fila = Assert.Single(await Servicio(contexto).GlobalAsync(D(13), D(17)));

        Assert.Equal("Calma", fila.Plan);
        Assert.Equal(1, fila.Personas);
        Assert.Equal(2m, fila.DiasEnRango);
        Assert.Equal(2m, fila.DiasConAvance);
    }

    [Fact]
    [Trait("Requirement", "RF-REP-04")]
    public async Task RF_REP_04_ElReporteGlobalAgrupaPorPlanDelCatalogoSinIdentificarANadie()
    {
        await Sembrar(Ana, "Calma", [0, 1, 2, 3, 4]);   // 5 de 5
        await Sembrar(Beto, "Calma", [0]);              // 1 de 5
        await Sembrar(Beto, "Descanso", [0, 1]);           // 2 de 5

        await using var contexto = NuevoContexto();
        var filas = await Servicio(contexto).GlobalAsync(D(13), D(17));

        Assert.Equal(2, filas.Count); // 3 planes de 2 personas, resumidos en 2 filas
        var calma = filas.Single(f => f.Plan == "Calma");
        Assert.Equal(2, calma.Personas);
        Assert.Equal(3m, calma.DiasConAvance);          // promedio de 5 y 1
        Assert.Equal(5m, calma.DiasEnRango);
        Assert.Equal(60m, calma.PorcentajeConstancia);  // 6 de 10 días, ponderado
        Assert.Equal(1, filas.Single(f => f.Plan == "Descanso").Personas);
        // La fila no puede llevar ningún dato de la persona.
        var propiedades = typeof(FilaConstanciaDto).GetProperties().Select(p => p.Name.ToLowerInvariant()).ToList();
        Assert.DoesNotContain(propiedades, p => p.Contains("correo") || p.Contains("usuario") || p.Contains("email") || p == "nombre");
    }

    [Fact]
    [Trait("Requirement", "RF-REP-05")]
    public async Task RF_REP_05_ElReporteGlobalCambiaConElRango()
    {
        await Sembrar(Ana, "Calma", [0, 1, 2, 3, 4]);
        await using var contexto = NuevoContexto();
        var servicio = Servicio(contexto);

        var primera = await servicio.GlobalAsync(D(13), D(14));
        var segunda = await servicio.GlobalAsync(D(16), D(17));

        Assert.Equal(2m, Assert.Single(primera).DiasConAvance);
        Assert.Equal(2m, Assert.Single(segunda).DiasConAvance);
        Assert.Equal(2m, Assert.Single(primera).DiasEnRango); // los dos rangos miden 2 días: el resultado depende del rango, no de su tamaño
        Assert.Equal(2m, Assert.Single(segunda).DiasEnRango);
        Assert.Empty(await servicio.GlobalAsync(D(1), D(10)));
    }
}
