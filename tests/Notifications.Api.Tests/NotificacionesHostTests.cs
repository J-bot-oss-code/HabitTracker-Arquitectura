using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using AccessControl.Application.Interfaces;
using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Notifications.Application.DTOs;
using Notifications.Application.Exceptions;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using WebApi.Adapters;
using WebApi.Controllers;
using WebApi.Middlewares;

namespace Notifications.Api.Tests;

/// <summary>Controller, adaptadores y errores de Notificaciones en el Host, con dobles (RD-12): sin API ni base de datos.</summary>
public class NotificacionesHostTests
{
    private static readonly Guid Ana = Guid.NewGuid();

    private static (NotificationsController Controlador, Mock<INotificacionService> Servicio) Crear(Guid? usuario = null, string? sub = null)
    {
        var servicio = new Mock<INotificacionService>();
        var identidad = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, sub ?? (usuario ?? Ana).ToString())], "Bearer");
        var controlador = new NotificationsController(servicio.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identidad) } }
        };
        return (controlador, servicio);
    }

    // ---------- Controller ----------

    [Fact]
    [Trait("Requirement", "RD-06")]
    public void RD_06_ElControllerExigeSesion_YNingunaAccionLaRelaja()
    {
        Assert.Single(typeof(NotificationsController).GetCustomAttributes(typeof(AuthorizeAttribute), false));
        Assert.DoesNotContain(typeof(NotificationsController).GetMethods(), m => m.GetCustomAttributes(typeof(AllowAnonymousAttribute), false).Length > 0);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-04")]
    public async Task RF_NOT_04_ListarUsaLaIdentidadDelToken_ConElFiltroDeNoLeidas()
    {
        var (c, servicio) = Crear();
        NotificacionDto[] lista = [new(Guid.NewGuid(), TipoNotificacion.LogroObtenido, "Primer avance", "Lo lograste.", DateTime.UtcNow, false, null)];
        servicio.Setup(s => s.ListarAsync(Ana, true, It.IsAny<CancellationToken>())).ReturnsAsync(lista);

        var resultado = await c.Listar(soloNoLeidas: true, CancellationToken.None);

        Assert.Same(lista, Assert.IsType<OkObjectResult>(resultado).Value);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-05")]
    public async Task RF_NOT_05_MarcarLeidaUsaLaIdentidadDelToken_NuncaUnaDelCliente()
    {
        var (c, servicio) = Crear();
        var id = Guid.NewGuid();

        Assert.IsType<NoContentResult>(await c.MarcarLeida(id, CancellationToken.None));

        servicio.Verify(s => s.MarcarLeidaAsync(Ana, id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_UnTokenSinIdentidadValida_NoEjecutaNada()
    {
        var (c, servicio) = Crear(sub: "no-es-un-guid");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => c.Listar(false, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => c.MarcarLeida(Guid.NewGuid(), CancellationToken.None));

        servicio.VerifyNoOtherCalls();
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-07")]
    public async Task RF_NOT_07_GuardarPreferenciasDelegaConLaIdentidadDelToken()
    {
        var (c, servicio) = Crear();
        servicio.Setup(s => s.GuardarPreferenciasAsync(Ana, false, It.IsAny<CancellationToken>())).ReturnsAsync(new PreferenciasNotificacionesDto(false));

        var resultado = await c.GuardarPreferencias(new GuardarPreferenciasRequest(false), CancellationToken.None);

        Assert.Equal(new PreferenciasNotificacionesDto(false), Assert.IsType<OkObjectResult>(resultado).Value);
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_UnCuerpoSinCorreoActivo_EsUnaPeticionInvalida_NoUnFalseSilencioso()
    {
        var parametro = typeof(GuardarPreferenciasRequest).GetConstructors().Single().GetParameters().Single();
        var atributos = parametro.GetCustomAttributes<ValidationAttribute>();
        var contexto = new ValidationContext(new object()) { MemberName = parametro.Name };

        Assert.False(Validator.TryValidateValue(null!, contexto, [], atributos));
        Assert.True(Validator.TryValidateValue(false, contexto, [], atributos));
    }

    // ---------- Errores con código estable ----------

    [Fact]
    [Trait("Requirement", "RD-08")]
    public async Task RD_08_NotificacionNoEncontrada_Es404ConCodigoYSinDetallesInternos()
    {
        var contexto = new DefaultHttpContext();
        contexto.Response.Body = new MemoryStream();

        await new GlobalExceptionMiddleware(_ => throw new NotificacionNoEncontradaException(), NullLogger<GlobalExceptionMiddleware>.Instance).InvokeAsync(contexto);

        contexto.Response.Body.Position = 0;
        var cuerpo = (await JsonDocument.ParseAsync(contexto.Response.Body)).RootElement;
        Assert.Equal(404, contexto.Response.StatusCode);
        Assert.Equal("notificacion_no_encontrada", cuerpo.GetProperty("codigo").GetString());
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-05")]
    public async Task RF_NOT_05_MarcarLaDeOtraPersona_SeTraduceA403()
    {
        var contexto = new DefaultHttpContext();
        contexto.Response.Body = new MemoryStream();
        contexto.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Ana.ToString())], "Bearer"));

        await new GlobalExceptionMiddleware(_ => throw new UnauthorizedAccessException("Solo puedes marcar tus propias notificaciones."), NullLogger<GlobalExceptionMiddleware>.Instance).InvokeAsync(contexto);

        Assert.Equal(403, contexto.Response.StatusCode);
    }

    // ---------- Adaptadores ----------

    private static Usuario Persona(string nombre, string correo, Rol rol, bool activo)
    {
        var u = new Usuario(nombre, new Email(correo), "hash", rol);
        if (activo)
        {
            u.Activar();
        }

        return u;
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-01")]
    public async Task RF_NOT_01_ElDirectorioDevuelveLosAdministradoresActivosConSuCorreo()
    {
        var admin = Persona("Admin", "admin@ejemplo.com", Rol.Administrador, activo: true);
        var repositorio = new Mock<IUsuarioRepository>();
        repositorio.Setup(r => r.ListarAdministradoresActivosAsync()).ReturnsAsync([admin]);

        var lista = await new DirectorioUsuariosAdapter(repositorio.Object).ListarAdministradoresActivosAsync();

        var destinatario = Assert.Single(lista);
        Assert.Equal(admin.Id, destinatario.UsuarioId);
        Assert.Equal("admin@ejemplo.com", destinatario.Correo);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [Trait("Requirement", "RF-NOT-02")]
    public async Task RF_NOT_02_ElDirectorioEncuentraPorCorreoSoloUnaCuentaActiva(bool activa, bool seEncuentra)
    {
        var ana = Persona("Ana", "ana@ejemplo.com", Rol.Estandar, activa);
        var repositorio = new Mock<IUsuarioRepository>();
        repositorio.Setup(r => r.GetByCorreoAsync(It.IsAny<Email>())).ReturnsAsync(ana);

        var resultado = await new DirectorioUsuariosAdapter(repositorio.Object).ObtenerPorCorreoAsync("ana@ejemplo.com");

        Assert.Equal(seEncuentra, resultado is not null);
        Assert.Equal(seEncuentra ? ana.Id : null, resultado?.UsuarioId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-es-un-correo")]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_UnCorreoVacioOMalFormado_NoEsDeNadie(string correo)
    {
        var repositorio = new Mock<IUsuarioRepository>();

        Assert.Null(await new DirectorioUsuariosAdapter(repositorio.Object).ObtenerPorCorreoAsync(correo));

        repositorio.Verify(r => r.GetByCorreoAsync(It.IsAny<Email>()), Times.Never);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-08")]
    public async Task RF_NOT_08_ElAdaptadorDeLaColaSoloEncola()
    {
        var cola = new Mock<IEmailQueue>();

        await new ColaCorreosAdapter(cola.Object).EncolarAsync("ana@ejemplo.com", "Asunto", "Titulo", "Mensaje");

        cola.Verify(c => c.EncolarCorreoAsync("ana@ejemplo.com", "Asunto", "Titulo", "Mensaje"), Times.Once);
        cola.VerifyNoOtherCalls();
    }

    [Fact]
    [Trait("Requirement", "RF-CA-19")]
    public async Task RF_CA_19_ElParticipanteExportaYBorraLosDatosDeNotificaciones()
    {
        var servicio = new Mock<INotificacionService>();
        servicio.Setup(s => s.ObtenerPreferenciasAsync(Ana, It.IsAny<CancellationToken>())).ReturnsAsync(new PreferenciasNotificacionesDto(true));
        servicio.Setup(s => s.ListarAsync(Ana, false, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var participante = new ParticipanteNotificaciones(servicio.Object);

        var exportado = await participante.ExportarAsync(Ana, "ana@ejemplo.com");
        await participante.EliminarAsync(Ana, "ana@ejemplo.com");

        Assert.Equal("notificaciones", participante.Seccion);
        Assert.NotNull(exportado);
        servicio.Verify(s => s.EliminarDeUsuarioAsync(Ana, It.IsAny<CancellationToken>()), Times.Once);
    }
}
