using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.RateLimiting;
using WebApi.Controllers;

namespace DocumentManager.Api.Tests;

/// <summary>Protecciones del endpoint de activación de cuenta (código de 6 dígitos): formato y límite por IP.</summary>
public class ActivacionApiTests
{
    [Theory]
    [InlineData(nameof(AuthController.Activate))]
    [InlineData(nameof(AuthController.ActivateGet))]
    [InlineData(nameof(AuthController.ResendActivation))]
    [Trait("Requirement", "RD-06")]
    public void RD_06_ActivacionYReenvio_TienenLimiteDePeticionesPorIp(string accion)
    {
        var metodo = typeof(AuthController).GetMethod(accion)!;

        var limite = metodo.GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.NotNull(limite);
        Assert.Equal("activacion", limite.PolicyName);
    }

    [Theory]
    [InlineData("000000", true)]
    [InlineData("048213", true)]
    [InlineData("999999", true)]
    [InlineData("12345", false)]
    [InlineData("1234567", false)]
    [InlineData("12345a", false)]
    [InlineData("abcdef", false)]
    [InlineData("123 56", false)]
    [InlineData("123456\n", false)]
    [Trait("Requirement", "RD-07")]
    public void RD_07_ActivateRequest_SoloAceptaSeisDigitos(string codigo, bool esperado)
    {
        var parametro = typeof(ActivateRequest).GetConstructors()[0].GetParameters().Single(p => p.Name == "Codigo");
        var validador = parametro.GetCustomAttributes<RegularExpressionAttribute>().Single();

        Assert.Equal(esperado, validador.IsValid(codigo));
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_ActivateGet_NoRecibeDatosDeLaUrl_ElCorreoYElCodigoSoloLosProcesaElPostValidado()
    {
        // El GET solo entrega una página: no tiene parámetros, así que nada de la URL llega al servidor ni cambia el estado.
        // La activación real es el POST (ActivateRequest), que sí valida el correo y el código de 6 dígitos.
        Assert.Empty(typeof(AuthController).GetMethod(nameof(AuthController.ActivateGet))!.GetParameters());

        var correo = typeof(ActivateRequest).GetConstructors()[0].GetParameters().Single(p => p.Name == "Correo");
        Assert.NotNull(correo.GetCustomAttribute<EmailAddressAttribute>());
    }

    [Theory]
    [InlineData(nameof(AuthController.Login), "login")]
    [InlineData(nameof(AuthController.Register), "registro")]
    [InlineData(nameof(AuthController.RecuperarPassword), "recuperacion")]
    [InlineData(nameof(AuthController.RestablecerPassword), "recuperacion")]
    [Trait("Requirement", "RD-06")]
    public void RD_06_LoginRegistroYRecuperacion_TienenLimiteDePeticionesPorIp(string accion, string politica)
    {
        // Sin este límite cualquiera podía bloquear la cuenta de otra persona (RF-CA-19), crear cuentas en masa o probar
        // códigos de recuperación sin fin.
        var limite = typeof(AuthController).GetMethod(accion)!.GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.NotNull(limite);
        Assert.Equal(politica, limite.PolicyName);
    }
}
