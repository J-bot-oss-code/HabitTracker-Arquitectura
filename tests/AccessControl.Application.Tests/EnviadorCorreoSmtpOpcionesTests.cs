using AccessControl.Infrastructure.Services;
using Microsoft.Extensions.Configuration;

namespace AccessControl.Application.Tests;

/// <summary>Configuración del servidor de correo (RF-NOT-13, RD-10): solo variables de entorno, validadas antes de enviar.</summary>
public class EnviadorCorreoSmtpOpcionesTests
{
    private static IConfiguration Config(params (string Clave, string? Valor)[] valores) =>
        new ConfigurationBuilder().AddInMemoryCollection(valores.ToDictionary(v => v.Clave, v => v.Valor)).Build();

    [Fact]
    [Trait("Requirement", "RF-NOT-13")]
    public void RF_NOT_13_UnServidorDePruebasSinCredencialesNiSsl_ConfiguraSoloHostPuertoYSsl()
    {
        var o = EnviadorCorreoSmtp.LeerOpciones(Config(("SMTP_HOST", "mailpit"), ("SMTP_PORT", "1025"), ("SMTP_SSL", "false")));

        Assert.Equal("mailpit", o.Host);
        Assert.Equal(1025, o.Puerto);
        Assert.False(o.Ssl);
        Assert.Null(o.Usuario);
        Assert.Null(o.Clave);
        Assert.Equal("no-responder@habittracker.local", o.Remitente);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-13")]
    public void RF_NOT_13_ConUsuarioYClave_SeAutenticaConSslPorDefectoYElUsuarioDeRemitente()
    {
        var o = EnviadorCorreoSmtp.LeerOpciones(Config(
            ("SMTP_HOST", "smtp.ejemplo.com"), ("SMTP_PORT", "587"), ("SMTP_USER", "avisos@ejemplo.com"), ("SMTP_PASS", "valor-de-prueba")));

        Assert.True(o.Ssl);
        Assert.Equal("avisos@ejemplo.com", o.Usuario);
        Assert.Equal("valor-de-prueba", o.Clave);
        Assert.Equal("avisos@ejemplo.com", o.Remitente);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-13")]
    public void RF_NOT_13_SmtpFromTienePrioridadComoRemitente()
    {
        var o = EnviadorCorreoSmtp.LeerOpciones(Config(
            ("SMTP_HOST", "h"), ("SMTP_PORT", "25"), ("SMTP_USER", "u@ejemplo.com"), ("SMTP_PASS", "p"), ("SMTP_FROM", "Hábitos <hola@ejemplo.com>")));

        Assert.Equal("Hábitos <hola@ejemplo.com>", o.Remitente);
    }

    [Theory]
    [InlineData(null, "25", null, null, "SMTP_HOST")]
    [InlineData("h", null, null, null, "SMTP_PORT")]
    [InlineData("h", "no-es-un-numero", null, null, "SMTP_PORT")]
    [InlineData("h", "70000", null, null, "SMTP_PORT")]
    [InlineData("h", "25", "solo-usuario@ejemplo.com", null, "SMTP_USER")]
    [InlineData("h", "25", null, "sola-clave", "SMTP_USER")]
    [InlineData("h", "25", "u@ejemplo.com", "p", "SMTP_SSL")]
    [Trait("Requirement", "RD-07")]
    public void RD_07_UnaConfiguracionIncompletaOInvalida_FallaConUnMensajeQueNombraLaVariable(
        string? host, string? puerto, string? usuario, string? clave, string variable)
    {
        var ssl = variable == "SMTP_SSL" ? "quizas" : null;

        var error = Assert.Throws<InvalidOperationException>(() => EnviadorCorreoSmtp.LeerOpciones(Config(
            ("SMTP_HOST", host), ("SMTP_PORT", puerto), ("SMTP_USER", usuario), ("SMTP_PASS", clave), ("SMTP_SSL", ssl))));

        Assert.Contains(variable, error.Message);
    }
}
