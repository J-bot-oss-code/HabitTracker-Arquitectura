using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Permissions.Domain.Entities;
using Permissions.Infrastructure.Persistence;
using Permissions.Infrastructure.Repositories;

namespace Permissions.Infrastructure.Tests;

/// <summary>Baja de cuenta en Permisos con EF Core y SQLite: se borran las solicitudes que la persona hizo (RD-09).</summary>
public sealed class BajaDeCuentaSolicitudesTests
{
    private static async Task<(SqliteConnection Conexion, DbContextOptions<PermissionsDbContext> Opciones)> PrepararAsync(
        params SolicitudPermiso[] solicitudes)
    {
        var conexion = new SqliteConnection("Data Source=:memory:");
        await conexion.OpenAsync();
        var opciones = new DbContextOptionsBuilder<PermissionsDbContext>().UseSqlite(conexion).Options;

        await using var contexto = new PermissionsDbContext(opciones);
        await contexto.Database.EnsureCreatedAsync();
        var repositorio = new SolicitudPermisoRepository(contexto);
        foreach (var solicitud in solicitudes)
        {
            await repositorio.AddAsync(solicitud);
        }

        return (conexion, opciones);
    }

    [Fact]
    [Trait("Requirement", "RD-09")]
    public async Task RD_09_SeBorranLasSolicitudesDeLaPersona_SinImportarMayusculasNiEstado()
    {
        var pendiente = new SolicitudPermiso("persona@ejemplo.com", "Acceso a reportes");
        var rechazada = new SolicitudPermiso("persona@ejemplo.com", "Acceso a documentos");
        rechazada.Rechazar("admin@ejemplo.com", "No cumple la política", DateTime.UtcNow);
        var ajena = new SolicitudPermiso("otra@ejemplo.com", "Acceso a reportes");
        var (conexion, opciones) = await PrepararAsync(pendiente, rechazada, ajena);
        await using var _ = conexion;

        int borradas;
        await using (var contexto = new PermissionsDbContext(opciones))
            borradas = await new SolicitudPermisoRepository(contexto).EliminarPorSolicitanteAsync("PERSONA@Ejemplo.com");

        Assert.Equal(2, borradas);
        await using var verificacion = new PermissionsDbContext(opciones);
        var restantes = await verificacion.SolicitudesPermisos.ToListAsync();
        Assert.Equal(ajena.Id, Assert.Single(restantes).Id);
    }

    [Fact]
    [Trait("Requirement", "RD-09")]
    public async Task RD_09_LasSolicitudesDeOtrosQueEstaPersonaResolvioComoAprobadoraNoSeTocan()
    {
        var deOtro = new SolicitudPermiso("otra@ejemplo.com", "Acceso a reportes");
        deOtro.Rechazar("admin@ejemplo.com", "No procede", DateTime.UtcNow);
        var (conexion, opciones) = await PrepararAsync(deOtro);
        await using var _ = conexion;

        await using (var contexto = new PermissionsDbContext(opciones))
            Assert.Equal(0, await new SolicitudPermisoRepository(contexto).EliminarPorSolicitanteAsync("admin@ejemplo.com"));

        await using var verificacion = new PermissionsDbContext(opciones);
        var conservada = await verificacion.SolicitudesPermisos.SingleAsync();
        Assert.Equal("admin@ejemplo.com", conservada.Aprobador);   // la decisión de administración sigue registrada
    }

    [Fact]
    [Trait("Requirement", "RD-09")]
    public async Task RD_09_BorrarDosVecesEsInofensivo()
    {
        var (conexion, opciones) = await PrepararAsync(new SolicitudPermiso("persona@ejemplo.com", "Acceso a reportes"));
        await using var _ = conexion;

        await using var contexto = new PermissionsDbContext(opciones);
        var repositorio = new SolicitudPermisoRepository(contexto);

        Assert.Equal(1, await repositorio.EliminarPorSolicitanteAsync("persona@ejemplo.com"));
        Assert.Equal(0, await repositorio.EliminarPorSolicitanteAsync("persona@ejemplo.com"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RD_07_UnSolicitanteVacioSeRechazaYNoBorraTodo(string solicitante)
    {
        var (conexion, opciones) = await PrepararAsync(new SolicitudPermiso("persona@ejemplo.com", "Acceso a reportes"));
        await using var _ = conexion;

        await using (var contexto = new PermissionsDbContext(opciones))
            await Assert.ThrowsAnyAsync<ArgumentException>(() => new SolicitudPermisoRepository(contexto).EliminarPorSolicitanteAsync(solicitante));

        await using var verificacion = new PermissionsDbContext(opciones);
        Assert.Single(await verificacion.SolicitudesPermisos.ToListAsync());
    }
}
