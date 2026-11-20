using DocumentManager.Application.DTOs;
using DocumentManager.Application.Exceptions;
using DocumentManager.Application.Interfaces;
using DocumentManager.Application.Services;
using DocumentManager.Domain.Entities;

namespace DocumentManager.Application.Tests;

/// <summary>
/// Toda subida y eliminación de documento queda en el historial de auditoría (RF-AUD-03, RF-DOC-06) por un punto de extensión
/// opcional. Dobles escritos a mano, sin base de datos ni disco (RD-12).
/// </summary>
public sealed class AuditoriaDeDocumentosTests
{
    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly Guid Admin = Guid.NewGuid();

    private sealed class Repositorio : IDocumentoRepository
    {
        public List<Documento> Todos { get; } = [];
        public Task AddAsync(Documento documento, CancellationToken c = default) { Todos.Add(documento); return Task.CompletedTask; }
        public Task UpdateAsync(Documento documento, CancellationToken c = default) => Task.CompletedTask;
        public Task<Documento?> ObtenerPorIdAsync(Guid id, CancellationToken c = default) => Task.FromResult(Todos.FirstOrDefault(d => d.Id == id));
        public Task<IReadOnlyList<Documento>> ListarTodosDeUsuarioAsync(Guid usuarioId, CancellationToken c = default) =>
            Task.FromResult<IReadOnlyList<Documento>>(Todos.Where(d => d.UsuarioId == usuarioId).ToList());
        public Task<int> PurgarDeUsuarioAsync(Guid usuarioId, CancellationToken c = default) => Task.FromResult(Todos.RemoveAll(d => d.UsuarioId == usuarioId));
        public Task<IReadOnlyList<Documento>> ListarNoEliminadosAsync(Guid? usuarioId, CancellationToken c = default) =>
            Task.FromResult<IReadOnlyList<Documento>>(Todos.Where(d => !d.Eliminado && (usuarioId is null || d.UsuarioId == usuarioId)).ToList());
    }

    private sealed class Almacen : IAlmacenamientoArchivos
    {
        public Dictionary<string, byte[]> Archivos { get; } = [];

        public async Task GuardarAsync(string nombreAlmacenado, Stream contenido, CancellationToken c = default)
        {
            using var ms = new MemoryStream();
            await contenido.CopyToAsync(ms, c);
            Archivos[nombreAlmacenado] = ms.ToArray();
        }

        public Task<Stream> AbrirLecturaAsync(string nombreAlmacenado, CancellationToken c = default) => Task.FromResult<Stream>(new MemoryStream(Archivos[nombreAlmacenado]));
        public Task DescartarAsync(string nombreAlmacenado, CancellationToken c = default) { Archivos.Remove(nombreAlmacenado); return Task.CompletedTask; }
    }

    private sealed class Guarda(bool permite) : IGuardaEliminacionDocumento
    {
        public Task<bool> PermiteEliminarAsync(Guid documentoId, Guid usuarioId, CancellationToken c = default) => Task.FromResult(permite);
    }

    private sealed record Registro(Guid UsuarioId, string Accion, Guid DocumentoId, string Anterior, string Nuevo);

    private sealed class Auditor : IAuditorDocumentos
    {
        public List<Registro> Registros { get; } = [];
        public bool Falla { get; set; }

        public Task RegistrarAsync(Guid usuarioId, string accion, Guid documentoId, string valorAnterior, string valorNuevo, CancellationToken c = default)
        {
            if (Falla) throw new InvalidOperationException("Auditoría no responde.");
            Registros.Add(new Registro(usuarioId, accion, documentoId, valorAnterior, valorNuevo));
            return Task.CompletedTask;
        }
    }

    private static (DocumentoService Servicio, Auditor Auditor) Crear(IGuardaEliminacionDocumento? guarda = null)
    {
        var auditor = new Auditor();
        return (new DocumentoService(new Repositorio(), new Almacen(), guarda, auditor), auditor);
    }

    private static SubirDocumentoCommand Subida(Guid usuario, string nombre = "informe.pdf") =>
        new(usuario, "ana@ejemplo.com", nombre, new MemoryStream([1, 2, 3, 4]));

    [Fact]
    [Trait("Requirement", "RF-AUD-03")]
    public async Task RF_AUD_03_SubirUnDocumento_RegistraQuienLoSubioQueDocumentoYSuTamano()
    {
        // Arrange
        var (servicio, auditor) = Crear();

        // Act
        var dto = await servicio.SubirAsync(Subida(Ana));

        // Assert
        var r = Assert.Single(auditor.Registros);
        Assert.Equal(Ana, r.UsuarioId);
        Assert.Equal("Subida de documento", r.Accion);
        Assert.Equal(dto.Id, r.DocumentoId);
        Assert.Equal("No existía", r.Anterior);
        Assert.Equal("informe.pdf (4 bytes)", r.Nuevo);
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-03")]
    public async Task RF_AUD_03_EliminarUnDocumento_RegistraQuienLoElimino_ConElEstadoAnteriorYElNuevo()
    {
        // Arrange
        var (servicio, auditor) = Crear();
        var dto = await servicio.SubirAsync(Subida(Ana));
        auditor.Registros.Clear();

        // Act
        await servicio.EliminarAsync(dto.Id, Ana, esAdministrador: false);

        // Assert
        var r = Assert.Single(auditor.Registros);
        Assert.Equal(Ana, r.UsuarioId);
        Assert.Equal("Eliminación de documento", r.Accion);
        Assert.Equal(dto.Id, r.DocumentoId);
        Assert.Equal("informe.pdf (visible)", r.Anterior);
        Assert.Equal("informe.pdf (eliminado)", r.Nuevo);
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-03")]
    public async Task RF_AUD_03_UnAdministradorQueEliminaElDocumentoDeOtro_QuedaRegistradoComoQuienActuo()
    {
        // Arrange
        var (servicio, auditor) = Crear();
        var dto = await servicio.SubirAsync(Subida(Ana));
        auditor.Registros.Clear();

        // Act
        await servicio.EliminarAsync(dto.Id, Admin, esAdministrador: true);

        // Assert
        var r = Assert.Single(auditor.Registros);
        Assert.Equal(Admin, r.UsuarioId);
        Assert.Contains("eliminado por un Administrador", r.Nuevo);
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-03")]
    public async Task RF_AUD_03_UnaSubidaOUnaEliminacionQueSeRechazan_NoDejanRegistro()
    {
        // Arrange: una extensión no permitida, y un documento en uso que la guarda impide eliminar
        var (servicio, auditor) = Crear(new Guarda(permite: false));
        var propio = await servicio.SubirAsync(Subida(Ana));
        auditor.Registros.Clear();

        // Act
        await Assert.ThrowsAsync<ArchivoInvalidoException>(() => servicio.SubirAsync(Subida(Ana, "virus.exe")));
        await Assert.ThrowsAsync<DocumentoProtegidoException>(() => servicio.EliminarAsync(propio.Id, Ana, esAdministrador: false));

        // Assert
        Assert.Empty(auditor.Registros);
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-03")]
    public async Task RF_AUD_03_UnFalloAlRegistrarNoSeTraga_LaOperacionResponde_Error()
    {
        // Arrange
        var (servicio, auditor) = Crear();
        auditor.Falla = true;

        // Act y Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.SubirAsync(Subida(Ana)));
    }
}
