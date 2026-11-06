using AccessControl.Application.Interfaces;
using AccessControl.Application.Service;
using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace AccessControl.Application.Tests;

/// <summary>El borrado definitivo al terminar el periodo de gracia (RF-CA-20, RD-03, RD-09, derecho a la supresión).</summary>
public class ServicioBajaCuentasTests
{
    private static readonly DateTime Ahora = DateTime.UtcNow;

    private readonly Mock<IUsuarioRepository> _usuarios = new();
    private readonly List<string> _orden = [];

    private Mock<IParticipanteDatosUsuario> Participante(string seccion)
    {
        var participante = new Mock<IParticipanteDatosUsuario>();
        participante.SetupGet(p => p.Seccion).Returns(seccion);
        participante.Setup(p => p.EliminarAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback(() => _orden.Add(seccion));
        return participante;
    }

    private ServicioBajaCuentas Servicio(params Mock<IParticipanteDatosUsuario>[] participantes) =>
        new(_usuarios.Object, participantes.Select(p => p.Object).ToList(), Mock.Of<ILogger<ServicioBajaCuentas>>());

    private Usuario Vencido(string correo = "persona@ejemplo.com")
    {
        var usuario = new Usuario("Persona", new Email(correo), "hash");
        usuario.Activar();
        usuario.ProgramarBaja(Ahora.AddDays(-31), 30);
        return usuario;
    }

    private void HayVencidas(params Usuario[] usuarios) =>
        _usuarios.Setup(u => u.ObtenerConBajaVencidaAsync(Ahora, It.IsAny<int>())).ReturnsAsync(usuarios);

    [Fact]
    public async Task RF_CA_20_SinCuentasVencidasNoSeBorraNada()
    {
        HayVencidas();

        var borradas = await Servicio(Participante("habitos")).EjecutarAsync(Ahora);

        Assert.Equal(0, borradas);
        _usuarios.Verify(u => u.EliminarDefinitivamenteAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RD_03_PrimeroSeBorranLosDatosDeCadaModuloYDespuesLaCuenta()
    {
        var usuario = Vencido();
        HayVencidas(usuario);
        _usuarios.Setup(u => u.EliminarDefinitivamenteAsync(usuario.Id, "persona@ejemplo.com"))
            .Returns(Task.CompletedTask).Callback(() => _orden.Add("cuenta"));

        var borradas = await Servicio(Participante("habitos"), Participante("permisos"), Participante("documentos")).EjecutarAsync(Ahora);

        Assert.Equal(1, borradas);
        Assert.Equal(["habitos", "permisos", "documentos", "cuenta"], _orden);   // la cuenta es lo último
    }

    [Fact]
    public async Task RD_03_CadaModuloRecibeElIdentificadorYElCorreoDeLaCuenta()
    {
        var usuario = Vencido("ana@ejemplo.com");
        HayVencidas(usuario);
        var modulo = Participante("permisos");

        await Servicio(modulo).EjecutarAsync(Ahora);

        modulo.Verify(p => p.EliminarAsync(usuario.Id, "ana@ejemplo.com", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RF_CA_20_SiUnModuloFallaLaCuentaNoSeBorra_YSeReintentaEnLaSiguientePasada()
    {
        var usuario = Vencido();
        HayVencidas(usuario);
        var roto = Participante("documentos");
        roto.Setup(p => p.EliminarAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("el disco no responde"));

        var borradas = await Servicio(Participante("habitos"), roto).EjecutarAsync(Ahora);

        Assert.Equal(0, borradas);
        _usuarios.Verify(u => u.EliminarDefinitivamenteAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RF_CA_20_UnaCuentaConProblemasNoDetieneLasDemas()
    {
        var mala = Vencido("mala@ejemplo.com");
        var buena = Vencido("buena@ejemplo.com");
        HayVencidas(mala, buena);
        var modulo = Participante("habitos");
        modulo.Setup(p => p.EliminarAsync(mala.Id, It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException());

        var borradas = await Servicio(modulo).EjecutarAsync(Ahora);

        Assert.Equal(1, borradas);
        _usuarios.Verify(u => u.EliminarDefinitivamenteAsync(buena.Id, "buena@ejemplo.com"), Times.Once);
        _usuarios.Verify(u => u.EliminarDefinitivamenteAsync(mala.Id, It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RF_CA_20_SiBorrarLaCuentaFallaDespuesDeLosModulos_SeReintentaSinPerderNada()
    {
        var usuario = Vencido();
        HayVencidas(usuario);
        _usuarios.Setup(u => u.EliminarDefinitivamenteAsync(It.IsAny<Guid>(), It.IsAny<string>())).ThrowsAsync(new InvalidOperationException());

        var borradas = await Servicio(Participante("habitos")).EjecutarAsync(Ahora);

        Assert.Equal(0, borradas);
    }

    [Fact]
    public async Task RF_CA_20_SinParticipantesIgualSeBorraLaCuenta()
    {
        var usuario = Vencido();
        HayVencidas(usuario);

        Assert.Equal(1, await Servicio().EjecutarAsync(Ahora));

        _usuarios.Verify(u => u.EliminarDefinitivamenteAsync(usuario.Id, "persona@ejemplo.com"), Times.Once);
    }

    [Fact]
    public async Task RD_09_UnaCancelacionDelProcesoSePropagaYNoSeTomaPorUnFallo()
    {
        var usuario = Vencido();
        HayVencidas(usuario);
        var modulo = Participante("habitos");
        modulo.Setup(p => p.EliminarAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => Servicio(modulo).EjecutarAsync(Ahora));
    }

    [Fact]
    public async Task RD_09_ElProcesoPideUnLoteLimitado()
    {
        HayVencidas();

        await Servicio().EjecutarAsync(Ahora);

        _usuarios.Verify(u => u.ObtenerConBajaVencidaAsync(Ahora, It.IsInRange(1, 100, Moq.Range.Inclusive)), Times.Once);
    }

    // ---------------- Anonimización del log ----------------

    [Fact]
    public void RD_08_ElIdentificadorAnonimoNoRevelaLaCuentaPeroSigueElMismoCaso()
    {
        var id = Guid.NewGuid();

        var anonimo = ServicioBajaCuentas.Anonimizar(id);

        Assert.Equal(12, anonimo.Length);
        Assert.Matches("^[0-9A-F]{12}$", anonimo);
        Assert.DoesNotContain(id.ToString("N")[..8], anonimo, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(anonimo, ServicioBajaCuentas.Anonimizar(id));                 // estable: sirve para seguir un caso
        Assert.NotEqual(anonimo, ServicioBajaCuentas.Anonimizar(Guid.NewGuid()));  // distinto para cada cuenta
    }

    [Fact]
    public async Task RD_08_ElLogDeLaBajaNoContieneElCorreoNiElNombre()
    {
        var usuario = Vencido("secreto@ejemplo.com");
        HayVencidas(usuario);
        var mensajes = new List<string>();
        var logger = new Mock<ILogger<ServicioBajaCuentas>>();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        logger.Setup(l => l.Log(
                It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(new InvocationAction(i =>
            {
                var formateador = (Delegate)i.Arguments[4]!;
                mensajes.Add(formateador.DynamicInvoke(i.Arguments[2], i.Arguments[3])?.ToString() ?? string.Empty);
            }));
        var servicio = new ServicioBajaCuentas(_usuarios.Object, [], logger.Object);

        await servicio.EjecutarAsync(Ahora);

        Assert.NotEmpty(mensajes);
        Assert.All(mensajes, m =>
        {
            Assert.DoesNotContain("secreto@ejemplo.com", m);
            Assert.DoesNotContain("Persona", m);
            Assert.DoesNotContain(usuario.Id.ToString(), m, StringComparison.OrdinalIgnoreCase);
        });
    }
}
