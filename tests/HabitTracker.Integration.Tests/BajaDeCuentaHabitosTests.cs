using HabitTracker.Application.DTOs;
using HabitTracker.Application.Interfaces;
using HabitTracker.Application.Services;
using HabitTracker.Domain.Entities;
using HabitTracker.Infrastructure.Persistence;
using HabitTracker.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Integration.Tests;

/// <summary>
/// Baja de cuenta en HabitTracker con EF Core y SQLite: se borran los hábitos de la persona con sus registros de avance
/// y etiquetas asignadas, y nada más (RD-09, derecho a la supresión).
/// </summary>
public class BajaDeCuentaHabitosTests
{
    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly Guid Beto = Guid.NewGuid();

    private sealed class ConPermisos : IPermisoService
    {
        public Task<bool> TienePermisoEfectivoAsync(string solicitante, string permisoSolicitado, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private static HabitoService Servicio(HabitTrackerDbContext contexto) =>
        new(new HabitoRepository(contexto), new HabitTrackerUnitOfWork(contexto), new ConPermisos(), new MetaRepository(contexto));

    private static async Task<(SqliteConnection Conexion, DbContextOptions<HabitTrackerDbContext> Opciones, Guid MetaId)> PrepararAsync()
    {
        var conexion = new SqliteConnection("Data Source=:memory:");
        await conexion.OpenAsync();
        var opciones = new DbContextOptionsBuilder<HabitTrackerDbContext>().UseSqlite(conexion).Options;

        var categoriaId = Guid.NewGuid();
        var metaId = Guid.NewGuid();
        await using var contexto = new HabitTrackerDbContext(opciones);
        await contexto.Database.EnsureCreatedAsync();
        contexto.Categorias.Add(new Categoria { Id = categoriaId, Nombre = "Salud", Descripcion = "Hábitos de salud" });
        contexto.Metas.Add(new Meta { Id = metaId, Titulo = "Meta", EstadoActual = "Activa", FechaLimite = DateTime.UtcNow.AddDays(30), CategoriaId = categoriaId });
        contexto.Etiquetas.Add(new Etiqueta { Id = Guid.NewGuid(), Nombre = "mañana" });
        await contexto.SaveChangesAsync();

        return (conexion, opciones, metaId);
    }

    /// <summary>Crea un hábito activo con un avance registrado y una etiqueta asignada.</summary>
    private static async Task<Guid> HabitoConDatosAsync(DbContextOptions<HabitTrackerDbContext> opciones, Guid metaId, Guid dueno, string nombre)
    {
        Guid id;
        await using (var contexto = new HabitTrackerDbContext(opciones))
            id = await Servicio(contexto).CrearHabitoAsync(new CrearHabitoCommand(nombre, Periodicidad.Diaria, 10, "paginas", metaId), dueno);
        await using (var contexto = new HabitTrackerDbContext(opciones))
            await Servicio(contexto).CambiarEstadoAsync(id, EstadoHabito.Activo, dueno);
        await using (var contexto = new HabitTrackerDbContext(opciones))
            await Servicio(contexto).RegistrarAvanceAsync(id, 4, DateTime.UtcNow, dueno);
        await using (var contexto = new HabitTrackerDbContext(opciones))
        {
            var habito = await contexto.Habitos.Include(h => h.Etiquetas).SingleAsync(h => h.Id == id);
            habito.Etiquetas.Add(await contexto.Etiquetas.SingleAsync());
            await contexto.SaveChangesAsync();
        }

        return id;
    }

    [Fact]
    [Trait("Requirement", "RD-09")]
    public async Task RD_09_LaBajaBorraLosHabitosDeLaPersonaConSusRegistrosYNadaDeLosDemas()
    {
        var (conexion, opciones, metaId) = await PrepararAsync();
        await using var _ = conexion;
        var deAna1 = await HabitoConDatosAsync(opciones, metaId, Ana, "Leer");
        var deAna2 = await HabitoConDatosAsync(opciones, metaId, Ana, "Caminar");
        var deBeto = await HabitoConDatosAsync(opciones, metaId, Beto, "Meditar");

        int borrados;
        await using (var contexto = new HabitTrackerDbContext(opciones))
            borrados = await Servicio(contexto).EliminarDatosDeUsuarioAsync(Ana);

        Assert.Equal(2, borrados);
        await using var verificacion = new HabitTrackerDbContext(opciones);
        Assert.Empty(await verificacion.Habitos.Where(h => h.UsuarioId == Ana).ToListAsync());
        Assert.Empty(await verificacion.RegistrosDiarios.Where(r => r.HabitoId == deAna1 || r.HabitoId == deAna2).ToListAsync());
        // Lo de otra persona queda intacto, con su registro de avance.
        Assert.Equal(deBeto, (await verificacion.Habitos.SingleAsync()).Id);
        Assert.Single(await verificacion.RegistrosDiarios.ToListAsync());
    }

    [Fact]
    [Trait("Requirement", "RD-09")]
    public async Task RD_09_LasMetasLasCategoriasYLasEtiquetasSonCatalogosCompartidosYNoSeBorran()
    {
        var (conexion, opciones, metaId) = await PrepararAsync();
        await using var _ = conexion;
        await HabitoConDatosAsync(opciones, metaId, Ana, "Leer");

        await using (var contexto = new HabitTrackerDbContext(opciones))
            await Servicio(contexto).EliminarDatosDeUsuarioAsync(Ana);

        await using var verificacion = new HabitTrackerDbContext(opciones);
        Assert.Single(await verificacion.Metas.ToListAsync());
        Assert.Single(await verificacion.Categorias.ToListAsync());
        var etiqueta = await verificacion.Etiquetas.Include(e => e.Habitos).SingleAsync();
        Assert.Empty(etiqueta.Habitos);   // la asignación a un hábito borrado desaparece; la etiqueta queda
    }

    [Fact]
    [Trait("Requirement", "RD-09")]
    public async Task RD_09_BorrarDosVecesEsInofensivo_UnaBajaReintentadaNoFalla()
    {
        var (conexion, opciones, metaId) = await PrepararAsync();
        await using var _ = conexion;
        await HabitoConDatosAsync(opciones, metaId, Ana, "Leer");

        int primera, segunda;
        await using (var contexto = new HabitTrackerDbContext(opciones))
            primera = await Servicio(contexto).EliminarDatosDeUsuarioAsync(Ana);
        await using (var contexto = new HabitTrackerDbContext(opciones))
            segunda = await Servicio(contexto).EliminarDatosDeUsuarioAsync(Ana);

        Assert.Equal(1, primera);
        Assert.Equal(0, segunda);
    }

    [Fact]
    [Trait("Requirement", "RD-09")]
    public async Task RD_09_UnaPersonaSinHabitosSePuedeDarDeBajaSinErrores()
    {
        var (conexion, opciones, _) = await PrepararAsync();
        await using var _ = conexion;

        await using var contexto = new HabitTrackerDbContext(opciones);
        Assert.Equal(0, await Servicio(contexto).EliminarDatosDeUsuarioAsync(Guid.NewGuid()));
    }
}
