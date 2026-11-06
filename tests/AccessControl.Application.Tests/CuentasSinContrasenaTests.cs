using AccessControl.Application.Interfaces;
using AccessControl.Application.DTOs;
using AccessControl.Application.Service;
using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Domain.Excepciones;
using AccessControl.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace AccessControl.Application.Tests;

/// <summary>Cuentas creadas con Google: no tienen contraseña, pero pueden crear una con la recuperación (RF-CA-02, 03, 09, 11, 13, 22).</summary>
public class CuentasSinContrasenaTests
{
    private const string Correo = "persona@ejemplo.com";

    private readonly Mock<IUsuarioRepository> _repo = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IEmailQueue> _cola = new();

    private UsuarioService Servicio() => new(
        _repo.Object, _hasher.Object, _cola.Object, Mock.Of<IAuditoriaService>(), Mock.Of<IJwtProvider>(),
        GestorSesionesDePrueba.Crear(), Mock.Of<ILogger<UsuarioService>>());

    private Usuario CuentaDeGoogle()
    {
        var usuario = Usuario.CrearConProveedorExterno("Persona", new Email(Correo));
        _repo.Setup(r => r.GetByCorreoAsync(It.IsAny<Email>())).ReturnsAsync(usuario);
        _repo.Setup(r => r.GetByIdAsync(usuario.Id)).ReturnsAsync(usuario);
        return usuario;
    }

    // ---------------- Dominio ----------------

    [Fact]
    public void RF_CA_02_UnaCuentaDeGoogleNaceActivaSinContrasenaNiCodigoDeActivacion()
    {
        var usuario = Usuario.CrearConProveedorExterno("Persona", new Email(Correo));

        Assert.True(usuario.Activo);
        Assert.False(usuario.PendienteDeActivacion);
        Assert.Null(usuario.PasswordHash);
        Assert.False(usuario.TienePassword);
        Assert.Null(usuario.TokenAcceso);
        Assert.Equal(Rol.Estandar, usuario.Rol);
        Assert.NotEqual(Guid.Empty, usuario.Id);
    }

    [Fact]
    public void RF_CA_02_UnaCuentaConContrasenaLaTiene_YUnaInvalidadaPorUnAdministradorYaNo()
    {
        var usuario = new Usuario("Persona", new Email(Correo), "hash-guardado");
        Assert.True(usuario.TienePassword);

        usuario.InvalidarPassword();

        Assert.False(usuario.TienePassword);
        Assert.NotNull(usuario.PasswordHash);   // sigue siendo un valor imposible de acertar
    }

    [Fact]
    public void RF_CA_13_InvalidarLaContrasenaDeUnaCuentaSinContrasenaNoInventaUnValor()
    {
        var usuario = Usuario.CrearConProveedorExterno("Persona", new Email(Correo));

        usuario.InvalidarPassword();

        Assert.Null(usuario.PasswordHash);
    }

    [Fact]
    public void RD_07_UnVinculoExigeUsuarioProveedorConocidoYSubject()
    {
        var ahora = DateTime.UtcNow;

        Assert.Throws<ArgumentException>(() => new LoginExterno(Guid.Empty, "google", "sub", ahora));
        Assert.Throws<ArgumentException>(() => new LoginExterno(Guid.NewGuid(), "facebook", "sub", ahora));
        Assert.Throws<ArgumentException>(() => new LoginExterno(Guid.NewGuid(), "", "sub", ahora));
        Assert.Throws<ArgumentException>(() => new LoginExterno(Guid.NewGuid(), "google", "  ", ahora));
        Assert.Throws<ArgumentException>(() => new LoginExterno(Guid.NewGuid(), "google", new string('s', 256), ahora));

        var valido = new LoginExterno(Guid.NewGuid(), "google", "sub", ahora);
        Assert.Equal("sub", valido.Subject);
        Assert.Equal(ahora, valido.VinculadoEn);
    }

    // ---------------- Inicio de sesión con contraseña ----------------

    [Fact]
    public async Task RF_CA_03_UnaCuentaSinContrasenaNoEntraConContrasena_Responde401GenericoSinContarElIntento()
    {
        var usuario = CuentaDeGoogle();

        await Assert.ThrowsAsync<CredencialesInvalidasException>(
            () => Servicio().AutenticarUsuarioAsync(new LoginRequestDto(Correo, "ClaveSegura-Nexus-2026")));

        _hasher.Verify(h => h.SimularVerificacion("ClaveSegura-Nexus-2026"), Times.Once);   // mismo tiempo que una verificación real
        _hasher.Verify(h => h.Verificar(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        Assert.Equal(0, usuario.Bloqueo.IntentosFallidos);   // no hay clave que adivinar: no se puede bloquear a la persona
        _repo.Verify(r => r.UpdateAsync(It.IsAny<Usuario>()), Times.Never);
    }

    // ---------------- Cambiar contraseña ----------------

    [Fact]
    public async Task RF_CA_22_CambiarLaContrasenaSinTenerlaResponde_sin_contrasena()
    {
        var usuario = CuentaDeGoogle();

        var error = await Assert.ThrowsAsync<SinContrasenaException>(
            () => Servicio().CambiarPasswordAsync(usuario.Id, "lo-que-sea", "NuevaClave-Nexus-2026"));

        Assert.Equal("sin_contrasena", error.Codigo);
        Assert.Equal(0, usuario.Bloqueo.IntentosFallidos);
        _hasher.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
    }

    // ---------------- Recuperación: crear la primera contraseña ----------------

    [Fact]
    public async Task RF_CA_09_UnaCuentaDeGoogleRecibeElCodigoDeRecuperacion()
    {
        CuentaDeGoogle();

        await Servicio().IniciarRecuperacionAsync(Correo);

        _cola.Verify(c => c.EncolarCorreoRecuperacionAsync(Correo, It.IsRegex("^[0-9]{6}$")), Times.Once);
    }

    [Fact]
    public async Task RF_CA_11_ConElCodigoUnaCuentaDeGooglePuedeCrearSuPrimeraContrasena()
    {
        var usuario = CuentaDeGoogle();
        usuario.GenerarCodigoRecuperacion("654321", 1);
        _hasher.Setup(h => h.Hash("NuevaClave-Nexus-2026")).Returns("hash-nuevo");

        await Servicio().RestablecerPasswordAsync(Correo, "654321", "NuevaClave-Nexus-2026");

        Assert.Equal("hash-nuevo", usuario.PasswordHash);
        Assert.True(usuario.TienePassword);   // ahora puede entrar con contraseña y desvincular Google
    }

    [Fact]
    public async Task RF_CA_13_ElRestablecimientoForzadoNoDejaSinContrasenaAUnaCuentaDeGoogleConUnValorInventado()
    {
        var usuario = CuentaDeGoogle();
        var admin = new Usuario("Admin", new Email("admin@ejemplo.com"), "hash", Rol.Administrador);
        admin.Activar();
        _repo.Setup(r => r.GetByIdAsync(admin.Id)).ReturnsAsync(admin);

        await Servicio().ForzarRestablecimientoAsync(usuario.Id, admin.Id);

        Assert.Null(usuario.PasswordHash);
        Assert.NotNull(usuario.Recuperacion);   // recibe el código para crear una contraseña
    }
}
