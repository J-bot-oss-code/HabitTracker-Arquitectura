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
/// Fase 1 de HabitTracker v2 contra EF Core y SQLite. Comprueba que las fechas leídas vuelven como UTC, que un hábito puede
/// ir sin meta y que borrar una meta del catálogo deja sus hábitos sueltos en lugar de borrarlos (RF-NEG-01, RD-09, RD-11).
/// </summary>
public class CimientosPersistenciaTests
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
    [Trait("Requirement", "RD-11")]
    public async Task RD_11_FechasLeidasDeLaBase_VuelvenComoUtc()
    {
        // Arrange
        var (conexion, opciones, _) = await PrepararBaseAsync();
        await using var _c = conexion;
        Guid habitoId;
        await using (var contexto = new HabitTrackerDbContext(opciones))
            habitoId = await CrearServicio(contexto).CrearHabitoAsync(new CrearHabitoCommand("Leer", Periodicidad.Diaria, 10, "paginas"), propietario);
        await using (var contexto = new HabitTrackerDbContext(opciones))
            await CrearServicio(contexto).CambiarEstadoAsync(habitoId, EstadoHabito.Activo, propietario);
        await using (var contexto = new HabitTrackerDbContext(opciones))
            await CrearServicio(contexto).RegistrarAvanceAsync(habitoId, 4, DateTime.UtcNow, propietario);

        // Act
        HabitoDto dto;
        await using (var contexto = new HabitTrackerDbContext(opciones))
            dto = await CrearServicio(contexto).ObtenerHabitoAsync(habitoId, propietario);

        // Assert: sin el conversor volvían como Unspecified y la API las serializaba sin "Z".
        Assert.Equal(DateTimeKind.Utc, dto.FechaInicioCiclo.Kind);
        Assert.Equal(DateTimeKind.Utc, Assert.Single(dto.Registros).Fecha.Kind);
        Assert.EndsWith("Z", System.Text.Json.JsonSerializer.Serialize(dto.FechaInicioCiclo).Trim('"'));
    }

    [Fact]
    [Trait("Requirement", "RD-11")]
    public async Task RD_11_FechaLocalAlGuardar_SeConvierteAUtc()
    {
        // Arrange: Meta tiene setters públicos, así que alguien podría asignarle una fecha Local.
        var (conexion, opciones, _) = await PrepararBaseAsync();
        await using var _c = conexion;
        var categoriaId = Guid.NewGuid();
        var metaId = Guid.NewGuid();
        var local = new DateTime(2026, 12, 31, 20, 0, 0, DateTimeKind.Local);

        // Act
        await using (var contexto = new HabitTrackerDbContext(opciones))
        {
            contexto.Categorias.Add(new Categoria { Id = categoriaId, Nombre = "Cat2", Descripcion = "Desc" });
            contexto.Metas.Add(new Meta { Id = metaId, Titulo = "Meta local", EstadoActual = "Activa", FechaLimite = local, CategoriaId = categoriaId });
            await contexto.SaveChangesAsync();
        }

        // Assert: se guarda el mismo instante, expresado en UTC.
        await using var verificacion = new HabitTrackerDbContext(opciones);
        var leida = (await verificacion.Metas.SingleAsync(m => m.Id == metaId)).FechaLimite;
        Assert.Equal(DateTimeKind.Utc, leida.Kind);
        Assert.Equal(local.ToUniversalTime(), leida);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-01")]
    [Trait("Requirement", "RD-09")]
    public async Task RF_NEG_01_HabitoSinMeta_SePersisteYSeRecupera()
    {
        // Arrange
        var (conexion, opciones, _) = await PrepararBaseAsync();
        await using var _c = conexion;

        // Act
        Guid habitoId;
        await using (var contexto = new HabitTrackerDbContext(opciones))
            habitoId = await CrearServicio(contexto).CrearHabitoAsync(new CrearHabitoCommand("Beber agua", Periodicidad.Diaria, 8, "vasos"), propietario);

        // Assert
        await using var verificacion = new HabitTrackerDbContext(opciones);
        var habito = await verificacion.Habitos.SingleAsync(h => h.Id == habitoId);
        Assert.Null(habito.MetaId);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-01")]
    [Trait("Requirement", "RD-09")]
    public async Task RF_NEG_01_BorrarMetaDelCatalogo_DejaSusHabitosSueltosSinBorrarlos()
    {
        // Arrange
        var (conexion, opciones, metaId) = await PrepararBaseAsync();
        await using var _c = conexion;
        Guid habitoId;
        await using (var contexto = new HabitTrackerDbContext(opciones))
            habitoId = await CrearServicio(contexto).CrearHabitoAsync(new CrearHabitoCommand("Leer", Periodicidad.Diaria, 10, "paginas", metaId), propietario);

        // Act: antes el FK era Cascade y esto borraba el hábito con sus avances.
        await using (var contexto = new HabitTrackerDbContext(opciones))
            await contexto.Metas.Where(m => m.Id == metaId).ExecuteDeleteAsync();

        // Assert
        await using var verificacion = new HabitTrackerDbContext(opciones);
        var habito = await verificacion.Habitos.SingleOrDefaultAsync(h => h.Id == habitoId);
        Assert.NotNull(habito);
        Assert.Null(habito.MetaId);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-01")]
    public void RF_NEG_01_Modelo_LongitudesYPrecisionAcotadas()
    {
        // Arrange
        var opciones = new DbContextOptionsBuilder<HabitTrackerDbContext>().UseSqlite("Data Source=:memory:").Options;
        using var contexto = new HabitTrackerDbContext(opciones);
        var habito = contexto.Model.FindEntityType(typeof(Habito))!;
        var registro = contexto.Model.FindEntityType(typeof(RegistroDiario))!;

        // Assert: los mismos límites que valida la API, en lugar de nvarchar(max) y decimal sin precisión.
        Assert.Equal(200, habito.FindProperty(nameof(Habito.Nombre))!.GetMaxLength());
        Assert.Equal(50, habito.FindProperty(nameof(Habito.Unidad))!.GetMaxLength());
        Assert.Equal(18, habito.FindProperty(nameof(Habito.CantidadObjetivo))!.GetPrecision());
        Assert.Equal(2, registro.FindProperty(nameof(RegistroDiario.Cantidad))!.GetScale());
        Assert.True(habito.FindProperty(nameof(Habito.MetaId))!.IsNullable);
    }
}
