using HabitTracker.Application.Exceptions;
using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Planes;
using HabitTracker.Infrastructure.Persistence;
using HabitTracker.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Integration.Tests;

/// <summary>Archivos vinculados a un plan con EF Core y SQLite: índice único, cascada con el plan y la consulta de «en curso» (RF-NEG-10, RD-09).</summary>
public class AdjuntosPlanPersistenciaTests
{
    private static readonly DateTime Ahora = new(2026, 11, 13, 14, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Usuario = Guid.NewGuid();
    private static readonly Guid Otro = Guid.NewGuid();

    private static PlanPlantilla Plantilla() =>
        new(new ContenidoPlan(
            "plan", "Plan", "Resumen.", "Descripción.", AreaPlan.Estres, NivelPlan.Basico, IntensidadPlan.Baja, SexoPlan.Todos, 16, 100, 3, [], null,
            false, [], [], [], ["Uno", "Dos", "Tres"], "Aviso.", [], false, 1,
            [new TareaPlantilla(1, "Respirar", "Instrucciones.", Periodicidad.Diaria, 1, "veces", "Versión mínima.", Ancla.AMediaTarde)]));

    private static async Task<(SqliteConnection Conexion, DbContextOptions<HabitTrackerDbContext> Opciones, Guid PlanId)> PrepararAsync(Guid? dueno = null)
    {
        var conexion = new SqliteConnection("Data Source=:memory:");
        await conexion.OpenAsync();
        var opciones = new DbContextOptionsBuilder<HabitTrackerDbContext>().UseSqlite(conexion).Options;
        await using var contexto = new HabitTrackerDbContext(opciones);
        await contexto.Database.EnsureCreatedAsync();
        var plan = PlanUsuario.Iniciar(dueno ?? Usuario, Plantilla(), null, Ahora);
        contexto.PlanesUsuario.Add(plan);
        await contexto.SaveChangesAsync();
        return (conexion, opciones, plan.Id);
    }

    private static async Task AdjuntarAsync(DbContextOptions<HabitTrackerDbContext> opciones, Guid planId, Guid documentoId, Guid? usuario = null)
    {
        await using var contexto = new HabitTrackerDbContext(opciones);
        await new AdjuntoPlanRepository(contexto).AgregarAsync(new AdjuntoPlan(planId, usuario ?? Usuario, documentoId, Ahora));
        await new HabitTrackerUnitOfWork(contexto).SaveChangesAsync();
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-10")]
    [Trait("Requirement", "RD-09")]
    public async Task RF_NEG_10_ElIndiceUnico_RechazaElMismoArchivoEnElMismoPlan()
    {
        var (conexion, opciones, planId) = await PrepararAsync();
        await using var _ = conexion;
        var documento = Guid.NewGuid();
        await AdjuntarAsync(opciones, planId, documento);

        var error = await Record.ExceptionAsync(() => AdjuntarAsync(opciones, planId, documento));

        Assert.IsType<ConflictoDeUnicidadException>(error);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-10")]
    [Trait("Requirement", "RD-09")]
    public async Task RF_NEG_10_LaBajaDeLosPlanes_BorraSusVinculosEnCascada()
    {
        var (conexion, opciones, planId) = await PrepararAsync();
        await using var _ = conexion;
        await AdjuntarAsync(opciones, planId, Guid.NewGuid());
        await AdjuntarAsync(opciones, planId, Guid.NewGuid());

        await using (var contexto = new HabitTrackerDbContext(opciones))
            await new PlanUsuarioRepository(contexto).EliminarDeUsuarioAsync(Usuario);

        await using var verificacion = new HabitTrackerDbContext(opciones);
        Assert.Empty(await verificacion.AdjuntosPlan.ToListAsync());
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-10")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_10_DocumentoEnPlanEnCurso_SoloParaSuDueno_YSoloMientrasElPlanSigue()
    {
        var (conexion, opciones, planId) = await PrepararAsync();
        await using var _ = conexion;
        var documento = Guid.NewGuid();
        await AdjuntarAsync(opciones, planId, documento);

        await using (var lectura = new HabitTrackerDbContext(opciones))
        {
            var repo = new AdjuntoPlanRepository(lectura);
            Assert.True(await repo.DocumentoEnPlanEnCursoAsync(documento, Usuario));
            Assert.False(await repo.DocumentoEnPlanEnCursoAsync(documento, Otro)); // el de otra persona no cuenta
            Assert.False(await repo.DocumentoEnPlanEnCursoAsync(Guid.NewGuid(), Usuario));
        }

        // En pausa sigue en curso: el archivo sigue protegido.
        await using (var contexto = new HabitTrackerDbContext(opciones))
        {
            var plan = await contexto.PlanesUsuario.SingleAsync(p => p.Id == planId);
            plan.Pausar(MotivoPausa.Viaje, Ahora);
            await contexto.SaveChangesAsync();
        }
        await using (var lectura = new HabitTrackerDbContext(opciones))
            Assert.True(await new AdjuntoPlanRepository(lectura).DocumentoEnPlanEnCursoAsync(documento, Usuario));

        await using (var contexto = new HabitTrackerDbContext(opciones))
        {
            var plan = await contexto.PlanesUsuario.SingleAsync(p => p.Id == planId);
            plan.Reanudar(Ahora, TimeZoneInfo.Utc);
            plan.Cancelar(Ahora);
            await contexto.SaveChangesAsync();
        }

        await using var despues = new HabitTrackerDbContext(opciones);
        Assert.False(await new AdjuntoPlanRepository(despues).DocumentoEnPlanEnCursoAsync(documento, Usuario));
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_Listar_DevuelveSoloLosVinculosDeLaPersona_ConElNombreYElEstadoDelPlan()
    {
        var (conexion, opciones, planId) = await PrepararAsync();
        await using var _ = conexion;
        var propio = Guid.NewGuid();
        await AdjuntarAsync(opciones, planId, propio);
        await AdjuntarAsync(opciones, planId, Guid.NewGuid(), usuario: Otro); // un vínculo mal asignado a otra persona

        await using var lectura = new HabitTrackerDbContext(opciones);
        var repo = new AdjuntoPlanRepository(lectura);

        var suyos = await repo.ListarDeUsuarioAsync(Usuario);
        var vinculo = Assert.Single(suyos);
        Assert.Equal(propio, vinculo.Adjunto.DocumentoId);
        Assert.Equal("Plan", vinculo.PlanNombre);
        Assert.Equal(EstadoPlanUsuario.Activo, vinculo.Estado);
        Assert.Single(await repo.ListarDePlanAsync(planId, Usuario));
        Assert.Empty(await repo.ListarDePlanAsync(planId, Guid.NewGuid()));
    }
}
