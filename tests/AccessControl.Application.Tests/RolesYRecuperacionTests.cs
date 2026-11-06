using System.Text.RegularExpressions;
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
/// Roles de administrador (RF-CA-05, 06, 08, 13, 20), recuperación de contraseña (RF-CA-09 a 12) y cambio de clave
/// (RF-CA-22), incluida su protección contra abuso (RD-06).
/// </summary>
public class RolesYRecuperacionTests
{
    private sealed class Escenario
    {
        public List<Usuario> Usuarios { get; } = new();
        public Mock<IUsuarioRepository> Repo { get; } = new();
        public Mock<IEmailQueue> Cola { get; } = new();
        public Mock<ITokenBlacklist> Blacklist { get; } = new();
        public Mock<IAuditoriaService> Auditoria { get; } = new();
        public Mock<IPasswordHasher> Hasher { get; } = new();
        public int AdministradoresActivos { get; set; } = 2;

        public Escenario()
        {
            Repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Guid id) => Usuarios.FirstOrDefault(u => u.Id == id));
            Repo.Setup(r => r.GetByCorreoAsync(It.IsAny<Email>()))
                .ReturnsAsync((Email correo) => Usuarios.FirstOrDefault(u => u.Correo.Valor == correo.Valor));
            Repo.Setup(r => r.UpdateAsync(It.IsAny<Usuario>())).Returns(Task.CompletedTask);
            Repo.Setup(r => r.ContarAdministradoresActivosAsync()).ReturnsAsync(() => AdministradoresActivos);
            Cola.Setup(c => c.EncolarCorreoRecuperacionAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
            Hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hash-nuevo");
        }

        public UsuarioService Servicio => new(
            Repo.Object, Hasher.Object, Cola.Object, Auditoria.Object,
            Mock.Of<IJwtProvider>(), GestorSesionesDePrueba.Crear(Blacklist.Object), Mock.Of<ILogger<UsuarioService>>());

        public Usuario Agregar(string correo, Rol rol, bool activo = true)
        {
            var usuario = new Usuario($"Usuario {correo}", new Email(correo), "hash-guardado", rol);
            if (activo)
            {
                usuario.Activar();
            }

            Usuarios.Add(usuario);
            return usuario;
        }
    }

    private static void RetrocederEmision(Usuario usuario, TimeSpan hace) =>
        typeof(CodigoRecuperacion).GetProperty(nameof(CodigoRecuperacion.FechaEmision))!
            .SetValue(usuario.Recuperacion, DateTime.UtcNow - hace);

    // ====================== Roles y administración ======================

    [Fact]
    public async Task RF_CA_06_UnEstandarNoPuedeCambiarRoles_NiElSuyo()
    {
        var e = new Escenario();
        var estandar = e.Agregar("estandar@ejemplo.com", Rol.Estandar);

        var error = await Assert.ThrowsAsync<OperacionNoPermitidaException>(
            () => e.Servicio.CambiarRolAsync(estandar.Id, "Administrador", estandar.Id));

        Assert.Equal("operacion_no_permitida", error.Codigo);
        Assert.Equal(Rol.Estandar, estandar.Rol);
        e.Repo.Verify(r => r.UpdateAsync(It.IsAny<Usuario>()), Times.Never);
    }

    [Fact]
    public async Task RD_06_UnAdministradorDesactivadoOInexistenteTampocoPuedeEjecutar()
    {
        var e = new Escenario();
        var destino = e.Agregar("destino@ejemplo.com", Rol.Estandar);
        var adminDesactivado = e.Agregar("viejo@ejemplo.com", Rol.Administrador);
        adminDesactivado.Desactivar();

        await Assert.ThrowsAsync<OperacionNoPermitidaException>(
            () => e.Servicio.CambiarRolAsync(destino.Id, "Administrador", adminDesactivado.Id));
        await Assert.ThrowsAsync<OperacionNoPermitidaException>(
            () => e.Servicio.CambiarRolAsync(destino.Id, "Administrador", Guid.NewGuid()));
    }

    [Fact]
    public async Task RF_CA_08_CambiarElRol_RevocaLasSesionesAbiertasYQuedaAuditado()
    {
        var e = new Escenario();
        var admin = e.Agregar("admin@ejemplo.com", Rol.Administrador);
        var otro = e.Agregar("otro@ejemplo.com", Rol.Administrador);

        await e.Servicio.CambiarRolAsync(otro.Id, "Estandar", admin.Id);

        Assert.Equal(Rol.Estandar, otro.Rol);
        // El rol viaja en el JWT: sin cortar las sesiones, el administrador degradado conservaba sus permisos.
        e.Blacklist.Verify(b => b.RevocarSesionesAnteriores(otro.Id), Times.Once);
        e.Auditoria.Verify(a => a.RegistroAuditoriaAsync(
            admin.Id, It.IsAny<string>(), "Cambio de rol", "Usuario", otro.Id.ToString(), "Administrador", "Estandar"), Times.Once);
    }

    [Fact]
    public async Task RF_CA_08_ElUltimoAdministradorActivoNoPuedeSerDegradado()
    {
        var e = new Escenario { AdministradoresActivos = 1 };
        var admin = e.Agregar("admin@ejemplo.com", Rol.Administrador);

        var error = await Assert.ThrowsAsync<UltimoAdministradorException>(
            () => e.Servicio.CambiarRolAsync(admin.Id, "Estandar", admin.Id));

        Assert.Equal("ultimo_administrador", error.Codigo);
        Assert.Equal(Rol.Administrador, admin.Rol);
        e.Repo.Verify(r => r.UpdateAsync(It.IsAny<Usuario>()), Times.Never);
        e.Blacklist.Verify(b => b.RevocarSesionesAnteriores(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task RF_CA_08_PromoverAAdministradorNoDependeDelConteo()
    {
        var e = new Escenario { AdministradoresActivos = 1 };
        var admin = e.Agregar("admin@ejemplo.com", Rol.Administrador);
        var estandar = e.Agregar("estandar@ejemplo.com", Rol.Estandar);

        await e.Servicio.CambiarRolAsync(estandar.Id, "Administrador", admin.Id);

        Assert.Equal(Rol.Administrador, estandar.Rol);
    }

    [Fact]
    public async Task RF_CA_20_DesactivarUnUsuario_RevocaSusSesiones()
    {
        var e = new Escenario();
        var admin = e.Agregar("admin@ejemplo.com", Rol.Administrador);
        var estandar = e.Agregar("estandar@ejemplo.com", Rol.Estandar);

        await e.Servicio.CambiarEstadoAsync(estandar.Id, false, admin.Id);

        Assert.False(estandar.Activo);
        e.Blacklist.Verify(b => b.RevocarSesionesAnteriores(estandar.Id), Times.Once);
    }

    [Fact]
    public async Task RF_CA_20_UnAdministradorNoPuedeDesactivarseASiMismo()
    {
        var e = new Escenario();
        var admin = e.Agregar("admin@ejemplo.com", Rol.Administrador);

        await Assert.ThrowsAsync<InvalidOperationException>(() => e.Servicio.CambiarEstadoAsync(admin.Id, false, admin.Id));

        Assert.True(admin.Activo);
    }

    [Fact]
    public async Task RD_06_UnEstandarNoPuedeCambiarElEstadoDeNadie()
    {
        var e = new Escenario();
        var estandar = e.Agregar("estandar@ejemplo.com", Rol.Estandar);
        var victima = e.Agregar("victima@ejemplo.com", Rol.Estandar);

        await Assert.ThrowsAsync<OperacionNoPermitidaException>(() => e.Servicio.CambiarEstadoAsync(victima.Id, false, estandar.Id));

        Assert.True(victima.Activo);
    }

    [Fact]
    public async Task RF_CA_20_ActivarUnaCuentaPendiente_DescartaSuCodigoDeActivacion()
    {
        var e = new Escenario();
        var admin = e.Agregar("admin@ejemplo.com", Rol.Administrador);
        var pendiente = e.Agregar("pendiente@ejemplo.com", Rol.Estandar, activo: false);
        pendiente.EstablecerTokenActivacion("123456", DateTime.UtcNow.AddHours(24));
        Assert.True(pendiente.PendienteDeActivacion);

        await e.Servicio.CambiarEstadoAsync(pendiente.Id, true, admin.Id);

        Assert.True(pendiente.Activo);
        Assert.Null(pendiente.TokenAcceso);
    }

    [Fact]
    public async Task RF_CA_13_SoloUnAdministradorPuedeForzarElRestablecimiento()
    {
        var e = new Escenario();
        var estandar = e.Agregar("estandar@ejemplo.com", Rol.Estandar);
        var victima = e.Agregar("victima@ejemplo.com", Rol.Estandar);

        await Assert.ThrowsAsync<OperacionNoPermitidaException>(() => e.Servicio.ForzarRestablecimientoAsync(victima.Id, estandar.Id));

        Assert.Equal("hash-guardado", victima.PasswordHash);
        e.Cola.Verify(c => c.EncolarCorreoRecuperacionAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RF_CA_13_ElAdministradorInvalidaLaClaveYEnviaUnCodigoDeSeisDigitos()
    {
        var e = new Escenario();
        var admin = e.Agregar("admin@ejemplo.com", Rol.Administrador);
        var victima = e.Agregar("victima@ejemplo.com", Rol.Estandar);
        string? codigo = null;
        e.Cola.Setup(c => c.EncolarCorreoRecuperacionAsync("victima@ejemplo.com", It.IsAny<string>()))
            .Callback<string, string>((_, c) => codigo = c).Returns(Task.CompletedTask);

        await e.Servicio.ForzarRestablecimientoAsync(victima.Id, admin.Id);

        Assert.StartsWith("INVALIDADO_", victima.PasswordHash);
        Assert.Matches("^[0-9]{6}$", codigo!);
        e.Blacklist.Verify(b => b.RevocarSesionesAnteriores(victima.Id), Times.Once);
    }

    // ====================== Recuperación de contraseña ======================

    [Fact]
    public async Task RF_CA_10_LaRecuperacionEnviaUnCodigoDeSeisDigitosAlCorreoNormalizado()
    {
        var e = new Escenario();
        var usuario = e.Agregar("persona@ejemplo.com", Rol.Estandar);
        string? destino = null, codigo = null;
        e.Cola.Setup(c => c.EncolarCorreoRecuperacionAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((d, c) => { destino = d; codigo = c; }).Returns(Task.CompletedTask);

        // El texto de la petición llega con espacios y mayúsculas.
        await e.Servicio.IniciarRecuperacionAsync("  Persona@Ejemplo.COM ");

        Assert.Equal("persona@ejemplo.com", destino);
        Assert.Matches("^[0-9]{6}$", codigo!);
        Assert.True(usuario.Recuperacion!.EsValido(codigo!));
    }

    [Fact]
    public async Task RD_06_DentroDelEnfriamiento_NoEmiteOtroCodigoNiOtroCorreo()
    {
        var e = new Escenario();
        var usuario = e.Agregar("persona@ejemplo.com", Rol.Estandar);
        await e.Servicio.IniciarRecuperacionAsync("persona@ejemplo.com");
        var primerCodigo = usuario.Recuperacion!.Valor;
        usuario.Recuperacion.RegistrarFallo();

        await e.Servicio.IniciarRecuperacionAsync("persona@ejemplo.com");

        // Sin el enfriamiento, pedir otro código reiniciaba el contador de fallos (fuerza bruta sin fin) e inundaba de correos.
        Assert.Equal(primerCodigo, usuario.Recuperacion.Valor);
        Assert.Equal(1, usuario.Recuperacion.IntentosFallidos);
        e.Cola.Verify(c => c.EncolarCorreoRecuperacionAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RF_CA_10_PasadoElEnfriamiento_SeEmiteUnCodigoNuevo()
    {
        var e = new Escenario();
        var usuario = e.Agregar("persona@ejemplo.com", Rol.Estandar);
        await e.Servicio.IniciarRecuperacionAsync("persona@ejemplo.com");
        RetrocederEmision(usuario, TimeSpan.FromMinutes(2));

        await e.Servicio.IniciarRecuperacionAsync("persona@ejemplo.com");

        e.Cola.Verify(c => c.EncolarCorreoRecuperacionAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RF_CA_09_ConUnCorreoInexistente_NoEnviaNadaYNoFalla()
    {
        var e = new Escenario();

        await e.Servicio.IniciarRecuperacionAsync("nadie@ejemplo.com");

        e.Cola.Verify(c => c.EncolarCorreoRecuperacionAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("ABC123", true)]
    [InlineData("abc123", false)]
    [InlineData("ABC12", false)]
    [InlineData("ABC1234", false)]
    [InlineData("", false)]
    public void RF_CA_10_ElCodigoSeComparaExactoYEnTiempoConstante(string ingresado, bool esperado)
    {
        var codigo = new CodigoRecuperacion("ABC123", 1);

        Assert.Equal(esperado, codigo.EsValido(ingresado));
        Assert.False(codigo.EsValido(null!));
    }

    [Fact]
    public async Task RF_CA_11_Restablecer_CambiaLaClaveLevantaElBloqueoYRevocaLasSesiones()
    {
        var e = new Escenario();
        var usuario = e.Agregar("persona@ejemplo.com", Rol.Estandar);
        for (var i = 0; i < 4; i++)
        {
            usuario.RegistrarIntentoFallido();
        }

        usuario.GenerarCodigoRecuperacion("654321", 1);

        await e.Servicio.RestablecerPasswordAsync("persona@ejemplo.com", "654321", "NuevaClave123");

        Assert.Equal("hash-nuevo", usuario.PasswordHash);
        Assert.Equal(0, usuario.Bloqueo.IntentosFallidos);
        e.Blacklist.Verify(b => b.RevocarSesionesAnteriores(usuario.Id), Times.Once);
        await Assert.ThrowsAsync<CodigoInvalidoException>(
            () => e.Servicio.RestablecerPasswordAsync("persona@ejemplo.com", "654321", "OtraClave1234"));
    }

    [Fact]
    public async Task RD_06_CincoCodigosIncorrectos_InvalidanElCodigoAunqueElSextoSeaElCorrecto()
    {
        var e = new Escenario();
        var usuario = e.Agregar("persona@ejemplo.com", Rol.Estandar);
        usuario.GenerarCodigoRecuperacion("654321", 1);

        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<CodigoInvalidoException>(
                () => e.Servicio.RestablecerPasswordAsync("persona@ejemplo.com", "000000", "NuevaClave123"));
        }

        await Assert.ThrowsAsync<CodigoInvalidoException>(
            () => e.Servicio.RestablecerPasswordAsync("persona@ejemplo.com", "654321", "NuevaClave123"));
        Assert.Equal("hash-guardado", usuario.PasswordHash);
    }

    [Fact]
    public async Task RF_CA_09_UnCorreoInexistenteDaElMismoRechazoQueUnCodigoMalo()
    {
        var e = new Escenario();
        var usuario = e.Agregar("persona@ejemplo.com", Rol.Estandar);
        usuario.GenerarCodigoRecuperacion("654321", 1);

        var conUsuario = await Assert.ThrowsAsync<CodigoInvalidoException>(
            () => e.Servicio.RestablecerPasswordAsync("persona@ejemplo.com", "000000", "NuevaClave123"));
        var sinUsuario = await Assert.ThrowsAsync<CodigoInvalidoException>(
            () => e.Servicio.RestablecerPasswordAsync("nadie@ejemplo.com", "000000", "NuevaClave123"));

        Assert.Equal(sinUsuario.Message, conUsuario.Message);
    }

    // ====================== Cambio de contraseña autenticado ======================

    [Fact]
    public async Task RF_CA_22_LaClaveActualIncorrecta_SeRechazaConSuCodigoYCuentaComoFallo()
    {
        var e = new Escenario();
        var usuario = e.Agregar("persona@ejemplo.com", Rol.Estandar);
        e.Hasher.Setup(h => h.Verificar(It.IsAny<string>(), "incorrecta")).Returns(false);

        var error = await Assert.ThrowsAsync<ClaveActualIncorrectaException>(
            () => e.Servicio.CambiarPasswordAsync(usuario.Id, "incorrecta", "NuevaClave123"));

        Assert.Equal("clave_actual_incorrecta", error.Codigo);
        Assert.Equal(1, usuario.Bloqueo.IntentosFallidos);
        Assert.Equal("hash-guardado", usuario.PasswordHash);
        e.Blacklist.Verify(b => b.RevocarSesionesAnteriores(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task RF_CA_19_CincoClavesActualesIncorrectas_BloqueanLaCuenta()
    {
        var e = new Escenario();
        var usuario = e.Agregar("persona@ejemplo.com", Rol.Estandar);
        e.Hasher.Setup(h => h.Verificar(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        for (var i = 1; i <= 4; i++)
        {
            await Assert.ThrowsAsync<ClaveActualIncorrectaException>(
                () => e.Servicio.CambiarPasswordAsync(usuario.Id, "incorrecta", "NuevaClave123"));
        }

        await Assert.ThrowsAsync<CuentaBloqueadaException>(
            () => e.Servicio.CambiarPasswordAsync(usuario.Id, "incorrecta", "NuevaClave123"));
        Assert.True(usuario.EstaBloqueado());
    }

    [Fact]
    public async Task RF_CA_22_ElCambioCorrecto_GuardaLaClaveNuevaYRevocaLasSesiones()
    {
        var e = new Escenario();
        var usuario = e.Agregar("persona@ejemplo.com", Rol.Estandar);
        e.Hasher.Setup(h => h.Verificar("hash-guardado", "ClaveActual123")).Returns(true);

        await e.Servicio.CambiarPasswordAsync(usuario.Id, "ClaveActual123", "NuevaClave123");

        Assert.Equal("hash-nuevo", usuario.PasswordHash);
        e.Blacklist.Verify(b => b.RevocarSesionesAnteriores(usuario.Id), Times.Once);
    }

    // ====================== Registro ======================

    [Fact]
    public async Task RF_CA_01_UnCorreoYaRegistrado_SeRechazaConSuCodigo()
    {
        var e = new Escenario();
        e.Agregar("persona@ejemplo.com", Rol.Estandar);
        e.Repo.Setup(r => r.CorreoExistsAsync(It.IsAny<Email>())).ReturnsAsync(true);

        var error = await Assert.ThrowsAsync<CorreoYaRegistradoException>(
            () => e.Servicio.RegistrarUsuarioAsync(new RegistroRequestDto("Persona", "persona@ejemplo.com", "ClaveSegura123", new ConsentimientosRegistroDto("v2.1", "v1.4", false))));

        Assert.Equal("correo_ya_registrado", error.Codigo);
        e.Repo.Verify(r => r.SaveAsync(It.IsAny<Usuario>(), It.IsAny<ConsentimientoUsuario>()), Times.Never);
    }

    [Fact]
    public async Task RF_CA_01_SiElRepositorioDetectaElDuplicado_ElRechazoLlegaIgualAlUsuario()
    {
        // Carrera: la comprobación previa dijo "no existe" pero otro registro ganó (el índice único lo detecta al guardar).
        var e = new Escenario();
        e.Repo.Setup(r => r.CorreoExistsAsync(It.IsAny<Email>())).ReturnsAsync(false);
        e.Repo.Setup(r => r.SaveAsync(It.IsAny<Usuario>(), It.IsAny<ConsentimientoUsuario>())).ThrowsAsync(new CorreoYaRegistradoException());

        await Assert.ThrowsAsync<CorreoYaRegistradoException>(
            () => e.Servicio.RegistrarUsuarioAsync(new RegistroRequestDto("Persona", "persona@ejemplo.com", "ClaveSegura123", new ConsentimientosRegistroDto("v2.1", "v1.4", false))));

        e.Cola.Verify(c => c.EncolarCorreoActivacionAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
