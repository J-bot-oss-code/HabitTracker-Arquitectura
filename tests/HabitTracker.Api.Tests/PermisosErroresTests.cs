using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Permissions.Application.Exceptions;
using WebApi.Controllers;
using WebApi.Middlewares;

namespace HabitTracker.Api.Tests;

/// <summary>Errores del módulo de Permisos con código estable y validación de la clave del permiso (RD-07, RD-08).</summary>
public class PermisosErroresTests
{
    private static async Task<(int Status, JsonElement Cuerpo)> EjecutarAsync(Exception excepcion)
    {
        var contexto = new DefaultHttpContext();
        contexto.Response.Body = new MemoryStream();
        await new GlobalExceptionMiddleware(_ => throw excepcion, NullLogger<GlobalExceptionMiddleware>.Instance).InvokeAsync(contexto);
        contexto.Response.Body.Position = 0;
        var cuerpo = await JsonDocument.ParseAsync(contexto.Response.Body);
        return (contexto.Response.StatusCode, cuerpo.RootElement.Clone());
    }

    [Fact]
    [Trait("Requirement", "RD-08")]
    public async Task RD_08_SolicitudNoEncontrada_Es404ConCodigo_SinDependerDelTexto()
    {
        var (status, cuerpo) = await EjecutarAsync(new SolicitudNoEncontradaException());

        Assert.Equal(404, status);
        Assert.Equal("solicitud_no_encontrada", cuerpo.GetProperty("codigo").GetString());
    }

    [Fact]
    [Trait("Requirement", "RF-GP-01")]
    public async Task RF_GP_01_SolicitudDuplicada_Es409ConCodigo()
    {
        var (status, cuerpo) = await EjecutarAsync(new SolicitudDuplicadaException());

        Assert.Equal(409, status);
        Assert.Equal("solicitud_duplicada", cuerpo.GetProperty("codigo").GetString());
    }

    [Theory]
    [InlineData("Habits.AdvancedReconfig", true)]
    [InlineData("Documents.DeleteLinked.0a1b2c3d4e5f60718293a4b5c6d7e8f9", true)]
    [InlineData("Acceso a reportes", false)]
    [InlineData("sin-puntos", false)]
    [InlineData("<script>.x", false)]
    [InlineData("A.", false)]
    [InlineData("", false)]
    [Trait("Requirement", "RD-07")]
    public void RD_07_CrearPermisoRequest_SoloAceptaClavesConPuntos(string permiso, bool valido)
    {
        // ASP.NET valida los atributos del PARÁMETRO del constructor del record ([param: ...]); con [property: ...] los ignora y lanza.
        var parametro = typeof(CrearPermisoRequest).GetConstructors().Single().GetParameters().Single();
        var validadores = parametro.GetCustomAttributes<ValidationAttribute>().ToList();

        Assert.NotEmpty(validadores);
        Assert.Equal(valido, validadores.All(v => v.IsValid(permiso)));
    }
}
