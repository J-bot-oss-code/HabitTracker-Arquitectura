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
/// Persistencia real con EF Core y SQLite: cada operación usa un DbContext nuevo,
/// igual que cada petición HTTP de la API (RF-NEG-02.4, RF-NEG-09, RD-09).
/// </summary>
public class HabitoPersistenciaTests
{
    private readonly Guid propietario = Guid.NewGuid();

    private sealed class SinPermisos : IPermisoService
    {
        public Task<bool> TienePermisoEfectivoAsync(string solicitante, string permisoSolicitado, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private static HabitoService CrearServicio(HabitTrackerDbContext contexto) =>
        new(new HabitoRepository(contexto), new HabitTrackerUnitOfWork(contexto), new SinPermisos(), new MetaRepository(contexto));

    private static async Task<(SqliteConnection Conexion, DbContextOptions<HabitTrackerDbContext> Opciones, Guid MetaId)> PrepararBaseAsync()
    {
        var conexion = new SqliteConnection("Data Source=:memory:");
        await conexion.OpenAsync();
        var opciones = new DbContextOptionsBuilder<HabitTrackerDbContext>().UseSqlite(conexion).Options;

        var categoriaId = Guid.NewGuid();
        var metaId = Guid.NewGuid();
        await using var contexto = new HabitTrackerDbContext(opciones);
        await contexto.Database.EnsureCreatedAsync();
        contexto.Categorias.Add(new Categoria { Id = categoriaId, Nombre = "Cat", Descripcion = "Desc" });
        contexto.Metas.Add(new Meta { Id = metaId, Titulo = "Meta", EstadoActual = "Activa", FechaLimite = DateTime.UtcNow.AddDays(30), CategoriaId = categoriaId });
        await contexto.SaveChangesAsync();

        return (conexion, opciones, metaId);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_RegistrarAvance_PersisteRegistroNuevoEnLaBaseDeDatos()
    {
        // Arrange
        var (conexion, opciones, metaId) = await PrepararBaseAsync();
        await using var _ = conexion;
        Guid habitoId;
        await using (var contexto = new HabitTrackerDbContext(opciones))
            habitoId = await CrearServicio(contexto).CrearHabitoAsync(new CrearHabitoCommand("Leer", Periodicidad.Diaria, 10, "paginas", metaId), propietario);
        await using (var contexto = new HabitTrackerDbContext(opciones))
            await CrearServicio(contexto).CambiarEstadoAsync(habitoId, EstadoHabito.Activo, propietario);

        // Act
        await using (var contexto = new HabitTrackerDbContext(opciones))
            await CrearServicio(contexto).RegistrarAvanceAsync(habitoId, 4, DateTime.UtcNow, propietario);

        // Assert
        await using var verificacion = new HabitTrackerDbContext(opciones);
        var registros = await verificacion.RegistrosDiarios.Where(r => r.HabitoId == habitoId).ToListAsync();
        Assert.Single(registros);
        Assert.Equal(4m, registros[0].Cantidad);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-04")]
    public async Task RF_NEG_04_Completar_ConAvancesPersistidosQueCumplenLaMeta_PasaACompletado()
    {
        // Arrange
        var (conexion, opciones, metaId) = await PrepararBaseAsync();
        await using var _ = conexion;
        Guid habitoId;
        await using (var contexto = new HabitTrackerDbContext(opciones))
            habitoId = await CrearServicio(contexto).CrearHabitoAsync(new CrearHabitoCommand("Leer", Periodicidad.Diaria, 10, "paginas", metaId), propietario);
        await using (var contexto = new HabitTrackerDbContext(opciones))
            await CrearServicio(contexto).CambiarEstadoAsync(habitoId, EstadoHabito.Activo, propietario);
        await using (var contexto = new HabitTrackerDbContext(opciones))
            await CrearServicio(contexto).RegistrarAvanceAsync(habitoId, 6, DateTime.UtcNow, propietario);
        await using (var contexto = new HabitTrackerDbContext(opciones))
            await CrearServicio(contexto).RegistrarAvanceAsync(habitoId, 4, DateTime.UtcNow, propietario);

        // Act
        await using (var contexto = new HabitTrackerDbContext(opciones))
            await CrearServicio(contexto).CambiarEstadoAsync(habitoId, EstadoHabito.Completado, propietario);

        // Assert
        await using var verificacion = new HabitTrackerDbContext(opciones);
        var habito = await verificacion.Habitos.Include(h => h.Registros).SingleAsync(h => h.Id == habitoId);
        Assert.Equal(EstadoHabito.Completado, habito.Estado);
        Assert.Equal(2, habito.Registros.Count);
    }
}
