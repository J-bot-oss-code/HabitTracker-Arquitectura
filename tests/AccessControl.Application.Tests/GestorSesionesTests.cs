using System.Security.Cryptography;
using System.Text;
using AccessControl.Application.Interfaces;
using AccessControl.Application.Service;
using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Domain.Excepciones;
using AccessControl.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace AccessControl.Application.Tests;

/// <summary>Sesiones con refresh token rotativo: emisión, rotación, detección de reutilización y revocación (RF-CA-12, 18, 20).</summary>
public class GestorSesionesTests
{
    private static readonly TimeSpan Duracion = TimeSpan.FromDays(14);

    private readonly Mock<ISesionRepository> _sesiones = new();
    private readonly Mock<IUsuarioRepository> _usuarios = new();
    private readonly Mock<ITokenBlacklist> _blacklist = new();
    private readonly Mock<IJwtProvider> _jwt = new();
    private readonly GestorSesiones _gestor;

    public GestorSesionesTests()
    {
        _jwt.Setup(j => j.Generar(It.IsAny<Usuario>())).Returns("jwt-de-acceso");
        _sesiones.Setup(s => s.RotarAsync(It.IsAny<Guid>(), It.IsAny<SesionUsuario>(), It.IsAny<DateTime>())).ReturnsAsync(true);
        _gestor = new GestorSesiones(
            _sesiones.Object, _usuarios.Object, _blacklist.Object, _jwt.Object,
            OpcionesSesion.PorDefecto, Mock.Of<ILogger<GestorSesiones>>());
    }

    private static string HashDe(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static Usuario UsuarioActivo()
    {
        var usuario = new Usuario("Persona", new Email("persona@ejemplo.com"), "hash-guardado", Rol.Estandar);
        usuario.Activar();
        return usuario;
    }

    /// <summary>Una sesión guardada cuyo refresh token es <paramref name="token"/>.</summary>
    private SesionUsuario Guardada(string token, Guid usuarioId, DateTime? ahora = null)
    {
        var sesion = new SesionUsuario(usuarioId, HashDe(token), ahora ?? DateTime.UtcNow, Duracion, "Firefox", "10.0.0.1");
        _sesiones.Setup(s => s.ObtenerPorHashAsync(HashDe(token))).ReturnsAsync(sesion);
        return sesion;
    }

    // ---------------- Iniciar ----------------

    [Fact]
    public async Task RF_CA_03_Iniciar_EmiteUnTokenAleatorioDe43CaracteresYGuardaSoloSuHash()
    {
        SesionUsuario? guardada = null;
        _sesiones.Setup(s => s.AgregarAsync(It.IsAny<SesionUsuario>())).Callback<SesionUsuario>(x => guardada = x).Returns(Task.CompletedTask);
        var usuarioId = Guid.NewGuid();

        var emitida = await _gestor.IniciarAsync(usuarioId, "Firefox en Windows", "10.0.0.1");

        Assert.Equal(43, emitida.RefreshToken.Length);
        Assert.NotNull(guardada);
        Assert.Equal(usuarioId, guardada!.UsuarioId);
        Assert.Equal(HashDe(emitida.RefreshToken), guardada.TokenHash);
        Assert.NotEqual(emitida.RefreshToken, guardada.TokenHash);   // el token nunca se guarda
        Assert.Equal("Firefox en Windows", guardada.Dispositivo);
        Assert.Equal(guardada.ExpiraEn, emitida.ExpiraEn);
        Assert.InRange(emitida.ExpiraEn, DateTime.UtcNow + Duracion - TimeSpan.FromMinutes(1), DateTime.UtcNow + Duracion + TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task RF_CA_03_Iniciar_CadaTokenEsDistinto_YLimpiaLasSesionesQueVencieronHaceMucho()
    {
        var primero = await _gestor.IniciarAsync(Guid.NewGuid(), null, null);
        var segundo = await _gestor.IniciarAsync(Guid.NewGuid(), null, null);

        Assert.NotEqual(primero.RefreshToken, segundo.RefreshToken);
        _sesiones.Verify(s => s.EliminarVencidasAsync(It.Is<DateTime>(d => d < DateTime.UtcNow.AddDays(-29))), Times.Exactly(2));
    }

    [Fact]
    public async Task RD_07_Iniciar_SinUsuario_Falla()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _gestor.IniciarAsync(Guid.Empty, null, null));
        _sesiones.Verify(s => s.AgregarAsync(It.IsAny<SesionUsuario>()), Times.Never);
    }

    // ---------------- Renovar ----------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RF_CA_03_Renovar_SinTokenEsSesionInvalida_SinTocarLaBaseDeDatos(string? token)
    {
        await Assert.ThrowsAsync<SesionInvalidaException>(() => _gestor.RenovarAsync(token));

        _sesiones.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RD_07_Renovar_ConUnTokenDemasiadoLargoSeRechazaAntesDeConsultar()
    {
        await Assert.ThrowsAsync<SesionInvalidaException>(() => _gestor.RenovarAsync(new string('x', 5000)));

        _sesiones.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RF_CA_03_Renovar_ConUnTokenDesconocidoEsSesionInvalida()
    {
        _sesiones.Setup(s => s.ObtenerPorHashAsync(It.IsAny<string>())).ReturnsAsync((SesionUsuario?)null);

        await Assert.ThrowsAsync<SesionInvalidaException>(() => _gestor.RenovarAsync("token-que-no-existe"));

        _sesiones.Verify(s => s.RotarAsync(It.IsAny<Guid>(), It.IsAny<SesionUsuario>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task RF_CA_12_Renovar_RotaElTokenYEmiteUnAccesoNuevo()
    {
        var usuario = UsuarioActivo();
        var actual = Guardada("refresh-actual", usuario.Id);
        _usuarios.Setup(u => u.GetByIdAsync(usuario.Id)).ReturnsAsync(usuario);
        SesionUsuario? siguiente = null;
        _sesiones.Setup(s => s.RotarAsync(actual.Id, It.IsAny<SesionUsuario>(), It.IsAny<DateTime>()))
            .Callback<Guid, SesionUsuario, DateTime>((_, nueva, _) => siguiente = nueva)
            .ReturnsAsync(true);

        var renovada = await _gestor.RenovarAsync("refresh-actual");

        Assert.Equal("jwt-de-acceso", renovada.Usuario.Token);
        Assert.Equal(usuario.Id, renovada.Usuario.Id);
        Assert.Equal("Persona", renovada.Usuario.Nombre);
        Assert.NotEqual("refresh-actual", renovada.RefreshToken);   // el token anterior no vuelve a servir
        Assert.NotNull(siguiente);
        Assert.Equal(HashDe(renovada.RefreshToken), siguiente!.TokenHash);
        Assert.Equal(actual.FamiliaId, siguiente.FamiliaId);        // misma familia: sigue siendo el mismo inicio de sesión
        Assert.Equal(siguiente.ExpiraEn, renovada.ExpiraEn);
        _sesiones.Verify(s => s.RevocarFamiliaAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task RF_CA_18_Renovar_ConUnTokenYaRevocado_RevocaToda_LaFamiliaPorPosibleRobo()
    {
        var usuario = UsuarioActivo();
        var actual = Guardada("refresh-viejo", usuario.Id);
        actual.Revocar(DateTime.UtcNow.AddMinutes(-1));

        await Assert.ThrowsAsync<SesionInvalidaException>(() => _gestor.RenovarAsync("refresh-viejo"));

        _sesiones.Verify(s => s.RevocarFamiliaAsync(actual.FamiliaId, It.IsAny<DateTime>()), Times.Once);
        _sesiones.Verify(s => s.RotarAsync(It.IsAny<Guid>(), It.IsAny<SesionUsuario>(), It.IsAny<DateTime>()), Times.Never);
        _jwt.Verify(j => j.Generar(It.IsAny<Usuario>()), Times.Never);
    }

    [Fact]
    public async Task RF_CA_18_Renovar_SiOtraPeticionGanoLaCarrera_RevocaLaFamiliaYNoEmiteNada()
    {
        var usuario = UsuarioActivo();
        var actual = Guardada("refresh-actual", usuario.Id);
        _usuarios.Setup(u => u.GetByIdAsync(usuario.Id)).ReturnsAsync(usuario);
        _sesiones.Setup(s => s.RotarAsync(actual.Id, It.IsAny<SesionUsuario>(), It.IsAny<DateTime>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<SesionInvalidaException>(() => _gestor.RenovarAsync("refresh-actual"));

        _sesiones.Verify(s => s.RevocarFamiliaAsync(actual.FamiliaId, It.IsAny<DateTime>()), Times.Once);
        _jwt.Verify(j => j.Generar(It.IsAny<Usuario>()), Times.Never);
    }

    [Fact]
    public async Task RF_CA_18_Renovar_ConUnaSesionVencidaEsSesionInvalida_SinRotar()
    {
        var usuario = UsuarioActivo();
        Guardada("refresh-vencido", usuario.Id, DateTime.UtcNow - Duracion - TimeSpan.FromHours(1));

        await Assert.ThrowsAsync<SesionInvalidaException>(() => _gestor.RenovarAsync("refresh-vencido"));

        _sesiones.Verify(s => s.RotarAsync(It.IsAny<Guid>(), It.IsAny<SesionUsuario>(), It.IsAny<DateTime>()), Times.Never);
        _jwt.Verify(j => j.Generar(It.IsAny<Usuario>()), Times.Never);
    }

    [Fact]
    public async Task RF_CA_20_Renovar_DeUnaCuentaDesactivada_RevocaLaFamiliaYNoEmiteAcceso()
    {
        var usuario = UsuarioActivo();
        usuario.Desactivar();
        var actual = Guardada("refresh-actual", usuario.Id);
        _usuarios.Setup(u => u.GetByIdAsync(usuario.Id)).ReturnsAsync(usuario);

        await Assert.ThrowsAsync<SesionInvalidaException>(() => _gestor.RenovarAsync("refresh-actual"));

        _sesiones.Verify(s => s.RevocarFamiliaAsync(actual.FamiliaId, It.IsAny<DateTime>()), Times.Once);
        _jwt.Verify(j => j.Generar(It.IsAny<Usuario>()), Times.Never);
    }

    [Fact]
    public async Task RF_CA_20_Renovar_DeUnaCuentaQueYaNoExiste_EsSesionInvalida()
    {
        var actual = Guardada("refresh-actual", Guid.NewGuid());
        _usuarios.Setup(u => u.GetByIdAsync(actual.UsuarioId)).ReturnsAsync((Usuario?)null);

        await Assert.ThrowsAsync<SesionInvalidaException>(() => _gestor.RenovarAsync("refresh-actual"));

        _jwt.Verify(j => j.Generar(It.IsAny<Usuario>()), Times.Never);
    }

    [Fact]
    public async Task RF_CA_19_Renovar_DeUnaCuentaBloqueada_NoEmiteAcceso()
    {
        var usuario = UsuarioActivo();
        for (var i = 0; i < 5; i++)
        {
            usuario.RegistrarIntentoFallido();
        }

        Assert.True(usuario.EstaBloqueado());
        Guardada("refresh-actual", usuario.Id);
        _usuarios.Setup(u => u.GetByIdAsync(usuario.Id)).ReturnsAsync(usuario);

        await Assert.ThrowsAsync<SesionInvalidaException>(() => _gestor.RenovarAsync("refresh-actual"));

        _sesiones.Verify(s => s.RotarAsync(It.IsAny<Guid>(), It.IsAny<SesionUsuario>(), It.IsAny<DateTime>()), Times.Never);
        _jwt.Verify(j => j.Generar(It.IsAny<Usuario>()), Times.Never);
    }

    // ---------------- Cerrar ----------------

    [Fact]
    public async Task RF_CA_18_Cerrar_RevocaLaFamiliaDeLaSesionDelPropioUsuario()
    {
        var usuarioId = Guid.NewGuid();
        var sesion = Guardada("refresh-actual", usuarioId);

        await _gestor.CerrarAsync(usuarioId, "refresh-actual");

        _sesiones.Verify(s => s.RevocarFamiliaAsync(sesion.FamiliaId, It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task RD_06_Cerrar_NoRevocaLaSesionDeOtroUsuarioAunqueLleguesuCookie()
    {
        Guardada("refresh-ajeno", Guid.NewGuid());

        await _gestor.CerrarAsync(Guid.NewGuid(), "refresh-ajeno");

        _sesiones.Verify(s => s.RevocarFamiliaAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task RF_CA_18_Cerrar_SinCookieNoHaceNada_PeroNoFalla(string? token)
    {
        await _gestor.CerrarAsync(Guid.NewGuid(), token);

        _sesiones.VerifyNoOtherCalls();
    }

    // ---------------- Revocar todas: único punto de corte (RD-04) ----------------

    [Fact]
    public async Task RD_04_RevocarTodas_CortaLosRefreshTokensYLosTokensDeAccesoEmitidos()
    {
        var usuarioId = Guid.NewGuid();

        await _gestor.RevocarTodasAsync(usuarioId, "prueba");

        _sesiones.Verify(s => s.RevocarTodasAsync(usuarioId, It.IsAny<DateTime>()), Times.Once);
        _blacklist.Verify(b => b.RevocarSesionesAnteriores(usuarioId), Times.Once);
    }

    [Fact]
    public async Task RD_07_RevocarTodas_SinUsuario_Falla()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _gestor.RevocarTodasAsync(Guid.Empty, "prueba"));

        _blacklist.VerifyNoOtherCalls();
    }

    // ---------------- Listar / revocar una ----------------

    [Fact]
    public async Task RF_CA_18_Listar_MarcaComoActualSoloLaDelRefreshQueEnvia()
    {
        var usuarioId = Guid.NewGuid();
        var ahora = DateTime.UtcNow;
        var esta = new SesionUsuario(usuarioId, HashDe("refresh-esta"), ahora, Duracion, "Firefox", null);
        var otra = new SesionUsuario(usuarioId, HashDe("refresh-otra"), ahora, Duracion, "Chrome", null);
        _sesiones.Setup(s => s.ListarActivasAsync(usuarioId, It.IsAny<DateTime>())).ReturnsAsync([esta, otra]);

        var lista = await _gestor.ListarAsync(usuarioId, "refresh-esta");

        Assert.Equal(2, lista.Count);
        Assert.True(lista.Single(s => s.Id == esta.Id).Actual);
        Assert.False(lista.Single(s => s.Id == otra.Id).Actual);
        Assert.All(lista, s => Assert.Equal(DateTimeKind.Utc, s.CreadaEn.Kind));
    }

    [Fact]
    public async Task RF_CA_18_Listar_SinCookieNingunaEsLaActual()
    {
        var usuarioId = Guid.NewGuid();
        var sesion = new SesionUsuario(usuarioId, HashDe("x"), DateTime.UtcNow, Duracion, "Firefox", null);
        _sesiones.Setup(s => s.ListarActivasAsync(usuarioId, It.IsAny<DateTime>())).ReturnsAsync([sesion]);

        var lista = await _gestor.ListarAsync(usuarioId, null);

        Assert.All(lista, s => Assert.False(s.Actual));
    }

    [Fact]
    public async Task RF_CA_18_Revocar_CierraLaFamiliaDeUnaSesionActivaDelUsuario()
    {
        var usuarioId = Guid.NewGuid();
        var sesion = new SesionUsuario(usuarioId, HashDe("x"), DateTime.UtcNow, Duracion, "Firefox", null);
        _sesiones.Setup(s => s.ObtenerAsync(usuarioId, sesion.Id)).ReturnsAsync(sesion);

        var resultado = await _gestor.RevocarAsync(usuarioId, sesion.Id);

        Assert.True(resultado);
        _sesiones.Verify(s => s.RevocarFamiliaAsync(sesion.FamiliaId, It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task RD_06_Revocar_UnaSesionAjenaOInexistenteDevuelveFalse_SinRevocarNada()
    {
        _sesiones.Setup(s => s.ObtenerAsync(It.IsAny<Guid>(), It.IsAny<Guid>())).ReturnsAsync((SesionUsuario?)null);

        var resultado = await _gestor.RevocarAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(resultado);
        _sesiones.Verify(s => s.RevocarFamiliaAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task RF_CA_18_Revocar_UnaSesionQueYaNoEstabaActivaDevuelveFalse()
    {
        var usuarioId = Guid.NewGuid();
        var sesion = new SesionUsuario(usuarioId, HashDe("x"), DateTime.UtcNow, Duracion, "Firefox", null);
        sesion.Revocar(DateTime.UtcNow);
        _sesiones.Setup(s => s.ObtenerAsync(usuarioId, sesion.Id)).ReturnsAsync(sesion);

        Assert.False(await _gestor.RevocarAsync(usuarioId, sesion.Id));
        _sesiones.Verify(s => s.RevocarFamiliaAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()), Times.Never);
    }
}
