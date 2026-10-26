using HabitTracker.Application.DTOs;
using HabitTracker.Application.Interfaces;
using HabitTracker.Application.Services;
using HabitTracker.Domain.Entities;
using HabitTracker.Infrastructure.Persistence;
using HabitTracker.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Permissions.Application.DTOs;
using Permissions.Application.Interfaces;
using Permissions.Application.Services;
using Permissions.Domain.Entities;
using Permissions.Infrastructure.Persistence;
using Permissions.Infrastructure.Repositories;

namespace HabitTracker.Integration.Tests;

public class HabitTrackerPermissionsIntegrationTests
{
    private readonly Guid propietario = Guid.NewGuid();
    private readonly Guid metaId = Guid.NewGuid();
    private readonly string correoSolicitante = "persona@ejemplo.com";
    private readonly string codigoPermiso = "Habits.AdvancedReconfig";

    private sealed class PermisoServiceAdapter(ISolicitudPermisoService service) : IPermisoService
    {
        public Task<bool> TienePermisoEfectivoAsync(string solicitante, string permisoSolicitado, CancellationToken cancellationToken = default) =>
            service.TienePermisoEfectivoAsync(solicitante, permisoSolicitado, cancellationToken);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-06")]
    public async Task RF_NEG_06_AdvancedReconfig_RechazaSinPermisoYPermiteTrasAprobacion()
    {
        // Arrange - SQLite en memoria (conexiones separadas por contexto)
        await using var habitConnection = new SqliteConnection("Data Source=:memory:");
        await using var permConnection = new SqliteConnection("Data Source=:memory:");
        await habitConnection.OpenAsync();
        await permConnection.OpenAsync();

        var habitOptions = new DbContextOptionsBuilder<HabitTrackerDbContext>()
            .UseSqlite(habitConnection)
            .Options;
        var permOptions = new DbContextOptionsBuilder<PermissionsDbContext>()
            .UseSqlite(permConnection)
            .Options;

        // Setup DBs
        await using (var ctx = new HabitTrackerDbContext(habitOptions))
            await ctx.Database.EnsureCreatedAsync();
        await using (var ctx = new PermissionsDbContext(permOptions))
            await ctx.Database.EnsureCreatedAsync();

        // Crear meta
        var categoriaId = Guid.NewGuid();
        await using (var ctx = new HabitTrackerDbContext(habitOptions))
        {
            ctx.Categorias.Add(new Categoria { Id = categoriaId, Nombre = "Cat", Descripcion = "Desc" });
            ctx.Metas.Add(new Meta { Id = metaId, Titulo = "Meta test", EstadoActual = "Activa", FechaLimite = DateTime.UtcNow.AddDays(30), CategoriaId = categoriaId });
            await ctx.SaveChangesAsync();
        }

        // Repositorios y servicios reales
        await using var habitContext = new HabitTrackerDbContext(habitOptions);
        var habitRepo = new HabitoRepository(habitContext);
        var metaRepo = new MetaRepository(habitContext);
        var unitOfWork = new HabitTrackerUnitOfWork(habitContext);

        await using var permContext1 = new PermissionsDbContext(permOptions);
        var permRepo1 = new SolicitudPermisoRepository(permContext1);
        var permService1 = new SolicitudPermisoService(permRepo1, NullLogger<SolicitudPermisoService>.Instance);
        var permisoAdapter1 = new PermisoServiceAdapter(permService1);

        var habitoService = new HabitoService(habitRepo, unitOfWork, permisoAdapter1, metaRepo);

        // Crear hábito
        var comando = new CrearHabitoCommand("Leer", Periodicidad.Diaria, 10, "paginas", metaId);
        var habitoId = await habitoService.CrearHabitoAsync(comando, propietario);
        await habitoService.CambiarEstadoAsync(habitoId, EstadoHabito.Activo, propietario);

        // Crear solicitud de permiso
        var solicitudId = await permService1.CrearAsync(new CrearSolicitudPermisoCommand(correoSolicitante, codigoPermiso));

        // Act 1 - Sin permiso aplicado: debe rechazar
        var excepcion = await Record.ExceptionAsync(() => habitoService.ReconfigurarHabitoAsync(
            habitoId, Periodicidad.Semanal, 20, "minutos", propietario, correoSolicitante));

        // Assert 1 - Rechazo
        Assert.NotNull(excepcion);
        Assert.IsType<UnauthorizedAccessException>(excepcion);
        Assert.Contains("Habits.AdvancedReconfig", excepcion.Message);

        // Act 2 - Aprobar como administrador (nuevo contexto)
        await using var permContext2 = new PermissionsDbContext(permOptions);
        var permRepo2 = new SolicitudPermisoRepository(permContext2);
        var permService2 = new SolicitudPermisoService(permRepo2, NullLogger<SolicitudPermisoService>.Instance);
        await permService2.AprobarAsync(new AprobarSolicitudCommand(solicitudId, "admin@ejemplo.com"));

        // Act 3 - Reintentar con nuevo permiso service (nuevo contexto)
        await using var permContext3 = new PermissionsDbContext(permOptions);
        var permRepo3 = new SolicitudPermisoRepository(permContext3);
        var permService3 = new SolicitudPermisoService(permRepo3, NullLogger<SolicitudPermisoService>.Instance);
        var permisoAdapter3 = new PermisoServiceAdapter(permService3);
        var habitoService2 = new HabitoService(habitRepo, unitOfWork, permisoAdapter3, metaRepo);

        await habitoService2.ReconfigurarHabitoAsync(
            habitoId, Periodicidad.Semanal, 20, "minutos", propietario, correoSolicitante);

        // Assert 3 - Reconfiguración aplicada
        var habitoActualizado = await habitRepo.GetByIdAsync(habitoId);
        Assert.Equal(Periodicidad.Semanal, habitoActualizado!.Periodicidad);
        Assert.Equal(20m, habitoActualizado.CantidadObjetivo);
        Assert.Equal("minutos", habitoActualizado.Unidad);
    }
}