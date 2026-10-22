using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Permissions.Domain.Entities;
using Permissions.Domain.Enums;
using Permissions.Infrastructure.Persistence;
using Permissions.Infrastructure.Repositories;

namespace Permissions.Infrastructure.Tests;

public sealed class SolicitudPermisoRepositoryTests
{
    [Fact]
    public async Task Repository_PersisteConsultaYListaPendientesSinLevantarLaApi()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PermissionsDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setupContext = new PermissionsDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        var solicitud = new SolicitudPermiso("persona@ejemplo.com", "Acceso a reportes");
        var id = solicitud.Id;

        await using (var writeContext = new PermissionsDbContext(options))
        {
            var repository = new SolicitudPermisoRepository(writeContext);
            await repository.AddAsync(solicitud);
        }

        await using (var readContext = new PermissionsDbContext(options))
        {
            var repository = new SolicitudPermisoRepository(readContext);
            var recuperada = await repository.GetByIdAsync(id);
            var pendientes = await repository.GetPendingAsync();

            Assert.NotNull(recuperada);
            Assert.Equal(EstadoSolicitudPermiso.Pendiente, recuperada.Estado);
            Assert.Equal(DateTimeKind.Utc, recuperada.FechaCreacion.Kind);
            Assert.Single(pendientes);
            Assert.Equal(id, pendientes[0].Id);
        }
    }

    [Fact]
    [Trait("Requirement", "RF-GP-05")]
    public async Task RF_GP_05_Repository_SoloAutorizaLaAccionTrasAplicarElPermisoAlSolicitanteCorrecto()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PermissionsDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setupContext = new PermissionsDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        var solicitud = new SolicitudPermiso("persona@ejemplo.com", "Consultar mi historial de permisos");
        await using (var writeContext = new PermissionsDbContext(options))
        {
            await new SolicitudPermisoRepository(writeContext).AddAsync(solicitud);
        }

        // Act / Assert: pendiente no permite la acción.
        await using (var pendingContext = new PermissionsDbContext(options))
        {
            var repository = new SolicitudPermisoRepository(pendingContext);
            Assert.False(await repository.TienePermisoAplicadoAsync("persona@ejemplo.com", "Consultar mi historial de permisos"));
        }

        // Arrange / Act: el administrador aprueba y el sistema aplica automáticamente.
        var fechaUtc = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(solicitud.Aprobar("admin@ejemplo.com", fechaUtc).IsSuccess);
        await using (var updateContext = new PermissionsDbContext(options))
        {
            await new SolicitudPermisoRepository(updateContext).UpdateAsync(solicitud);
        }

        // Assert: solo el solicitante autorizado puede ejecutar la acción.
        await using (var appliedContext = new PermissionsDbContext(options))
        {
            var repository = new SolicitudPermisoRepository(appliedContext);
            Assert.True(await repository.TienePermisoAplicadoAsync("persona@ejemplo.com", "Consultar mi historial de permisos"));
            Assert.False(await repository.TienePermisoAplicadoAsync("otra-persona@ejemplo.com", "Consultar mi historial de permisos"));
            Assert.True(await repository.TienePermisoAplicadoAsync("PERSONA@EJEMPLO.COM", "consultar mi historial de permisos"));
            Assert.Single(await repository.GetBySolicitanteAsync("PERSONA@EJEMPLO.COM"));
            Assert.Empty(await repository.GetBySolicitanteAsync("otra-persona@ejemplo.com"));
        }
    }
}
