using HabitTracker.Application.Exceptions;
using HabitTracker.Application.Interfaces;
using HabitTracker.Application.Services;
using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Planes;

namespace HabitTracker.Application.Notificaciones;

/// <summary>Lo que la página del enlace puede mostrar antes de que la persona confirme. Nada más que el nombre y el día.</summary>
public sealed record InfoCheckin(string Habito, DateOnly FechaLocal);

public interface ICheckinCorreoService
{
    /// <summary>La página del GET: valida el enlace y dice qué se va a registrar. <b>No tiene efectos.</b></summary>
    Task<InfoCheckin> VerAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>El POST: registra el avance de ese día, una sola vez por enlace y día. Lanza <see cref="EnlaceNoValidoException"/>.</summary>
    Task EjecutarAsync(string token, CancellationToken cancellationToken = default);
}

/// <summary>Cuentas de Control de Acceso, vistas desde HabitTracker. La implementa un adaptador del Host (RD-03).</summary>
public interface IEstadoCuentas
{
    /// <summary>La cuenta existe, está activa y no está en baja.</summary>
    Task<bool> CuentaActivaAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}

/// <summary>
/// «Ya lo hice» desde el correo (RF-NEG-07). El enlace no abre sesión: el token dice usuario, hábito y día, y aquí se
/// comprueba que todo siga vigente en el servidor (RD-06). La lógica de registrar es la de siempre
/// (<see cref="IHabitoService.RegistrarAvanceAsync"/>); este servicio solo valida y fija la fecha.
/// </summary>
public sealed class CheckinCorreoService(
    IFirmadorEnlaces firmador,
    IEstadoCuentas directorio,
    IHabitoRepository habitos,
    IPlanUsuarioRepository planes,
    IPerfilUsuarioRepository perfiles,
    IHabitoService servicioHabitos,
    TimeProvider? reloj = null) : ICheckinCorreoService
{
    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    public async Task<InfoCheckin> VerAsync(string token, CancellationToken cancellationToken = default)
    {
        var (habito, datos, _) = await ResolverAsync(token, cancellationToken);
        return new InfoCheckin(habito.Nombre, datos.FechaLocal);
    }

    public async Task EjecutarAsync(string token, CancellationToken cancellationToken = default)
    {
        var (habito, datos, zona) = await ResolverAsync(token, cancellationToken);
        var ahoraUtc = _reloj.GetUtcNow().UtcDateTime;

        // El avance cuenta para el día del enlace: a su mediodía local, o ahora si ese mediodía todavía no llegó.
        var mediodiaLocal = DateTime.SpecifyKind(datos.FechaLocal.ToDateTime(new TimeOnly(12, 0)), DateTimeKind.Unspecified);
        var fechaUtc = DateTime.SpecifyKind(
            new[] { ahoraUtc, TimeZoneInfo.ConvertTimeToUtc(mediodiaLocal, zona) }.Min(), DateTimeKind.Utc);

        try
        {
            await servicioHabitos.RegistrarAvanceAsync(
                habito.Id, 1, fechaUtc, datos.UsuarioId, false, OrigenRegistro.Correo,
                $"correo:{datos.FechaLocal:yyyy-MM-dd}", cancellationToken);
        }
        catch (ConflictoDeUnicidadException)
        {
            // Dos clics a la vez: el otro ganó y el avance ya está. Para la persona es lo mismo.
        }
    }

    private async Task<(Habito Habito, DatosEnlaceCheckin Datos, TimeZoneInfo Zona)> ResolverAsync(string token, CancellationToken cancellationToken)
    {
        var datos = firmador.LeerCheckin(token) ?? throw new EnlaceNoValidoException();
        if (!await directorio.CuentaActivaAsync(datos.UsuarioId, cancellationToken))
            throw new EnlaceNoValidoException();

        // El botón solo se ofrece en tareas de cantidad 1 de un plan en curso: lo demás se hace desde la app.
        var habito = await habitos.GetByIdAsync(datos.HabitoId, cancellationToken);
        if (habito is not { Estado: EstadoHabito.Activo, PlanUsuarioId: { } planId, CantidadObjetivo: 1m }
            || habito.UsuarioId != datos.UsuarioId)
            throw new EnlaceNoValidoException();
        var plan = await planes.ObtenerAsync(planId, datos.UsuarioId, cancellationToken);
        if (plan is not { Estado: EstadoPlanUsuario.Activo })
            throw new EnlaceNoValidoException();

        var zona = Seguimiento.Zona(await perfiles.ObtenerAsync(datos.UsuarioId, cancellationToken));
        var hoyLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(_reloj.GetUtcNow().UtcDateTime, zona));
        if (datos.FechaLocal > hoyLocal)
            throw new EnlaceNoValidoException();
        return (habito, datos, zona);
    }
}
