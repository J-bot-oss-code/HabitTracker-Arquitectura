using AccessControl.Infrastructure.Workers;
using Microsoft.Extensions.Configuration;

namespace AccessControl.Application.Tests;

public class EmailSenderWorkerModoConsolaTests
{
    private static IConfiguration Config(params (string Clave, string? Valor)[] valores) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(valores.Select(v => new KeyValuePair<string, string?>(v.Clave, v.Valor)))
            .Build();

    [Theory]
    [InlineData("consola", null)]
    [InlineData("consola", "")]
    [InlineData("consola", "   ")]
    [InlineData("CONSOLA", null)]
    [InlineData(" consola ", null)]
    [Trait("Requirement", "RF-CA-01")]
    public void RF_CA_01_ModoConsolaPedidoYSinSmtp_EscribeEnConsola(string modo, string? host)
    {
        var config = Config(("CORREO_MODO", modo), ("SMTP_HOST", host));

        Assert.True(EmailSenderWorker.DebeEscribirEnConsola(config));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("smtp", null)]
    [InlineData("consola", "smtp.ejemplo.com")]
    [Trait("Requirement", "RD-10")]
    public void RD_10_SinPedirloOConSmtpDefinido_NuncaEscribeEnConsola(string? modo, string? host)
    {
        var config = Config(("CORREO_MODO", modo), ("SMTP_HOST", host));

        Assert.False(EmailSenderWorker.DebeEscribirEnConsola(config));
    }
}
