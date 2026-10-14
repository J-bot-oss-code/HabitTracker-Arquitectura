using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using WebApi.Middlewares;
using Xunit;

namespace WebApi.Tests;

public sealed class GlobalExceptionMiddlewareTests
{
    [Fact]
    public async Task NoExponeElMensajeInternoDeUnaExcepcionDeOperacion()
    {
        const string detalleInterno = "La cuenta privada user@example.com no está activa.";
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new InvalidOperationException(detalleInterno),
            NullLogger<GlobalExceptionMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = await JsonDocument.ParseAsync(context.Response.Body);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("La solicitud no pudo completarse.", body.RootElement.GetProperty("detail").GetString());
        Assert.DoesNotContain(detalleInterno, body.RootElement.GetRawText(), StringComparison.Ordinal);
    }
}
