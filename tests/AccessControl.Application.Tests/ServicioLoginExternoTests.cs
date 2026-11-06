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

/// <summary>Inicio de sesión con Google: los tres caminos, la vinculación con contraseña y las formas de entrar (RF-CA-01, 03, 15, 19, 20).</summary>
public class ServicioLoginExternoTests
{
    private const string Token = "id-token-de-google";
    private const string Sub = "1234567890";
    private const string Correo = "persona@ejemplo.com";

    private readonly Mock<IValidadorIdTokenGoogle> _validador = new();
    private readonly Mock<IUsuarioRepository> _usuarios = new();
    private readonly Mock<ILoginExternoRepository> _vinculos = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IJwtProvider> _jwt = new();
    private readonly ServicioLoginExterno _servicio;

    public ServicioLoginExternoTests()
    {
        _validador.Setup(v => v.ValidarAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentidadExterna(Sub, Correo, true, "Persona de Google"));
        _jwt.Setup(j => j.Generar(It.IsAny<Usuario>())).Returns("jwt-de-acceso");
        _servicio = new ServicioLoginExterno(
            _validador.Object, _usuarios.Object, _vinculos.Object, _hasher.Object, _jwt.Object,
            VersionesLegales.PorDefecto, Mock.Of<ILogger<ServicioLoginExterno>>());
    }

    private static ConsentimientosRegistroDto Vigentes(bool marketing = false) =>
        new(VersionesLegales.PorDefecto.Terminos, VersionesLegales.PorDefecto.Privacidad, marketing);

    private static Usuario ConPassword(bool activo = true)
    {
        var usuario = new Usuario("Persona", new Email(Correo), "hash-guardado");
        if (activo)
        {
            usuario.Activar();
        }
        else
        {
            usuario.EstablecerTokenActivacion("123456", DateTime.UtcNow.AddHours(24));
        }

        return usuario;
    }

    private void CuentaConCorreo(Usuario? usuario) =>
        _usuarios.Setup(u => u.GetByCorreoAsync(It.IsAny<Email>())).ReturnsAsync(usuario);

    // ---------------- Validación del token ----------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RD_07_SinTokenSeRechazaSinLlamarAlValidador(string token)
    {
        await Assert.ThrowsAsync<TokenExternoInvalidoException>(() => _servicio.IniciarConGoogleAsync(token, Vigentes()));

        _validador.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RD_07_UnTokenDeMasDe4096CaracteresSeRechazaSinLlamarAlValidador()
    {
        await Assert.ThrowsAsync<TokenExternoInvalidoException>(
            () => _servicio.IniciarConGoogleAsync(new string('x', 4097), Vigentes()));

        _validador.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RF_CA_01_UnCorreoQueGoogleNoVerificoNoSirveNiParaEntrarNiParaCrearLaCuenta()
    {
        _validador.Setup(v => v.ValidarAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentidadExterna(Sub, Correo, CorreoVerificado: false, "Persona"));

        await Assert.ThrowsAsync<TokenExternoInvalidoException>(() => _servicio.IniciarConGoogleAsync(Token, Vigentes()));
        await Assert.ThrowsAsync<TokenExternoInvalidoException>(() => _servicio.VincularGoogleAsync(Token, "clave"));

        _usuarios.VerifyNoOtherCalls();
        _vinculos.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RD_07_SiElValidadorRechazaElTokenNoSeToca_LaBaseDeDatos()
    {
        _validador.Setup(v => v.ValidarAsync(Token, It.IsAny<CancellationToken>())).ThrowsAsync(new TokenExternoInvalidoException());

        await Assert.ThrowsAsync<TokenExternoInvalidoException>(() => _servicio.IniciarConGoogleAsync(Token, Vigentes()));

        _usuarios.VerifyNoOtherCalls();
        _vinculos.VerifyNoOtherCalls();
    }

    // ---------------- Camino 1: la identidad ya está vinculada ----------------

    private Usuario VinculadoA(Usuario usuario)
    {
        _vinculos.Setup(v => v.ObtenerPorSubjectAsync(LoginExterno.ProveedorGoogle, Sub))
            .ReturnsAsync(new LoginExterno(usuario.Id, LoginExterno.ProveedorGoogle, Sub, DateTime.UtcNow));
        _usuarios.Setup(u => u.GetByIdAsync(usuario.Id)).ReturnsAsync(usuario);
        return usuario;
    }

    [Fact]
    public async Task RF_CA_03_UnaIdentidadVinculadaIniciaSesion_SinCrearNiVincularNada()
    {
        var usuario = VinculadoA(ConPassword());

        var resultado = await _servicio.IniciarConGoogleAsync(Token, consentimientos: null);

        Assert.False(resultado.CuentaNueva);
        Assert.Equal(usuario.Id, resultado.Usuario.Id);
        Assert.Equal("jwt-de-acceso", resultado.Usuario.Token);
        _usuarios.Verify(u => u.SaveAsync(It.IsAny<Usuario>(), It.IsAny<ConsentimientoUsuario>(), It.IsAny<LoginExterno>()), Times.Never);
        _vinculos.Verify(v => v.AgregarAsync(It.IsAny<LoginExterno>()), Times.Never);
    }

    [Fact]
    public async Task RF_CA_20_UnaCuentaDesactivadaPorUnAdministradorNoEntraConGoogle()
    {
        var usuario = ConPassword();
        usuario.Desactivar();
        VinculadoA(usuario);

        await Assert.ThrowsAsync<CuentaDesactivadaException>(() => _servicio.IniciarConGoogleAsync(Token, null));

        _jwt.Verify(j => j.Generar(It.IsAny<Usuario>()), Times.Never);
    }

    [Fact]
    public async Task RF_CA_15_UnaCuentaPendienteDeActivacionNoEntra()
    {
        VinculadoA(ConPassword(activo: false));

        await Assert.ThrowsAsync<CuentaNoActivaException>(() => _servicio.IniciarConGoogleAsync(Token, null));
    }

    [Fact]
    public async Task RF_CA_19_UnaCuentaBloqueadaNoEntraConGoogle()
    {
        var usuario = ConPassword();
        for (var i = 0; i < 5; i++)
        {
            usuario.RegistrarIntentoFallido();
        }

        VinculadoA(usuario);

        await Assert.ThrowsAsync<CuentaBloqueadaException>(() => _servicio.IniciarConGoogleAsync(Token, null));
    }

    [Fact]
    public async Task RD_06_SiElVinculoApuntaAUnaCuentaQueYaNoExiste_SeRechazaComoToken_Invalido()
    {
        _vinculos.Setup(v => v.ObtenerPorSubjectAsync(LoginExterno.ProveedorGoogle, Sub))
            .ReturnsAsync(new LoginExterno(Guid.NewGuid(), LoginExterno.ProveedorGoogle, Sub, DateTime.UtcNow));
        _usuarios.Setup(u => u.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Usuario?)null);

        await Assert.ThrowsAsync<TokenExternoInvalidoException>(() => _servicio.IniciarConGoogleAsync(Token, null));
    }

    // ---------------- Camino 2: el correo ya tiene cuenta ----------------

    [Fact]
    public async Task RF_CA_01_UnCorreoConCuentaNoSeVinculaSola_PideConfirmarLaContrasena()
    {
        _usuarios.Setup(u => u.CorreoExistsAsync(It.IsAny<Email>())).ReturnsAsync(true);

        var error = await Assert.ThrowsAsync<VinculacionRequeridaException>(() => _servicio.IniciarConGoogleAsync(Token, Vigentes()));

        Assert.Equal("vinculacion_requerida", error.Codigo);
        _usuarios.Verify(u => u.SaveAsync(It.IsAny<Usuario>(), It.IsAny<ConsentimientoUsuario>(), It.IsAny<LoginExterno>()), Times.Never);
        _vinculos.Verify(v => v.AgregarAsync(It.IsAny<LoginExterno>()), Times.Never);
        _jwt.Verify(j => j.Generar(It.IsAny<Usuario>()), Times.Never);
    }

    // ---------------- Camino 3: correo nuevo ----------------

    [Fact]
    public async Task RF_CA_01_UnCorreoNuevoSinConsentimientosNoCreaLaCuenta()
    {
        var error = await Assert.ThrowsAsync<ConsentimientoRequeridoException>(() => _servicio.IniciarConGoogleAsync(Token, null));

        Assert.Equal("consentimiento_requerido", error.Codigo);
        _usuarios.Verify(u => u.SaveAsync(It.IsAny<Usuario>(), It.IsAny<ConsentimientoUsuario>(), It.IsAny<LoginExterno>()), Times.Never);
    }

    public static TheoryData<ConsentimientosRegistroDto> ConsentimientosNoValidos => new()
    {
        new ConsentimientosRegistroDto("v0.1", VersionesLegales.PorDefecto.Privacidad, false),
        new ConsentimientosRegistroDto(VersionesLegales.PorDefecto.Terminos, "v0.1", false),
        new ConsentimientosRegistroDto(VersionesLegales.PorDefecto.Terminos, VersionesLegales.PorDefecto.Privacidad, null),
    };

    [Theory]
    [MemberData(nameof(ConsentimientosNoValidos))]
    public async Task RF_CA_01_UnCorreoNuevoConVersionesQueNoSonLasVigentesNoCreaLaCuenta(ConsentimientosRegistroDto consentimientos)
    {
        await Assert.ThrowsAsync<ConsentimientoNoValidoException>(() => _servicio.IniciarConGoogleAsync(Token, consentimientos));

        _usuarios.Verify(u => u.SaveAsync(It.IsAny<Usuario>(), It.IsAny<ConsentimientoUsuario>(), It.IsAny<LoginExterno>()), Times.Never);
    }

    [Fact]
    public async Task RF_CA_15_UnCorreoNuevoCreaUnaCuentaActivaSinContrasena_ConSuConsentimientoYSuVinculoEnUnaOperacion()
    {
        Usuario? creado = null;
        ConsentimientoUsuario? consentimiento = null;
        LoginExterno? vinculo = null;
        _usuarios.Setup(u => u.SaveAsync(It.IsAny<Usuario>(), It.IsAny<ConsentimientoUsuario>(), It.IsAny<LoginExterno>()))
            .Callback<Usuario, ConsentimientoUsuario, LoginExterno>((u, c, l) => (creado, consentimiento, vinculo) = (u, c, l))
            .Returns(Task.CompletedTask);

        var resultado = await _servicio.IniciarConGoogleAsync(Token, Vigentes(marketing: true));

        Assert.True(resultado.CuentaNueva);
        Assert.Equal("jwt-de-acceso", resultado.Usuario.Token);
        Assert.NotNull(creado);
        Assert.True(creado!.Activo);                    // Google ya verificó el correo
        Assert.Null(creado.PasswordHash);               // no hay contraseña que guardar
        Assert.False(creado.TienePassword);
        Assert.Null(creado.TokenAcceso);
        Assert.Equal("Persona de Google", creado.NombreCompleto);
        Assert.Equal(Correo, creado.Correo.Valor);
        Assert.Equal(Rol.Estandar, creado.Rol);
        Assert.Equal(creado.Id, consentimiento!.UsuarioId);
        Assert.Equal(ConsentimientoUsuario.OrigenGoogle, consentimiento.Origen);
        Assert.True(consentimiento.Marketing);
        Assert.Equal(VersionesLegales.PorDefecto.Terminos, consentimiento.TerminosVersion);
        Assert.Equal(creado.Id, vinculo!.UsuarioId);
        Assert.Equal(LoginExterno.ProveedorGoogle, vinculo.Proveedor);
        Assert.Equal(Sub, vinculo.Subject);
    }

    [Fact]
    public async Task RF_CA_01_SiGoogleNoTrajoElNombre_SeUsaLaParteLocalDelCorreo()
    {
        _validador.Setup(v => v.ValidarAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentidadExterna(Sub, Correo, true, null));
        Usuario? creado = null;
        _usuarios.Setup(u => u.SaveAsync(It.IsAny<Usuario>(), It.IsAny<ConsentimientoUsuario>(), It.IsAny<LoginExterno>()))
            .Callback<Usuario, ConsentimientoUsuario, LoginExterno>((u, _, _) => creado = u).Returns(Task.CompletedTask);

        await _servicio.IniciarConGoogleAsync(Token, Vigentes());

        Assert.Equal("persona", creado!.NombreCompleto);
    }

    [Fact]
    public async Task RD_07_UnNombreDeMasDe200CaracteresSeTrunca()
    {
        _validador.Setup(v => v.ValidarAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentidadExterna(Sub, Correo, true, new string('N', 500)));
        Usuario? creado = null;
        _usuarios.Setup(u => u.SaveAsync(It.IsAny<Usuario>(), It.IsAny<ConsentimientoUsuario>(), It.IsAny<LoginExterno>()))
            .Callback<Usuario, ConsentimientoUsuario, LoginExterno>((u, _, _) => creado = u).Returns(Task.CompletedTask);

        await _servicio.IniciarConGoogleAsync(Token, Vigentes());

        Assert.Equal(200, creado!.NombreCompleto.Length);
    }

    // ---------------- Vincular con la contraseña ----------------

    [Fact]
    public async Task RF_CA_19_VincularConUnaClaveIncorrectaCuentaParaElBloqueo_YNoVincula()
    {
        var usuario = ConPassword();
        CuentaConCorreo(usuario);
        _hasher.Setup(h => h.Verificar("hash-guardado", "mala")).Returns(false);

        await Assert.ThrowsAsync<CredencialesInvalidasException>(() => _servicio.VincularGoogleAsync(Token, "mala"));

        Assert.Equal(1, usuario.Bloqueo.IntentosFallidos);
        _usuarios.Verify(u => u.UpdateAsync(usuario), Times.Once);
        _vinculos.Verify(v => v.AgregarAsync(It.IsAny<LoginExterno>()), Times.Never);
        _jwt.Verify(j => j.Generar(It.IsAny<Usuario>()), Times.Never);
    }

    [Fact]
    public async Task RF_CA_19_AlQuintoIntentoFallidoDeVincularLaCuentaQuedaBloqueada()
    {
        var usuario = ConPassword();
        CuentaConCorreo(usuario);
        _hasher.Setup(h => h.Verificar("hash-guardado", "mala")).Returns(false);

        for (var i = 0; i < 4; i++)
        {
            await Assert.ThrowsAsync<CredencialesInvalidasException>(() => _servicio.VincularGoogleAsync(Token, "mala"));
        }

        await Assert.ThrowsAsync<CuentaBloqueadaException>(() => _servicio.VincularGoogleAsync(Token, "mala"));
        Assert.True(usuario.EstaBloqueado());
    }

    [Fact]
    public async Task RF_CA_19_ConLaClaveCorrectaPeroLaCuentaBloqueadaNoSeVincula()
    {
        var usuario = ConPassword();
        for (var i = 0; i < 5; i++)
        {
            usuario.RegistrarIntentoFallido();
        }

        CuentaConCorreo(usuario);
        _hasher.Setup(h => h.Verificar("hash-guardado", "buena")).Returns(true);

        await Assert.ThrowsAsync<CuentaBloqueadaException>(() => _servicio.VincularGoogleAsync(Token, "buena"));

        _vinculos.Verify(v => v.AgregarAsync(It.IsAny<LoginExterno>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RF_CA_03_VincularSinUnaCuentaConContrasenaResponde401GenericoGastandoElMismoTiempo(bool existeSinContrasena)
    {
        CuentaConCorreo(existeSinContrasena ? Usuario.CrearConProveedorExterno("Persona", new Email(Correo)) : null);

        await Assert.ThrowsAsync<CredencialesInvalidasException>(() => _servicio.VincularGoogleAsync(Token, "clave"));

        _hasher.Verify(h => h.SimularVerificacion("clave"), Times.Once);
        _vinculos.Verify(v => v.AgregarAsync(It.IsAny<LoginExterno>()), Times.Never);
    }

    [Fact]
    public async Task RF_CA_20_VincularEnUnaCuentaDesactivadaPorUnAdministradorSeRechaza()
    {
        var usuario = ConPassword();
        usuario.Desactivar();
        CuentaConCorreo(usuario);
        _hasher.Setup(h => h.Verificar("hash-guardado", "buena")).Returns(true);

        await Assert.ThrowsAsync<CuentaDesactivadaException>(() => _servicio.VincularGoogleAsync(Token, "buena"));

        _vinculos.Verify(v => v.AgregarAsync(It.IsAny<LoginExterno>()), Times.Never);
    }

    [Fact]
    public async Task RF_CA_03_VincularConLaClaveCorrectaCreaElVinculoYIniciaSesion()
    {
        var usuario = ConPassword();
        usuario.RegistrarIntentoFallido();
        CuentaConCorreo(usuario);
        _hasher.Setup(h => h.Verificar("hash-guardado", "buena")).Returns(true);
        LoginExterno? guardado = null;
        _vinculos.Setup(v => v.AgregarAsync(It.IsAny<LoginExterno>())).Callback<LoginExterno>(l => guardado = l).Returns(Task.CompletedTask);

        var resultado = await _servicio.VincularGoogleAsync(Token, "buena");

        Assert.False(resultado.CuentaNueva);
        Assert.Equal(usuario.Id, resultado.Usuario.Id);
        Assert.Equal("jwt-de-acceso", resultado.Usuario.Token);
        Assert.Equal(usuario.Id, guardado!.UsuarioId);
        Assert.Equal(Sub, guardado.Subject);
        Assert.Equal(0, usuario.Bloqueo.IntentosFallidos);   // la clave correcta levanta los intentos fallidos
        _usuarios.Verify(u => u.UpdateAsync(usuario), Times.Once);
    }

    [Fact]
    public async Task RF_CA_15_VincularActivaUnaCuentaQueEsperabaSuCodigo_PorqueGoogleVerificoElCorreo()
    {
        var usuario = ConPassword(activo: false);
        Assert.True(usuario.PendienteDeActivacion);
        CuentaConCorreo(usuario);
        _hasher.Setup(h => h.Verificar("hash-guardado", "buena")).Returns(true);

        await _servicio.VincularGoogleAsync(Token, "buena");

        Assert.True(usuario.Activo);
        Assert.Null(usuario.TokenAcceso);
    }

    [Fact]
    public async Task RD_06_UnaIdentidadDeGoogleQueYaEsDeOtraCuentaNoSePuedeVincularAEsta()
    {
        var usuario = ConPassword();
        CuentaConCorreo(usuario);
        _hasher.Setup(h => h.Verificar("hash-guardado", "buena")).Returns(true);
        _vinculos.Setup(v => v.ObtenerPorSubjectAsync(LoginExterno.ProveedorGoogle, Sub))
            .ReturnsAsync(new LoginExterno(Guid.NewGuid(), LoginExterno.ProveedorGoogle, Sub, DateTime.UtcNow));

        var error = await Assert.ThrowsAsync<ProveedorYaVinculadoException>(() => _servicio.VincularGoogleAsync(Token, "buena"));

        Assert.Equal("proveedor_ya_vinculado", error.Codigo);
        _vinculos.Verify(v => v.AgregarAsync(It.IsAny<LoginExterno>()), Times.Never);
    }

    [Fact]
    public async Task RD_06_UnaCuentaQueYaTieneOtraIdentidadDeGoogleNoAdmiteUnaSegunda()
    {
        var usuario = ConPassword();
        CuentaConCorreo(usuario);
        _hasher.Setup(h => h.Verificar("hash-guardado", "buena")).Returns(true);
        _vinculos.Setup(v => v.ObtenerDeUsuarioAsync(usuario.Id, LoginExterno.ProveedorGoogle))
            .ReturnsAsync(new LoginExterno(usuario.Id, LoginExterno.ProveedorGoogle, "otro-sub", DateTime.UtcNow));

        await Assert.ThrowsAsync<ProveedorYaVinculadoException>(() => _servicio.VincularGoogleAsync(Token, "buena"));

        _vinculos.Verify(v => v.AgregarAsync(It.IsAny<LoginExterno>()), Times.Never);
    }

    [Fact]
    public async Task RF_CA_03_VincularDosVecesLaMismaIdentidadEsIdempotente_NoCreaOtroVinculo()
    {
        var usuario = ConPassword();
        CuentaConCorreo(usuario);
        _hasher.Setup(h => h.Verificar("hash-guardado", "buena")).Returns(true);
        _vinculos.Setup(v => v.ObtenerPorSubjectAsync(LoginExterno.ProveedorGoogle, Sub))
            .ReturnsAsync(new LoginExterno(usuario.Id, LoginExterno.ProveedorGoogle, Sub, DateTime.UtcNow));

        var resultado = await _servicio.VincularGoogleAsync(Token, "buena");

        Assert.Equal("jwt-de-acceso", resultado.Usuario.Token);
        _vinculos.Verify(v => v.AgregarAsync(It.IsAny<LoginExterno>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RD_07_VincularSinContrasenaSeRechazaAntesDeValidarElToken(string password)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _servicio.VincularGoogleAsync(Token, password));

        _validador.VerifyNoOtherCalls();
    }

    // ---------------- Vínculos de la cuenta ----------------

    [Fact]
    public async Task RF_CA_03_ListarMuestraSiLaCuentaTieneContrasenaYSusProveedores()
    {
        var usuario = ConPassword();
        _usuarios.Setup(u => u.GetByIdAsync(usuario.Id)).ReturnsAsync(usuario);
        _vinculos.Setup(v => v.ListarDeUsuarioAsync(usuario.Id))
            .ReturnsAsync([new LoginExterno(usuario.Id, LoginExterno.ProveedorGoogle, Sub, DateTime.UtcNow)]);

        var resultado = await _servicio.ListarVinculosAsync(usuario.Id);

        Assert.True(resultado.TienePassword);
        var vinculo = Assert.Single(resultado.Vinculos);
        Assert.Equal("google", vinculo.Proveedor);
        Assert.Equal(DateTimeKind.Utc, vinculo.VinculadoEn.Kind);
    }

    [Fact]
    public async Task RF_CA_03_DesvincularConContrasenaQuitaElVinculo()
    {
        var usuario = ConPassword();
        _usuarios.Setup(u => u.GetByIdAsync(usuario.Id)).ReturnsAsync(usuario);
        _vinculos.Setup(v => v.ObtenerDeUsuarioAsync(usuario.Id, "google"))
            .ReturnsAsync(new LoginExterno(usuario.Id, "google", Sub, DateTime.UtcNow));
        _vinculos.Setup(v => v.EliminarAsync(usuario.Id, "google")).ReturnsAsync(true);

        Assert.True(await _servicio.DesvincularAsync(usuario.Id, "google"));

        _vinculos.Verify(v => v.EliminarAsync(usuario.Id, "google"), Times.Once);
    }

    [Fact]
    public async Task RF_CA_03_NoSePuedeDesvincularGoogleSiEsLaUnicaFormaDeEntrar()
    {
        var usuario = Usuario.CrearConProveedorExterno("Persona", new Email(Correo));
        _usuarios.Setup(u => u.GetByIdAsync(usuario.Id)).ReturnsAsync(usuario);
        _vinculos.Setup(v => v.ObtenerDeUsuarioAsync(usuario.Id, "google"))
            .ReturnsAsync(new LoginExterno(usuario.Id, "google", Sub, DateTime.UtcNow));

        var error = await Assert.ThrowsAsync<UltimoMetodoAccesoException>(() => _servicio.DesvincularAsync(usuario.Id, "google"));

        Assert.Equal("ultimo_metodo_acceso", error.Codigo);
        _vinculos.Verify(v => v.EliminarAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RF_CA_03_DesvincularUnProveedorQueNoEstaVinculadoDevuelveFalse()
    {
        var usuario = Usuario.CrearConProveedorExterno("Persona", new Email(Correo));
        _usuarios.Setup(u => u.GetByIdAsync(usuario.Id)).ReturnsAsync(usuario);

        Assert.False(await _servicio.DesvincularAsync(usuario.Id, "google"));

        _vinculos.Verify(v => v.EliminarAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RD_06_ListarYDesvincularConUnUsuarioInexistenteSeRechazan()
    {
        _usuarios.Setup(u => u.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Usuario?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _servicio.ListarVinculosAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _servicio.DesvincularAsync(Guid.NewGuid(), "google"));
    }
}
