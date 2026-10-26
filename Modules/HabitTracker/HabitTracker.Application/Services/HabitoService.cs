using HabitTracker.Application.DTOs;
using HabitTracker.Application.Interfaces;
using HabitTracker.Domain.Entities;

namespace HabitTracker.Application.Services;

public sealed class HabitoService(
    IHabitoRepository habitoRepository,
    IUnitOfWork unitOfWork,
    IPermisoService permisoService)
{
    public async Task<Guid> CrearHabitoAsync(
        CrearHabitoCommand command,
        Guid usuarioActualId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Nombre))
            throw new ArgumentException("El nombre es obligatorio.", nameof(command.Nombre));

        if (string.IsNullOrWhiteSpace(command.Unidad))
            throw new ArgumentException("La unidad es obligatoria.", nameof(command.Unidad));

        if (command.CantidadObjetivo <= 0)
            throw new ArgumentException("La cantidad objetivo debe ser positiva.", nameof(command.CantidadObjetivo));

        var habito = new Habito(
            usuarioActualId,
            command.Nombre,
            command.Periodicidad,
            command.CantidadObjetivo,
            command.Unidad,
            Guid.NewGuid());

        await habitoRepository.AddAsync(habito, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return habito.Id;
    }

    public async Task<IReadOnlyList<HabitoDto>> ConsultarHabitosAsync(
        Guid usuarioActualId,
        CancellationToken cancellationToken = default)
    {
        var habitos = await habitoRepository.GetByUsuarioIdAsync(usuarioActualId, cancellationToken);

        return habitos
            .Select(MapToDto)
            .ToList();
    }

    public async Task CambiarEstadoAsync(
        Guid habitoId,
        EstadoHabito nuevoEstado,
        Guid usuarioActualId,
        CancellationToken cancellationToken = default)
    {
        var habito = await ObtenerHabitoPropietarioAsync(habitoId, usuarioActualId, cancellationToken);

        if (nuevoEstado == EstadoHabito.Completado && !habito.CumpleMetaFinal)
            throw new InvalidOperationException("No se alcanzó la meta del período.");

        switch (nuevoEstado)
        {
            case EstadoHabito.Activo:
                habito.Activar(usuarioActualId);
                break;
            case EstadoHabito.Pausado:
                habito.Pausar(usuarioActualId);
                break;
            case EstadoHabito.Completado:
                habito.Completar(usuarioActualId);
                break;
            default:
                throw new InvalidOperationException($"La transición a {nuevoEstado} no es válida desde {habito.Estado}.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RegistrarAvanceAsync(
        Guid habitoId,
        decimal cantidad,
        DateTime fechaUtc,
        Guid usuarioActualId,
        CancellationToken cancellationToken = default)
    {
        if (cantidad <= 0)
            throw new ArgumentException("La cantidad debe ser positiva.", nameof(cantidad));

        if (fechaUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("La fecha debe ser UTC.", nameof(fechaUtc));

        var habito = await ObtenerHabitoPropietarioAsync(habitoId, usuarioActualId, cancellationToken);
        habito.RegistrarAvance(usuarioActualId, cantidad, fechaUtc);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ReconfigurarHabitoAsync(
        Guid habitoId,
        Periodicidad periodicidad,
        decimal cantidadObjetivo,
        string unidad,
        Guid usuarioActualId,
        string correoSolicitante,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(correoSolicitante))
            throw new ArgumentException("El correo del solicitante es obligatorio.", nameof(correoSolicitante));

        if (string.IsNullOrWhiteSpace(unidad))
            throw new ArgumentException("La unidad es obligatoria.", nameof(unidad));

        if (cantidadObjetivo <= 0)
            throw new ArgumentException("La cantidad objetivo debe ser positiva.", nameof(cantidadObjetivo));

        var habito = await ObtenerHabitoPropietarioAsync(habitoId, usuarioActualId, cancellationToken);

        var tienePermiso = await permisoService.TienePermisoEfectivoAsync(
            correoSolicitante,
            "Habits.AdvancedReconfig",
            cancellationToken);

        if (!tienePermiso)
            throw new UnauthorizedAccessException("El usuario no tiene aplicado el permiso Habits.AdvancedReconfig.");

        habito.Reconfigurar(usuarioActualId, periodicidad, cantidadObjetivo, unidad);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Habito> ObtenerHabitoPropietarioAsync(
        Guid habitoId,
        Guid usuarioActualId,
        CancellationToken cancellationToken)
    {
        var habito = await habitoRepository.GetByIdAsync(habitoId, cancellationToken)
            ?? throw new InvalidOperationException("El hábito no existe.");

        if (habito.UsuarioId != usuarioActualId)
            throw new UnauthorizedAccessException("El usuario no es el propietario del hábito.");

        return habito;
    }

    private static HabitoDto MapToDto(Habito habito) => new(
        habito.Id,
        habito.Nombre,
        habito.Periodicidad,
        habito.CantidadObjetivo,
        habito.Unidad,
        habito.Estado,
        habito.CumpleMetaFinal,
        habito.FechaInicioCiclo,
        habito.Registros.Select(r => new RegistroAvanceDto(r.Id, r.Cantidad, r.Fecha, r.Unidad)).ToList());
}