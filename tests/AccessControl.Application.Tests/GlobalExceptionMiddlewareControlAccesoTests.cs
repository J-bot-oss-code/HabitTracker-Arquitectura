using System.Text.Json;
using AccessControl.Domain.Excepciones;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using WebApi.Middlewares;

namespace AccessControl.Application.Tests;

/// <summary>Cómo traduce el middleware global los rechazos de Control de Acceso (RD-08): estado, código estable y mensaje genérico.</summary>
public class GlobalExceptionMiddlewareControlAccesoTests
{
    public static TheoryData<Exception, int, string?> Casos => new()
    {
        { new CuentaNoActivaException(), 400, "cuenta_no_activa" },
        { new CuentaDesactivadaException(), 400, "cuenta_desactivada" },
        { new CuentaBloqueadaException(), 400, "cuenta_bloqueada" },
        { new CorreoYaRegistradoException(), 400, "correo_ya_registrado" },
        { new CodigoInvalidoException(), 400, "codigo_invalido" },
        { new ClaveActualIncorrectaException(), 400, "clave_actual_incorrecta" },
        { new UltimoAdministradorException(), 400, "ultimo_administrador" },
        { new ConsentimientoNoValidoException(), 400, "consentimiento_no_valido" },
        { new ContrasenaNoValidaException(), 400, "contrasena_no_valida" },
        { new ContrasenaFiltradaException(), 400, "contrasena_filtrada" },
        { new OperacionNoPermitidaException(), 403, "operacion_no_permitida" },
        { new CredencialesInvalidasException(), 401, null },
        { new SesionInvalidaException(), 401, "sesion_invalida" },
        { new TokenExternoInvalidoException(), 401, "token_externo_invalido" },
        { new VinculacionRequeridaException(), 409, "vinculacion_requerida" },
        { new ProveedorExternoNoDisponibleException(), 503, "proveedor_no_disponible" },
        { new ConsentimientoRequeridoException(), 400, "consentimiento_requerido" },
        { new ProveedorYaVinculadoException(), 400, "proveedor_ya_vinculado" },
        { new SinContrasenaException(), 400, "sin_contrasena" },
        { new UltimoMetodoAccesoException(), 400, "ultimo_metodo_acceso" },
        { new ConfirmacionIncorrectaException(), 400, "confirmacion_incorrecta" },
    };

    private static async Task<(DefaultHttpContext Contexto, JsonElement Cuerpo, string Texto)> Ejecutar(Exception excepcion)
    {
        var contexto = new DefaultHttpContext();
        contexto.Response.Body = new MemoryStream();
        var middleware = new GlobalExceptionMiddleware(_ => throw excepcion, NullLogger<GlobalExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(contexto);

        contexto.Response.Body.Position = 0;
        var texto = await new StreamReader(contexto.Response.Body).ReadToEndAsync();
        return (contexto, JsonDocument.Parse(texto).RootElement.Clone(), texto);
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public async Task RD_08_CadaRechazoSeTraduceAProblemDetailsConSuCodigo(Exception excepcion, int estado, string? codigo)
    {
        var (contexto, cuerpo, texto) = await Ejecutar(excepcion);

        Assert.Equal(estado, contexto.Response.StatusCode);
        Assert.Equal("application/problem+json", contexto.Response.ContentType);
        Assert.Equal(estado, cuerpo.GetProperty("status").GetInt32());

        if (codigo is null)
        {
            Assert.False(cuerpo.TryGetProperty("codigo", out _));
        }
        else
        {
            Assert.Equal(codigo, cuerpo.GetProperty("codigo").GetString());
        }

        // Nada interno hacia el cliente: ni el tipo de la excepción ni rastros de pila.
        Assert.DoesNotContain("Exception", texto);
        Assert.DoesNotContain("   at ", texto);
    }

    [Fact]
    public async Task RF_CA_03_CredencialesInvalidasNoDiceSiFalloElCorreoOLaClave()
    {
        var (_, cuerpo, _) = await Ejecutar(new CredencialesInvalidasException());

        Assert.Equal("Correo o contraseña incorrectos.", cuerpo.GetProperty("detail").GetString());
    }
}
