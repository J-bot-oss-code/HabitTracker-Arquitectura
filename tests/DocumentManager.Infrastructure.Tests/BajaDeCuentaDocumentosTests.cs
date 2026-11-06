using DocumentManager.Domain.Entities;
using DocumentManager.Infrastructure.Persistence;
using DocumentManager.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DocumentManager.Infrastructure.Tests;

/// <summary>
/// Baja de cuenta en el manejador de documentos con EF Core y SQLite: se borran los registros de TODOS los documentos de la
/// persona, también los marcados como eliminados (RD-09). El borrado normal sigue siendo lógico (RF-DOC-04).
/// </summary>
public class BajaDeCuentaDocumentosTests
{
    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly Guid Beto = Guid.NewGuid();

    private static Documento Nuevo(Guid dueno, string nombre = "informe.pdf") =>
        new(nombre, "application/pdf", 100, dueno, "ana@ejemplo.com");

    private static async Task<(SqliteConnection Conexion, DbContextOptions<DocumentManagerDbContext> Opciones)> CrearBaseAsync(params Documento[] documentos)
    {
        var conexion = new SqliteConnection("Data Source=:memory:");
        await conexion.OpenAsync();
        var opciones = new DbContextOptionsBuilder<DocumentManagerDbContext>().UseSqlite(conexion).Options;

        await using var contexto = new DocumentManagerDbContext(opciones);
        await contexto.Database.EnsureCreatedAsync();
        var repositorio = new DocumentoRepository(contexto);
        foreach (var documento in documentos)
        {
            await repositorio.AddAsync(documento);
        }

        return (conexion, opciones);
    }

    [Fact]
    [Trait("Requirement", "RD-09")]
    public async Task RD_09_ListarTodosIncluyeLosDocumentosMarcadosComoEliminados()
    {
        var vigente = Nuevo(Ana, "vigente.pdf");
        var eliminado = Nuevo(Ana, "eliminado.pdf");
        eliminado.Eliminar(DateTime.UtcNow);
        var (conexion, opciones) = await CrearBaseAsync(vigente, eliminado, Nuevo(Beto, "ajeno.pdf"));
        await using var _ = conexion;

        await using var contexto = new DocumentManagerDbContext(opciones);
        var repositorio = new DocumentoRepository(contexto);

        var todos = await repositorio.ListarTodosDeUsuarioAsync(Ana);
        var visibles = await repositorio.ListarNoEliminadosAsync(Ana);

        Assert.Equal(2, todos.Count);
        Assert.Single(visibles);   // el listado normal sigue ocultando el eliminado
        Assert.All(todos, d => Assert.Equal(Ana, d.UsuarioId));
    }

    [Fact]
    [Trait("Requirement", "RD-09")]
    public async Task RD_09_PurgarBorraLosRegistrosDeLaPersonaYNadaDeLosDemas()
    {
        var eliminado = Nuevo(Ana, "eliminado.pdf");
        eliminado.Eliminar(DateTime.UtcNow);
        var deBeto = Nuevo(Beto, "ajeno.pdf");
        var (conexion, opciones) = await CrearBaseAsync(Nuevo(Ana), eliminado, deBeto);
        await using var _ = conexion;

        int borrados;
        await using (var contexto = new DocumentManagerDbContext(opciones))
            borrados = await new DocumentoRepository(contexto).PurgarDeUsuarioAsync(Ana);

        Assert.Equal(2, borrados);
        await using var verificacion = new DocumentManagerDbContext(opciones);
        Assert.Equal(deBeto.Id, (await verificacion.Documentos.SingleAsync()).Id);
    }

    [Fact]
    [Trait("Requirement", "RD-09")]
    public async Task RD_09_PurgarDosVecesEsInofensivo()
    {
        var (conexion, opciones) = await CrearBaseAsync(Nuevo(Ana));
        await using var _ = conexion;

        await using var contexto = new DocumentManagerDbContext(opciones);
        var repositorio = new DocumentoRepository(contexto);

        Assert.Equal(1, await repositorio.PurgarDeUsuarioAsync(Ana));
        Assert.Equal(0, await repositorio.PurgarDeUsuarioAsync(Ana));
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-04")]
    public void RF_DOC_04_ElBorradoNormalSigueSiendoLogico_NoHayUnMetodoDeBorradoEnElDominio()
    {
        // Eliminar un documento sigue siendo una marca: el único borrado físico es la purga por baja de cuenta, por usuario.
        var metodos = typeof(Documento).GetMethods().Select(m => m.Name).ToArray();

        Assert.DoesNotContain(metodos, n => n.StartsWith("Remove", StringComparison.Ordinal) || n.StartsWith("Delete", StringComparison.Ordinal));
    }
}
