using DocumentManager.Application.DTOs;
using DocumentManager.Application.Interfaces;
using HabitTracker.Application.DTOs;
using HabitTracker.Application.Interfaces;
using HabitTracker.Application.Services;
using HabitTracker.Domain.Entities;
using Moq;
using Permissions.Application.DTOs;
using Permissions.Application.Interfaces;
using WebApi.Adapters;

namespace AccessControl.Application.Tests;

/// <summary>
/// Los adaptadores del Host que conectan cada módulo con la descarga y la baja de Control de Acceso (RD-03): cada uno solo
/// llama al servicio de Application de su módulo con la identidad correcta.
/// </summary>
public class ParticipantesDeDatosTests
{
    private static readonly Guid UsuarioId = Guid.NewGuid();
    private const string Correo = "persona@ejemplo.com";

    // ---------------- Hábitos ----------------

    private static (ParticipanteHabitos Adaptador, Mock<IHabitoRepository> Repo) Habitos()
    {
        var repo = new Mock<IHabitoRepository>();
        var servicio = new HabitoService(repo.Object, Mock.Of<IUnitOfWork>(), Mock.Of<IPermisoService>(), Mock.Of<IMetaRepository>());
        return (new ParticipanteHabitos(servicio), repo);
    }

    [Fact]
    public async Task RD_03_LosHabitosSeEliminanPorElIdentificadorDelUsuario_NuncaPorElCorreo()
    {
        var (adaptador, repo) = Habitos();
        repo.Setup(r => r.EliminarDeUsuarioAsync(UsuarioId, It.IsAny<CancellationToken>())).ReturnsAsync(2);

        await adaptador.EliminarAsync(UsuarioId, Correo);

        repo.Verify(r => r.EliminarDeUsuarioAsync(UsuarioId, It.IsAny<CancellationToken>()), Times.Once);
        repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RF_NEG_01_LaDescargaDeHabitosTraeSusRegistrosDeAvance()
    {
        var (adaptador, repo) = Habitos();
        var habito = new Habito(UsuarioId, "Beber agua", Periodicidad.Diaria, 2m, "litros", Guid.NewGuid());
        repo.Setup(r => r.GetByUsuarioIdAsync(UsuarioId, It.IsAny<CancellationToken>())).ReturnsAsync([habito]);

        var datos = await adaptador.ExportarAsync(UsuarioId, Correo);

        var lista = Assert.IsAssignableFrom<IReadOnlyList<HabitoDto>>(datos);
        Assert.Equal("Beber agua", Assert.Single(lista).Nombre);
        Assert.Equal("habitos", adaptador.Seccion);
    }

    [Fact]
    public async Task RD_06_BorrarLosHabitosDeUnaIdentidadVaciaSeRechaza()
    {
        var (adaptador, repo) = Habitos();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => adaptador.EliminarAsync(Guid.Empty, Correo));

        repo.VerifyNoOtherCalls();
    }

    // ---------------- Permisos ----------------

    [Fact]
    public async Task RD_03_LasSolicitudesSeEliminanPorElCorreoDelSolicitante()
    {
        var servicio = new Mock<ISolicitudPermisoService>();
        var adaptador = new ParticipantePermisos(servicio.Object);

        await adaptador.EliminarAsync(UsuarioId, Correo);

        servicio.Verify(s => s.EliminarDeSolicitanteAsync(Correo, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("permisos", adaptador.Seccion);
    }

    [Fact]
    public async Task RF_GP_02_LaDescargaDePermisosSoloPideLasSolicitudesPropiasIncluidasLasResueltas()
    {
        var servicio = new Mock<ISolicitudPermisoService>();
        IReadOnlyList<SolicitudPermisoDto> propias =
            [new(Guid.NewGuid(), Correo, "Acceso a reportes", "Aprobada", "admin@ejemplo.com", DateTime.UtcNow, DateTime.UtcNow, null)];
        servicio.Setup(s => s.ConsultarAsync(
                It.Is<ObtenerSolicitudesQuery>(q => q.UsuarioConsulta == Correo && q.RolUsuario == "Estandar" && !q.SoloPendientes),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(propias);

        var datos = await new ParticipantePermisos(servicio.Object).ExportarAsync(UsuarioId, Correo);

        Assert.Same(propias, datos);   // nunca se consulta como administrador: no se mezclan solicitudes ajenas
    }

    // ---------------- Documentos ----------------

    [Fact]
    public async Task RD_03_LosDocumentosSePurganPorElIdentificadorDelUsuario()
    {
        var servicio = new Mock<IDocumentoService>();
        var adaptador = new ParticipanteDocumentos(servicio.Object);

        await adaptador.EliminarAsync(UsuarioId, Correo);

        servicio.Verify(s => s.PurgarDeUsuarioAsync(UsuarioId, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("documentos", adaptador.Seccion);
    }

    [Fact]
    public async Task RF_DOC_02_LaDescargaDeDocumentosListaSoloLosPropios_NuncaComoAdministrador()
    {
        var servicio = new Mock<IDocumentoService>();
        IReadOnlyList<DocumentoDto> propios = [new(Guid.NewGuid(), "informe.pdf", "application/pdf", 10, DateTime.UtcNow, UsuarioId, Correo)];
        servicio.Setup(s => s.ListarAsync(UsuarioId, false, It.IsAny<CancellationToken>())).ReturnsAsync(propios);

        var datos = await new ParticipanteDocumentos(servicio.Object).ExportarAsync(UsuarioId, Correo);

        Assert.Same(propios, datos);
        servicio.Verify(s => s.ListarAsync(UsuarioId, true, It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------- Secciones ----------------

    [Fact]
    public void RD_03_CadaModuloTieneUnaSeccionDistinta_ParaQueNoSePisenEnLaDescarga()
    {
        var secciones = new[]
        {
            Habitos().Adaptador.Seccion,
            new ParticipantePermisos(Mock.Of<ISolicitudPermisoService>()).Seccion,
            new ParticipanteDocumentos(Mock.Of<IDocumentoService>()).Seccion,
        };

        Assert.Equal(secciones.Length, secciones.Distinct().Count());
    }
}
