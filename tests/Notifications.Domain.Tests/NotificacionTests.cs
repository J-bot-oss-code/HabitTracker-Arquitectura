using Notifications.Domain;

namespace Notifications.Domain.Tests;

/// <summary>Reglas de la notificación y de la preferencia (RF-NOT-04, RF-NOT-05, RF-NOT-07), sin base de datos (RD-12).</summary>
public class NotificacionTests
{
    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly Guid Beto = Guid.NewGuid();
    private static readonly DateTime Ahora = new(2026, 11, 18, 15, 0, 0, DateTimeKind.Utc);

    private static Notificacion Nueva(Guid? usuario = null) =>
        new(usuario ?? Ana, TipoNotificacion.SolicitudCreada, "Nueva solicitud", "Ana pidió un permiso.", Ahora);

    [Fact]
    [Trait("Requirement", "RF-NOT-04")]
    public void RF_NOT_04_UnaNotificacionNuevaNaceSinLeerYEsDeSuDestinatario()
    {
        var n = Nueva();

        Assert.Equal(Ana, n.UsuarioId);
        Assert.False(n.Leida);
        Assert.Null(n.LeidaEnUtc);
        Assert.NotEqual(Guid.Empty, n.Id);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-05")]
    public void RF_NOT_05_LaDestinatariaPuedeMarcarlaComoLeida()
    {
        var n = Nueva();

        n.MarcarLeida(Ana, Ahora.AddMinutes(5));

        Assert.True(n.Leida);
        Assert.Equal(Ahora.AddMinutes(5), n.LeidaEnUtc);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-05")]
    public void RF_NOT_05_MarcarLaNotificacionDeOtraPersonaSeRechazaYNoCambiaNada()
    {
        var n = Nueva(Ana);

        Assert.Throws<UnauthorizedAccessException>(() => n.MarcarLeida(Beto, Ahora));

        Assert.False(n.Leida);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-05")]
    public void RF_NOT_05_MarcarDosVecesConservaLaPrimeraFecha()
    {
        var n = Nueva();
        n.MarcarLeida(Ana, Ahora.AddMinutes(1));

        n.MarcarLeida(Ana, Ahora.AddMinutes(30));

        Assert.Equal(Ahora.AddMinutes(1), n.LeidaEnUtc);
    }

    [Theory]
    [InlineData("", "mensaje")]
    [InlineData("   ", "mensaje")]
    [InlineData("titulo", "")]
    [InlineData("titulo", "   ")]
    [Trait("Requirement", "RD-07")]
    public void RD_07_UnTituloOUnMensajeVacioSeRechaza(string titulo, string mensaje)
    {
        Assert.Throws<ArgumentException>(() => new Notificacion(Ana, TipoNotificacion.LogroObtenido, titulo, mensaje, Ahora));
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_ElDestinatarioElTipoYLaFechaUtcSonObligatorios()
    {
        Assert.Throws<ArgumentException>(() => new Notificacion(Guid.Empty, TipoNotificacion.LogroObtenido, "t", "m", Ahora));
        Assert.Throws<ArgumentException>(() => new Notificacion(Ana, (TipoNotificacion)99, "t", "m", Ahora));
        Assert.Throws<ArgumentException>(() => new Notificacion(Ana, TipoNotificacion.LogroObtenido, "t", "m", DateTime.SpecifyKind(Ahora, DateTimeKind.Local)));
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_UnTextoLargoSeAcortaAlMaximo()
    {
        var n = new Notificacion(Ana, TipoNotificacion.SolicitudResuelta, new string('t', 500), new string('m', 5000), Ahora);

        Assert.Equal(Notificacion.LongitudMaximaTitulo, n.Titulo.Length);
        Assert.Equal(Notificacion.LongitudMaximaMensaje, n.Mensaje.Length);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-07")]
    public void RF_NOT_07_ElCorreoEstaActivoPorDefectoYSePuedeDesactivarYVolverAActivar()
    {
        var p = new PreferenciaNotificaciones(Ana);
        Assert.True(p.CorreoActivo);

        p.CambiarCorreo(false);
        Assert.False(p.CorreoActivo);

        p.CambiarCorreo(true);
        Assert.True(p.CorreoActivo);
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_LaPreferenciaExigeUnaPersona()
    {
        Assert.Throws<ArgumentException>(() => new PreferenciaNotificaciones(Guid.Empty));
    }
}
