using AccessControl.Application.Interfaces;
using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Domain.Interfaces;
using HabitTracker.Application.Notificaciones;
using Moq;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using WebApi.Adapters;

namespace Notifications.Api.Tests;

/// <summary>
/// El adaptador por donde salen los recordatorios de HabitTracker: cuenta (Control de Acceso), interruptor de correos
/// (Notificaciones) y cola de correos, cada uno por su puerto (RD-03, RD-12).
/// </summary>
public class CanalRecordatoriosAdapterTests
{
    private static (CanalRecordatoriosAdapter Canal, Mock<IEmailQueue> Cola) Canal(Usuario? usuario, bool correoActivo = true)
    {
        var usuarios = new Mock<IUsuarioRepository>();
        usuarios.Setup(u => u.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(usuario);
        var notificaciones = new Mock<INotificacionService>();
        notificaciones.Setup(n => n.ObtenerPreferenciasAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreferenciasNotificacionesDto(correoActivo));
        var cola = new Mock<IEmailQueue>();
        return (new CanalRecordatoriosAdapter(usuarios.Object, notificaciones.Object, cola.Object), cola);
    }

    private static Usuario Activa()
    {
        var usuario = new Usuario("Ana", new Email("ana@ejemplo.com"), "hash");
        usuario.Activar();
        return usuario;
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public async Task RF_NEG_07_Canal_UnaCuentaActivaConCorreosEncendidos_RecibeEnSuCorreo()
    {
        var (canal, _) = Canal(Activa());

        Assert.Equal("ana@ejemplo.com", await canal.DestinatarioAsync(Guid.NewGuid()));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public async Task RF_NEG_07_Canal_ElInterruptorGeneralDeCorreosTambienApagaLosRecordatorios()
    {
        var (canal, _) = Canal(Activa(), correoActivo: false);

        Assert.Null(await canal.DestinatarioAsync(Guid.NewGuid()));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public async Task RF_NEG_07_Canal_UnaCuentaInexistenteOInactiva_NoRecibe()
    {
        Assert.Null(await Canal(null).Canal.DestinatarioAsync(Guid.NewGuid()));
        Assert.Null(await Canal(new Usuario("Ana", new Email("ana@ejemplo.com"), "hash")).Canal.DestinatarioAsync(Guid.NewGuid()));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public async Task RF_NEG_07_Canal_EncolaConLosBotonesYElEnlaceDeBaja()
    {
        var (canal, cola) = Canal(Activa());
        var correo = new CorreoRecordatorio("Asunto", "Título", "Mensaje", [new("Abrir la app", "https://app.ejemplo.com/hoy")], "https://api.ejemplo.com/baja");

        await canal.EncolarAsync("ana@ejemplo.com", correo);

        cola.Verify(c => c.EncolarCorreoConAccionesAsync(
            "ana@ejemplo.com", "Asunto", "Título", "Mensaje",
            It.Is<IReadOnlyList<AccionCorreo>>(a => a.Single() == new AccionCorreo("Abrir la app", "https://app.ejemplo.com/hoy")),
            "https://api.ejemplo.com/baja"), Times.Once);
    }
}
