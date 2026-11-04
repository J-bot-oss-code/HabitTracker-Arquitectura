using System.Security.Claims;
using System.Text.Json;
using DocumentManager.Application.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using WebApi.Middlewares;

namespace DocumentManager.Api.Tests;

public class GlobalExceptionMiddlewareDocumentosTests
{
    private static async Task<(int Status, JsonElement Cuerpo)> EjecutarAsync(Exception excepcion, bool autenticado = true)
    {
        var contexto = new DefaultHttpContext();
        contexto.Response.Body = new MemoryStream();
        if (autenticado)
        {
            contexto.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, "a@b.com")], "test"));
        }

        var middleware = new GlobalExceptionMiddleware(_ => throw excepcion, NullLogger<GlobalExceptionMiddleware>.Instance);
        await middleware.InvokeAsync(contexto);

        contexto.Response.Body.Position = 0;
        var cuerpo = await JsonDocument.ParseAsync(contexto.Response.Body);
        return (contexto.Response.StatusCode, cuerpo.RootElement.Clone());
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-03")]
    public async Task RF_DOC_03_DocumentoNoEncontrado_Responde404()
    {
        var (status, cuerpo) = await EjecutarAsync(new DocumentoNoEncontradoException());

        Assert.Equal(404, status);
        Assert.Equal("El documento no existe.", cuerpo.GetProperty("detail").GetString());
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_ArchivoInvalido_Responde400ConSuMensajeComprensible()
    {
        var (status, cuerpo) = await EjecutarAsync(new ArchivoInvalidoException("Debe adjuntar un archivo que no esté vacío."));

        Assert.Equal(400, status);
        Assert.Equal("Debe adjuntar un archivo que no esté vacío.", cuerpo.GetProperty("detail").GetString());
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-03")]
    public async Task RF_DOC_03_NoAutorizadoConSesion_Responde403()
    {
        var (status, _) = await EjecutarAsync(new UnauthorizedAccessException("No tiene permiso para acceder a este documento."));

        Assert.Equal(403, status);
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_NoAutorizadoSinSesion_Responde401()
    {
        var (status, _) = await EjecutarAsync(new UnauthorizedAccessException("Sesión inválida."), autenticado: false);

        Assert.Equal(401, status);
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_CuerpoDemasiadoGrande_Responde413Generico()
    {
        var (status, cuerpo) = await EjecutarAsync(
            new BadHttpRequestException("Request body too large. Limit is 11534336 bytes.", StatusCodes.Status413PayloadTooLarge));

        Assert.Equal(413, status);
        Assert.DoesNotContain("11534336", cuerpo.GetProperty("detail").GetString());
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_SolicitudMalFormada_Responde400()
    {
        var (status, cuerpo) = await EjecutarAsync(
            new BadHttpRequestException("Malformed request: detalle interno del servidor.", StatusCodes.Status400BadRequest));

        Assert.Equal(400, status);
        Assert.DoesNotContain("interno", cuerpo.GetProperty("detail").GetString());
    }

    [Fact]
    [Trait("Requirement", "RD-08")]
    public async Task RD_08_LosErroresSeResponden_ComoProblemJson()
    {
        var contexto = new DefaultHttpContext();
        contexto.Response.Body = new MemoryStream();
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new DocumentoNoEncontradoException(), NullLogger<GlobalExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(contexto);

        Assert.Equal("application/problem+json", contexto.Response.ContentType);
    }

    [Fact]
    [Trait("Requirement", "RD-08")]
    public async Task RD_08_ClienteQueAborta_NoGeneraUn500()
    {
        using var cancelacion = new CancellationTokenSource();
        var contexto = new DefaultHttpContext { RequestAborted = cancelacion.Token };
        contexto.Response.Body = new MemoryStream();
        await cancelacion.CancelAsync();
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new OperationCanceledException(), NullLogger<GlobalExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(contexto);

        Assert.NotEqual(500, contexto.Response.StatusCode);
        Assert.Equal(0, contexto.Response.Body.Length);
    }

    [Fact]
    [Trait("Requirement", "RD-08")]
    public async Task RD_08_ErrorInesperado_Responde500SinFiltrarMensajeNiRutas()
    {
        var (status, cuerpo) = await EjecutarAsync(new IOException(@"No se pudo escribir en C:\secretos\documentos\abc"));

        Assert.Equal(500, status);
        var texto = cuerpo.GetRawText();
        Assert.DoesNotContain("secretos", texto);
        Assert.DoesNotContain("IOException", texto);
    }
}
