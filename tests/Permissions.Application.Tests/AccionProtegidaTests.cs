using Microsoft.Extensions.Logging.Abstractions;
using Permissions.Application.DTOs;
using Permissions.Application.Interfaces;
using Permissions.Application.Services;
using Permissions.Domain.Entities;
using Permissions.Domain.Enums;

namespace Permissions.Application.Tests;

public sealed class AccionProtegidaTests
{
    private const string Solicitante = "persona@ejemplo.com";
    private const string Permiso = "Consultar mi historial de permisos";

    [Fact]
    [Trait("Requirement", "RF-GP-05")]
    public async Task RF_GP_05_AccionProtegida_RechazaAntesDeAprobar()
    {
        // Arrange
        var repositorio = new RepositorioEnMemoria();
        var servicio = CrearServicio(repositorio);
        var id = await servicio.CrearAsync(new CrearSolicitudPermisoCommand(Solicitante, Permiso));

        // Act
        var error = await Record.ExceptionAsync(() =>
            servicio.EjecutarAccionProtegidaAsync(Solicitante, Permiso));

        // Assert
        Assert.IsType<UnauthorizedAccessException>(error);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, repositorio.Solicitudes.Single(s => s.Id == id).Estado);
    }

    [Fact]
    [Trait("Requirement", "RF-GP-05")]
    public async Task RF_GP_05_AccionProtegida_SeEjecutaTrasAprobacionSinIntervencionManual()
    {
        // Arrange
        var repositorio = new RepositorioEnMemoria();
        var servicio = CrearServicio(repositorio);
        var id = await servicio.CrearAsync(new CrearSolicitudPermisoCommand(Solicitante, Permiso));

        // Act
        await servicio.AprobarAsync(new AprobarSolicitudCommand(id, "admin@ejemplo.com"));
        var resultado = await servicio.EjecutarAccionProtegidaAsync(Solicitante, Permiso);

        // Assert
        var solicitud = Assert.Single(resultado);
        Assert.Equal(Solicitante, solicitud.Solicitante);
        Assert.Equal(Permiso, solicitud.PermisoSolicitado);
        Assert.Equal("Aplicada", solicitud.Estado);
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, repositorio.Solicitudes.Single(s => s.Id == id).Estado);
    }

    [Fact]
    [Trait("Requirement", "RF-GP-05")]
    public async Task RF_GP_05_AccionProtegida_NoAutorizaAOtroSolicitante()
    {
        // Arrange
        var repositorio = new RepositorioEnMemoria();
        var servicio = CrearServicio(repositorio);
        var id = await servicio.CrearAsync(new CrearSolicitudPermisoCommand(Solicitante, Permiso));
        await servicio.AprobarAsync(new AprobarSolicitudCommand(id, "admin@ejemplo.com"));

        // Act
        var error = await Record.ExceptionAsync(() =>
            servicio.EjecutarAccionProtegidaAsync("otra-persona@ejemplo.com", Permiso));

        // Assert
        Assert.IsType<UnauthorizedAccessException>(error);
    }

    private static SolicitudPermisoService CrearServicio(RepositorioEnMemoria repositorio) =>
        new(repositorio, NullLogger<SolicitudPermisoService>.Instance);

    private sealed class RepositorioEnMemoria : ISolicitudPermisoRepository
    {
        public List<SolicitudPermiso> Solicitudes { get; } = [];

        public Task AddAsync(SolicitudPermiso solicitud, CancellationToken cancellationToken = default)
        {
            Solicitudes.Add(solicitud);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(SolicitudPermiso solicitud, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<SolicitudPermiso?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Solicitudes.SingleOrDefault(s => s.Id == id));

        public Task<IReadOnlyList<SolicitudPermiso>> GetPendingAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SolicitudPermiso>>(Solicitudes.Where(s => s.Estado == EstadoSolicitudPermiso.Pendiente).ToList());

        public Task<IReadOnlyList<SolicitudPermiso>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SolicitudPermiso>>(Solicitudes);

        public Task<bool> TienePermisoAplicadoAsync(
            string solicitante,
            string permisoSolicitado,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Solicitudes.Any(s =>
                s.Estado == EstadoSolicitudPermiso.Aplicada &&
                string.Equals(s.Solicitante, solicitante, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(s.PermisoSolicitado, permisoSolicitado, StringComparison.OrdinalIgnoreCase)));

        public Task<IReadOnlyList<SolicitudPermiso>> GetBySolicitanteAsync(
            string solicitante,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SolicitudPermiso>>(Solicitudes
                .Where(s => string.Equals(s.Solicitante, solicitante, StringComparison.OrdinalIgnoreCase))
                .ToList());
    }
}
