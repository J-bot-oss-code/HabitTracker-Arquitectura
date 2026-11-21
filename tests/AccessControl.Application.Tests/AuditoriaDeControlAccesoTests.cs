using AccessControl.Application.Interfaces;
using AccessControl.Application.Service;
using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Domain.Interfaces;
using AccessControl.Domain.Excepciones;
using Microsoft.Extensions.Logging;
using Moq;

namespace AccessControl.Application.Tests;

/// <summary>
/// Eventos de Control de Acceso que van al historial de auditoría (RF-AUD-04 y 05): cambio de rol, desactivación, reactivación y
/// restablecimiento de contraseña (propio y forzado). Con un auditor que captura lo recibido, sin API ni base de datos (RD-12).
/// </summary>
public class AuditoriaDeControlAccesoTests
{
    private const string ClaveNueva = "ClaveNueva-Nexus-2026";

    private sealed record Registro(Guid UsuarioId, string Responsable, string Accion, string Entidad, string Identificador, string Anterior, string Nuevo)
    {
        /// <summary>RF-AUD-01: ninguno de los datos del registro puede faltar.</summary>
        public bool EstaCompleto =>
            UsuarioId != Guid.Empty && new[] { Responsable, Accion, Entidad, Identificador, Anterior, Nuevo }.All(d => !string.IsNullOrWhiteSpace(d));

        public bool Contiene(string texto) =>
            new[] { Responsable, Accion, Entidad, Identificador, Anterior, Nuevo }.Any(d => d.Contains(texto, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class AuditorQueCaptura : IAuditoriaService
    {
        public List<Registro> Registros { get; } = [];

        public Task RegistroAuditoriaAsync(
            Guid usuarioId, string nombreUsuarioResponsable, string accion, string entidadAfectada, string identificador,
            string valorAnterior, string valorNuevo)
        {
            Registros.Add(new Registro(usuarioId, nombreUsuarioResponsable, accion, entidadAfectada, identificador, valorAnterior, valorNuevo));
            return Task.CompletedTask;
        }
    }

    private sealed class Escenario
    {
        public List<Usuario> Usuarios { get; } = [];
        public List<string> CodigosEnviados { get; } = [];
        public AuditorQueCaptura Auditor { get; } = new();
        public Mock<IUsuarioRepository> Repo { get; } = new();
        public Mock<IEmailQueue> Cola { get; } = new();
        public Mock<IPasswordHasher> Hasher { get; } = new();

        public Escenario()
        {
            Repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Guid id) => Usuarios.FirstOrDefault(u => u.Id == id));
            Repo.Setup(r => r.GetByCorreoAsync(It.IsAny<Email>()))
                .ReturnsAsync((Email correo) => Usuarios.FirstOrDefault(u => u.Correo.Valor == correo.Valor));
            Repo.Setup(r => r.UpdateAsync(It.IsAny<Usuario>())).Returns(Task.CompletedTask);
            Repo.Setup(r => r.ContarAdministradoresActivosAsync()).ReturnsAsync(2);
            Cola.Setup(c => c.EncolarCorreoRecuperacionAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((_, codigo) => CodigosEnviados.Add(codigo))
                .Returns(Task.CompletedTask);
            Hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hash-secreto-nuevo");
            Hasher.Setup(h => h.Verificar("hash-guardado", "buena")).Returns(true);
        }

        public UsuarioService Servicio => new(
            Repo.Object, Hasher.Object, Cola.Object, Auditor, Mock.Of<IJwtProvider>(),
            GestorSesionesDePrueba.Crear(), Mock.Of<ILogger<UsuarioService>>());

        public Usuario Agregar(string correo, Rol rol = Rol.Estandar)
        {
            var usuario = new Usuario($"Usuario {correo}", new Email(correo), "hash-guardado", rol);
            usuario.Activar();
            Usuarios.Add(usuario);
            return usuario;
        }
    }

    private static void RetrocederEmision(Usuario usuario) =>
        typeof(CodigoRecuperacion).GetProperty(nameof(CodigoRecuperacion.FechaEmision))!
            .SetValue(usuario.Recuperacion, DateTime.UtcNow - TimeSpan.FromMinutes(5));

    // ---------- RF-AUD-04: rol y estado ----------

    [Fact]
    [Trait("Requirement", "RF-AUD-04")]
    public async Task RF_AUD_04_CambiarElRol_DejaUnRegistroConQuienActuoLaCuentaAfectadaYElRolAnteriorYElNuevo()
    {
        // Arrange
        var e = new Escenario();
        var admin = e.Agregar("admin@ejemplo.com", Rol.Administrador);
        var persona = e.Agregar("persona@ejemplo.com");

        // Act
        await e.Servicio.CambiarRolAsync(persona.Id, "Administrador", admin.Id);

        // Assert
        var r = Assert.Single(e.Auditor.Registros);
        Assert.True(r.EstaCompleto);
        Assert.Equal(admin.Id, r.UsuarioId);
        Assert.Equal("Cambio de rol", r.Accion);
        Assert.Equal("Usuario", r.Entidad);
        Assert.Equal(persona.Id.ToString(), r.Identificador);
        Assert.Equal("Estandar", r.Anterior);
        Assert.Equal("Administrador", r.Nuevo);
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-04")]
    public async Task RF_AUD_04_DesactivarYLuegoReactivarUnUsuario_DejanDosRegistrosConElEstadoAnteriorYElNuevo()
    {
        // Arrange
        var e = new Escenario();
        var admin = e.Agregar("admin@ejemplo.com", Rol.Administrador);
        var persona = e.Agregar("persona@ejemplo.com");

        // Act
        await e.Servicio.CambiarEstadoAsync(persona.Id, false, admin.Id);
        await e.Servicio.CambiarEstadoAsync(persona.Id, true, admin.Id);

        // Assert
        Assert.Equal(2, e.Auditor.Registros.Count);
        Assert.All(e.Auditor.Registros, r =>
        {
            Assert.True(r.EstaCompleto);
            Assert.Equal(admin.Id, r.UsuarioId);
            Assert.Equal("Cambio de estado", r.Accion);
            Assert.Equal(persona.Id.ToString(), r.Identificador);
        });
        Assert.Equal(("Activo", "Inactivo"), (e.Auditor.Registros[0].Anterior, e.Auditor.Registros[0].Nuevo));
        Assert.Equal(("Inactivo", "Activo"), (e.Auditor.Registros[1].Anterior, e.Auditor.Registros[1].Nuevo));
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-04")]
    public async Task RF_AUD_04_QuienReactivaSuPropiaCuentaAlIniciarSesion_QuedaRegistrado()
    {
        // Arrange
        var e = new Escenario();
        var persona = e.Agregar("persona@ejemplo.com");
        persona.SolicitarDesactivacion(DateTime.UtcNow);
        var jwt = new Mock<IJwtProvider>();
        jwt.Setup(j => j.Generar(It.IsAny<Usuario>())).Returns("jwt");
        var servicio = new UsuarioService(
            e.Repo.Object, e.Hasher.Object, e.Cola.Object, e.Auditor, jwt.Object,
            GestorSesionesDePrueba.Crear(), Mock.Of<ILogger<UsuarioService>>());

        // Act
        await servicio.AutenticarUsuarioAsync(new("persona@ejemplo.com", "buena"));

        // Assert
        var r = Assert.Single(e.Auditor.Registros);
        Assert.True(r.EstaCompleto);
        Assert.Equal(persona.Id, r.UsuarioId);
        Assert.Equal("Reactivación de cuenta", r.Accion);
        Assert.Equal("Inactivo (desactivada por su dueño)", r.Anterior);
        Assert.Equal("Activo", r.Nuevo);
    }

    // ---------- RF-AUD-05: restablecimiento de contraseña ----------

    [Fact]
    [Trait("Requirement", "RF-AUD-05")]
    public async Task RF_AUD_05_UnRestablecimientoQueIniciaLaPropiaPersona_QuedaRegistradoComoSuyo_SinLaContrasenaNiElCodigo()
    {
        // Arrange
        var e = new Escenario();
        var persona = e.Agregar("persona@ejemplo.com");
        persona.GenerarCodigoRecuperacion("ABC123", 1);

        // Act
        await e.Servicio.RestablecerPasswordAsync("persona@ejemplo.com", "ABC123", ClaveNueva);

        // Assert
        var r = Assert.Single(e.Auditor.Registros);
        Assert.True(r.EstaCompleto);
        Assert.Equal(persona.Id, r.UsuarioId);
        Assert.Equal("Restablecimiento de contraseña", r.Accion);
        Assert.Equal(persona.Id.ToString(), r.Identificador);
        Assert.Equal("Contraseña anterior (no se guarda)", r.Anterior);
        Assert.Equal("Contraseña nueva (no se guarda); iniciado por: el propio usuario", r.Nuevo);
        Assert.DoesNotContain(e.Auditor.Registros, x => x.Contiene("ABC123") || x.Contiene(ClaveNueva) || x.Contiene("hash"));
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-05")]
    public async Task RF_AUD_05_UnRestablecimientoForzadoPorUnAdministrador_DejaDosRegistros_YElSegundoDiceQueLoInicioElAdministrador()
    {
        // Arrange
        var e = new Escenario();
        var admin = e.Agregar("admin@ejemplo.com", Rol.Administrador);
        var persona = e.Agregar("persona@ejemplo.com");

        // Act: el Administrador lo fuerza y la persona lo completa con el código que le llegó
        await e.Servicio.ForzarRestablecimientoAsync(persona.Id, admin.Id);
        var codigo = Assert.Single(e.CodigosEnviados);
        await e.Servicio.RestablecerPasswordAsync("persona@ejemplo.com", codigo, ClaveNueva);

        // Assert
        Assert.Equal(2, e.Auditor.Registros.Count);
        var forzado = e.Auditor.Registros[0];
        var completado = e.Auditor.Registros[1];
        Assert.True(forzado.EstaCompleto);
        Assert.Equal(admin.Id, forzado.UsuarioId);
        Assert.Equal("Restablecimiento forzado de contraseña", forzado.Accion);
        Assert.Equal(persona.Id.ToString(), forzado.Identificador);
        Assert.Equal("Contraseña anterior activa", forzado.Anterior);
        Assert.Equal("Contraseña invalidada (restablecimiento forzado por administrador)", forzado.Nuevo);
        Assert.True(completado.EstaCompleto);
        Assert.Equal(persona.Id, completado.UsuarioId);
        Assert.Equal("Restablecimiento de contraseña", completado.Accion);
        Assert.Equal("Contraseña anterior (no se guarda)", completado.Anterior);
        Assert.Equal("Contraseña nueva (no se guarda); iniciado por: un Administrador", completado.Nuevo);
        Assert.DoesNotContain(e.Auditor.Registros, x => x.Contiene(codigo) || x.Contiene(ClaveNueva) || x.Contiene("hash"));
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-05")]
    public async Task RF_AUD_05_SiLaPersonaPideSuPropioCodigoDespuesDeUnForzado_ElRegistroDiceQueLoInicioElla()
    {
        // Arrange: un Administrador forzó el restablecimiento, pero la persona pide ella misma otro código
        var e = new Escenario();
        var admin = e.Agregar("admin@ejemplo.com", Rol.Administrador);
        var persona = e.Agregar("persona@ejemplo.com");
        await e.Servicio.ForzarRestablecimientoAsync(persona.Id, admin.Id);
        RetrocederEmision(persona); // pasado el enfriamiento de un minuto

        // Act
        await e.Servicio.IniciarRecuperacionAsync("persona@ejemplo.com");
        var codigoPropio = e.CodigosEnviados.Last();
        await e.Servicio.RestablecerPasswordAsync("persona@ejemplo.com", codigoPropio, ClaveNueva);

        // Assert
        Assert.Equal(2, e.CodigosEnviados.Count);
        Assert.Contains("iniciado por: el propio usuario", e.Auditor.Registros.Last().Nuevo);
    }

    // ---------- Acciones de seguridad de la propia cuenta (para «Mi actividad») ----------

    [Fact]
    [Trait("Requirement", "RF-AUD-04")]
    public async Task RF_AUD_04_ActivarLaCuentaConElCodigo_QuedaRegistradoComoSuyo()
    {
        // Arrange
        var e = new Escenario();
        var persona = new Usuario("Persona", new Email("persona@ejemplo.com"), "hash-guardado", Rol.Estandar);
        persona.EstablecerTokenActivacion("123456", DateTime.UtcNow.AddHours(24));
        e.Usuarios.Add(persona);
        e.Repo.Setup(r => r.ReservarIntentoActivacionAsync(persona.Id)).ReturnsAsync(1);

        // Act
        await e.Servicio.ActivarCuentaAsync("persona@ejemplo.com", "123456");

        // Assert
        var r = Assert.Single(e.Auditor.Registros);
        Assert.True(r.EstaCompleto);
        Assert.Equal(persona.Id, r.UsuarioId);
        Assert.Equal(("Activación de cuenta", "Pendiente de activación", "Activo"), (r.Accion, r.Anterior, r.Nuevo));
        Assert.False(r.Contiene("123456"));
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-04")]
    public async Task RF_AUD_04_UnCodigoDeActivacionIncorrecto_NoDejaRegistro()
    {
        var e = new Escenario();
        var persona = new Usuario("Persona", new Email("persona@ejemplo.com"), "hash-guardado", Rol.Estandar);
        persona.EstablecerTokenActivacion("123456", DateTime.UtcNow.AddHours(24));
        e.Usuarios.Add(persona);
        e.Repo.Setup(r => r.ReservarIntentoActivacionAsync(persona.Id)).ReturnsAsync(1);

        await Assert.ThrowsAsync<CodigoInvalidoException>(() => e.Servicio.ActivarCuentaAsync("persona@ejemplo.com", "654321"));

        Assert.Empty(e.Auditor.Registros);
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-04")]
    public async Task RF_AUD_04_CambiarLaContrasena_QuedaRegistradoSinNingunaDeLasDos()
    {
        // Arrange
        var e = new Escenario();
        var persona = e.Agregar("persona@ejemplo.com");

        // Act
        await e.Servicio.CambiarPasswordAsync(persona.Id, "buena", ClaveNueva);

        // Assert
        var r = Assert.Single(e.Auditor.Registros);
        Assert.True(r.EstaCompleto);
        Assert.Equal(persona.Id, r.UsuarioId);
        Assert.Equal("Cambio de contraseña", r.Accion);
        Assert.False(r.Contiene("buena"));
        Assert.False(r.Contiene(ClaveNueva));
        Assert.False(r.Contiene("hash"));
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-04")]
    public async Task RF_AUD_04_UnaClaveActualIncorrecta_NoRegistraUnCambio()
    {
        var e = new Escenario();
        var persona = e.Agregar("persona@ejemplo.com");

        await Assert.ThrowsAsync<ClaveActualIncorrectaException>(() => e.Servicio.CambiarPasswordAsync(persona.Id, "mala", ClaveNueva));

        Assert.Empty(e.Auditor.Registros);
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-04")]
    public async Task RF_AUD_04_VincularYDesvincularGoogle_QuedanRegistrados()
    {
        // Arrange
        var e = new Escenario();
        var persona = e.Agregar("persona@ejemplo.com");
        var validador = new Mock<IValidadorIdTokenGoogle>();
        validador.Setup(v => v.ValidarAsync("id-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentidadExterna("sub-1", "persona@ejemplo.com", true, "Persona"));
        var vinculos = new Mock<ILoginExternoRepository>();
        vinculos.Setup(v => v.AgregarAsync(It.IsAny<LoginExterno>())).Returns(Task.CompletedTask);
        var jwt = new Mock<IJwtProvider>();
        jwt.Setup(j => j.Generar(It.IsAny<Usuario>())).Returns("jwt");
        var servicio = new ServicioLoginExterno(
            validador.Object, e.Repo.Object, vinculos.Object, e.Hasher.Object, jwt.Object,
            VersionesLegales.PorDefecto, Mock.Of<ILogger<ServicioLoginExterno>>(), e.Auditor);

        // Act
        await servicio.VincularGoogleAsync("id-token", "buena");
        vinculos.Setup(v => v.ObtenerDeUsuarioAsync(persona.Id, "google"))
            .ReturnsAsync(new LoginExterno(persona.Id, "google", "sub-1", DateTime.UtcNow));
        vinculos.Setup(v => v.EliminarAsync(persona.Id, "google")).ReturnsAsync(true);
        await servicio.DesvincularAsync(persona.Id, "google");

        // Assert
        Assert.Equal(2, e.Auditor.Registros.Count);
        Assert.All(e.Auditor.Registros, r => { Assert.True(r.EstaCompleto); Assert.Equal(persona.Id, r.UsuarioId); });
        Assert.Equal(("Vinculación de Google", "Sin Google", "Google vinculado"),
            (e.Auditor.Registros[0].Accion, e.Auditor.Registros[0].Anterior, e.Auditor.Registros[0].Nuevo));
        Assert.Equal(("Desvinculación de Google", "Google vinculado", "Sin Google"),
            (e.Auditor.Registros[1].Accion, e.Auditor.Registros[1].Anterior, e.Auditor.Registros[1].Nuevo));
        Assert.DoesNotContain(e.Auditor.Registros, r => r.Contiene("sub-1"));
    }
}
