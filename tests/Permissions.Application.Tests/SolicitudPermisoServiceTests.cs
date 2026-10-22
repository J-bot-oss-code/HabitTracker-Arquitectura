using Microsoft.Extensions.Logging.Abstractions;
using Permissions.Application.DTOs;
using Permissions.Application.Interfaces;
using Permissions.Application.Services;
using Permissions.Domain.Entities;
using Permissions.Domain.Enums;

namespace Permissions.Application.Tests;

public sealed class SolicitudPermisoServiceTests
{
    [Fact]
    [Trait("Requirement", "RF-GP-02")]
    public async Task RF_GP_02_Consultar_EstandarRecibeTodasSusSolicitudesYNoLasDeOtros()
    {
        // Arrange
        var propiaPendiente = new SolicitudPermiso("persona@ejemplo.com", "Acceso a reportes");
        var propiaRechazada = new SolicitudPermiso("persona@ejemplo.com", "Acceso a documentos");
        propiaRechazada.Rechazar("admin@ejemplo.com", "No cumple la política", DateTime.UtcNow);
        var ajena = new SolicitudPermiso("otra-persona@ejemplo.com", "Acceso a reportes");
        var repository = new InMemorySolicitudPermisoRepository([propiaPendiente, propiaRechazada, ajena]);
        var service = CrearServicio(repository);
        var query = new ObtenerSolicitudesQuery(
            "persona@ejemplo.com",
            "Estándar",
            SoloPendientes: false);

        // Act
        var resultado = await service.ConsultarAsync(query);

        // Assert
        Assert.Equal(2, resultado.Count);
        Assert.Contains(resultado, solicitud => solicitud.Id == propiaPendiente.Id);
        Assert.Contains(resultado, solicitud => solicitud.Id == propiaRechazada.Id);
        Assert.Equal(EstadoSolicitudPermiso.Rechazada.ToString(),
            resultado.Single(solicitud => solicitud.Id == propiaRechazada.Id).Estado);
        Assert.DoesNotContain(resultado, solicitud => solicitud.Id == ajena.Id);
        Assert.Equal(1, repository.OwnerQueryCount);
        Assert.Equal(0, repository.AllQueryCount);
    }

    [Fact]
    [Trait("Requirement", "RF-GP-07")]
    public async Task RF_GP_07_Aprobar_AdminNoPuedeResolverSuPropiaSolicitud()
    {
        // Arrange
        var solicitud = new SolicitudPermiso("admin@ejemplo.com", "Acceso a reportes");
        var repository = new InMemorySolicitudPermisoRepository([solicitud]);
        var service = CrearServicio(repository);
        var command = new AprobarSolicitudCommand(solicitud.Id, "admin@ejemplo.com");

        // Act
        var error = await Record.ExceptionAsync(() => service.AprobarAsync(command));

        // Assert
        Assert.IsType<InvalidOperationException>(error);
        Assert.Equal(0, repository.UpdateCount);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, solicitud.Estado);
    }

    [Fact]
    [Trait("Requirement", "RF-GP-02")]
    public async Task RF_GP_02_Consultar_EstandarConSoloPendientesRecibeSoloSusPendientes()
    {
        // Arrange
        var propia = new SolicitudPermiso("persona@ejemplo.com", "Acceso a reportes");
        var ajena = new SolicitudPermiso("otra-persona@ejemplo.com", "Acceso a documentos");
        var repository = new InMemorySolicitudPermisoRepository([propia, ajena]);
        var service = CrearServicio(repository);
        var query = new ObtenerSolicitudesQuery("persona@ejemplo.com", "Estandar");

        // Act
        var resultado = await service.ConsultarAsync(query);

        // Assert
        var solicitud = Assert.Single(resultado);
        Assert.Equal(propia.Id, solicitud.Id);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente.ToString(), solicitud.Estado);
        Assert.Equal(1, repository.OwnerQueryCount);
        Assert.Equal(0, repository.AllQueryCount);
    }

    private static SolicitudPermisoService CrearServicio(ISolicitudPermisoRepository repository) =>
        new(repository, NullLogger<SolicitudPermisoService>.Instance);

    private sealed class InMemorySolicitudPermisoRepository(
        IReadOnlyList<SolicitudPermiso> solicitudes) : ISolicitudPermisoRepository
    {
        public int UpdateCount { get; private set; }
        public int OwnerQueryCount { get; private set; }
        public int AllQueryCount { get; private set; }

        public Task AddAsync(SolicitudPermiso solicitud, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateAsync(SolicitudPermiso solicitud, CancellationToken cancellationToken = default)
        {
            UpdateCount++;
            return Task.CompletedTask;
        }

        public Task<SolicitudPermiso?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(solicitudes.SingleOrDefault(solicitud => solicitud.Id == id));

        public Task<IReadOnlyList<SolicitudPermiso>> GetPendingAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SolicitudPermiso>>(
                solicitudes.Where(solicitud => solicitud.Estado == EstadoSolicitudPermiso.Pendiente).ToList());

        public Task<IReadOnlyList<SolicitudPermiso>> GetBySolicitanteAsync(
            string solicitante,
            bool soloPendientes,
            CancellationToken cancellationToken = default)
        {
            OwnerQueryCount++;
            var propias = solicitudes.Where(solicitud =>
                string.Equals(solicitud.Solicitante, solicitante.Trim(), StringComparison.OrdinalIgnoreCase));

            if (soloPendientes)
            {
                propias = propias.Where(solicitud => solicitud.Estado == EstadoSolicitudPermiso.Pendiente);
            }

            return Task.FromResult<IReadOnlyList<SolicitudPermiso>>(propias.ToList());
        }

        public Task<IReadOnlyList<SolicitudPermiso>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            AllQueryCount++;
            return Task.FromResult(solicitudes);
        }
    }
}
