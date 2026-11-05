using AccessControl.Application.DTOs;
using AccessControl.Application.Interfaces;
using AccessControl.Application.Service;
using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Domain.Excepciones;
using AccessControl.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace AccessControl.Application.Tests;

/// <summary>
/// Inicio de sesión: cada rechazo tiene su código estable (RF-CA-03, RF-CA-15, RF-CA-19, RF-CA-20) y un correo
/// inexistente no se puede distinguir de una clave incorrecta (ni por el mensaje ni por el tiempo de respuesta).
/// </summary>
public class LoginYCodigosDeErrorTests
{
    private const string Correo = "persona@ejemplo.com";
    private const string Clave = "ClaveSegura123";

    private static Usuario UsuarioActivo()
    {
        var usuario = new Usuario("Persona de prueba", new Email(Correo), "hash-guardado", Rol.Estandar);
        usuario.EstablecerTokenActivacion("123456", DateTime.UtcNow.AddHours(24));
        usuario.CompletarActivacion("123456");
        return usuario;
    }

    private static Usuario UsuarioPendiente()
    {
        var usuario = new Usuario("Persona de prueba", new Email(Correo), "hash-guardado", Rol.Estandar);
        usuario.EstablecerTokenActivacion("123456", DateTime.UtcNow.AddHours(24));
        return usuario;
    }

    private static (UsuarioService Servicio, Mock<IPasswordHasher> Hasher, Mock<IUsuarioRepository> Repo) Crear(
        Usuario? usuario, bool claveCorrecta)
    {
        var repo = new Mock<IUsuarioRepository>();
        repo.Setup(r => r.GetByCorreoAsync(It.IsAny<Email>())).ReturnsAsync(usuario);
        repo.Setup(r => r.UpdateAsync(It.IsAny<Usuario>())).Returns(Task.CompletedTask);

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Verificar(It.IsAny<string>(), It.IsAny<string>())).Returns(claveCorrecta);

        var jwt = new Mock<IJwtProvider>();
        jwt.Setup(j => j.Generar(It.IsAny<Usuario>())).Returns("jwt-de-prueba");

        var servicio = new UsuarioService(
            repo.Object, hasher.Object, Mock.Of<IEmailQueue>(), Mock.Of<IAuditoriaService>(),
            jwt.Object, Mock.Of<ITokenBlacklist>(), Mock.Of<ILogger<UsuarioService>>());
        return (servicio, hasher, repo);
    }

    private static LoginRequestDto Login(string clave = Clave) => new(Correo, clave);

    [Fact]
    public async Task RF_CA_03_CorreoInexistente_GastaElMismoTiempoQueUnaVerificacionRealYRechazaIgual()
    {
        var (servicio, hasher, _) = Crear(usuario: null, claveCorrecta: false);

        await Assert.ThrowsAsync<CredencialesInvalidasException>(() => servicio.AutenticarUsuarioAsync(Login()));

        // Sin la simulación, este camino respondería antes y delataría que el correo no está registrado.
        hasher.Verify(h => h.SimularVerificacion(Clave), Times.Once);
        hasher.Verify(h => h.Verificar(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RF_CA_03_ClaveIncorrecta_RespondeIgualQueUnCorreoInexistente()
    {
        var (servicio, _, _) = Crear(UsuarioActivo(), claveCorrecta: false);

        var conCorreoReal = await Assert.ThrowsAsync<CredencialesInvalidasException>(() => servicio.AutenticarUsuarioAsync(Login("otra")));
        var (servicioSinUsuario, _, _) = Crear(null, claveCorrecta: false);
        var sinCorreo = await Assert.ThrowsAsync<CredencialesInvalidasException>(() => servicioSinUsuario.AutenticarUsuarioAsync(Login("otra")));

        Assert.Equal(sinCorreo.Message, conCorreoReal.Message);
    }

    [Fact]
    public async Task RF_CA_15_ClaveCorrectaEnCuentaSinActivar_DiceQueNoEstaActiva()
    {
        var (servicio, _, _) = Crear(UsuarioPendiente(), claveCorrecta: true);

        var error = await Assert.ThrowsAsync<CuentaNoActivaException>(() => servicio.AutenticarUsuarioAsync(Login()));

        Assert.Equal("cuenta_no_activa", error.Codigo);
    }

    [Fact]
    public async Task RF_CA_15_ClaveIncorrectaEnCuentaSinActivar_NoRevelaQueEstaSinActivar()
    {
        var (servicio, _, _) = Crear(UsuarioPendiente(), claveCorrecta: false);

        await Assert.ThrowsAsync<CredencialesInvalidasException>(() => servicio.AutenticarUsuarioAsync(Login("mala")));
    }

    [Fact]
    public async Task RF_CA_20_ClaveCorrectaEnCuentaDesactivadaPorUnAdministrador_DiceQueEstaDesactivada()
    {
        var usuario = UsuarioActivo();
        usuario.Desactivar();
        var (servicio, _, _) = Crear(usuario, claveCorrecta: true);

        var error = await Assert.ThrowsAsync<CuentaDesactivadaException>(() => servicio.AutenticarUsuarioAsync(Login()));

        Assert.Equal("cuenta_desactivada", error.Codigo);
        Assert.False(usuario.PendienteDeActivacion);
    }

    [Fact]
    public async Task RF_CA_19_QuintoFalloBloqueaYLaClaveCorrectaTampocoEntraMientrasDuraElBloqueo()
    {
        var usuario = UsuarioActivo();
        var (servicio, hasher, _) = Crear(usuario, claveCorrecta: false);

        for (var i = 1; i <= 4; i++)
        {
            await Assert.ThrowsAsync<CredencialesInvalidasException>(() => servicio.AutenticarUsuarioAsync(Login("mala")));
        }

        var quinto = await Assert.ThrowsAsync<CuentaBloqueadaException>(() => servicio.AutenticarUsuarioAsync(Login("mala")));
        Assert.Equal("cuenta_bloqueada", quinto.Codigo);

        // Sexto intento, ahora con la clave correcta: sigue rechazado.
        hasher.Setup(h => h.Verificar(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        await Assert.ThrowsAsync<CuentaBloqueadaException>(() => servicio.AutenticarUsuarioAsync(Login()));
    }

    [Fact]
    public async Task RF_CA_03_LoginCorrecto_ReiniciaElContadorYEmiteElToken()
    {
        var usuario = UsuarioActivo();
        usuario.RegistrarIntentoFallido();
        var (servicio, _, _) = Crear(usuario, claveCorrecta: true);

        var respuesta = await servicio.AutenticarUsuarioAsync(Login());

        Assert.Equal("jwt-de-prueba", respuesta.Token);
        Assert.Equal(0, usuario.Bloqueo.IntentosFallidos);
    }

    [Theory]
    [InlineData("persona@ejemplo.com", "p***@ejemplo.com")]
    [InlineData("a@b.co", "a***@b.co")]
    [InlineData("", "***")]
    [InlineData(null, "***")]
    [InlineData("sin-arroba", "***")]
    [InlineData("@dominio.com", "***")]
    public void RD_08_Enmascarar_NoDejaElCorreoCompletoEnLosLogs(string? correo, string esperado)
    {
        Assert.Equal(esperado, Email.Enmascarar(correo));
    }

    [Fact]
    public void RD_08_Enmascarado_UsaElCorreoNormalizado()
    {
        Assert.Equal("p***@ejemplo.com", new Email("  Persona@Ejemplo.COM ").Enmascarado());
    }
}
