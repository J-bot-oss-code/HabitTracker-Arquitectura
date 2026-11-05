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
/// Política de contraseñas (RF-CA-14, RD-07): 12 a 1024 caracteres con letra y número, y que no aparezca en filtraciones
/// conocidas. Cumplir esta política implica cumplir el mínimo del PDF (8 con letras y números).
/// </summary>
public class PoliticaContrasenaTests
{
    // ====================== La política sola ======================

    [Theory]
    [InlineData("ClaveSegura123")]
    [InlineData("abcdefghij12")]                 // justo 12
    [InlineData("Con espacios y 12345")]
    [InlineData("Contraseña-ñandú-2026")]
    public void RF_CA_14_UnaClaveConFormatoValido_Pasa(string password)
    {
        PoliticaContrasena.SoloFormato.ValidarFormato(password);
    }

    [Fact]
    public void RF_CA_14_ElLimiteSuperiorDe1024CaracteresEsValido_Y1025No()
    {
        var justo = new string('a', 1022) + "12";
        Assert.Equal(1024, justo.Length);
        PoliticaContrasena.SoloFormato.ValidarFormato(justo);

        Assert.Throws<ContrasenaNoValidaException>(() => PoliticaContrasena.SoloFormato.ValidarFormato(justo + "x"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("            ")]                 // 12 espacios
    [InlineData("Clave123")]                     // 8: cumplía el mínimo anterior, ya no
    [InlineData("Corta1abcde")]                  // 11
    [InlineData("SoloLetrasSinNumero")]
    [InlineData("1234567890123456")]            // sin letras
    public void RF_CA_14_UnaClaveCortaOSinLetraONumero_SeRechazaConSuCodigo(string? password)
    {
        var error = Assert.Throws<ContrasenaNoValidaException>(() => PoliticaContrasena.SoloFormato.ValidarFormato(password));

        Assert.Equal("contrasena_no_valida", error.Codigo);
    }

    [Fact]
    public async Task RF_CA_14_UnaClaveFiltrada_SeRechazaAunqueTengaElFormatoCorrecto()
    {
        var verificador = new Mock<IVerificadorContrasenasFiltradas>();
        verificador.Setup(v => v.EstaFiltradaAsync("ClaveSegura123", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var politica = new PoliticaContrasena(verificador.Object);

        politica.ValidarFormato("ClaveSegura123");
        var error = await Assert.ThrowsAsync<ContrasenaFiltradaException>(() => politica.ValidarNoFiltradaAsync("ClaveSegura123"));

        Assert.Equal("contrasena_filtrada", error.Codigo);
    }

    [Fact]
    public async Task RF_CA_14_UnaClaveQueNoEstaFiltrada_Pasa_YLaPoliticaSoloFormatoNuncaLaMarca()
    {
        var verificador = new Mock<IVerificadorContrasenasFiltradas>();
        verificador.Setup(v => v.EstaFiltradaAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await new PoliticaContrasena(verificador.Object).ValidarNoFiltradaAsync("UnaClaveNueva1234");
        await PoliticaContrasena.SoloFormato.ValidarNoFiltradaAsync("password1234");
    }

    // ====================== Integrada en el servicio ======================

    private sealed class Escenario
    {
        public Mock<IUsuarioRepository> Repo { get; } = new();
        public Mock<IEmailQueue> Cola { get; } = new();
        public Mock<ITokenBlacklist> Blacklist { get; } = new();
        public Mock<IPasswordHasher> Hasher { get; } = new();
        public Mock<IVerificadorContrasenasFiltradas> Verificador { get; } = new();
        public Usuario? Usuario { get; set; }

        public Escenario(bool claveFiltrada = false)
        {
            Verificador.Setup(v => v.EstaFiltradaAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(claveFiltrada);
            Repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(() => Usuario);
            Repo.Setup(r => r.GetByCorreoAsync(It.IsAny<Email>())).ReturnsAsync(() => Usuario);
            Repo.Setup(r => r.UpdateAsync(It.IsAny<Usuario>())).Returns(Task.CompletedTask);
            Repo.Setup(r => r.SaveAsync(It.IsAny<Usuario>())).Returns(Task.CompletedTask);
            Repo.Setup(r => r.CorreoExistsAsync(It.IsAny<Email>())).ReturnsAsync(false);
            Cola.Setup(c => c.EncolarCorreoActivacionAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
            Hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hash-nuevo");
        }

        public UsuarioService Servicio => new(
            Repo.Object, Hasher.Object, Cola.Object, Mock.Of<IAuditoriaService>(), Mock.Of<IJwtProvider>(),
            Blacklist.Object, Mock.Of<ILogger<UsuarioService>>(), new PoliticaContrasena(Verificador.Object));

        public void VerificadorNoFueConsultado() =>
            Verificador.Verify(v => v.EstaFiltradaAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static RegistroRequestDto Registro(string password, string correo = "persona@ejemplo.com") =>
        new("Persona", correo, password);

    [Fact]
    public async Task RF_CA_14_Registro_ConClaveDe8Caracteres_SeRechazaSinConsultarNadaNiGuardar()
    {
        var e = new Escenario();

        await Assert.ThrowsAsync<ContrasenaNoValidaException>(() => e.Servicio.RegistrarUsuarioAsync(Registro("Clave123")));

        // El formato se comprueba antes de la base de datos y de cualquier llamada externa (RD-07).
        e.VerificadorNoFueConsultado();
        e.Repo.Verify(r => r.CorreoExistsAsync(It.IsAny<Email>()), Times.Never);
        e.Repo.Verify(r => r.SaveAsync(It.IsAny<Usuario>()), Times.Never);
    }

    [Fact]
    public async Task RF_CA_14_Registro_ConClaveFiltrada_SeRechazaYNoSeCreaLaCuentaNiSeEnviaCorreo()
    {
        var e = new Escenario(claveFiltrada: true);

        var error = await Assert.ThrowsAsync<ContrasenaFiltradaException>(() => e.Servicio.RegistrarUsuarioAsync(Registro("ClaveSegura123")));

        Assert.Equal("contrasena_filtrada", error.Codigo);
        e.Repo.Verify(r => r.SaveAsync(It.IsAny<Usuario>()), Times.Never);
        e.Cola.Verify(c => c.EncolarCorreoActivacionAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RD_07_Registro_ConCorreoYaRegistrado_NoHaceLaConsultaExternaDeFiltraciones()
    {
        var e = new Escenario();
        e.Repo.Setup(r => r.CorreoExistsAsync(It.IsAny<Email>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<CorreoYaRegistradoException>(() => e.Servicio.RegistrarUsuarioAsync(Registro("ClaveSegura123")));

        e.VerificadorNoFueConsultado();
    }

    [Fact]
    public async Task RF_CA_14_Registro_ConClaveValidaYNoFiltrada_CreaLaCuenta()
    {
        var e = new Escenario();

        var respuesta = await e.Servicio.RegistrarUsuarioAsync(Registro("ClaveSegura123"));

        Assert.False(respuesta.Activo);
        e.Verificador.Verify(v => v.EstaFiltradaAsync("ClaveSegura123", It.IsAny<CancellationToken>()), Times.Once);
        e.Repo.Verify(r => r.SaveAsync(It.IsAny<Usuario>()), Times.Once);
    }

    private static Usuario UsuarioConCodigoDeRecuperacion(string codigo)
    {
        var usuario = new Usuario("Persona", new Email("persona@ejemplo.com"), "hash-guardado", Rol.Estandar);
        usuario.Activar();
        usuario.GenerarCodigoRecuperacion(codigo, 1);
        return usuario;
    }

    [Fact]
    public async Task RF_CA_11_Restablecer_ConClaveFiltrada_SeRechazaPeroElCodigoNoSeConsume()
    {
        var e = new Escenario(claveFiltrada: true) { Usuario = UsuarioConCodigoDeRecuperacion("654321") };

        await Assert.ThrowsAsync<ContrasenaFiltradaException>(
            () => e.Servicio.RestablecerPasswordAsync("persona@ejemplo.com", "654321", "ClaveSegura123"));

        Assert.Equal("hash-guardado", e.Usuario.PasswordHash);
        Assert.False(e.Usuario.Recuperacion!.Usado);
        Assert.Equal(0, e.Usuario.Recuperacion.IntentosFallidos);
        e.Blacklist.Verify(b => b.RevocarSesionesAnteriores(It.IsAny<Guid>()), Times.Never);

        // Con otra clave que no esté filtrada, el MISMO código sigue sirviendo.
        e.Verificador.Setup(v => v.EstaFiltradaAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        await e.Servicio.RestablecerPasswordAsync("persona@ejemplo.com", "654321", "OtraClaveSegura987");
        Assert.Equal("hash-nuevo", e.Usuario.PasswordHash);
    }

    [Fact]
    public async Task RD_07_Restablecer_ConCodigoIncorrecto_NoConsultaFiltracionesYSiCuentaElFallo()
    {
        var e = new Escenario(claveFiltrada: true) { Usuario = UsuarioConCodigoDeRecuperacion("654321") };

        await Assert.ThrowsAsync<CodigoInvalidoException>(
            () => e.Servicio.RestablecerPasswordAsync("persona@ejemplo.com", "000000", "ClaveSegura123"));

        e.VerificadorNoFueConsultado();
        Assert.Equal(1, e.Usuario.Recuperacion!.IntentosFallidos);
    }

    [Fact]
    public async Task RF_CA_14_Restablecer_ConClaveCorta_SeRechazaAntesDeTocarLaBase()
    {
        var e = new Escenario { Usuario = UsuarioConCodigoDeRecuperacion("654321") };

        await Assert.ThrowsAsync<ContrasenaNoValidaException>(
            () => e.Servicio.RestablecerPasswordAsync("persona@ejemplo.com", "654321", "Corta123"));

        e.Repo.Verify(r => r.GetByCorreoAsync(It.IsAny<Email>()), Times.Never);
        Assert.False(e.Usuario.Recuperacion!.Usado);
    }

    private static Usuario UsuarioActivo()
    {
        var usuario = new Usuario("Persona", new Email("persona@ejemplo.com"), "hash-guardado", Rol.Estandar);
        usuario.Activar();
        return usuario;
    }

    [Fact]
    public async Task RF_CA_22_Cambiar_ConClaveNuevaCorta_SeRechazaAntesDeTocarLaBase()
    {
        var e = new Escenario { Usuario = UsuarioActivo() };

        await Assert.ThrowsAsync<ContrasenaNoValidaException>(
            () => e.Servicio.CambiarPasswordAsync(e.Usuario.Id, "ClaveActual123", "Corta123"));

        e.Repo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task RF_CA_22_Cambiar_ConClaveNuevaFiltrada_SeRechazaYNoCambiaNiCierraSesiones()
    {
        var e = new Escenario(claveFiltrada: true) { Usuario = UsuarioActivo() };
        e.Hasher.Setup(h => h.Verificar("hash-guardado", "ClaveActual123")).Returns(true);

        await Assert.ThrowsAsync<ContrasenaFiltradaException>(
            () => e.Servicio.CambiarPasswordAsync(e.Usuario.Id, "ClaveActual123", "ClaveSegura123"));

        Assert.Equal("hash-guardado", e.Usuario.PasswordHash);
        e.Blacklist.Verify(b => b.RevocarSesionesAnteriores(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task RF_CA_22_Cambiar_ConClaveActualIncorrecta_NoConsultaFiltraciones()
    {
        var e = new Escenario(claveFiltrada: true) { Usuario = UsuarioActivo() };
        e.Hasher.Setup(h => h.Verificar(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        await Assert.ThrowsAsync<ClaveActualIncorrectaException>(
            () => e.Servicio.CambiarPasswordAsync(e.Usuario.Id, "Incorrecta1234", "ClaveSegura123"));

        e.VerificadorNoFueConsultado();
    }

    [Fact]
    public async Task RF_CA_22_Cambiar_ConClaveNuevaValida_GuardaLaClaveYCierraLasSesiones()
    {
        var e = new Escenario { Usuario = UsuarioActivo() };
        e.Hasher.Setup(h => h.Verificar("hash-guardado", "ClaveActual123")).Returns(true);

        await e.Servicio.CambiarPasswordAsync(e.Usuario.Id, "ClaveActual123", "ClaveSegura123");

        Assert.Equal("hash-nuevo", e.Usuario.PasswordHash);
        e.Blacklist.Verify(b => b.RevocarSesionesAnteriores(e.Usuario.Id), Times.Once);
    }

    [Fact]
    public async Task RF_CA_03_UnaCuentaConClaveAntiguaDe8Caracteres_SigueIniciandoSesion()
    {
        // La política solo se aplica al fijar una clave nueva: iniciar sesión no la consulta.
        var e = new Escenario { Usuario = UsuarioActivo() };
        e.Hasher.Setup(h => h.Verificar("hash-guardado", "Antigua1")).Returns(true);
        var jwt = new Mock<IJwtProvider>();
        jwt.Setup(j => j.Generar(It.IsAny<Usuario>())).Returns("jwt");
        var servicio = new UsuarioService(
            e.Repo.Object, e.Hasher.Object, e.Cola.Object, Mock.Of<IAuditoriaService>(), jwt.Object,
            e.Blacklist.Object, Mock.Of<ILogger<UsuarioService>>(), new PoliticaContrasena(e.Verificador.Object));

        var respuesta = await servicio.AutenticarUsuarioAsync(new LoginRequestDto("persona@ejemplo.com", "Antigua1"));

        Assert.Equal("jwt", respuesta.Token);
        e.VerificadorNoFueConsultado();
    }
}
