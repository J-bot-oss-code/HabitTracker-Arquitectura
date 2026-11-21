using HabitTracker.Application.DTOs;
using HabitTracker.Application.Exceptions;
using HabitTracker.Application.Interfaces;
using HabitTracker.Application.Services;
using HabitTracker.Domain.Entities;
using HabitTracker.Infrastructure.Persistence;
using HabitTracker.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Integration.Tests;

/// <summary>
/// Fase 5: el avance que llega por correo es idempotente. La clave la respeta el servicio y la garantiza el índice único
/// filtrado de la base (un segundo POST del mismo enlace nunca duplica, ni con dos peticiones a la vez).
/// </summary>
public class RegistroIdempotentePersistenciaTests
{
    private readonly Guid propietario = Guid.NewGuid();
    private const string Clave = "correo:2026-11-13";

    private sealed class SinPermisos : IPermisoService
    {
        public Task<bool> TienePermisoEfectivoAsync(string solicitante, string permisoSolicitado, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private static HabitoService Servicio(HabitTrackerDbContext contexto) =>
        new(new HabitoRepository(contexto), new HabitTrackerUnitOfWork(contexto), new SinPermisos(), new MetaRepository(contexto));

    private async Task<(SqliteConnection Conexion, DbContextOptions<HabitTrackerDbContext> Opciones, Guid HabitoId)> PrepararAsync()
    {
        var conexion = new SqliteConnection("Data Source=:memory:");
        await conexion.OpenAsync();
        var opciones = new DbContextOptionsBuilder<HabitTrackerDbContext>().UseSqlite(conexion).Options;
        await using (var contexto = new HabitTrackerDbContext(opciones))
            await contexto.Database.EnsureCreatedAsync();

        Guid habitoId;
        await using (var contexto = new HabitTrackerDbContext(opciones))
            habitoId = await Servicio(contexto).CrearHabitoAsync(new CrearHabitoCommand("Beber agua", Periodicidad.Diaria, 1, "vaso"), propietario);
        await using (var contexto = new HabitTrackerDbContext(opciones))
            await Servicio(contexto).CambiarEstadoAsync(habitoId, EstadoHabito.Activo, propietario);
        return (conexion, opciones, habitoId);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    [Trait("Requirement", "RD-09")]
    public async Task RF_NEG_07_AvanceConLaMismaClave_SeRegistraUnaSolaVez_YGuardaElOrigen()
    {
        var (conexion, opciones, habitoId) = await PrepararAsync();
        await using var _ = conexion;

        // Dos POST del mismo enlace, cada uno con su propio contexto como en dos peticiones HTTP.
        for (var i = 0; i < 2; i++)
            await using (var contexto = new HabitTrackerDbContext(opciones))
                await Servicio(contexto).RegistrarAvanceAsync(habitoId, 1, DateTime.UtcNow, propietario, false, OrigenRegistro.Correo, Clave);

        await using var verificacion = new HabitTrackerDbContext(opciones);
        var registro = Assert.Single(await verificacion.RegistrosDiarios.Where(r => r.HabitoId == habitoId).ToListAsync());
        Assert.Equal(OrigenRegistro.Correo, registro.Origen);
        Assert.Equal(Clave, registro.ClaveIdempotencia);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public async Task RF_NEG_07_ClavesDeOtroDia_SonOtroAvance_YSinClaveNuncaSeAgrupa()
    {
        var (conexion, opciones, habitoId) = await PrepararAsync();
        await using var _ = conexion;

        await using (var c = new HabitTrackerDbContext(opciones))
            await Servicio(c).RegistrarAvanceAsync(habitoId, 1, DateTime.UtcNow, propietario, false, OrigenRegistro.Correo, "correo:2026-11-12");
        await using (var c = new HabitTrackerDbContext(opciones))
            await Servicio(c).RegistrarAvanceAsync(habitoId, 1, DateTime.UtcNow, propietario, false, OrigenRegistro.Correo, "correo:2026-11-13");
        // Dos avances de la app, sin clave: ambos cuentan (el índice solo mira las claves que existen).
        for (var i = 0; i < 2; i++)
            await using (var c = new HabitTrackerDbContext(opciones))
                await Servicio(c).RegistrarAvanceAsync(habitoId, 1, DateTime.UtcNow, propietario);

        await using var verificacion = new HabitTrackerDbContext(opciones);
        Assert.Equal(4, await verificacion.RegistrosDiarios.CountAsync(r => r.HabitoId == habitoId));
        Assert.Equal(2, await verificacion.RegistrosDiarios.CountAsync(r => r.Origen == OrigenRegistro.App));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    [Trait("Requirement", "RD-09")]
    public async Task RF_NEG_07_ElIndiceUnico_RechazaElDuplicadoAunqueElServicioNoLoVea()
    {
        var (conexion, opciones, habitoId) = await PrepararAsync();
        await using var _ = conexion;
        await using (var c = new HabitTrackerDbContext(opciones))
            await Servicio(c).RegistrarAvanceAsync(habitoId, 1, DateTime.UtcNow, propietario, false, OrigenRegistro.Correo, Clave);

        // Una carrera: otra petición ya había leído el hábito sin el registro y agrega el suyo.
        await using var contexto = new HabitTrackerDbContext(opciones);
        var habito = await contexto.Habitos.SingleAsync(h => h.Id == habitoId);
        contexto.RegistrosDiarios.Add(new RegistroDiario(1, DateTime.UtcNow, "vaso", false, OrigenRegistro.Correo, Clave));
        contexto.Entry(contexto.RegistrosDiarios.Local.Single()).Property(nameof(RegistroDiario.HabitoId)).CurrentValue = habito.Id;

        var excepcion = await Record.ExceptionAsync(() => new HabitTrackerUnitOfWork(contexto).SaveChangesAsync());

        Assert.IsType<ConflictoDeUnicidadException>(excepcion);
    }
}
