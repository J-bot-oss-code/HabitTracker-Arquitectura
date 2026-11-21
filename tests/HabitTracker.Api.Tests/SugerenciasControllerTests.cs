using System.ComponentModel.DataAnnotations;
using System.Reflection;
using HabitTracker.Domain.Sugerencias;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using WebApi.Controllers;
using WebApi.Extensions;

namespace HabitTracker.Api.Tests;

/// <summary>Quejas y mejoras: quién puede llamar a cada ruta y qué valida el cuerpo, sin levantar la API (RD-06, RD-07, RD-12).</summary>
public class SugerenciasControllerTests
{
    private static string? Rol(string accion) =>
        typeof(SugerenciasController).GetMethod(accion)!.GetCustomAttributes<AuthorizeAttribute>().Single().Roles;

    [Theory]
    [InlineData(nameof(SugerenciasController.Enviar), "Estandar")]
    [InlineData(nameof(SugerenciasController.ListarMias), "Estandar")]
    [InlineData(nameof(SugerenciasController.Listar), "Administrador")]
    [InlineData(nameof(SugerenciasController.CambiarEstado), "Administrador")]
    [Trait("Requirement", "RD-06")]
    public void RD_06_CadaRutaExigeSuRolEnElServidor(string accion, string rol) => Assert.Equal(rol, Rol(accion));

    [Fact]
    [Trait("Requirement", "RD-06")]
    public void RD_06_ExigeSesionYTieneLimitePorPersona()
    {
        var tipo = typeof(SugerenciasController);

        Assert.NotEmpty(tipo.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal(LimitacionDePeticionesExtensions.Usuario, tipo.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public void RD_06_LaPersonaSaleDelToken_NingunaRutaRecibeElIdentificadorDeQuienEnvia()
    {
        foreach (var accion in new[] { nameof(SugerenciasController.Enviar), nameof(SugerenciasController.ListarMias) })
            Assert.DoesNotContain(typeof(SugerenciasController).GetMethod(accion)!.GetParameters(), p => p.Name!.Contains("usuario", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(EnviarSugerenciaRequest).GetProperties(), p => p.Name.Contains("Usuario", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_ElCuerpoExigeTipoYTextoYLimitaLaLongitud()
    {
        var ctor = typeof(EnviarSugerenciaRequest).GetConstructors()[0].GetParameters();
        Assert.NotNull(ctor.Single(p => p.Name == "Tipo").GetCustomAttribute<RequiredAttribute>());
        var texto = ctor.Single(p => p.Name == "Texto");
        Assert.NotNull(texto.GetCustomAttribute<RequiredAttribute>());
        Assert.Equal(Sugerencia.LongitudMaxima, texto.GetCustomAttribute<StringLengthAttribute>()!.MaximumLength);
        Assert.NotNull(typeof(CambiarEstadoSugerenciaRequest).GetConstructors()[0].GetParameters().Single().GetCustomAttribute<RequiredAttribute>());
    }
}
