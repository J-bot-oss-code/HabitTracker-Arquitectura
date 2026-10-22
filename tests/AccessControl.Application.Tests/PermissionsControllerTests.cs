using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Permissions.Application.Interfaces;
using WebApi.Controllers;

namespace AccessControl.Application.Tests;

public sealed class PermissionsControllerTests
{
    [Fact]
    public async Task ConsultarPermisoEfectivo_PermisoAplicado_DevuelveOk()
    {
        // Arrange
        const string email = "persona@ejemplo.com";
        const string permiso = "Acceso reportes";
        var service = new Mock<ISolicitudPermisoService>();
        service
            .Setup(s => s.TienePermisoEfectivoAsync(email, permiso, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var controller = CrearController(service.Object, email);

        // Act
        var resultado = await controller.ConsultarPermisoEfectivo(permiso);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        service.Verify(s => s.TienePermisoEfectivoAsync(email, permiso, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsultarPermisoEfectivo_PermisoNoAplicado_DevuelveForbid()
    {
        // Arrange
        const string email = "persona@ejemplo.com";
        const string permiso = "Acceso reportes";
        var service = new Mock<ISolicitudPermisoService>();
        service
            .Setup(s => s.TienePermisoEfectivoAsync(email, permiso, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var controller = CrearController(service.Object, email);
        var authorization = Assert.Single(
            typeof(PermissionsController)
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>());
        Assert.Null(authorization.Roles);

        // Act
        var resultado = await controller.ConsultarPermisoEfectivo(permiso);

        // Assert
        Assert.IsType<ForbidResult>(resultado);
        service.Verify(s => s.TienePermisoEfectivoAsync(email, permiso, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ConsultarPermisoEfectivo_RutaValidaParametroConLimiteDe256Caracteres()
    {
        // Arrange
        var metodo = typeof(PermissionsController).GetMethod(
            nameof(PermissionsController.ConsultarPermisoEfectivo))!;
        var ruta = Assert.Single(
            metodo.GetCustomAttributes(typeof(HttpGetAttribute), inherit: true)
                .Cast<HttpGetAttribute>());
        var parametro = Assert.Single(metodo.GetParameters());
        var requerido = parametro.GetCustomAttribute<RequiredAttribute>();
        var longitud = parametro.GetCustomAttribute<StringLengthAttribute>();

        // Act
        var valorValido = longitud!.IsValid(new string('x', 256));
        var valorExcedido = longitud.IsValid(new string('x', 257));

        // Assert
        Assert.Equal("effective/{permiso}", ruta.Template);
        Assert.NotNull(requerido);
        Assert.True(valorValido);
        Assert.False(valorExcedido);
    }

    private static PermissionsController CrearController(ISolicitudPermisoService service, string email)
    {
        var controller = new PermissionsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.Email, email)],
                        authenticationType: "tests"))
                }
            }
        };

        return controller;
    }
}
