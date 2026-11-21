using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Planes;
using HabitTracker.Infrastructure.Persistence;
using HabitTracker.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Integration.Tests;

/// <summary>
/// Fase 5: el reclamo del recordatorio con EF Core y SQLite. Comparar y marcar van en una sola instrucción, así que de varias
/// pasadas a la vez solo una gana el día (un solo correo), y la lista solo trae planes activos con el recordatorio encendido.
/// </summary>
public class RecordatoriosPersistenciaTests
{
    private static readonly DateTime Ahora = new(2026, 11, 13, 14, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Hoy = new(2026, 11, 13);

    private static PlanPlantilla Plantilla() =>
        new(new ContenidoPlan(
            "plan", "Plan", "Resumen.", "Descripción.", AreaPlan.Estres, NivelPlan.Basico, IntensidadPlan.Baja, SexoPlan.Todos, 16, 100, 3, [], null,
            false, [], [], [], ["Uno", "Dos", "Tres"], "Aviso.", [], false, 1,
            [new TareaPlantilla(1, "Respirar", "Instrucciones.", Periodicidad.Diaria, 1, "veces", "Versión mínima.", Ancla.AMediaTarde)]));

    private static async Task<(SqliteConnection Conexion, DbContextOptions<HabitTrackerDbContext> Opciones, PlanUsuario Plan)> PrepararAsync(
        bool recordatorio = true)
    {
        var conexion = new SqliteConnection("Data Source=:memory:");
        await conexion.OpenAsync();
        var opciones = new DbContextOptionsBuilder<HabitTrackerDbContext>().UseSqlite(conexion).Options;
        await using var contexto = new HabitTrackerDbContext(opciones);
        await contexto.Database.EnsureCreatedAsync();
        var plan = PlanUsuario.Iniciar(Guid.NewGuid(), Plantilla(), new TimeOnly(8, 0), Ahora);
        if (recordatorio)
            plan.ConfigurarRecordatorio(null, true);
        contexto.PlanesUsuario.Add(plan);
        await contexto.SaveChangesAsync();
        return (conexion, opciones, plan);
    }

    private static async Task<bool> ReclamarAsync(DbContextOptions<HabitTrackerDbContext> opciones, Guid planId, DateOnly dia)
    {
        // Cada pasada con su propio contexto, como dos procesos o dos réplicas de la API.
        await using var contexto = new HabitTrackerDbContext(opciones);
        return await new PlanUsuarioRepository(contexto).ReclamarRecordatorioAsync(planId, dia);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    [Trait("Requirement", "RD-09")]
    public async Task RF_NEG_07_DeVariasPasadasALaVez_SoloUnaReclamaElDia()
    {
        var (conexion, opciones, plan) = await PrepararAsync();
        await using var _ = conexion;

        var resultados = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => ReclamarAsync(opciones, plan.Id, Hoy)));

        Assert.Equal(1, resultados.Count(r => r));
        await using var lectura = new HabitTrackerDbContext(opciones);
        Assert.Equal(Hoy, (await lectura.PlanesUsuario.SingleAsync()).UltimoRecordatorioLocal);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public async Task RF_NEG_07_ElDiaSiguienteSePuedeReclamarOtraVez_YUnDiaAnteriorNunca()
    {
        var (conexion, opciones, plan) = await PrepararAsync();
        await using var _ = conexion;

        Assert.True(await ReclamarAsync(opciones, plan.Id, Hoy));
        Assert.False(await ReclamarAsync(opciones, plan.Id, Hoy));
        Assert.False(await ReclamarAsync(opciones, plan.Id, Hoy.AddDays(-1)));
        Assert.True(await ReclamarAsync(opciones, plan.Id, Hoy.AddDays(1)));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public async Task RF_NEG_07_LaListaSoloTraePlanesActivosConElRecordatorioEncendido()
    {
        var (conexion, opciones, encendido) = await PrepararAsync();
        await using var _ = conexion;
        await using (var contexto = new HabitTrackerDbContext(opciones))
        {
            var apagado = PlanUsuario.Iniciar(Guid.NewGuid(), Plantilla(), new TimeOnly(9, 0), Ahora);
            var pausado = PlanUsuario.Iniciar(Guid.NewGuid(), Plantilla(), new TimeOnly(9, 0), Ahora);
            pausado.ConfigurarRecordatorio(null, true);
            pausado.Pausar(MotivoPausa.Viaje, Ahora);
            contexto.PlanesUsuario.AddRange(apagado, pausado);
            await contexto.SaveChangesAsync();
        }

        await using var lectura = new HabitTrackerDbContext(opciones);
        var lista = await new PlanUsuarioRepository(lectura).ListarConRecordatorioAsync();

        Assert.Equal([encendido.Id], lista.Select(p => p.Id));
    }
}
