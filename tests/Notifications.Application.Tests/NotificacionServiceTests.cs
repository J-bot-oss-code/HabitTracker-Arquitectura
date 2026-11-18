using Microsoft.Extensions.Logging.Abstractions;
using Notifications.Application.DTOs;
using Notifications.Application.Exceptions;
using Notifications.Application.Interfaces;
using Notifications.Application.Services;
using Notifications.Domain;

namespace Notifications.Application.Tests;

/// <summary>Reglas del servicio de notificaciones con dobles escritos a mano (RD-12): sin base de datos ni servidor de correo.</summary>
public class NotificacionServiceTests
{
    private static readonly DestinatarioNotificacion Admin1 = new(Guid.NewGuid(), "admin1@ejemplo.com");
    private static readonly DestinatarioNotificacion Admin2 = new(Guid.NewGuid(), "admin2@ejemplo.com");
    private static readonly DestinatarioNotificacion Ana = new(Guid.NewGuid(), "ana@ejemplo.com");
    private static readonly DestinatarioNotificacion Beto = new(Guid.NewGuid(), "beto@ejemplo.com");

    private sealed class Notificaciones : INotificacionRepository
    {
        public List<Notificacion> Lista { get; } = [];
        public Task AgregarAsync(Notificacion n, CancellationToken c = default) { Lista.Add(n); return Task.CompletedTask; }
        public Task<IReadOnlyList<Notificacion>> ListarDeUsuarioAsync(Guid usuarioId, bool soloNoLeidas, CancellationToken c = default) =>
            Task.FromResult<IReadOnlyList<Notificacion>>([.. Lista.Where(n => n.UsuarioId == usuarioId && (!soloNoLeidas || !n.Leida)).OrderByDescending(n => n.CreadaEnUtc)]);
        public Task<Notificacion?> ObtenerAsync(Guid id, CancellationToken c = default) => Task.FromResult(Lista.FirstOrDefault(n => n.Id == id));
        public Task GuardarCambiosAsync(CancellationToken c = default) => Task.CompletedTask;
        public Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken c = default) => Task.FromResult(Lista.RemoveAll(n => n.UsuarioId == usuarioId));
    }

    private sealed class Preferencias : IPreferenciaNotificacionesRepository
    {
        public List<PreferenciaNotificaciones> Lista { get; } = [];
        public Task<PreferenciaNotificaciones?> ObtenerAsync(Guid usuarioId, CancellationToken c = default) => Task.FromResult(Lista.FirstOrDefault(p => p.UsuarioId == usuarioId));
        public Task AgregarAsync(PreferenciaNotificaciones p, CancellationToken c = default) { Lista.Add(p); return Task.CompletedTask; }
        public Task GuardarCambiosAsync(CancellationToken c = default) => Task.CompletedTask;
        public Task EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken c = default) { Lista.RemoveAll(p => p.UsuarioId == usuarioId); return Task.CompletedTask; }
    }

    private sealed class Cola : IColaCorreos
    {
        public List<(string Destinatario, string Asunto, string Titulo, string Mensaje)> Encolados { get; } = [];
        public bool Falla { get; set; }
        public Task EncolarAsync(string destinatario, string asunto, string titulo, string mensaje, CancellationToken c = default)
        {
            if (Falla) throw new InvalidOperationException("La base de datos no responde.");
            Encolados.Add((destinatario, asunto, titulo, mensaje));
            return Task.CompletedTask;
        }
    }

    private sealed class Escenario
    {
        public Notificaciones Notificaciones { get; } = new();
        public Preferencias Preferencias { get; } = new();
        public Cola Cola { get; } = new();
        public NotificacionService Servicio { get; }

        public Escenario() =>
            Servicio = new NotificacionService(Notificaciones, Preferencias, Cola, NullLogger<NotificacionService>.Instance);
    }

    // ---------- Notificar: dos canales (RF-NOT-01, RF-NOT-06, RF-NOT-07) ----------

    [Fact]
    [Trait("Requirement", "RF-NOT-01")]
    public async Task RF_NOT_01_NotificarALosAdministradores_CadaUnoRecibeUnaYNadieMas()
    {
        var e = new Escenario();

        await e.Servicio.NotificarAsync([Admin1, Admin2], TipoNotificacion.SolicitudCreada, "Nueva solicitud", "Ana pidió un permiso.");

        Assert.Single(await e.Servicio.ListarAsync(Admin1.UsuarioId, soloNoLeidas: true));
        Assert.Single(await e.Servicio.ListarAsync(Admin2.UsuarioId, soloNoLeidas: true));
        Assert.Empty(await e.Servicio.ListarAsync(Ana.UsuarioId, soloNoLeidas: true));
        Assert.Empty(await e.Servicio.ListarAsync(Beto.UsuarioId, soloNoLeidas: true));
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-06")]
    public async Task RF_NOT_06_ConElCorreoActivo_LlegaPorLosDosCanales()
    {
        var e = new Escenario();

        await e.Servicio.NotificarAsync([Ana], TipoNotificacion.SolicitudResuelta, "Solicitud aprobada", "Ya puedes reconfigurar tus hábitos.");

        var interna = Assert.Single(await e.Servicio.ListarAsync(Ana.UsuarioId, soloNoLeidas: false));
        Assert.Equal(TipoNotificacion.SolicitudResuelta, interna.Tipo);
        var correo = Assert.Single(e.Cola.Encolados);
        Assert.Equal("ana@ejemplo.com", correo.Destinatario);
        Assert.Equal("Solicitud aprobada", correo.Asunto);
        Assert.Equal("Ya puedes reconfigurar tus hábitos.", correo.Mensaje);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-07")]
    public async Task RF_NOT_07_ConElCorreoDesactivado_QuedaLaInternaYNoSeEncolaNingunCorreo()
    {
        var e = new Escenario();
        await e.Servicio.GuardarPreferenciasAsync(Ana.UsuarioId, correoActivo: false);

        await e.Servicio.NotificarAsync([Ana, Beto], TipoNotificacion.LogroObtenido, "Primer avance", "Lo lograste.");

        Assert.Single(await e.Servicio.ListarAsync(Ana.UsuarioId, soloNoLeidas: false));
        Assert.DoesNotContain(e.Cola.Encolados, c => c.Destinatario == "ana@ejemplo.com");
        Assert.Contains(e.Cola.Encolados, c => c.Destinatario == "beto@ejemplo.com"); // quien no lo desactivó sigue recibiéndolo
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-07")]
    public async Task RF_NOT_07_AlVolverAActivarElCorreo_LasSiguientesSiSeEncolan()
    {
        var e = new Escenario();
        await e.Servicio.GuardarPreferenciasAsync(Ana.UsuarioId, false);
        await e.Servicio.NotificarAsync([Ana], TipoNotificacion.LogroObtenido, "Uno", "Mensaje");

        await e.Servicio.GuardarPreferenciasAsync(Ana.UsuarioId, true);
        await e.Servicio.NotificarAsync([Ana], TipoNotificacion.LogroObtenido, "Dos", "Mensaje");

        Assert.Equal(2, (await e.Servicio.ListarAsync(Ana.UsuarioId, false)).Count);
        Assert.Equal("Dos", Assert.Single(e.Cola.Encolados).Asunto);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-06")]
    public async Task RF_NOT_06_UnDestinatarioRepetido_RecibeUnaSolaNotificacion()
    {
        var e = new Escenario();

        await e.Servicio.NotificarAsync([Ana, Ana], TipoNotificacion.LogroObtenido, "Titulo", "Mensaje");

        Assert.Single(await e.Servicio.ListarAsync(Ana.UsuarioId, false));
        Assert.Single(e.Cola.Encolados);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-08")]
    public async Task RF_NOT_08_SiNoSePuedeEncolarElCorreo_LaNotificacionInternaSeConservaYLaOperacionNoFalla()
    {
        var e = new Escenario { Cola = { Falla = true } };

        await e.Servicio.NotificarAsync([Ana], TipoNotificacion.SolicitudResuelta, "Titulo", "Mensaje");

        Assert.Single(await e.Servicio.ListarAsync(Ana.UsuarioId, false));
    }

    // ---------- Consultar y marcar (RF-NOT-04, RF-NOT-05) ----------

    [Fact]
    [Trait("Requirement", "RF-NOT-04")]
    public async Task RF_NOT_04_ListarDevuelveSoloLasPropias_ConLeidasYNoLeidas()
    {
        var e = new Escenario();
        await e.Servicio.NotificarAsync([Ana], TipoNotificacion.LogroObtenido, "Una", "m");
        await e.Servicio.NotificarAsync([Ana], TipoNotificacion.LogroObtenido, "Dos", "m");
        await e.Servicio.NotificarAsync([Beto], TipoNotificacion.LogroObtenido, "De Beto", "m");
        var primera = (await e.Servicio.ListarAsync(Ana.UsuarioId, false))[0];
        await e.Servicio.MarcarLeidaAsync(Ana.UsuarioId, primera.Id);

        var todas = await e.Servicio.ListarAsync(Ana.UsuarioId, soloNoLeidas: false);
        var sinLeer = await e.Servicio.ListarAsync(Ana.UsuarioId, soloNoLeidas: true);

        Assert.Equal(2, todas.Count);
        Assert.DoesNotContain(todas, n => n.Titulo == "De Beto");
        Assert.Single(sinLeer);
        Assert.Contains(todas, n => n.Leida);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-05")]
    public async Task RF_NOT_05_MarcarLaNotificacionDeOtraPersonaSeRechazaYSigueSinLeer()
    {
        var e = new Escenario();
        await e.Servicio.NotificarAsync([Beto], TipoNotificacion.LogroObtenido, "De Beto", "m");
        var deBeto = (await e.Servicio.ListarAsync(Beto.UsuarioId, false)).Single();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => e.Servicio.MarcarLeidaAsync(Ana.UsuarioId, deBeto.Id));

        Assert.False((await e.Servicio.ListarAsync(Beto.UsuarioId, false)).Single().Leida);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-05")]
    public async Task RF_NOT_05_MarcarUnaNotificacionQueNoExisteDaNoEncontrada()
    {
        var e = new Escenario();

        await Assert.ThrowsAsync<NotificacionNoEncontradaException>(() => e.Servicio.MarcarLeidaAsync(Ana.UsuarioId, Guid.NewGuid()));
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_SinIdentidadValida_NingunaOperacionSeEjecuta()
    {
        var e = new Escenario();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => e.Servicio.ListarAsync(Guid.Empty, false));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => e.Servicio.MarcarLeidaAsync(Guid.Empty, Guid.NewGuid()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => e.Servicio.ObtenerPreferenciasAsync(Guid.Empty));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => e.Servicio.GuardarPreferenciasAsync(Guid.Empty, false));
    }

    // ---------- Preferencias y baja ----------

    [Fact]
    [Trait("Requirement", "RF-NOT-07")]
    public async Task RF_NOT_07_SinPreferenciaGuardadaElCorreoEstaActivo_YGuardarDosVecesNoDuplica()
    {
        var e = new Escenario();
        Assert.True((await e.Servicio.ObtenerPreferenciasAsync(Ana.UsuarioId)).CorreoActivo);

        await e.Servicio.GuardarPreferenciasAsync(Ana.UsuarioId, false);
        await e.Servicio.GuardarPreferenciasAsync(Ana.UsuarioId, false);

        Assert.False((await e.Servicio.ObtenerPreferenciasAsync(Ana.UsuarioId)).CorreoActivo);
        Assert.Single(e.Preferencias.Lista);
        Assert.True((await e.Servicio.ObtenerPreferenciasAsync(Beto.UsuarioId)).CorreoActivo);
    }

    [Fact]
    [Trait("Requirement", "RF-CA-19")]
    public async Task RF_CA_19_LaBajaDeCuentaBorraLasNotificacionesYLaPreferenciaDeEsaPersonaSolamente()
    {
        var e = new Escenario();
        await e.Servicio.NotificarAsync([Ana, Beto], TipoNotificacion.LogroObtenido, "t", "m");
        await e.Servicio.GuardarPreferenciasAsync(Ana.UsuarioId, false);

        await e.Servicio.EliminarDeUsuarioAsync(Ana.UsuarioId);
        await e.Servicio.EliminarDeUsuarioAsync(Ana.UsuarioId); // idempotente

        Assert.Empty(await e.Servicio.ListarAsync(Ana.UsuarioId, false));
        Assert.Single(await e.Servicio.ListarAsync(Beto.UsuarioId, false));
        Assert.Empty(e.Preferencias.Lista);
    }
}
