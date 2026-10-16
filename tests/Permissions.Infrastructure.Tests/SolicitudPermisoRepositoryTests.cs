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
}
