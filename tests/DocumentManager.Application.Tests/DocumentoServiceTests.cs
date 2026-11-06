using DocumentManager.Application.DTOs;
using DocumentManager.Application.Exceptions;
using DocumentManager.Application.Interfaces;
using DocumentManager.Application.Services;
using DocumentManager.Domain.Entities;

namespace DocumentManager.Application.Tests;

public class DocumentoServiceTests
{
    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly Guid Beto = Guid.NewGuid();
    private static readonly Guid Admin = Guid.NewGuid();

    private static (DocumentoService Servicio, RepositorioEnMemoria Repo, AlmacenEnMemoria Almacen) Crear()
    {
        var repo = new RepositorioEnMemoria();
        var almacen = new AlmacenEnMemoria();
        return (new DocumentoService(repo, almacen), repo, almacen);
    }

    private static SubirDocumentoCommand Subida(Guid usuario, string nombre = "informe.pdf", byte[]? bytes = null)
    {
        bytes ??= [1, 2, 3, 4];
        return new SubirDocumentoCommand(usuario, "ana@ejemplo.com", nombre, new MemoryStream(bytes));
    }

    // ---------- RF-DOC-01 ----------

    [Fact]
    [Trait("Requirement", "RF-DOC-01")]
    public async Task RF_DOC_01_Subir_GuardaMetadatosYArchivo()
    {
        var (servicio, repo, almacen) = Crear();

        var dto = await servicio.SubirAsync(Subida(Ana));

        Assert.Equal("informe.pdf", dto.Nombre);
        Assert.Equal("application/pdf", dto.Tipo);
        Assert.Equal(4, dto.TamanoBytes);
        Assert.Equal(Ana, dto.UsuarioId);
        Assert.Equal(DateTimeKind.Utc, dto.FechaSubidaUtc.Kind);
        Assert.Single(repo.Todos);
        Assert.Single(almacen.Archivos);
    }

    [Theory]
    [InlineData("..\\..\\secreto.txt")]
    [InlineData("../../secreto.txt")]
    [InlineData("carpeta/sub/secreto.txt")]
    [Trait("Requirement", "RF-DOC-01")]
    public async Task RF_DOC_01_Subir_NombreConRuta_SeGuardaSoloElNombreDelArchivo(string nombreConRuta)
    {
        var (servicio, _, _) = Crear();

        var dto = await servicio.SubirAsync(Subida(Ana, nombreConRuta));

        Assert.Equal("secreto.txt", dto.Nombre);
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_Subir_TamanoGuardadoEsElReal_NoUnoDeclarado()
    {
        var (servicio, repo, _) = Crear();

        var dto = await servicio.SubirAsync(Subida(Ana, bytes: new byte[1234]));

        Assert.Equal(1234, dto.TamanoBytes);
        Assert.Equal(1234, repo.Todos.Single().TamanoBytes);
    }

    [Theory]
    [InlineData("nota.txt", "text/plain")]
    [InlineData("INFORME.PDF", "application/pdf")]
    [InlineData("foto.jpeg", "image/jpeg")]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_Subir_TipoSeDerivaDeLaExtension_NoDelCliente(string nombre, string tipoEsperado)
    {
        var (servicio, _, _) = Crear();

        var dto = await servicio.SubirAsync(Subida(Ana, nombre));

        Assert.Equal(tipoEsperado, dto.Tipo);
    }

    [Fact]
    [Trait("Requirement", "RD-08")]
    public void RD_08_DocumentoDto_NoExponeNombreAlmacenadoNiRutas()
    {
        var propiedades = typeof(DocumentoDto).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain("NombreAlmacenado", propiedades);
        Assert.DoesNotContain(propiedades, p => p.Contains("Ruta", StringComparison.OrdinalIgnoreCase));
    }

    // ---------- RD-07 ----------

    [Fact]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_Subir_ArchivoAusente_RechazaSinTocarAlmacenNiRepositorio()
    {
        var (servicio, repo, almacen) = Crear();
        var comando = new SubirDocumentoCommand(Ana, "ana@ejemplo.com", "a.pdf", null);

        await Assert.ThrowsAsync<ArchivoInvalidoException>(() => servicio.SubirAsync(comando));

        Assert.Empty(repo.Todos);
        Assert.Empty(almacen.Archivos);
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_Subir_ArchivoVacio_Rechaza()
    {
        var (servicio, repo, almacen) = Crear();
        var comando = new SubirDocumentoCommand(Ana, "ana@ejemplo.com", "a.pdf", new MemoryStream());

        await Assert.ThrowsAsync<ArchivoInvalidoException>(() => servicio.SubirAsync(comando));

        Assert.Empty(repo.Todos);
        Assert.Empty(almacen.Archivos);
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_Subir_SuperaTamanoMaximoReal_RechazaSinGuardar()
    {
        var (servicio, repo, almacen) = Crear();
        var enorme = new byte[DocumentoService.TamanoMaximoBytes + 1];

        await Assert.ThrowsAsync<ArchivoInvalidoException>(() => servicio.SubirAsync(Subida(Ana, bytes: enorme)));

        Assert.Empty(repo.Todos);
        Assert.Empty(almacen.Archivos);
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_Subir_NombreMasLargoQueElMaximo_RechazaControlado()
    {
        var (servicio, repo, _) = Crear();
        var nombre = new string('a', 300) + ".pdf";

        await Assert.ThrowsAsync<ArchivoInvalidoException>(() => servicio.SubirAsync(Subida(Ana, nombre)));

        Assert.Empty(repo.Todos);
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_Subir_UsuarioVacio_RechazaControlado()
    {
        var (servicio, repo, _) = Crear();

        await Assert.ThrowsAsync<ArchivoInvalidoException>(() => servicio.SubirAsync(Subida(Guid.Empty)));

        Assert.Empty(repo.Todos);
    }

    [Theory]
    [InlineData("virus.exe")]
    [InlineData("script.ps1")]
    [InlineData("sinextension")]
    [InlineData("informe.pdf.exe")]
    [InlineData("nombre\u0001raro.pdf")]
    [InlineData("   ")]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_Subir_ExtensionNoPermitida_Rechaza(string nombre)
    {
        var (servicio, repo, _) = Crear();

        await Assert.ThrowsAsync<ArchivoInvalidoException>(() => servicio.SubirAsync(Subida(Ana, nombre)));

        Assert.Empty(repo.Todos);
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-05")]
    public async Task RF_DOC_05_Subir_SiFallaElRegistro_DescartaElArchivoHuerfano()
    {
        var (_, repo, almacen) = Crear();
        repo.FallarAlAgregar = true;
        var servicio = new DocumentoService(repo, almacen);

        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.SubirAsync(Subida(Ana)));

        Assert.Empty(almacen.Archivos);
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-05")]
    public async Task RF_DOC_05_Subir_SiFallaElGuardadoDelArchivo_NoQuedaRegistroNiArchivoParcial()
    {
        var (_, repo, almacen) = Crear();
        almacen.FallarAlGuardar = true;
        var servicio = new DocumentoService(repo, almacen);

        await Assert.ThrowsAsync<IOException>(() => servicio.SubirAsync(Subida(Ana)));

        Assert.Empty(repo.Todos);
        Assert.Empty(almacen.Archivos);
    }

    // ---------- RF-DOC-02 ----------

    [Fact]
    [Trait("Requirement", "RF-DOC-02")]
    public async Task RF_DOC_02_Listar_Estandar_SoloVeLosSuyosYNoLosEliminados()
    {
        var (servicio, _, _) = Crear();
        var propio = await servicio.SubirAsync(Subida(Ana, "uno.pdf"));
        var eliminado = await servicio.SubirAsync(Subida(Ana, "dos.pdf"));
        await servicio.SubirAsync(Subida(Beto, "ajeno.pdf"));
        await servicio.EliminarAsync(eliminado.Id, Ana, esAdministrador: false);

        var lista = await servicio.ListarAsync(Ana, esAdministrador: false);

        var unico = Assert.Single(lista);
        Assert.Equal(propio.Id, unico.Id);
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-02")]
    public async Task RF_DOC_02_Listar_Administrador_VeTodosLosNoEliminados()
    {
        var (servicio, _, _) = Crear();
        await servicio.SubirAsync(Subida(Ana, "uno.pdf"));
        await servicio.SubirAsync(Subida(Beto, "dos.pdf"));
        var eliminado = await servicio.SubirAsync(Subida(Beto, "tres.pdf"));
        await servicio.EliminarAsync(eliminado.Id, Beto, esAdministrador: false);

        var lista = await servicio.ListarAsync(Admin, esAdministrador: true);

        Assert.Equal(2, lista.Count);
    }

    // ---------- RF-DOC-03 ----------

    [Fact]
    [Trait("Requirement", "RF-DOC-03")]
    public async Task RF_DOC_03_Obtener_DuenoYAdministrador_RecibenElRegistroConTodosSusDatos()
    {
        var (servicio, _, _) = Crear();
        var subido = await servicio.SubirAsync(Subida(Ana));

        var delDueno = await servicio.ObtenerAsync(subido.Id, Ana, esAdministrador: false);
        var delAdmin = await servicio.ObtenerAsync(subido.Id, Admin, esAdministrador: true);

        Assert.Equal(subido, delDueno);
        Assert.Equal(subido, delAdmin);
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-03")]
    public async Task RF_DOC_03_EliminadoYAjeno_DaNoEncontradoYNoFiltraSuExistencia()
    {
        var (servicio, _, _) = Crear();
        var dto = await servicio.SubirAsync(Subida(Ana));
        await servicio.EliminarAsync(dto.Id, Ana, esAdministrador: false);

        await Assert.ThrowsAsync<DocumentoNoEncontradoException>(() =>
            servicio.DescargarAsync(dto.Id, Beto, esAdministrador: false));
        await Assert.ThrowsAsync<DocumentoNoEncontradoException>(() =>
            servicio.EliminarAsync(dto.Id, Beto, esAdministrador: false));
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-03")]
    public async Task RF_DOC_03_Descargar_Dueno_RecibeLosMismosBytes()
    {
        var (servicio, _, _) = Crear();
        var dto = await servicio.SubirAsync(Subida(Ana, bytes: [9, 8, 7]));

        var archivo = await servicio.DescargarAsync(dto.Id, Ana, esAdministrador: false);

        using var ms = new MemoryStream();
        await archivo.Contenido.CopyToAsync(ms);
        Assert.Equal(new byte[] { 9, 8, 7 }, ms.ToArray());
        Assert.Equal("informe.pdf", archivo.NombreArchivo);
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-03")]
    public async Task RF_DOC_03_Descargar_Administrador_Puede()
    {
        var (servicio, _, _) = Crear();
        var dto = await servicio.SubirAsync(Subida(Ana));

        var archivo = await servicio.DescargarAsync(dto.Id, Admin, esAdministrador: true);

        Assert.NotNull(archivo.Contenido);
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-03")]
    public async Task RF_DOC_03_Descargar_OtroEstandar_EsRechazado()
    {
        var (servicio, _, _) = Crear();
        var dto = await servicio.SubirAsync(Subida(Ana));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            servicio.DescargarAsync(dto.Id, Beto, esAdministrador: false));
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-03")]
    public async Task RF_DOC_03_Obtener_OtroEstandar_EsRechazado()
    {
        var (servicio, _, _) = Crear();
        var dto = await servicio.SubirAsync(Subida(Ana));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            servicio.ObtenerAsync(dto.Id, Beto, esAdministrador: false));
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-03")]
    public async Task RF_DOC_03_Descargar_Inexistente_LanzaNoEncontrado()
    {
        var (servicio, _, _) = Crear();

        await Assert.ThrowsAsync<DocumentoNoEncontradoException>(() =>
            servicio.DescargarAsync(Guid.NewGuid(), Ana, esAdministrador: false));
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-03")]
    public async Task RF_DOC_03_Descargar_ArchivoFaltanteEnAlmacen_LanzaNoEncontrado()
    {
        var (servicio, _, almacen) = Crear();
        var dto = await servicio.SubirAsync(Subida(Ana));
        almacen.Archivos.Clear();

        await Assert.ThrowsAsync<DocumentoNoEncontradoException>(() =>
            servicio.DescargarAsync(dto.Id, Ana, esAdministrador: false));
    }

    // ---------- RF-DOC-04 ----------

    [Fact]
    [Trait("Requirement", "RF-DOC-04")]
    public async Task RF_DOC_04_Eliminar_Inexistente_LanzaNoEncontrado()
    {
        var (servicio, _, _) = Crear();

        await Assert.ThrowsAsync<DocumentoNoEncontradoException>(() =>
            servicio.EliminarAsync(Guid.NewGuid(), Ana, esAdministrador: false));
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-04")]
    public async Task RF_DOC_04_Eliminar_EsLogico_ElRegistroYElArchivoSiguenExistiendo()
    {
        var (servicio, repo, almacen) = Crear();
        var dto = await servicio.SubirAsync(Subida(Ana));

        await servicio.EliminarAsync(dto.Id, Ana, esAdministrador: false);

        var registro = Assert.Single(repo.Todos);
        Assert.True(registro.Eliminado);
        Assert.NotNull(registro.FechaEliminacionUtc);
        Assert.Single(almacen.Archivos);
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-04")]
    public async Task RF_DOC_04_Eliminar_OtroEstandar_EsRechazadoYNoCambiaNada()
    {
        var (servicio, repo, _) = Crear();
        var dto = await servicio.SubirAsync(Subida(Ana));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            servicio.EliminarAsync(dto.Id, Beto, esAdministrador: false));

        Assert.False(repo.Todos.Single().Eliminado);
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-04")]
    public async Task RF_DOC_04_Eliminar_Administrador_Puede()
    {
        var (servicio, repo, _) = Crear();
        var dto = await servicio.SubirAsync(Subida(Ana));

        await servicio.EliminarAsync(dto.Id, Admin, esAdministrador: true);

        Assert.True(repo.Todos.Single().Eliminado);
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-04")]
    public async Task RF_DOC_04_DocumentoEliminado_NoSeDescargaNiSeEliminaDeNuevo()
    {
        var (servicio, _, _) = Crear();
        var dto = await servicio.SubirAsync(Subida(Ana));
        await servicio.EliminarAsync(dto.Id, Ana, esAdministrador: false);

        await Assert.ThrowsAsync<DocumentoNoEncontradoException>(() =>
            servicio.DescargarAsync(dto.Id, Ana, esAdministrador: false));
        await Assert.ThrowsAsync<DocumentoNoEncontradoException>(() =>
            servicio.EliminarAsync(dto.Id, Ana, esAdministrador: false));
    }

    // ---------- Baja de cuenta: purga definitiva (RD-09) ----------

    [Fact]
    [Trait("Requirement", "RD-09")]
    public async Task RD_09_Purgar_BorraLosArchivosDelDiscoYLosRegistros_TambienLosYaEliminados()
    {
        var (servicio, repo, almacen) = Crear();
        var vigente = await servicio.SubirAsync(Subida(Ana, "vigente.pdf"));
        var eliminado = await servicio.SubirAsync(Subida(Ana, "eliminado.pdf"));
        await servicio.EliminarAsync(eliminado.Id, Ana, esAdministrador: false);   // borrado lógico (RF-DOC-04): el archivo sigue en disco
        var ajeno = await servicio.SubirAsync(Subida(Beto, "ajeno.pdf"));
        Assert.Equal(3, almacen.Archivos.Count);

        var purgados = await servicio.PurgarDeUsuarioAsync(Ana);

        Assert.Equal(2, purgados);
        Assert.DoesNotContain(repo.Todos, d => d.UsuarioId == Ana);
        Assert.Equal(ajeno.Id, Assert.Single(repo.Todos).Id);
        Assert.Single(almacen.Archivos);   // solo queda el archivo de la otra persona
        Assert.NotNull(vigente);
    }

    [Fact]
    [Trait("Requirement", "RD-09")]
    public async Task RD_09_Purgar_ReintentarloNoFalla_LoQueYaSeBorroNoSeBuscaDeNuevo()
    {
        var (servicio, _, _) = Crear();
        await servicio.SubirAsync(Subida(Ana));

        Assert.Equal(1, await servicio.PurgarDeUsuarioAsync(Ana));
        Assert.Equal(0, await servicio.PurgarDeUsuarioAsync(Ana));
    }

    [Fact]
    [Trait("Requirement", "RD-09")]
    public async Task RD_09_Purgar_SiFaltaElArchivoDelDiscoIgualSeBorraElRegistro()
    {
        var (servicio, repo, almacen) = Crear();
        await servicio.SubirAsync(Subida(Ana));
        almacen.Archivos.Clear();   // un reintento después de borrar el archivo a mitad de una pasada anterior

        Assert.Equal(1, await servicio.PurgarDeUsuarioAsync(Ana));
        Assert.Empty(repo.Todos);
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_Purgar_SinUsuarioSeRechazaYNoBorraNada()
    {
        var (servicio, repo, _) = Crear();
        await servicio.SubirAsync(Subida(Ana));

        await Assert.ThrowsAsync<ArgumentException>(() => servicio.PurgarDeUsuarioAsync(Guid.Empty));

        Assert.Single(repo.Todos);
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-04")]
    public async Task RF_DOC_04_ElBorradoNormalNoCambia_SigueSiendoLogicoYElArchivoSeConserva()
    {
        var (servicio, repo, almacen) = Crear();
        var dto = await servicio.SubirAsync(Subida(Ana));

        await servicio.EliminarAsync(dto.Id, Ana, esAdministrador: false);

        Assert.True(Assert.Single(repo.Todos).Eliminado);
        Assert.Single(almacen.Archivos);
        Assert.Equal(0, repo.Purgados);
    }

    // ---------- Dobles en memoria (sin base de datos ni disco: RD-12) ----------

    private sealed class RepositorioEnMemoria : IDocumentoRepository
    {
        public List<Documento> Todos { get; } = [];
        public bool FallarAlAgregar { get; set; }

        public Task AddAsync(Documento documento, CancellationToken cancellationToken = default)
        {
            if (FallarAlAgregar)
            {
                throw new InvalidOperationException("Fallo simulado de base de datos.");
            }

            Todos.Add(documento);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Documento documento, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<Documento?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Todos.FirstOrDefault(d => d.Id == id));

        public int Purgados { get; private set; }

        public Task<IReadOnlyList<Documento>> ListarTodosDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Documento>>(Todos.Where(d => d.UsuarioId == usuarioId).ToList());

        public Task<int> PurgarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default)
        {
            var borrados = Todos.RemoveAll(d => d.UsuarioId == usuarioId);
            Purgados += borrados;
            return Task.FromResult(borrados);
        }

        public Task<IReadOnlyList<Documento>> ListarNoEliminadosAsync(Guid? usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Documento>>(
                Todos.Where(d => !d.Eliminado && (usuarioId is null || d.UsuarioId == usuarioId)).ToList());
    }

    private sealed class AlmacenEnMemoria : IAlmacenamientoArchivos
    {
        public Dictionary<string, byte[]> Archivos { get; } = [];
        public bool FallarAlGuardar { get; set; }

        public async Task GuardarAsync(string nombreAlmacenado, Stream contenido, CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream();
            await contenido.CopyToAsync(ms, cancellationToken);
            Archivos[nombreAlmacenado] = ms.ToArray();

            if (FallarAlGuardar)
            {
                // Simula un disco que falla DESPUÉS de haber escrito parte del archivo.
                throw new IOException("Fallo simulado de disco.");
            }
        }

        public Task<Stream> AbrirLecturaAsync(string nombreAlmacenado, CancellationToken cancellationToken = default)
        {
            if (!Archivos.TryGetValue(nombreAlmacenado, out var bytes))
            {
                throw new FileNotFoundException();
            }

            return Task.FromResult<Stream>(new MemoryStream(bytes));
        }

        public Task DescartarAsync(string nombreAlmacenado, CancellationToken cancellationToken = default)
        {
            Archivos.Remove(nombreAlmacenado);
            return Task.CompletedTask;
        }
    }
}
