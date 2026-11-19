using HabitTracker.Domain.Adherencia;
using Moq;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using WebApi.Adapters;

namespace Notifications.Api.Tests;

/// <summary>A quién y con qué texto se felicita un logro (RF-NOT-03), con dobles (RD-12).</summary>
public class NotificadorUsuarioAdapterTests
{
    private static readonly DestinatarioNotificacion Ana = new(Guid.NewGuid(), "ana@ejemplo.com");

    [Fact]
    [Trait("Requirement", "RF-NOT-03")]
    public async Task RF_NOT_03_UnLogroSeAvisaSoloALaPersonaQueLoGano()
    {
        var notificaciones = new Mock<INotificacionService>();
        var directorio = new Mock<IDirectorioUsuarios>();
        directorio.Setup(d => d.ObtenerPorIdAsync(Ana.UsuarioId, It.IsAny<CancellationToken>())).ReturnsAsync(Ana);

        await new NotificadorUsuarioAdapter(notificaciones.Object, directorio.Object).LogroObtenidoAsync(Ana.UsuarioId, ClaveLogro.PrimerAvance);

        notificaciones.Verify(n => n.NotificarAsync(
            It.Is<IEnumerable<DestinatarioNotificacion>>(d => d.SequenceEqual(new[] { Ana })),
            TipoNotificacion.LogroObtenido,
            "Primer avance",
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-03")]
    public async Task RF_NOT_03_SiLaCuentaYaNoEstaActiva_NoSeAvisaANadie()
    {
        var notificaciones = new Mock<INotificacionService>();
        var directorio = new Mock<IDirectorioUsuarios>();
        directorio.Setup(d => d.ObtenerPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((DestinatarioNotificacion?)null);

        await new NotificadorUsuarioAdapter(notificaciones.Object, directorio.Object).LogroObtenidoAsync(Guid.NewGuid(), ClaveLogro.PrimerAvance);

        notificaciones.VerifyNoOtherCalls();
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-03")]
    public void RF_NOT_03_TodoLogroTieneTextoDeAviso()
    {
        foreach (var clave in Enum.GetValues<ClaveLogro>())
        {
            var (titulo, mensaje) = NotificadorUsuarioAdapter.Texto(clave);

            Assert.False(string.IsNullOrWhiteSpace(titulo));
            Assert.False(string.IsNullOrWhiteSpace(mensaje));
            Assert.True(titulo.Length <= Notificacion.LongitudMaximaTitulo && mensaje.Length <= Notificacion.LongitudMaximaMensaje);
        }
    }

    [Fact]
    [Trait("Requirement", "RD-12")]
    public void RD_12_LosTextosCelebranLaConductaYNuncaHablanDeCuerpoNiDeCulpa()
    {
        // P3 y P4: sin peso, medidas, calorías, apariencia ni lenguaje de culpa o de castigo.
        string[] prohibidas = ["peso", "kg", "imc", "caloría", "medida", "cuerpo", "gord", "flac", "culpa", "fallaste", "perdiste", "castigo"];

        foreach (var clave in Enum.GetValues<ClaveLogro>())
        {
            var (titulo, mensaje) = NotificadorUsuarioAdapter.Texto(clave);
            var texto = $"{titulo} {mensaje}".ToLowerInvariant();

            Assert.DoesNotContain(prohibidas, palabra => texto.Contains(palabra));
        }
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_UnaClaveDesconocidaNoTieneTexto()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NotificadorUsuarioAdapter.Texto((ClaveLogro)999));
    }
}
