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
    [Trait("Requirement", "RF-GP-02")]
    public async Task RF_GP_02_Repository_FiltraPorSolicitanteYEstadoEnLaBaseDeDatos()
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

        var propiaPendiente = new SolicitudPermiso("persona@ejemplo.com", "Acceso a reportes");
        var propiaRechazada = new SolicitudPermiso("persona@ejemplo.com", "Acceso a documentos");
        propiaRechazada.Rechazar("admin@ejemplo.com", "No cumple la política", DateTime.UtcNow);
        var ajena = new SolicitudPermiso("otra-persona@ejemplo.com", "Acceso a reportes");

        await using (var writeContext = new PermissionsDbContext(options))
        {
            var repository = new SolicitudPermisoRepository(writeContext);
            await repository.AddAsync(propiaPendiente);
            await repository.AddAsync(propiaRechazada);
            await repository.AddAsync(ajena);
        }

        // Act
        IReadOnlyList<SolicitudPermiso> propias;
        IReadOnlyList<SolicitudPermiso> pendientesPropias;
        await using (var readContext = new PermissionsDbContext(options))
        {
            var repository = new SolicitudPermisoRepository(readContext);
            propias = await repository.GetBySolicitanteAsync("PERSONA@EJEMPLO.COM", soloPendientes: false);
            pendientesPropias = await repository.GetBySolicitanteAsync("persona@ejemplo.com", soloPendientes: true);
        }

        // Assert
        Assert.Equal(2, propias.Count);
        Assert.Contains(propias, solicitud => solicitud.Id == propiaPendiente.Id);
        Assert.Contains(propias, solicitud => solicitud.Id == propiaRechazada.Id);
        Assert.DoesNotContain(propias, solicitud => solicitud.Id == ajena.Id);
        var pendientePropia = Assert.Single(pendientesPropias);
        Assert.Equal(propiaPendiente.Id, pendientePropia.Id);
    }

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
    public async Task RF_GP_05_Repository_SoloReconoceComoEfectivoElPermisoAplicado()
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

        var pendiente = new SolicitudPermiso("persona@ejemplo.com", "Acceso a reportes");
        var aprobada = new SolicitudPermiso("persona@ejemplo.com", "Acceso a documentos");
        aprobada.Aprobar("admin@ejemplo.com", DateTime.UtcNow);

        await using (var writeContext = new PermissionsDbContext(options))
        {
            var repository = new SolicitudPermisoRepository(writeContext);
            await repository.AddAsync(pendiente);
            await repository.AddAsync(aprobada);
        }

        // Act
        await using var readContext = new PermissionsDbContext(options);
        var readRepository = new SolicitudPermisoRepository(readContext);
        var permisoPendienteEfectivo = await readRepository.TienePermisoAplicadoAsync(
            "PERSONA@EJEMPLO.COM",
            "Acceso a reportes");
        var permisoAplicadoEfectivo = await readRepository.TienePermisoAplicadoAsync(
            "persona@ejemplo.com",
            "ACCESO A DOCUMENTOS");
        var noPropietarioEfectivo = await readRepository.TienePermisoAplicadoAsync(
            "otra-persona@ejemplo.com",
            "Acceso a documentos");

        // Assert
        Assert.False(permisoPendienteEfectivo);
        Assert.True(permisoAplicadoEfectivo);
        Assert.False(noPropietarioEfectivo);
    }
}
