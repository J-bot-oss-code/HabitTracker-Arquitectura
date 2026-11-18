using Moq;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using WebApi.Adapters;

namespace Notifications.Api.Tests;

/// <summary>A quién y con qué texto se avisa de una solicitud de permiso (RF-NOT-01 y RF-NOT-02), con dobles (RD-12).</summary>
public class NotificadorPermisosAdapterTests
{
    private static readonly DestinatarioNotificacion Admin1 = new(Guid.NewGuid(), "admin1@ejemplo.com");
    private static readonly DestinatarioNotificacion Admin2 = new(Guid.NewGuid(), "admin2@ejemplo.com");
    private static readonly DestinatarioNotificacion Ana = new(Guid.NewGuid(), "ana@ejemplo.com");

    private static (NotificadorPermisosAdapter Adaptador, Mock<INotificacionService> Notificaciones) Crear()
    {
        var notificaciones = new Mock<INotificacionService>();
        var directorio = new Mock<IDirectorioUsuarios>();
        directorio.Setup(d => d.ListarAdministradoresActivosAsync(It.IsAny<CancellationToken>())).ReturnsAsync([Admin1, Admin2]);
        directorio.Setup(d => d.ObtenerPorCorreoAsync("ana@ejemplo.com", It.IsAny<CancellationToken>())).ReturnsAsync(Ana);
        directorio.Setup(d => d.ObtenerPorCorreoAsync("fantasma@ejemplo.com", It.IsAny<CancellationToken>())).ReturnsAsync((DestinatarioNotificacion?)null);
        return (new NotificadorPermisosAdapter(notificaciones.Object, directorio.Object), notificaciones);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-01")]
    public async Task RF_NOT_01_UnaSolicitudNuevaSeAvisaATodosLosAdministradoresYSoloAEllos()
    {
        var (adaptador, notificaciones) = Crear();

        await adaptador.SolicitudCreadaAsync("ana@ejemplo.com", "Habits.AdvancedReconfig");

        notificaciones.Verify(n => n.NotificarAsync(
            It.Is<IEnumerable<DestinatarioNotificacion>>(d => d.SequenceEqual(new[] { Admin1, Admin2 })),
            TipoNotificacion.SolicitudCreada,
            It.IsAny<string>(),
            It.Is<string>(m => m.Contains("ana@ejemplo.com") && m.Contains("Habits.AdvancedReconfig")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-02")]
    public async Task RF_NOT_02_UnaSolicitudAprobadaSeAvisaSoloAlSolicitante()
    {
        var (adaptador, notificaciones) = Crear();

        await adaptador.SolicitudResueltaAsync("ana@ejemplo.com", "Habits.AdvancedReconfig", aprobada: true, motivoRechazo: null);

        notificaciones.Verify(n => n.NotificarAsync(
            It.Is<IEnumerable<DestinatarioNotificacion>>(d => d.SequenceEqual(new[] { Ana })),
            TipoNotificacion.SolicitudResuelta,
            "Solicitud aprobada",
            It.Is<string>(m => m.Contains("aprobó") && m.Contains("Habits.AdvancedReconfig")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-02")]
    public async Task RF_NOT_02_UnaSolicitudRechazadaIncluyeElMotivo()
    {
        var (adaptador, notificaciones) = Crear();

        await adaptador.SolicitudResueltaAsync("ana@ejemplo.com", "Habits.AdvancedReconfig", aprobada: false, motivoRechazo: "Falta justificación");

        notificaciones.Verify(n => n.NotificarAsync(
            It.IsAny<IEnumerable<DestinatarioNotificacion>>(),
            TipoNotificacion.SolicitudResuelta,
            "Solicitud rechazada",
            It.Is<string>(m => m.Contains("rechazó") && m.Contains("Motivo: Falta justificación")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-02")]
    public async Task RF_NOT_02_SiLaCuentaDelSolicitanteYaNoExiste_NoSeAvisaANadie()
    {
        var (adaptador, notificaciones) = Crear();

        await adaptador.SolicitudResueltaAsync("fantasma@ejemplo.com", "Habits.AdvancedReconfig", aprobada: true, motivoRechazo: null);

        notificaciones.VerifyNoOtherCalls();
    }
}
