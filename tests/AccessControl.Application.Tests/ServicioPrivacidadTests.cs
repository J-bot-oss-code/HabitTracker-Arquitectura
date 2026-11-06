using System.Text.Json;
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

/// <summary>Baja de cuenta, cancelación y descarga de datos (RF-CA-19, RF-CA-20, RD-03, RD-06, Ley 172-13 y GDPR).</summary>
public class ServicioPrivacidadTests
{
    private const string Correo = "persona@ejemplo.com";
    private const string Clave = "clave-correcta";

    private readonly Mock<IUsuarioRepository> _usuarios = new();
    private readonly Mock<IConsentimientoRepository> _consentimientos = new();
    private readonly Mock<ILoginExternoRepository> _vinculos = new();
    private readonly Mock<ISesionRepository> _sesionesRepo = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IValidadorIdTokenGoogle> _google = new();
    private readonly Mock<IGestorSesiones> _sesiones = new();
    private readonly Mock<IAuditoriaService> _auditoria = new();
    private readonly List<Mock<IParticipanteDatosUsuario>> _participantes = [];

    public ServicioPrivacidadTests()
    {
        _hasher.Setup(h => h.Verificar("hash-guardado", Clave)).Returns(true);
        _consentimientos.Setup(c => c.ListarHistorialAsync(It.IsAny<Guid>())).ReturnsAsync([]);
        _vinculos.Setup(v => v.ListarDeUsuarioAsync(It.IsAny<Guid>())).ReturnsAsync([]);
        _sesionesRepo.Setup(s => s.ListarActivasAsync(It.IsAny<Guid>(), It.IsAny<DateTime>())).ReturnsAsync([]);
    }

    private ServicioPrivacidad Servicio(OpcionesPrivacidad? opciones = null) => new(
        _usuarios.Object, _consentimientos.Object, _vinculos.Object, _sesionesRepo.Object, _hasher.Object, _google.Object,
        _sesiones.Object, _participantes.Select(p => p.Object).ToList(), _auditoria.Object,
        opciones ?? new OpcionesPrivacidad(), Mock.Of<ILogger<ServicioPrivacidad>>());

    private Usuario Existente(Rol rol = Rol.Estandar, bool conPassword = true)
    {
        var usuario = conPassword
            ? new Usuario("Persona", new Email(Correo), "hash-guardado", rol)
            : Usuario.CrearConProveedorExterno("Persona", new Email(Correo));
        if (conPassword)
        {
            usuario.Activar();
        }

        _usuarios.Setup(u => u.GetByIdAsync(usuario.Id)).ReturnsAsync(usuario);
        return usuario;
    }

    private Mock<IParticipanteDatosUsuario> Participante(string seccion, object? datos = null)
    {
        var participante = new Mock<IParticipanteDatosUsuario>();
        participante.SetupGet(p => p.Seccion).Returns(seccion);
        participante.Setup(p => p.ExportarAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(datos ?? new { seccion });
        _participantes.Add(participante);
        return participante;
    }

    // ---------------- Eliminar ----------------

    [Fact]
    public async Task RF_CA_20_EliminarProgramaElBorradoAlTerminarLaGracia_YCortaTodasLasSesiones()
    {
        var usuario = Existente();

        var resultado = await Servicio().SolicitarBajaAsync(usuario.Id, "eliminar", Clave, null);

        Assert.Equal("eliminar", resultado.Modo);
        Assert.NotNull(resultado.BorradoEn);
        Assert.InRange(resultado.BorradoEn!.Value, DateTime.UtcNow.AddDays(30).AddMinutes(-1), DateTime.UtcNow.AddDays(30).AddMinutes(1));
        Assert.Equal(DateTimeKind.Utc, resultado.BorradoEn.Value.Kind);
        Assert.True(usuario.TieneBajaProgramada);
        Assert.True(usuario.Activo);   // durante la gracia el login funciona
        _usuarios.Verify(u => u.UpdateAsync(usuario), Times.Once);
        _sesiones.Verify(s => s.RevocarTodasAsync(usuario.Id, It.IsAny<string>()), Times.Once);
        _auditoria.Verify(a => a.RegistroAuditoriaAsync(
            usuario.Id, "Estandar", It.IsAny<string>(), "Usuario", usuario.Id.ToString(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RF_CA_20_ElPeriodoDeGraciaSaleDeLaConfiguracion()
    {
        var usuario = Existente();

        var resultado = await Servicio(new OpcionesPrivacidad { DiasGraciaBaja = 7 }).SolicitarBajaAsync(usuario.Id, "eliminar", Clave, null);

        Assert.InRange(resultado.BorradoEn!.Value, DateTime.UtcNow.AddDays(7).AddMinutes(-1), DateTime.UtcNow.AddDays(7).AddMinutes(1));
    }

    [Fact]
    public async Task RF_CA_20_PedirLaEliminacionDosVecesConservaLaFechaOriginal()
    {
        var usuario = Existente();
        var servicio = Servicio();

        var primera = await servicio.SolicitarBajaAsync(usuario.Id, "eliminar", Clave, null);
        var segunda = await servicio.SolicitarBajaAsync(usuario.Id, "eliminar", Clave, null);

        Assert.Equal(primera.BorradoEn, segunda.BorradoEn);
    }

    [Theory]
    [InlineData("ELIMINAR")]
    [InlineData("Eliminar")]
    public async Task RD_07_ElModoSeAceptaSinImportarMayusculas_YSeNormalizaALaConstante(string modo)
    {
        var usuario = Existente();

        var resultado = await Servicio().SolicitarBajaAsync(usuario.Id, modo, Clave, null);

        Assert.Equal("eliminar", resultado.Modo);
    }

    // ---------------- Desactivar ----------------

    [Fact]
    public async Task RF_CA_20_DesactivarDejaLaCuentaInactivaPorSuDuenoYSinFechaDeBorrado()
    {
        var usuario = Existente();

        var resultado = await Servicio().SolicitarBajaAsync(usuario.Id, "desactivar", Clave, null);

        Assert.Equal("desactivar", resultado.Modo);
        Assert.Null(resultado.BorradoEn);
        Assert.False(usuario.Activo);
        Assert.True(usuario.DesactivadaPorElUsuario);
        Assert.False(usuario.TieneBajaProgramada);
        _sesiones.Verify(s => s.RevocarTodasAsync(usuario.Id, It.IsAny<string>()), Times.Once);
    }

    // ---------------- Validaciones ----------------

    [Theory]
    [InlineData("")]
    [InlineData("borrar")]
    [InlineData("eliminar ")]
    [InlineData("'; DROP TABLE usuarios;--")]
    public async Task RD_07_UnModoQueNoEstaEnLaListaSeRechazaSinTocarLaBaseDeDatos(string modo)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Servicio().SolicitarBajaAsync(Guid.NewGuid(), modo, Clave, null));

        _usuarios.VerifyNoOtherCalls();
        _sesiones.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RD_06_UnAdministradorNoPuedeDarseDeBaja_AunqueLleguePorElServicio()
    {
        var admin = Existente(Rol.Administrador);

        await Assert.ThrowsAsync<OperacionNoPermitidaException>(() => Servicio().SolicitarBajaAsync(admin.Id, "eliminar", Clave, null));

        Assert.False(admin.TieneBajaProgramada);
        _sesiones.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RD_06_UnUsuarioInexistenteSeRechazaComoSesionInvalida()
    {
        _usuarios.Setup(u => u.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Usuario?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Servicio().SolicitarBajaAsync(Guid.NewGuid(), "eliminar", Clave, null));
    }

    // ---------------- Confirmación con la contraseña ----------------

    [Fact]
    public async Task RF_CA_19_UnaClaveIncorrectaEsUn400QueCuentaParaElBloqueo_YNoDaDeBaja()
    {
        var usuario = Existente();
        _hasher.Setup(h => h.Verificar("hash-guardado", "mala")).Returns(false);

        var error = await Assert.ThrowsAsync<ClaveActualIncorrectaException>(
            () => Servicio().SolicitarBajaAsync(usuario.Id, "eliminar", "mala", null));

        Assert.Equal("clave_actual_incorrecta", error.Codigo);   // un ControlAccesoException = 400, nunca un 401
        Assert.Equal(1, usuario.Bloqueo.IntentosFallidos);
        Assert.False(usuario.TieneBajaProgramada);
        _sesiones.Verify(s => s.RevocarTodasAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RF_CA_19_AlQuintoIntentoFallidoLaCuentaQuedaBloqueada()
    {
        var usuario = Existente();
        _hasher.Setup(h => h.Verificar("hash-guardado", "mala")).Returns(false);

        for (var i = 0; i < 4; i++)
        {
            await Assert.ThrowsAsync<ClaveActualIncorrectaException>(() => Servicio().SolicitarBajaAsync(usuario.Id, "eliminar", "mala", null));
        }

        await Assert.ThrowsAsync<CuentaBloqueadaException>(() => Servicio().SolicitarBajaAsync(usuario.Id, "eliminar", "mala", null));
        Assert.True(usuario.EstaBloqueado());
    }

    [Fact]
    public async Task RF_CA_19_ConLaClaveCorrectaPeroLaCuentaBloqueadaNoSeDaDeBaja()
    {
        var usuario = Existente();
        for (var i = 0; i < 5; i++)
        {
            usuario.RegistrarIntentoFallido();
        }

        await Assert.ThrowsAsync<CuentaBloqueadaException>(() => Servicio().SolicitarBajaAsync(usuario.Id, "eliminar", Clave, null));

        Assert.False(usuario.TieneBajaProgramada);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RD_07_SinContrasenaEnUnaCuentaQueLaTieneNoSeDaDeBaja(string? password)
    {
        var usuario = Existente();

        await Assert.ThrowsAsync<ClaveActualIncorrectaException>(() => Servicio().SolicitarBajaAsync(usuario.Id, "eliminar", password, null));

        Assert.False(usuario.TieneBajaProgramada);
        _hasher.Verify(h => h.Verificar(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ---------------- Confirmación con Google (cuenta sin contraseña) ----------------

    private Usuario CuentaDeGoogle(string sub = "sub-1", bool vinculada = true)
    {
        var usuario = Existente(conPassword: false);
        if (vinculada)
        {
            _vinculos.Setup(v => v.ObtenerPorSubjectAsync("google", sub))
                .ReturnsAsync(new LoginExterno(usuario.Id, "google", sub, DateTime.UtcNow));
        }

        return usuario;
    }

    private void GoogleResponde(string sub = "sub-1", bool verificado = true) =>
        _google.Setup(g => g.ValidarAsync("id-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentidadExterna(sub, Correo, verificado, "Persona"));

    [Fact]
    public async Task RF_CA_20_UnaCuentaDeGoogleConfirmaLaBajaConSuPropioToken()
    {
        var usuario = CuentaDeGoogle();
        GoogleResponde();

        var resultado = await Servicio().SolicitarBajaAsync(usuario.Id, "eliminar", null, "id-token");

        Assert.NotNull(resultado.BorradoEn);
        Assert.True(usuario.TieneBajaProgramada);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task RF_CA_20_SinTokenDeGoogleUnaCuentaSinContrasenaNoSeDaDeBaja(string? token)
    {
        var usuario = CuentaDeGoogle();

        var error = await Assert.ThrowsAsync<ConfirmacionIncorrectaException>(
            () => Servicio().SolicitarBajaAsync(usuario.Id, "eliminar", null, token));

        Assert.Equal("confirmacion_incorrecta", error.Codigo);
        Assert.False(usuario.TieneBajaProgramada);
    }

    [Fact]
    public async Task RF_CA_20_UnTokenFalsoEsUn400_NoUn401QueCerrariaLaSesion()
    {
        var usuario = CuentaDeGoogle();
        _google.Setup(g => g.ValidarAsync("id-token", It.IsAny<CancellationToken>())).ThrowsAsync(new TokenExternoInvalidoException());

        await Assert.ThrowsAsync<ConfirmacionIncorrectaException>(() => Servicio().SolicitarBajaAsync(usuario.Id, "eliminar", null, "id-token"));

        Assert.False(usuario.TieneBajaProgramada);
    }

    [Fact]
    public async Task RD_06_UnTokenDeGoogleDeOtraCuentaNoSirveParaDarDeBajaEsta()
    {
        var usuario = CuentaDeGoogle(vinculada: false);
        GoogleResponde(sub: "sub-de-otra-persona");
        _vinculos.Setup(v => v.ObtenerPorSubjectAsync("google", "sub-de-otra-persona"))
            .ReturnsAsync(new LoginExterno(Guid.NewGuid(), "google", "sub-de-otra-persona", DateTime.UtcNow));

        await Assert.ThrowsAsync<ConfirmacionIncorrectaException>(() => Servicio().SolicitarBajaAsync(usuario.Id, "eliminar", null, "id-token"));

        Assert.False(usuario.TieneBajaProgramada);
    }

    [Fact]
    public async Task RD_06_UnTokenSinVinculoNoSirve()
    {
        var usuario = CuentaDeGoogle(vinculada: false);
        GoogleResponde(sub: "sub-sin-vinculo");

        await Assert.ThrowsAsync<ConfirmacionIncorrectaException>(() => Servicio().SolicitarBajaAsync(usuario.Id, "eliminar", null, "id-token"));
    }

    [Fact]
    public async Task RD_06_UnCorreoSinVerificarNoSirve()
    {
        var usuario = CuentaDeGoogle();
        GoogleResponde(verificado: false);

        await Assert.ThrowsAsync<ConfirmacionIncorrectaException>(() => Servicio().SolicitarBajaAsync(usuario.Id, "eliminar", null, "id-token"));
    }

    [Fact]
    public async Task RF_CA_20_SiGoogleNoEstaDisponibleSePropagaEl503_NoSeDisfrazaDeConfirmacionIncorrecta()
    {
        var usuario = CuentaDeGoogle();
        _google.Setup(g => g.ValidarAsync("id-token", It.IsAny<CancellationToken>())).ThrowsAsync(new ProveedorExternoNoDisponibleException());

        await Assert.ThrowsAsync<ProveedorExternoNoDisponibleException>(() => Servicio().SolicitarBajaAsync(usuario.Id, "eliminar", null, "id-token"));
    }

    // ---------------- Cancelar y consultar ----------------

    [Fact]
    public async Task RF_CA_20_CancelarLaBajaConservaLaCuenta_YSeAudita()
    {
        var usuario = Existente();
        usuario.ProgramarBaja(DateTime.UtcNow, 30);

        Assert.True(await Servicio().CancelarBajaAsync(usuario.Id));

        Assert.False(usuario.TieneBajaProgramada);
        _usuarios.Verify(u => u.UpdateAsync(usuario), Times.Once);
        _auditoria.Verify(a => a.RegistroAuditoriaAsync(
            usuario.Id, It.IsAny<string>(), It.IsAny<string>(), "Usuario", usuario.Id.ToString(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RF_CA_20_CancelarSinBajaProgramadaDevuelveFalse_SinEscribirNada()
    {
        var usuario = Existente();

        Assert.False(await Servicio().CancelarBajaAsync(usuario.Id));

        _usuarios.Verify(u => u.UpdateAsync(It.IsAny<Usuario>()), Times.Never);
    }

    [Fact]
    public async Task RF_CA_20_ObtenerBajaDevuelveLaFechaEnUtcONulo()
    {
        var usuario = Existente();
        Assert.Null(await Servicio().ObtenerBajaProgramadaAsync(usuario.Id));

        usuario.ProgramarBaja(DateTime.UtcNow, 30);

        var fecha = await Servicio().ObtenerBajaProgramadaAsync(usuario.Id);
        Assert.Equal(DateTimeKind.Utc, fecha!.Value.Kind);
    }

    // ---------------- Descarga de datos ----------------

    [Fact]
    public async Task RF_CA_07_LaDescargaReuneLaCuentaYUnaSeccionPorCadaModulo()
    {
        var usuario = Existente();
        var habitos = Participante("habitos", new[] { new { nombre = "Beber agua" } });
        Participante("permisos");
        _consentimientos.Setup(c => c.ListarHistorialAsync(usuario.Id)).ReturnsAsync(
            [ConsentimientoUsuario.DelRegistro(usuario.Id, "v2.1", "v1.4", true, DateTime.UtcNow)]);
        _vinculos.Setup(v => v.ListarDeUsuarioAsync(usuario.Id)).ReturnsAsync(
            [new LoginExterno(usuario.Id, "google", "sub", DateTime.UtcNow)]);
        _sesionesRepo.Setup(s => s.ListarActivasAsync(usuario.Id, It.IsAny<DateTime>())).ReturnsAsync(
            [new SesionUsuario(usuario.Id, new string('a', 64), DateTime.UtcNow, TimeSpan.FromDays(14), "Firefox", "10.0.0.1")]);

        var datos = await Servicio().ExportarAsync(usuario.Id);

        Assert.Equal(usuario.Id, datos.Cuenta.Id);
        Assert.Equal(Correo, datos.Cuenta.Correo);
        Assert.True(datos.Cuenta.TienePassword);
        Assert.Single(datos.Consentimientos);
        Assert.True(datos.Consentimientos[0].Marketing);
        Assert.Equal("google", Assert.Single(datos.Vinculos).Proveedor);
        var sesion = Assert.Single(datos.Sesiones);
        Assert.Equal("10.0.0.1", sesion.Ip);   // la IP también es un dato personal
        Assert.Equal(["habitos", "permisos"], datos.Modulos.Keys.OrderBy(k => k).ToArray());
        habitos.Verify(p => p.ExportarAsync(usuario.Id, Correo, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RD_05_LaDescargaNuncaIncluyeLaContrasenaNiSuHashNiLosTokens()
    {
        var usuario = Existente();
        _sesionesRepo.Setup(s => s.ListarActivasAsync(usuario.Id, It.IsAny<DateTime>())).ReturnsAsync(
            [new SesionUsuario(usuario.Id, new string('a', 64), DateTime.UtcNow, TimeSpan.FromDays(14), "Firefox", null)]);
        usuario.GenerarCodigoRecuperacion("123456", 1);

        var json = JsonSerializer.Serialize(await Servicio().ExportarAsync(usuario.Id));

        Assert.DoesNotContain("hash-guardado", json);
        Assert.DoesNotContain("PasswordHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(new string('a', 64), json);   // el hash del refresh token
        Assert.DoesNotContain("123456", json);              // el código de recuperación
    }

    [Fact]
    public async Task RF_CA_07_LaDescargaDeDatosQuedaAuditada()
    {
        var usuario = Existente();

        await Servicio().ExportarAsync(usuario.Id);

        _auditoria.Verify(a => a.RegistroAuditoriaAsync(
            usuario.Id, "Estandar", It.IsAny<string>(), "Usuario", usuario.Id.ToString(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RF_CA_07_SiUnModuloFallaLaDescargaFallaEntera_NoSeEntregaUnArchivoIncompleto()
    {
        var usuario = Existente();
        var roto = Participante("permisos");
        roto.Setup(p => p.ExportarAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("fallo del módulo"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Servicio().ExportarAsync(usuario.Id));

        _auditoria.Verify(a => a.RegistroAuditoriaAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RD_06_UnAdministradorNoUsaLaDescarga()
    {
        var admin = Existente(Rol.Administrador);

        await Assert.ThrowsAsync<OperacionNoPermitidaException>(() => Servicio().ExportarAsync(admin.Id));
    }
}
