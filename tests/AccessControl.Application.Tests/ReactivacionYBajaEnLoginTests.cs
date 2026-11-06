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

/// <summary>Qué pasa al iniciar sesión con una cuenta desactivada por su dueño o con una baja programada (RF-CA-03, RF-CA-20).</summary>
public class ReactivacionYBajaEnLoginTests
{
    private const string Correo = "persona@ejemplo.com";

    private readonly Mock<IUsuarioRepository> _repo = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IJwtProvider> _jwt = new();

    public ReactivacionYBajaEnLoginTests()
    {
        _hasher.Setup(h => h.Verificar("hash-guardado", "buena")).Returns(true);
        _jwt.Setup(j => j.Generar(It.IsAny<Usuario>())).Returns("jwt-de-acceso");
    }

    private UsuarioService Servicio() => new(
        _repo.Object, _hasher.Object, Mock.Of<IEmailQueue>(), Mock.Of<IAuditoriaService>(), _jwt.Object,
        GestorSesionesDePrueba.Crear(), Mock.Of<ILogger<UsuarioService>>());

    private Usuario Cuenta()
    {
        var usuario = new Usuario("Persona", new Email(Correo), "hash-guardado");
        usuario.Activar();
        _repo.Setup(r => r.GetByCorreoAsync(It.IsAny<Email>())).ReturnsAsync(usuario);
        return usuario;
    }

    private static LoginRequestDto Login(string clave = "buena") => new(Correo, clave);

    [Fact]
    public async Task RF_CA_20_QuienDesactivoSuCuentaLaReactivaAlIniciarSesionConLaClaveCorrecta()
    {
        var usuario = Cuenta();
        usuario.SolicitarDesactivacion(DateTime.UtcNow);

        var respuesta = await Servicio().AutenticarUsuarioAsync(Login());

        Assert.True(usuario.Activo);
        Assert.Null(usuario.DesactivadaPorUsuarioEn);
        Assert.True(respuesta.Activo);
        Assert.Equal("jwt-de-acceso", respuesta.Token);
        _repo.Verify(r => r.UpdateAsync(usuario), Times.AtLeastOnce);   // la reactivación se guarda
    }

    [Fact]
    public async Task RF_CA_03_ConUnaClaveIncorrectaLaCuentaDesactivadaPorSuDuenoSigueDesactivada()
    {
        var usuario = Cuenta();
        usuario.SolicitarDesactivacion(DateTime.UtcNow);
        _hasher.Setup(h => h.Verificar("hash-guardado", "mala")).Returns(false);

        await Assert.ThrowsAsync<CredencialesInvalidasException>(() => Servicio().AutenticarUsuarioAsync(Login("mala")));

        Assert.False(usuario.Activo);
        Assert.True(usuario.DesactivadaPorElUsuario);
    }

    [Fact]
    public async Task RF_CA_20_UnaCuentaDesactivadaPorUnAdministradorNoSeReactivaAlIniciarSesion()
    {
        var usuario = Cuenta();
        usuario.Desactivar();

        await Assert.ThrowsAsync<CuentaDesactivadaException>(() => Servicio().AutenticarUsuarioAsync(Login()));

        Assert.False(usuario.Activo);
    }

    [Fact]
    public async Task RF_CA_20_UnaCuentaConBajaProgramadaEntraYLaRespuestaTraeLaFechaEnUtc()
    {
        var usuario = Cuenta();
        usuario.ProgramarBaja(DateTime.UtcNow, 30);

        var respuesta = await Servicio().AutenticarUsuarioAsync(Login());

        Assert.NotNull(respuesta.BajaProgramadaPara);
        Assert.Equal(DateTimeKind.Utc, respuesta.BajaProgramadaPara!.Value.Kind);
        Assert.Equal(usuario.BajaProgramadaPara, respuesta.BajaProgramadaPara);
        Assert.Equal("jwt-de-acceso", respuesta.Token);   // el login sigue funcionando para poder cancelar
    }

    [Fact]
    public async Task RF_CA_20_UnaCuentaSinBajaNoTraeFecha()
    {
        Cuenta();

        var respuesta = await Servicio().AutenticarUsuarioAsync(Login());

        Assert.Null(respuesta.BajaProgramadaPara);
    }

    // ---------------- Google ----------------

    private ServicioLoginExterno Externo(Usuario usuario)
    {
        var validador = new Mock<IValidadorIdTokenGoogle>();
        validador.Setup(v => v.ValidarAsync("id-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentidadExterna("sub-1", Correo, true, "Persona"));
        var vinculos = new Mock<ILoginExternoRepository>();
        vinculos.Setup(v => v.ObtenerPorSubjectAsync("google", "sub-1"))
            .ReturnsAsync(new LoginExterno(usuario.Id, "google", "sub-1", DateTime.UtcNow));
        _repo.Setup(r => r.GetByIdAsync(usuario.Id)).ReturnsAsync(usuario);
        return new ServicioLoginExterno(
            validador.Object, _repo.Object, vinculos.Object, _hasher.Object, _jwt.Object,
            VersionesLegales.PorDefecto, Mock.Of<ILogger<ServicioLoginExterno>>());
    }

    [Fact]
    public async Task RF_CA_20_QuienDesactivoSuCuentaLaReactivaTambienEntrandoConGoogle()
    {
        var usuario = Cuenta();
        usuario.SolicitarDesactivacion(DateTime.UtcNow);

        var resultado = await Externo(usuario).IniciarConGoogleAsync("id-token", null);

        Assert.True(usuario.Activo);
        Assert.True(resultado.Usuario.Activo);
        _repo.Verify(r => r.UpdateAsync(usuario), Times.Once);
    }

    [Fact]
    public async Task RF_CA_20_UnaCuentaDesactivadaPorUnAdministradorTampocoEntraConGoogle()
    {
        var usuario = Cuenta();
        usuario.Desactivar();

        await Assert.ThrowsAsync<CuentaDesactivadaException>(() => Externo(usuario).IniciarConGoogleAsync("id-token", null));

        Assert.False(usuario.Activo);
    }

    [Fact]
    public async Task RF_CA_20_EntrarConGoogleTambienDevuelveLaFechaDeBajaProgramada()
    {
        var usuario = Cuenta();
        usuario.ProgramarBaja(DateTime.UtcNow, 30);

        var resultado = await Externo(usuario).IniciarConGoogleAsync("id-token", null);

        Assert.Equal(usuario.BajaProgramadaPara, resultado.Usuario.BajaProgramadaPara);
    }
}
