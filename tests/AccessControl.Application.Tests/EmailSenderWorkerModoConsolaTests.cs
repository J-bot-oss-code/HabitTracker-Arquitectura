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

    [Fact]
    [Trait("Requirement", "RF-CA-01")]
    public void RF_CA_01_ResumirCuerpo_Activacion_ExtraeElEnlaceYElToken()
    {
        const string html = """
            <html><body><p>Hola</p>
            <a href="http://localhost:8080/api/auth/activate?correo=a%40b.com&amp;codigo=048213" style="x">Activar mi cuenta</a>
            <div style="font-size:22px;letter-spacing:3px;color:#0f766e;">048213</div></body></html>
            """;

        var resumen = EmailSenderWorker.ResumirCuerpo(html);

        Assert.Equal("Enlace: http://localhost:8080/api/auth/activate?correo=a%40b.com&codigo=048213 | Código: 048213", resumen);
        Assert.DoesNotContain("\n", resumen);
    }

    [Fact]
    [Trait("Requirement", "RF-CA-01")]
    public void RF_CA_01_ResumirCuerpo_Recuperacion_ExtraeSoloElCodigo()
    {
        const string html = """<p>Usa este código</p><div style="letter-spacing:3px;">K7Q2ZP</div>""";

        Assert.Equal("Código: K7Q2ZP", EmailSenderWorker.ResumirCuerpo(html));
    }

    [Fact]
    [Trait("Requirement", "RF-CA-01")]
    public void RF_CA_01_ResumirCuerpo_SinEnlaceNiCodigo_DevuelveElTextoSinEtiquetas()
    {
        Assert.Equal("Hola mundo", EmailSenderWorker.ResumirCuerpo("<p>Hola</p>\n<b>mundo</b>"));
        Assert.Equal(string.Empty, EmailSenderWorker.ResumirCuerpo("  "));
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
