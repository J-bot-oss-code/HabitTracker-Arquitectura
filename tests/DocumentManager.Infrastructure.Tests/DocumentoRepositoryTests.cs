using DocumentManager.Domain.Entities;
using DocumentManager.Infrastructure.Persistence;
using DocumentManager.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DocumentManager.Infrastructure.Tests;

public class DocumentoRepositoryTests
{
    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly Guid Beto = Guid.NewGuid();

    private static Documento Nuevo(Guid dueno, string nombre = "informe.pdf") =>
        new(nombre, "application/pdf", 100, dueno, "ana@ejemplo.com");

    /// <summary>SQLite en memoria: la conexión debe quedar abierta o la base desaparece.</summary>
    private static async Task<(SqliteConnection Conexion, DbContextOptions<DocumentManagerDbContext> Opciones)> CrearBaseAsync()
    {
        var conexion = new SqliteConnection("Data Source=:memory:");
        await conexion.OpenAsync();
        var opciones = new DbContextOptionsBuilder<DocumentManagerDbContext>().UseSqlite(conexion).Options;

        await using var contexto = new DocumentManagerDbContext(opciones);
        await contexto.Database.EnsureCreatedAsync();
        return (conexion, opciones);
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-01")]
    public async Task RF_DOC_01_Agregar_PersisteTodosLosMetadatosYLaFechaVuelveEnUtc()
    {
        var (conexion, opciones) = await CrearBaseAsync();
        await using var _ = conexion;
        var documento = Nuevo(Ana);

        await using (var escritura = new DocumentManagerDbContext(opciones))
        {
            await new DocumentoRepository(escritura).AddAsync(documento);
        }

        await using var lectura = new DocumentManagerDbContext(opciones);
        var leido = await new DocumentoRepository(lectura).ObtenerPorIdAsync(documento.Id);

        Assert.NotNull(leido);
        Assert.Equal("informe.pdf", leido.NombreOriginal);
        Assert.Equal("application/pdf", leido.TipoContenido);
        Assert.Equal(100, leido.TamanoBytes);
        Assert.Equal(Ana, leido.UsuarioId);
        Assert.Equal(documento.NombreAlmacenado, leido.NombreAlmacenado);
        Assert.Equal(DateTimeKind.Utc, leido.FechaSubidaUtc.Kind);
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-04")]
    public async Task RF_DOC_04_Eliminar_DejaLaFilaEnLaBaseConLaMarcaYLaFechaUtc()
    {
        var (conexion, opciones) = await CrearBaseAsync();
        await using var _ = conexion;
        var documento = Nuevo(Ana);

        await using (var escritura = new DocumentManagerDbContext(opciones))
        {
            var repositorio = new DocumentoRepository(escritura);
            await repositorio.AddAsync(documento);
            documento.Eliminar(DateTime.UtcNow);
            await repositorio.UpdateAsync(documento);
        }

        await using var lectura = new DocumentManagerDbContext(opciones);
        var fila = await lectura.Documentos.SingleAsync(d => d.Id == documento.Id);

        Assert.True(fila.Eliminado);
        Assert.NotNull(fila.FechaEliminacionUtc);
        Assert.Equal(DateTimeKind.Utc, fila.FechaEliminacionUtc!.Value.Kind);
        Assert.Equal(1, await lectura.Documentos.CountAsync());
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-02")]
    public async Task RF_DOC_02_ListarNoEliminados_ExcluyeEliminadosYFiltraPorDueno()
    {
        var (conexion, opciones) = await CrearBaseAsync();
        await using var _ = conexion;
        var propio = Nuevo(Ana, "uno.pdf");
        var eliminado = Nuevo(Ana, "dos.pdf");
        var ajeno = Nuevo(Beto, "tres.pdf");

        await using (var escritura = new DocumentManagerDbContext(opciones))
        {
            var repositorio = new DocumentoRepository(escritura);
            await repositorio.AddAsync(propio);
            await repositorio.AddAsync(eliminado);
            await repositorio.AddAsync(ajeno);
            eliminado.Eliminar(DateTime.UtcNow);
            await repositorio.UpdateAsync(eliminado);
        }

        await using var lectura = new DocumentManagerDbContext(opciones);
        var repo = new DocumentoRepository(lectura);

        var deAna = await repo.ListarNoEliminadosAsync(Ana);
        var deTodos = await repo.ListarNoEliminadosAsync(null);

        Assert.Equal(propio.Id, Assert.Single(deAna).Id);
        Assert.Equal(2, deTodos.Count);
        Assert.DoesNotContain(deTodos, d => d.Id == eliminado.Id);
    }
}
