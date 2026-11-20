using Microsoft.Extensions.Logging.Abstractions;
using Permissions.Application.DTOs;
using Permissions.Application.Interfaces;
using Permissions.Application.Services;
using Permissions.Domain.Entities;
using Permissions.Domain.Enums;

namespace Permissions.Application.Tests;

/// <summary>
/// Toda resolución de una solicitud de permiso queda en el historial de auditoría (RF-AUD-02) por un punto de extensión opcional.
/// Dobles escritos a mano, sin base de datos (RD-12).
/// </summary>
public sealed class AuditoriaDeSolicitudesTests
{
    private sealed class Repositorio : ISolicitudPermisoRepository
    {
        public List<SolicitudPermiso> Solicitudes { get; } = [];
        public Task AddAsync(SolicitudPermiso s, CancellationToken c = default) { Solicitudes.Add(s); return Task.CompletedTask; }
        public Task UpdateAsync(SolicitudPermiso s, CancellationToken c = default) => Task.CompletedTask;
        public Task<SolicitudPermiso?> GetByIdAsync(Guid id, CancellationToken c = default) => Task.FromResult(Solicitudes.SingleOrDefault(s => s.Id == id));
        public Task<IReadOnlyList<SolicitudPermiso>> GetPendingAsync(CancellationToken c = default) => Task.FromResult<IReadOnlyList<SolicitudPermiso>>([.. Solicitudes]);
        public Task<IReadOnlyList<SolicitudPermiso>> GetBySolicitanteAsync(string solicitante, bool soloPendientes, CancellationToken c = default) => Task.FromResult<IReadOnlyList<SolicitudPermiso>>([]);
        public Task<IReadOnlyList<SolicitudPermiso>> GetAllAsync(CancellationToken c = default) => Task.FromResult<IReadOnlyList<SolicitudPermiso>>([.. Solicitudes]);
        public Task<int> EliminarPorSolicitanteAsync(string solicitante, CancellationToken c = default) => Task.FromResult(0);
        public Task<bool> ExisteVigenteAsync(string solicitante, string permisoSolicitado, CancellationToken c = default) => Task.FromResult(false);
        public Task<bool> TienePermisoAplicadoAsync(string solicitante, string permisoSolicitado, CancellationToken c = default) => Task.FromResult(false);
    }

    private sealed record Registro(string Aprobador, Guid SolicitudId, string Accion, string Anterior, string Nuevo);

    private sealed class Auditor : IAuditorPermisos
    {
        public List<Registro> Registros { get; } = [];
        public bool Falla { get; set; }

        public Task ResolucionRegistradaAsync(
            string aprobador, Guid solicitudId, string accion, string valorAnterior, string valorNuevo, CancellationToken c = default)
        {
            if (Falla) throw new InvalidOperationException("Auditoría no responde.");
            Registros.Add(new Registro(aprobador, solicitudId, accion, valorAnterior, valorNuevo));
            return Task.CompletedTask;
        }
    }

    private static (SolicitudPermisoService Servicio, Repositorio Repo, Auditor Auditor, SolicitudPermiso Solicitud) Preparar(string solicitante = "ana@ejemplo.com")
    {
        var repo = new Repositorio();
        var auditor = new Auditor();
        var solicitud = new SolicitudPermiso(solicitante, "Habits.AdvancedReconfig");
        repo.Solicitudes.Add(solicitud);
        var servicio = new SolicitudPermisoService(repo, NullLogger<SolicitudPermisoService>.Instance, notificador: null, auditor);
        return (servicio, repo, auditor, solicitud);
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-02")]
    public async Task RF_AUD_02_AprobarUnaSolicitud_RegistraQuienLaAproboQueSolicitudYElEstadoAnteriorYElNuevo()
    {
        // Arrange
        var (servicio, _, auditor, solicitud) = Preparar();

        // Act
        await servicio.AprobarAsync(new AprobarSolicitudCommand(solicitud.Id, "admin@ejemplo.com"));

        // Assert
        var r = Assert.Single(auditor.Registros);
        Assert.Equal("admin@ejemplo.com", r.Aprobador);
        Assert.Equal(solicitud.Id, r.SolicitudId);
        Assert.Equal("Aprobación de solicitud de permiso", r.Accion);
        Assert.Equal("Pendiente", r.Anterior);
        Assert.StartsWith("Aplicada", r.Nuevo);
        Assert.Contains("Habits.AdvancedReconfig", r.Nuevo);
        Assert.Contains("ana@ejemplo.com", r.Nuevo);
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-02")]
    public async Task RF_AUD_02_RechazarUnaSolicitud_RegistraQuienLaRechazoYElMotivo()
    {
        // Arrange
        var (servicio, _, auditor, solicitud) = Preparar();

        // Act
        await servicio.RechazarAsync(new RechazarSolicitudCommand(solicitud.Id, "admin@ejemplo.com", "Falta justificación"));

        // Assert
        var r = Assert.Single(auditor.Registros);
        Assert.Equal("admin@ejemplo.com", r.Aprobador);
        Assert.Equal(solicitud.Id, r.SolicitudId);
        Assert.Equal("Rechazo de solicitud de permiso", r.Accion);
        Assert.Equal("Pendiente", r.Anterior);
        Assert.StartsWith("Rechazada", r.Nuevo);
        Assert.Contains("motivo: Falta justificación", r.Nuevo);
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-02")]
    public async Task RF_AUD_02_UnaResolucionQueElDominioRechaza_NoDejaRegistro()
    {
        // Arrange: un Administrador no resuelve su propia solicitud (RF-GP-07)
        var (servicio, _, auditor, solicitud) = Preparar(solicitante: "admin@ejemplo.com");

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.AprobarAsync(new AprobarSolicitudCommand(solicitud.Id, "admin@ejemplo.com")));

        // Assert
        Assert.Empty(auditor.Registros);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, solicitud.Estado);
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-02")]
    public async Task RF_AUD_02_UnFalloAlRegistrarNoSeTraga_LaResolucionResponde_Error()
    {
        // Arrange
        var (servicio, _, auditor, solicitud) = Preparar();
        auditor.Falla = true;

        // Act y Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.RechazarAsync(new RechazarSolicitudCommand(solicitud.Id, "admin@ejemplo.com", "No cumple")));
    }
}
