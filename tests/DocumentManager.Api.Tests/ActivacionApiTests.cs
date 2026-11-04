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
    public void RD_07_ActivateGet_ExigeElMismoFormatoDeCodigoQueElPost()
    {
        var parametro = typeof(AuthController).GetMethod(nameof(AuthController.ActivateGet))!
            .GetParameters().Single(p => p.Name == "codigo");

        var validador = parametro.GetCustomAttributes<RegularExpressionAttribute>().Single();

        Assert.True(validador.IsValid("123456"));
        Assert.False(validador.IsValid("12345"));
        Assert.False(validador.IsValid("abcdef"));
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_ActivacionPorGet_TambienExigeElCorreoConFormatoValido()
    {
        var parametro = typeof(AuthController).GetMethod(nameof(AuthController.ActivateGet))!
            .GetParameters().Single(p => p.Name == "correo");

        Assert.NotNull(parametro.GetCustomAttribute<EmailAddressAttribute>());
    }
}
