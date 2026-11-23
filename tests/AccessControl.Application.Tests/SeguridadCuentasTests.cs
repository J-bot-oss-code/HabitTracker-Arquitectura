using System.Net;
using System.Reflection;
using AccessControl.Application.DTOs;
using AccessControl.Application.Interfaces;
using AccessControl.Application.Service;
using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Domain.Excepciones;
using AccessControl.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using WebApi.Controllers;
using WebApi.Extensions;

namespace AccessControl.Application.Tests;

/// <summary>Correcciones de la auditoría de seguridad en Control de Acceso (SEG-01, 02, 03, 11, 16, 19), sin API ni base de datos (RD-12).</summary>
public class SeguridadCuentasTests
{
    private const string Correo = "persona@ejemplo.com";
    private static readonly ConsentimientosRegistroDto Consentimientos = new("v2.1", "v1.4", false);

    private sealed class Escenario
    {
        public Mock<IUsuarioRepository> Repo { get; } = new Mock<IUsuarioRepository>().ConReservaDeRecuperacion();
        public Mock<IEmailQueue> Cola { get; } = new();
        public Mock<IPasswordHasher> Hasher { get; } = new();
        public Usuario? Usuario { get; set; }

        public Escenario()
        {
            Repo.Setup(r => r.GetByCorreoAsync(It.IsAny<Email>())).ReturnsAsync(() => Usuario);
            Repo.Setup(r => r.CorreoExistsAsync(It.IsAny<Email>())).ReturnsAsync(() => Usuario is not null);
            Repo.Setup(r => r.ReservarIntentoActivacionAsync(It.IsAny<Guid>())).ReturnsAsync(1);
            Hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns<string>(c => "hash-de-" + c);
        }

        public UsuarioService Servicio
        {
            get
            {
                var jwt = new Mock<IJwtProvider>();
                jwt.Setup(j => j.Generar(It.IsAny<Usuario>())).Returns("jwt");
                return new UsuarioService(
                    Repo.Object, Hasher.Object, Cola.Object, Mock.Of<IAuditoriaService>(), jwt.Object,
                    GestorSesionesDePrueba.Crear(), Mock.Of<ILogger<UsuarioService>>());
            }
        }

        public string? UltimoCodigoDeActivacion()
        {
            string? codigo = null;
            foreach (var llamada in Cola.Invocations.Where(i => i.Method.Name == nameof(IEmailQueue.EncolarCorreoActivacionAsync)))
                codigo = (string?)llamada.Arguments[1];
            return codigo;
        }
    }

    private static Usuario Pendiente(string hash = "hash-del-atacante")
    {
        var usuario = new Usuario("Atacante", new Email(Correo), hash);
        usuario.EstablecerTokenActivacion("111111", DateTime.UtcNow.AddHours(24));
        return usuario;
    }

    // ---------- SEG-01: tope de códigos por cuenta y enfriamiento ----------

    [Fact]
    [Trait("Requirement", "RD-06")]
    public void SEG_01_ComoMuchoCincoCodigosPor24Horas_YLaVentanaSeReinicia()
    {
        var usuario = new Usuario("Persona", new Email(Correo), "hash");
        for (var i = 0; i < Usuario.MaximoCodigosPorDia; i++)
        {
            Assert.True(usuario.PuedeEmitirCodigo(DateTime.UtcNow));
            usuario.GenerarCodigoRecuperacion($"00000{i}", 1);
        }

        Assert.False(usuario.PuedeEmitirCodigo(DateTime.UtcNow));
        Assert.True(usuario.PuedeEmitirCodigo(DateTime.UtcNow.AddHours(24).AddMinutes(1)));
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public void SEG_01_LosCodigosQueEmiteUnAdministradorNoGastanElTopeDeLaPersona()
    {
        var usuario = new Usuario("Persona", new Email(Correo), "hash");
        for (var i = 0; i < 10; i++)
            usuario.GenerarCodigoRecuperacion("123456", 24, iniciadoPorAdministrador: true);

        Assert.True(usuario.PuedeEmitirCodigo(DateTime.UtcNow));
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task SEG_01_UnCodigoInvalidadoPorFallos_NoDaOtroAlInstante()
    {
        var e = new Escenario();
        var usuario = new Usuario("Persona", new Email(Correo), "hash");
        usuario.Activar();
        usuario.GenerarCodigoRecuperacion("654321", 1);
        for (var i = 0; i < 5; i++) usuario.Recuperacion!.RegistrarFallo(); // invalidado (Usado = true)
        e.Usuario = usuario;

        await e.Servicio.IniciarRecuperacionAsync(Correo);

        e.Cola.Verify(c => c.EncolarCorreoRecuperacionAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task SEG_01_SinReservaDeIntento_ElCodigoCorrectoTambienSeRechaza()
    {
        var e = new Escenario();
        var usuario = new Usuario("Persona", new Email(Correo), "hash-guardado");
        usuario.Activar();
        usuario.GenerarCodigoRecuperacion("654321", 1);
        e.Usuario = usuario;
        e.Repo.Setup(r => r.ReservarIntentoRecuperacionAsync(usuario.Id)).ReturnsAsync(0); // los 5 ya se gastaron

        await Assert.ThrowsAsync<CodigoInvalidoException>(() => e.Servicio.RestablecerPasswordAsync(Correo, "654321", "NuevaClave123"));

        Assert.Equal("hash-guardado", usuario.PasswordHash);
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task SEG_01_AgotadoElTopeDiario_ElReenvioDeActivacionNoEmiteNada()
    {
        var e = new Escenario();
        var usuario = Pendiente();
        for (var i = 1; i < Usuario.MaximoCodigosPorDia; i++)
            usuario.EstablecerTokenActivacion("111111", DateTime.UtcNow.AddHours(22)); // emitido "hace 2 h": no aplica el enfriamiento de 60 s
        e.Usuario = usuario;

        await e.Servicio.ReenviarEnlaceActivacionAsync(Correo);

        e.Cola.Verify(c => c.EncolarCorreoActivacionAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ---------- SEG-02: pre-secuestro de cuenta ----------

    [Fact]
    [Trait("Requirement", "RF-CA-01")]
    public async Task SEG_02_LaVictimaSeRegistraDespuesDelAtacante_GanaSuContrasena_YActivarNoAbreSesion()
    {
        var e = new Escenario { Usuario = Pendiente() };

        await e.Servicio.RegistrarUsuarioAsync(new RegistroRequestDto("Víctima", Correo, "ClaveDeLaVictima1", Consentimientos));

        Assert.Equal("hash-de-ClaveDeLaVictima1", e.Usuario!.PasswordHash);
        Assert.True(e.Usuario.RegistroRepetido);
        var respuesta = await e.Servicio.ActivarCuentaAsync(Correo, e.UltimoCodigoDeActivacion()!);
        Assert.True(e.Usuario.Activo);
        Assert.Null(respuesta.Token); // debe entrar con su contraseña
    }

    [Fact]
    [Trait("Requirement", "RF-CA-01")]
    public async Task SEG_02_ElAtacanteSeRegistraDespuesDeLaVictima_ActivarNoLeAbreSesionConLaClaveAjena()
    {
        var e = new Escenario { Usuario = Pendiente("hash-de-la-victima") };

        await e.Servicio.RegistrarUsuarioAsync(new RegistroRequestDto("Atacante", Correo, "ClaveDelAtacante1", Consentimientos));
        var respuesta = await e.Servicio.ActivarCuentaAsync(Correo, e.UltimoCodigoDeActivacion()!);

        Assert.Null(respuesta.Token); // la víctima no queda dentro de una cuenta con la clave del atacante; la recupera
    }

    [Fact]
    [Trait("Requirement", "RF-CA-15")]
    public async Task SEG_02_UnaCuentaRegistradaUnaSolaVez_SigueEntrandoDirecto()
    {
        var e = new Escenario { Usuario = Pendiente() };

        var respuesta = await e.Servicio.ActivarCuentaAsync(Correo, "111111");

        Assert.Equal("jwt", respuesta.Token);
    }

    [Fact]
    [Trait("Requirement", "RF-CA-01")]
    public async Task SEG_02_UnaCuentaActiva_SeSigueRechazando()
    {
        var activa = new Usuario("Persona", new Email(Correo), "hash");
        activa.Activar();
        var e = new Escenario { Usuario = activa };

        await Assert.ThrowsAsync<CorreoYaRegistradoException>(
            () => e.Servicio.RegistrarUsuarioAsync(new RegistroRequestDto("Otro", Correo, "ClaveSegura123", Consentimientos)));

        Assert.Equal("hash", activa.PasswordHash);
    }

    [Fact]
    [Trait("Requirement", "RF-CA-15")]
    public async Task SEG_02_ElControllerNoAbreSesionSiLaActivacionNoTraeToken()
    {
        var servicio = new Mock<IUsuarioService>();
        servicio.Setup(s => s.ActivarCuentaAsync(Correo, "123456"))
            .ReturnsAsync(new UsuarioResponseDto(Guid.NewGuid(), "Persona", Correo, "Estandar", true, Token: null));
        var sesiones = new Mock<IGestorSesiones>();
        var controlador = new AuthController(servicio.Object, Mock.Of<ITokenBlacklist>(), Mock.Of<IConsentimientoService>(), sesiones.Object, OpcionesSesion.PorDefecto, Mock.Of<IServicioLoginExterno>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        Assert.IsType<OkObjectResult>(await controlador.Activate(new ActivateRequest(Correo, "123456")));

        sesiones.VerifyNoOtherCalls();
    }

    [Fact]
    [Trait("Requirement", "RD-12")]
    public async Task SEG_02_LaPurgaBorraSoloLasPendientesVencidas()
    {
        var repo = new Mock<IUsuarioRepository>();
        var vieja = Pendiente();
        repo.Setup(r => r.ObtenerConBajaVencidaAsync(It.IsAny<DateTime>(), It.IsAny<int>())).ReturnsAsync([]);
        repo.Setup(r => r.ObtenerPendientesSinActivarAsync(It.IsAny<DateTime>(), It.IsAny<int>())).ReturnsAsync([vieja]);
        var ahora = DateTime.UtcNow;

        var borradas = await new ServicioBajaCuentas(repo.Object, [], Mock.Of<ILogger<ServicioBajaCuentas>>()).EjecutarAsync(ahora);

        Assert.Equal(1, borradas);
        repo.Verify(r => r.ObtenerPendientesSinActivarAsync(ahora - ServicioBajaCuentas.PlazoActivacion, It.IsAny<int>()), Times.Once);
        repo.Verify(r => r.EliminarDefinitivamenteAsync(vieja.Id, Correo), Times.Once);
    }

    // ---------- SEG-03: IPv6 por /64 ----------

    [Theory]
    [Trait("Requirement", "RD-06")]
    [InlineData("2001:db8:1:2:aaaa::1", "2001:db8:1:2:ffff::9", true)]
    [InlineData("2001:db8:1:2::1", "2001:db8:1:3::1", false)]
    [InlineData("203.0.113.5", "203.0.113.6", false)]
    [InlineData("::ffff:203.0.113.5", "203.0.113.5", true)]
    public void SEG_03_LasIPv6DelMismoBloque64ComparteElLimite(string a, string b, bool iguales)
    {
        var claveA = LimitacionDePeticionesExtensions.ClaveDeIp(IPAddress.Parse(a));
        var claveB = LimitacionDePeticionesExtensions.ClaveDeIp(IPAddress.Parse(b));

        Assert.Equal(iguales, claveA == claveB);
    }

    // ---------- SEG-16: límites en todos los endpoints ----------

    [Theory]
    [Trait("Requirement", "RD-06")]
    [InlineData(typeof(AuthController))]
    [InlineData(typeof(PrivacidadController))]
    public void SEG_16_LosControllersDeCuentaTienenLimitePorPersonaParaLoQueNoTieneElSuyo(Type controller)
    {
        Assert.Equal(LimitacionDePeticionesExtensions.Usuario, controller.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
    }

    // ---------- SEG-19: valores de ejemplo en producción ----------

    private static IConfiguration Config(params (string Clave, string? Valor)[] valores) =>
        new ConfigurationBuilder().AddInMemoryCollection(valores.ToDictionary(v => v.Clave, v => v.Valor)).Build();

    [Theory]
    [Trait("Requirement", "RD-10")]
    [InlineData("JwtSettings:Clave", "clave-de-ejemplo-de-mas-de-32-caracteres")]
    [InlineData("ADMIN_CLAVE", "Ejemplo_Admin_2026!")]
    [InlineData("ConnectionStrings:AccessControl", "Server=db;User Id=sa;Password=Ejemplo_Cambiar")]
    public void SEG_19_EnProduccionNoArrancaConLosValoresDeEjemplo(string clave, string valor)
    {
        var config = Config(("Seguridad:ExigirSecretosPropios", "true"), (clave, valor));

        Assert.Throws<InvalidOperationException>(() => VerificacionDelEntorno.ExigirSecretosPropios(config));
    }

    [Fact]
    [Trait("Requirement", "RD-10")]
    public void SEG_19_ConValoresPropios_OSinLaBanderaDeProduccion_Arranca()
    {
        VerificacionDelEntorno.ExigirSecretosPropios(Config(("Seguridad:ExigirSecretosPropios", "true"), ("JwtSettings:Clave", "una-clave-propia-larga-y-aleatoria-123")));
        VerificacionDelEntorno.ExigirSecretosPropios(Config(("JwtSettings:Clave", "clave-de-ejemplo"))); // la demostración no la activa
    }
}
