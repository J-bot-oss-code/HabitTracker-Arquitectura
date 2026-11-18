using Microsoft.Extensions.Logging.Abstractions;
using Permissions.Application.DTOs;
using Permissions.Application.Exceptions;
using Permissions.Application.Interfaces;
using Permissions.Application.Services;
using Permissions.Domain.Entities;
using Permissions.Domain.Enums;

namespace Permissions.Application.Tests;

/// <summary>
/// El servicio avisa al crear, aprobar y rechazar una solicitud (RF-NOT-01 y RF-NOT-02) por un punto de extensión opcional, sin
/// que un fallo del aviso deshaga la operación ya guardada. Dobles escritos a mano (RD-12).
/// </summary>
public sealed class AvisosDeSolicitudesTests
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
        public Task<bool> ExisteVigenteAsync(string solicitante, string permisoSolicitado, CancellationToken c = default) =>
            Task.FromResult(Solicitudes.Any(s => s.Solicitante == solicitante && s.PermisoSolicitado == permisoSolicitado && s.Estado != EstadoSolicitudPermiso.Rechazada));
        public Task<bool> TienePermisoAplicadoAsync(string solicitante, string permisoSolicitado, CancellationToken c = default) => Task.FromResult(false);
    }

    private sealed class Notificador : INotificadorPermisos
    {
        public List<string> Avisos { get; } = [];
        public bool Falla { get; set; }

        public Task SolicitudCreadaAsync(string solicitante, string permisoSolicitado, CancellationToken c = default)
        {
            if (Falla) throw new InvalidOperationException("Notificaciones no responde.");
            Avisos.Add($"creada|{solicitante}|{permisoSolicitado}");
            return Task.CompletedTask;
        }

        public Task SolicitudResueltaAsync(string solicitante, string permisoSolicitado, bool aprobada, string? motivoRechazo, CancellationToken c = default)
        {
            if (Falla) throw new InvalidOperationException("Notificaciones no responde.");
            Avisos.Add($"resuelta|{solicitante}|{permisoSolicitado}|{(aprobada ? "aprobada" : "rechazada")}|{motivoRechazo}");
            return Task.CompletedTask;
        }
    }

    private static SolicitudPermisoService Servicio(Repositorio repo, Notificador? notificador) =>
        new(repo, NullLogger<SolicitudPermisoService>.Instance, notificador);

    [Fact]
    [Trait("Requirement", "RF-NOT-01")]
    public async Task RF_NOT_01_CrearUnaSolicitudAvisaUnaVezConElSolicitanteYElPermiso()
    {
        var notificador = new Notificador();

        await Servicio(new Repositorio(), notificador).CrearAsync(new CrearSolicitudPermisoCommand("ana@ejemplo.com", "Habits.AdvancedReconfig"));

        Assert.Equal(["creada|ana@ejemplo.com|Habits.AdvancedReconfig"], notificador.Avisos);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-01")]
    public async Task RF_NOT_01_UnaSolicitudDuplicadaNoAvisaDeNuevo()
    {
        var repo = new Repositorio();
        var notificador = new Notificador();
        var servicio = Servicio(repo, notificador);
        await servicio.CrearAsync(new CrearSolicitudPermisoCommand("ana@ejemplo.com", "Habits.AdvancedReconfig"));

        await Assert.ThrowsAsync<SolicitudDuplicadaException>(() =>
            servicio.CrearAsync(new CrearSolicitudPermisoCommand("ana@ejemplo.com", "Habits.AdvancedReconfig")));

        Assert.Single(notificador.Avisos);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-02")]
    public async Task RF_NOT_02_AprobarAvisaAlSolicitanteConElResultado()
    {
        var repo = new Repositorio();
        var notificador = new Notificador();
        var servicio = Servicio(repo, notificador);
        var id = await servicio.CrearAsync(new CrearSolicitudPermisoCommand("ana@ejemplo.com", "Habits.AdvancedReconfig"));
        notificador.Avisos.Clear();

        await servicio.AprobarAsync(new AprobarSolicitudCommand(id, "admin@ejemplo.com"));

        Assert.Equal(["resuelta|ana@ejemplo.com|Habits.AdvancedReconfig|aprobada|"], notificador.Avisos);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-02")]
    public async Task RF_NOT_02_RechazarAvisaAlSolicitanteConElMotivo()
    {
        var repo = new Repositorio();
        var notificador = new Notificador();
        var servicio = Servicio(repo, notificador);
        var id = await servicio.CrearAsync(new CrearSolicitudPermisoCommand("ana@ejemplo.com", "Habits.AdvancedReconfig"));
        notificador.Avisos.Clear();

        await servicio.RechazarAsync(new RechazarSolicitudCommand(id, "admin@ejemplo.com", "Falta justificación"));

        Assert.Equal(["resuelta|ana@ejemplo.com|Habits.AdvancedReconfig|rechazada|Falta justificación"], notificador.Avisos);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-02")]
    public async Task RF_NOT_02_UnaResolucionInvalida_NoAvisa()
    {
        var repo = new Repositorio();
        var notificador = new Notificador();
        var servicio = Servicio(repo, notificador);
        var id = await servicio.CrearAsync(new CrearSolicitudPermisoCommand("ana@ejemplo.com", "Habits.AdvancedReconfig"));
        await servicio.AprobarAsync(new AprobarSolicitudCommand(id, "admin@ejemplo.com"));
        notificador.Avisos.Clear();

        // Aprobar de nuevo una ya aplicada se rechaza (transición fuera de la tabla): no hay resolución nueva que avisar.
        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.AprobarAsync(new AprobarSolicitudCommand(id, "admin@ejemplo.com")));

        Assert.Empty(notificador.Avisos);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-08")]
    public async Task RF_NOT_08_SiElAvisoFalla_LaSolicitudSeCreaYSeResuelveIgual()
    {
        var repo = new Repositorio();
        var notificador = new Notificador { Falla = true };
        var servicio = Servicio(repo, notificador);

        var id = await servicio.CrearAsync(new CrearSolicitudPermisoCommand("ana@ejemplo.com", "Habits.AdvancedReconfig"));
        await servicio.AprobarAsync(new AprobarSolicitudCommand(id, "admin@ejemplo.com"));

        Assert.Equal(EstadoSolicitudPermiso.Aplicada, repo.Solicitudes.Single().Estado);
    }

    [Fact]
    [Trait("Requirement", "RF-GP-01")]
    public async Task RF_GP_01_SinNotificadorElModuloFuncionaComoAntes()
    {
        var repo = new Repositorio();
        var servicio = Servicio(repo, notificador: null);

        var id = await servicio.CrearAsync(new CrearSolicitudPermisoCommand("ana@ejemplo.com", "Habits.AdvancedReconfig"));
        await servicio.RechazarAsync(new RechazarSolicitudCommand(id, "admin@ejemplo.com", "No"));

        Assert.Equal(EstadoSolicitudPermiso.Rechazada, repo.Solicitudes.Single().Estado);
    }
}
