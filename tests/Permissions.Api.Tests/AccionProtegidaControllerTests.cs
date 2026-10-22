using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Permissions.Application.DTOs;
using Permissions.Application.Interfaces;
using WebApi.Controllers;

namespace Permissions.Api.Tests;

public sealed class AccionProtegidaControllerTests
{
    private const string Solicitante = "persona@ejemplo.com";
    private const string Permiso = "Consultar mi historial de permisos";
    private static readonly DateTime FechaUtc = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Requirement", "RF-GP-05")]
    public async Task RF_GP_05_Controlador_Devuelve403SiElPermisoNoEstaAplicado()
    {
        // Arrange
        var servicio = new FakeSolicitudPermisoService(permisoAplicado: false);
        var controlador = CrearControlador(servicio);

        // Act
        var resultado = await controlador.EjecutarAccionProtegida(Permiso);

        // Assert
        Assert.IsType<ForbidResult>(resultado);
        Assert.Equal(Solicitante, servicio.UltimoSolicitante);
        Assert.Equal(Permiso, servicio.UltimoPermiso);
    }

    [Fact]
    [Trait("Requirement", "RF-GP-05")]
    public async Task RF_GP_05_Controlador_DevuelveElHistorialAlSolicitanteAutorizado()
    {
        // Arrange
        var servicio = new FakeSolicitudPermisoService(permisoAplicado: true);
        var controlador = CrearControlador(servicio);

        // Act
        var resultado = await controlador.EjecutarAccionProtegida(Permiso);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(resultado);
        var historial = Assert.IsAssignableFrom<IEnumerable<SolicitudPermisoDto>>(ok.Value);
        Assert.Single(historial);
        Assert.Equal(Solicitante, servicio.UltimoSolicitante);
        Assert.Equal(Permiso, servicio.UltimoPermiso);
    }

    private static PermissionsController CrearControlador(ISolicitudPermisoService servicio)
    {
        var controlador = new PermissionsController(servicio)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.Email, Solicitante)], "pruebas"))
                }
            }
        };

        return controlador;
    }

    private sealed class FakeSolicitudPermisoService(bool permisoAplicado) : ISolicitudPermisoService
    {
        public string? UltimoSolicitante { get; private set; }
        public string? UltimoPermiso { get; private set; }

        public Task<IReadOnlyList<SolicitudPermisoDto>> EjecutarAccionProtegidaAsync(
            string solicitante,
            string permisoSolicitado,
            CancellationToken cancellationToken = default)
        {
            UltimoSolicitante = solicitante;
            UltimoPermiso = permisoSolicitado;
            if (!permisoAplicado)
            {
                throw new UnauthorizedAccessException();
            }

            IReadOnlyList<SolicitudPermisoDto> historial =
            [new SolicitudPermisoDto(Guid.NewGuid(), solicitante, permisoSolicitado, "Aplicada", "admin@ejemplo.com", FechaUtc, FechaUtc, null)];
            return Task.FromResult(historial);
        }

        public Task<Guid> CrearAsync(CrearSolicitudPermisoCommand command, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SolicitudPermisoDto>> ConsultarAsync(ObtenerSolicitudesQuery query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SolicitudPermisoDto> ObtenerPorIdAsync(Guid id, string usuarioConsulta, string rolUsuario, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AprobarAsync(AprobarSolicitudCommand command, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task RechazarAsync(RechazarSolicitudCommand command, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
