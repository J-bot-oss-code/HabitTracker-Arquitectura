using System.Reflection;
using System.Security.Claims;
using DocumentManager.Application.DTOs;
using DocumentManager.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WebApi.Controllers;

namespace DocumentManager.Api.Tests;

public class DocumentsControllerTests
{
    private static readonly Guid UsuarioId = Guid.NewGuid();

    [Fact]
    [Trait("Requirement", "RD-06")]
    public void RD_06_Controlador_ExigeSesionEnTodasLasOperaciones()
    {
        var authorize = typeof(DocumentsController).GetCustomAttribute<AuthorizeAttribute>();
        var anonimos = typeof(DocumentsController).GetMethods()
            .Where(m => m.GetCustomAttribute<AllowAnonymousAttribute>() is not null);

        Assert.NotNull(authorize);
        Assert.Empty(anonimos);
    }

    [Theory]
    [InlineData("Estandar", false)]
    [InlineData("Administrador", true)]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_Listar_LaIdentidadYElRolSalenDeLosClaims(string rol, bool esperadoAdmin)
    {
        var servicio = new ServicioFalso();
        var controlador = Crear(servicio, ClaimsDe(UsuarioId, rol));

        var resultado = await controlador.Listar(CancellationToken.None);

        Assert.IsType<OkObjectResult>(resultado);
        Assert.Equal(UsuarioId, servicio.UltimoUsuarioId);
        Assert.Equal(esperadoAdmin, servicio.UltimoEsAdministrador);
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_Operacion_SinClaimDeUsuario_LanzaNoAutorizado()
    {
        var servicio = new ServicioFalso();
        var sinSub = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, "a@b.com")], "test"));
        var controlador = Crear(servicio, sinSub);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controlador.Listar(CancellationToken.None));
        Assert.Null(servicio.UltimoUsuarioId);
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_Operacion_ConSubQueNoEsGuid_LanzaNoAutorizado()
    {
        var servicio = new ServicioFalso();
        var raro = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "no-es-guid"), new Claim(ClaimTypes.Email, "a@b.com")], "test"));
        var controlador = Crear(servicio, raro);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            controlador.Eliminar(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-01")]
    public async Task RF_DOC_01_Subir_SinArchivo_PasaContenidoNuloAlServicio()
    {
        var servicio = new ServicioFalso();
        var controlador = Crear(servicio, ClaimsDe(UsuarioId, "Estandar"));

        await controlador.Subir(null, CancellationToken.None);

        Assert.NotNull(servicio.UltimoComando);
        Assert.Null(servicio.UltimoComando.Contenido);
        Assert.Equal(UsuarioId, servicio.UltimoComando.UsuarioId);
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-01")]
    public async Task RF_DOC_01_Subir_PasaNombreCorreoYContenidoDelArchivoAlServicio()
    {
        var servicio = new ServicioFalso();
        var controlador = Crear(servicio, ClaimsDe(UsuarioId, "Estandar"));
        var formFile = new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "archivo", "informe.pdf");

        await controlador.Subir(formFile, CancellationToken.None);

        Assert.Equal("informe.pdf", servicio.UltimoComando!.NombreArchivo);
        Assert.Equal("ana@ejemplo.com", servicio.UltimoComando.CorreoUsuario);
        Assert.NotNull(servicio.UltimoComando.Contenido);
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_Subir_TieneLimiteDeTamanoDelCuerpo()
    {
        var metodo = typeof(DocumentsController).GetMethod(nameof(DocumentsController.Subir))!;

        Assert.NotNull(metodo.GetCustomAttribute<RequestSizeLimitAttribute>());
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_Administrador_LlegaComoAdministradorEnObtenerDescargarYEliminar()
    {
        var servicio = new ServicioFalso();
        var controlador = Crear(servicio, ClaimsDe(UsuarioId, "Administrador"));

        await controlador.Obtener(Guid.NewGuid(), CancellationToken.None);
        Assert.True(servicio.UltimoEsAdministrador);

        servicio.UltimoEsAdministrador = null;
        await controlador.Descargar(Guid.NewGuid(), CancellationToken.None);
        Assert.True(servicio.UltimoEsAdministrador);

        servicio.UltimoEsAdministrador = null;
        await controlador.Eliminar(Guid.NewGuid(), CancellationToken.None);
        Assert.True(servicio.UltimoEsAdministrador);
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_Estandar_NuncaLlegaComoAdministrador()
    {
        var servicio = new ServicioFalso();
        var controlador = Crear(servicio, ClaimsDe(UsuarioId, "Estandar"));

        await controlador.Obtener(Guid.NewGuid(), CancellationToken.None);
        await controlador.Descargar(Guid.NewGuid(), CancellationToken.None);
        await controlador.Eliminar(Guid.NewGuid(), CancellationToken.None);

        Assert.False(servicio.UltimoEsAdministrador);
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-01")]
    public async Task RF_DOC_01_Subir_DevuelveCreatedConLosMetadatos()
    {
        var servicio = new ServicioFalso();
        var controlador = Crear(servicio, ClaimsDe(UsuarioId, "Estandar"));

        var resultado = await controlador.Subir(null, CancellationToken.None);

        var creado = Assert.IsType<CreatedAtActionResult>(resultado);
        Assert.Equal(StatusCodes.Status201Created, creado.StatusCode);
        Assert.Equal(nameof(DocumentsController.Obtener), creado.ActionName);
        Assert.IsType<DocumentoDto>(creado.Value);
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-03")]
    public async Task RF_DOC_03_Descargar_DevuelveElArchivoComoAdjuntoYConNosniff()
    {
        var servicio = new ServicioFalso();
        var controlador = Crear(servicio, ClaimsDe(UsuarioId, "Estandar"));

        var resultado = await controlador.Descargar(Guid.NewGuid(), CancellationToken.None);

        var archivo = Assert.IsType<FileStreamResult>(resultado);
        Assert.Equal("application/pdf", archivo.ContentType);
        Assert.Equal("informe.pdf", archivo.FileDownloadName);
        Assert.Equal("nosniff", controlador.Response.Headers.XContentTypeOptions.ToString());
        Assert.Equal("no-store", controlador.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-04")]
    public async Task RF_DOC_04_Eliminar_DevuelveNoContent()
    {
        var servicio = new ServicioFalso();
        var controlador = Crear(servicio, ClaimsDe(UsuarioId, "Estandar"));
        var id = Guid.NewGuid();

        var resultado = await controlador.Eliminar(id, CancellationToken.None);

        Assert.IsType<NoContentResult>(resultado);
        Assert.Equal(id, servicio.UltimoIdEliminado);
    }

    private static ClaimsPrincipal ClaimsDe(Guid id, string rol) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, id.ToString()),
                new Claim(ClaimTypes.Email, "ana@ejemplo.com"),
                new Claim(ClaimTypes.Role, rol)
            ],
            authenticationType: "test"));

    private static DocumentsController Crear(IDocumentoService servicio, ClaimsPrincipal usuario) =>
        new(servicio)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = usuario } }
        };

    private sealed class ServicioFalso : IDocumentoService
    {
        public Guid? UltimoUsuarioId { get; private set; }
        public bool? UltimoEsAdministrador { get; set; }
        public SubirDocumentoCommand? UltimoComando { get; private set; }
        public Guid? UltimoIdEliminado { get; private set; }

        private static DocumentoDto Dto() =>
            new(Guid.NewGuid(), "informe.pdf", "application/pdf", 4, DateTime.UtcNow, Guid.NewGuid(), "ana@ejemplo.com");

        public Task<DocumentoDto> SubirAsync(SubirDocumentoCommand command, CancellationToken cancellationToken = default)
        {
            UltimoComando = command;
            return Task.FromResult(Dto());
        }

        public Task<IReadOnlyList<DocumentoDto>> ListarAsync(Guid usuarioId, bool esAdministrador, CancellationToken cancellationToken = default)
        {
            UltimoUsuarioId = usuarioId;
            UltimoEsAdministrador = esAdministrador;
            return Task.FromResult<IReadOnlyList<DocumentoDto>>([]);
        }

        public Task<DocumentoDto> ObtenerAsync(Guid id, Guid usuarioId, bool esAdministrador, CancellationToken cancellationToken = default)
        {
            UltimoEsAdministrador = esAdministrador;
            return Task.FromResult(Dto());
        }

        public Task<ArchivoDescargaDto> DescargarAsync(Guid id, Guid usuarioId, bool esAdministrador, CancellationToken cancellationToken = default)
        {
            UltimoEsAdministrador = esAdministrador;
            return Task.FromResult(new ArchivoDescargaDto(new MemoryStream([1, 2]), "application/pdf", "informe.pdf"));
        }

        public Task EliminarAsync(Guid id, Guid usuarioId, bool esAdministrador, CancellationToken cancellationToken = default)
        {
            UltimoIdEliminado = id;
            UltimoEsAdministrador = esAdministrador;
            return Task.CompletedTask;
        }
    }
}
