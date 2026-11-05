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

/// <summary>Activación de cuenta con un código de 6 dígitos (RF-CA-01, RF-CA-15) y su protección contra fuerza bruta.</summary>
public class ActivacionConCodigoTests
{
    private const string Correo = "persona@ejemplo.com";

    private static Usuario CrearUsuarioPendiente(string codigo = "123456", TimeSpan? emitidoHace = null)
    {
        var usuario = new Usuario("Persona de prueba", new Email(Correo), "hash", Rol.Estandar);
        // La emisión se deduce del vencimiento: vencimiento = emisión + 24 h.
        var emision = DateTime.UtcNow - (emitidoHace ?? TimeSpan.FromMinutes(10));
        usuario.EstablecerTokenActivacion(codigo, emision.AddHours(24));
        return usuario;
    }

    private static (UsuarioService Servicio, Mock<IUsuarioRepository> Repo, Mock<IEmailQueue> Cola) Crear(Usuario? usuario)
    {
        var repo = new Mock<IUsuarioRepository>();
        repo.Setup(r => r.GetByCorreoAsync(It.IsAny<Email>())).ReturnsAsync(usuario);
        repo.Setup(r => r.UpdateAsync(It.IsAny<Usuario>())).Returns(Task.CompletedTask);
        repo.Setup(r => r.CorreoExistsAsync(It.IsAny<Email>())).ReturnsAsync(false);
        repo.Setup(r => r.SaveAsync(It.IsAny<Usuario>(), It.IsAny<ConsentimientoUsuario>())).Returns(Task.CompletedTask);

        // Simula la reserva ATÓMICA de la base de datos: cada llamada obtiene un número distinto, aunque sea en paralelo.
        var intentos = 0;
        repo.Setup(r => r.ReservarIntentoActivacionAsync(It.IsAny<Guid>()))
            .ReturnsAsync(() => usuario?.TokenAcceso != null && !usuario.Activo ? Interlocked.Increment(ref intentos) : 0);

        var cola = new Mock<IEmailQueue>();
        cola.Setup(c => c.EncolarCorreoActivacionAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hash");

        var servicio = new UsuarioService(
            repo.Object, hasher.Object, cola.Object, Mock.Of<IAuditoriaService>(),
            Mock.Of<IJwtProvider>(), Mock.Of<ITokenBlacklist>(), Mock.Of<ILogger<UsuarioService>>());
        return (servicio, repo, cola);
    }

    // ---------- Generación ----------

    [Fact]
    [Trait("Requirement", "RF-CA-01")]
    public async Task RF_CA_01_Registro_EnviaUnCodigoDeExactamenteSeisDigitos()
    {
        var (servicio, _, cola) = Crear(null);
        string? enviado = null;
        cola.Setup(c => c.EncolarCorreoActivacionAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, codigo) => enviado = codigo).Returns(Task.CompletedTask);

        await servicio.RegistrarUsuarioAsync(new("Persona", Correo, "ClaveSegura123", new ConsentimientosRegistroDto("v2.1", "v1.4", false)));

        Assert.Matches(@"^\d{6}$", enviado);
    }

    [Fact]
    [Trait("Requirement", "RF-CA-01")]
    public async Task RF_CA_01_Registro_LosCodigosSonAleatoriosYConservanLosCerosIniciales()
    {
        var (servicio, _, cola) = Crear(null);
        var codigos = new List<string>();
        cola.Setup(c => c.EncolarCorreoActivacionAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, codigo) => codigos.Add(codigo)).Returns(Task.CompletedTask);

        for (var i = 0; i < 300; i++)
        {
            await servicio.RegistrarUsuarioAsync(new("Persona", $"p{i}@ejemplo.com", "ClaveSegura123", new ConsentimientosRegistroDto("v2.1", "v1.4", false)));
        }

        Assert.All(codigos, c => Assert.Matches(@"^\d{6}$", c));
        Assert.True(codigos.Distinct().Count() > 250, "300 códigos deberían ser casi todos distintos");
        Assert.Contains(codigos, c => c.StartsWith('0')); // ~10 % empieza en 0: "000123" no puede perder sus ceros
    }

    // ---------- Activación ----------

    [Fact]
    [Trait("Requirement", "RF-CA-15")]
    public async Task RF_CA_15_CorreoYCodigoCorrectos_ActivanLaCuenta()
    {
        var usuario = CrearUsuarioPendiente("048213");
        var (servicio, repo, _) = Crear(usuario);

        await servicio.ActivarCuentaAsync(Correo, "048213");

        Assert.True(usuario.Activo);
        Assert.Null(usuario.TokenAcceso);
        repo.Verify(r => r.UpdateAsync(usuario), Times.Once);
    }

    [Fact]
    [Trait("Requirement", "RF-CA-15")]
    public async Task RF_CA_15_CodigoIncorrecto_SeRechazaYSeReservaUnIntento()
    {
        var usuario = CrearUsuarioPendiente("123456");
        var (servicio, repo, _) = Crear(usuario);

        await Assert.ThrowsAsync<CodigoInvalidoException>(() => servicio.ActivarCuentaAsync(Correo, "654321"));

        Assert.False(usuario.Activo);
        repo.Verify(r => r.ReservarIntentoActivacionAsync(usuario.Id), Times.Once);
        repo.Verify(r => r.UpdateAsync(It.IsAny<Usuario>()), Times.Never); // no se reescribe la fila con un contador viejo
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_RafagaDeCienIntentosEnParalelo_AgotaElLimiteYElCodigoCorrectoYaNoSirve()
    {
        // Con leer-incrementar-guardar, 100 peticiones simultáneas leerían el mismo contador y costarían 1 intento.
        // Con la reserva atómica, cada una obtiene su número y pasados los 5 primeros ninguna vuelve a comparar.
        var usuario = CrearUsuarioPendiente("123456");
        var (servicio, repo, _) = Crear(usuario);

        var rafaga = Enumerable.Range(0, 100).Select(_ => Task.Run(async () =>
        {
            await Assert.ThrowsAsync<CodigoInvalidoException>(() => servicio.ActivarCuentaAsync(Correo, "000000"));
        }));
        await Task.WhenAll(rafaga);

        await Assert.ThrowsAsync<CodigoInvalidoException>(() => servicio.ActivarCuentaAsync(Correo, "123456"));
        Assert.False(usuario.Activo);
        repo.Verify(r => r.ReservarIntentoActivacionAsync(usuario.Id), Times.Exactly(101));
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_CorreoInexistenteOCuentaActiva_NoReservanIntentos()
    {
        var activo = new Usuario("Activo", new Email(Correo), "hash", Rol.Estandar);
        activo.Activar();

        foreach (var usuario in new Usuario?[] { null, activo })
        {
            var (servicio, repo, _) = Crear(usuario);
            await Assert.ThrowsAsync<CodigoInvalidoException>(() => servicio.ActivarCuentaAsync(Correo, "123456"));
            repo.Verify(r => r.ReservarIntentoActivacionAsync(It.IsAny<Guid>()), Times.Never);
        }
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_TrasCincoFallos_ElCodigoCorrectoYaNoSirve()
    {
        var usuario = CrearUsuarioPendiente("123456");
        var (servicio, _, _) = Crear(usuario);

        for (var i = 0; i < TokenActivacion.MaxIntentos; i++)
        {
            await Assert.ThrowsAsync<CodigoInvalidoException>(() => servicio.ActivarCuentaAsync(Correo, "000000"));
        }

        // Aunque ahora acierte, el código quedó invalidado: debe pedir uno nuevo.
        await Assert.ThrowsAsync<CodigoInvalidoException>(() => servicio.ActivarCuentaAsync(Correo, "123456"));
        Assert.False(usuario.Activo);
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_CuatroFallosYUnAcierto_SiActiva()
    {
        var usuario = CrearUsuarioPendiente("123456");
        var (servicio, _, _) = Crear(usuario);

        for (var i = 0; i < TokenActivacion.MaxIntentos - 1; i++)
        {
            await Assert.ThrowsAsync<CodigoInvalidoException>(() => servicio.ActivarCuentaAsync(Correo, "000000"));
        }

        await servicio.ActivarCuentaAsync(Correo, "123456");

        Assert.True(usuario.Activo);
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_ElCodigoDeOtraPersona_NoActivaLaCuenta()
    {
        // Con 6 dígitos dos personas pueden recibir el mismo código: siempre se valida junto al correo.
        var ana = CrearUsuarioPendiente("111111");
        var (servicio, _, _) = Crear(ana);

        await Assert.ThrowsAsync<CodigoInvalidoException>(() => servicio.ActivarCuentaAsync(Correo, "222222"));
        Assert.False(ana.Activo);
    }

    [Fact]
    [Trait("Requirement", "RD-08")]
    public async Task RD_08_CorreoInexistenteYaActivoOCodigoMalo_DanElMismoMensaje()
    {
        var pendiente = CrearUsuarioPendiente("123456");
        var activo = new Usuario("Activo", new Email(Correo), "hash", Rol.Estandar);
        activo.Activar();

        var mensajes = new List<string>();
        foreach (var usuario in new Usuario?[] { null, activo, pendiente })
        {
            var (servicio, _, _) = Crear(usuario);
            var ex = await Assert.ThrowsAsync<CodigoInvalidoException>(() => servicio.ActivarCuentaAsync(Correo, "999999"));
            mensajes.Add(ex.Message);
        }

        Assert.Single(mensajes.Distinct());
    }

    [Fact]
    [Trait("Requirement", "RF-CA-15")]
    public async Task RF_CA_15_CodigoVencido_SeRechaza()
    {
        var usuario = new Usuario("Persona", new Email(Correo), "hash", Rol.Estandar);
        usuario.EstablecerTokenActivacion("123456", DateTime.UtcNow.AddMilliseconds(150));
        await Task.Delay(300);
        var (servicio, _, _) = Crear(usuario);

        await Assert.ThrowsAsync<CodigoInvalidoException>(() => servicio.ActivarCuentaAsync(Correo, "123456"));
        Assert.False(usuario.Activo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12345a")]
    [InlineData("12 456")]
    [InlineData("123456\n")]
    [InlineData("١٢٣٤٥٦")] // dígitos arábigos-índicos: la clase \d de .NET los aceptaría
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_CodigoQueNoSonSeisDigitos_SeRechazaAntesDeConsultarLaBase(string codigo)
    {
        var (servicio, repo, _) = Crear(CrearUsuarioPendiente());

        await Assert.ThrowsAsync<ArgumentException>(() => servicio.ActivarCuentaAsync(Correo, codigo));

        repo.Verify(r => r.GetByCorreoAsync(It.IsAny<Email>()), Times.Never);
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_CorreoConFormatoInvalido_SeRechaza()
    {
        var (servicio, _, _) = Crear(null);

        await Assert.ThrowsAsync<ArgumentException>(() => servicio.ActivarCuentaAsync("no-es-un-correo", "123456"));
    }

    // ---------- Reenvío ----------

    [Fact]
    [Trait("Requirement", "RF-CA-17")]
    public async Task RF_CA_17_Reenvio_PasadoElEnfriamiento_GeneraUnCodigoNuevoYReiniciaLosIntentos()
    {
        var usuario = CrearUsuarioPendiente("123456", emitidoHace: TimeSpan.FromMinutes(5));
        usuario.RegistrarFalloActivacion();
        var (servicio, _, cola) = Crear(usuario);
        string? nuevo = null;
        cola.Setup(c => c.EncolarCorreoActivacionAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, codigo) => nuevo = codigo).Returns(Task.CompletedTask);

        await servicio.ReenviarEnlaceActivacionAsync(Correo);

        Assert.Matches(@"^\d{6}$", nuevo);
        Assert.Equal(nuevo, usuario.TokenAcceso!.Valor);
        Assert.Equal(0, usuario.TokenAcceso.IntentosFallidos);
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_Reenvio_DentroDelEnfriamiento_NoCambiaElCodigoNiEnviaOtroCorreo()
    {
        var usuario = CrearUsuarioPendiente("123456", emitidoHace: TimeSpan.FromSeconds(10));
        var (servicio, repo, cola) = Crear(usuario);

        await servicio.ReenviarEnlaceActivacionAsync(Correo);

        Assert.Equal("123456", usuario.TokenAcceso!.Valor);
        cola.Verify(c => c.EncolarCorreoActivacionAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        repo.Verify(r => r.UpdateAsync(It.IsAny<Usuario>()), Times.Never);
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_Reenvio_TrasInvalidarElCodigo_SigueRigiendoElEnfriamiento()
    {
        // El atacante agota los 5 intentos y pide otro código al instante para reiniciar el contador: no debe poder.
        var usuario = CrearUsuarioPendiente("123456", emitidoHace: TimeSpan.FromSeconds(10));
        for (var i = 0; i < TokenActivacion.MaxIntentos; i++) usuario.RegistrarFalloActivacion();
        var (servicio, _, cola) = Crear(usuario);

        await servicio.ReenviarEnlaceActivacionAsync(Correo);

        Assert.Equal("123456", usuario.TokenAcceso!.Valor);
        cola.Verify(c => c.EncolarCorreoActivacionAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    [Trait("Requirement", "RF-CA-17")]
    public async Task RF_CA_17_Reenvio_CuentaYaActivaOCorreoInexistente_NoHaceNada()
    {
        var activo = new Usuario("Activo", new Email(Correo), "hash", Rol.Estandar);
        activo.Activar();

        foreach (var usuario in new Usuario?[] { null, activo })
        {
            var (servicio, _, cola) = Crear(usuario);
            await servicio.ReenviarEnlaceActivacionAsync(Correo);
            cola.Verify(c => c.EncolarCorreoActivacionAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }

    // ---------- Dominio ----------

    [Fact]
    [Trait("Requirement", "RD-06")]
    public void RD_06_TokenActivacion_AlLlegarAlMaximoDeFallos_NoValidaAunqueElCodigoSeaCorrecto()
    {
        var token = new TokenActivacion("123456", DateTime.UtcNow.AddHours(1));
        Assert.True(token.EsValido("123456"));

        for (var i = 0; i < TokenActivacion.MaxIntentos; i++) token.RegistrarFallo();

        Assert.False(token.EsValido("123456"));
        Assert.Equal(TokenActivacion.MaxIntentos, token.IntentosFallidos);
    }
}
