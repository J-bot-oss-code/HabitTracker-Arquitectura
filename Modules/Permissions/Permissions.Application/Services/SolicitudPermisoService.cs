using Permissions.Application.DTOs;
using Permissions.Application.Interfaces;
using Permissions.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Permissions.Application.Services;

public sealed class SolicitudPermisoService(
    ISolicitudPermisoRepository repository,
    ILogger<SolicitudPermisoService> logger) : ISolicitudPermisoService
{
    public async Task<Guid> CrearAsync(CrearSolicitudPermisoCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Solicitante))
        {
            throw new ArgumentException("El solicitante es obligatorio.", nameof(command));
        }

        if (string.IsNullOrWhiteSpace(command.PermisoSolicitado))
        {
            throw new ArgumentException("El permiso solicitado es obligatorio.", nameof(command));
        }

        var solicitud = new SolicitudPermiso(command.Solicitante, command.PermisoSolicitado);

        await repository.AddAsync(solicitud, cancellationToken);

        logger.LogInformation("Solicitud de permiso {SolicitudId} creada por {Solicitante}.", solicitud.Id, solicitud.Solicitante);

        return solicitud.Id;
    }

    public async Task<IReadOnlyList<SolicitudPermisoDto>> ConsultarAsync(ObtenerSolicitudesQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (EsAdministrador(query.RolUsuario))
        {
            var solicitudes = query.SoloPendientes
                ? await repository.GetPendingAsync(cancellationToken)
                : await repository.GetAllAsync(cancellationToken);

            return solicitudes.Select(ToDto).ToList();
        }

        if (EsEstandar(query.RolUsuario))
        {
            var propias = await repository.GetBySolicitanteAsync(
                query.UsuarioConsulta,
                query.SoloPendientes,
                cancellationToken);

            return propias
                .Select(ToDto)
                .ToList();
        }

        throw new UnauthorizedAccessException("Rol no reconocido para consultar solicitudes.");
    }

    public async Task<SolicitudPermisoDto> ObtenerPorIdAsync(Guid id, string usuarioConsulta, string rolUsuario, CancellationToken cancellationToken = default)
    {
        var solicitud = await repository.GetByIdAsync(id, cancellationToken);

        if (solicitud is null)
        {
            throw new InvalidOperationException("La solicitud no existe.");
        }

        if (EsAdministrador(rolUsuario) || (EsEstandar(rolUsuario) && EsPropietario(solicitud, usuarioConsulta)))
        {
            return ToDto(solicitud);
        }

        throw new UnauthorizedAccessException("No tiene permiso para consultar esta solicitud.");
    }

    public async Task AprobarAsync(AprobarSolicitudCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var solicitud = await repository.GetByIdAsync(command.SolicitudId, cancellationToken);

        if (solicitud is null)
        {
            throw new InvalidOperationException("La solicitud no existe.");
        }

        var resultado = solicitud.Aprobar(command.Aprobador, DateTime.UtcNow);

        if (!resultado.IsSuccess)
        {
            throw new InvalidOperationException(resultado.ErrorMessage ?? "La solicitud no pudo aprobarse.");
        }

        await repository.UpdateAsync(solicitud, cancellationToken);

        logger.LogInformation("Solicitud {SolicitudId} aprobada por {Aprobador}.", solicitud.Id, command.Aprobador);
    }

    public async Task RechazarAsync(RechazarSolicitudCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var solicitud = await repository.GetByIdAsync(command.SolicitudId, cancellationToken);

        if (solicitud is null)
        {
            throw new InvalidOperationException("La solicitud no existe.");
        }

        var resultado = solicitud.Rechazar(command.Aprobador, command.MotivoRechazo, DateTime.UtcNow);

        if (!resultado.IsSuccess)
        {
            throw new InvalidOperationException(resultado.ErrorMessage ?? "La solicitud no pudo rechazarse.");
        }

        await repository.UpdateAsync(solicitud, cancellationToken);

        logger.LogInformation("Solicitud {SolicitudId} rechazada por {Aprobador}.", solicitud.Id, command.Aprobador);
    }

    public Task<bool> TienePermisoEfectivoAsync(
        string solicitante,
        string permisoSolicitado,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solicitante);
        ArgumentException.ThrowIfNullOrWhiteSpace(permisoSolicitado);

        if (solicitante.Trim().Length > 256 || permisoSolicitado.Trim().Length > 256)
        {
            throw new ArgumentException("El solicitante o el permiso excede la longitud permitida.");
        }

        return repository.TienePermisoAplicadoAsync(
            solicitante,
            permisoSolicitado,
            cancellationToken);
    }

    public async Task<IReadOnlyList<SolicitudPermisoDto>> EjecutarAccionProtegidaAsync(
        string solicitante,
        string permisoSolicitado,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solicitante);
        ArgumentException.ThrowIfNullOrWhiteSpace(permisoSolicitado);

        var identidad = solicitante.Trim();
        var permiso = permisoSolicitado.Trim();
        if (identidad.Length > 256 || permiso.Length > 256)
        {
            throw new ArgumentException("El solicitante o el permiso excede la longitud permitida.");
        }

        var tienePermiso = await repository.TienePermisoAplicadoAsync(identidad, permiso, cancellationToken);
        if (!tienePermiso)
        {
            throw new UnauthorizedAccessException("El usuario no tiene aplicado el permiso solicitado.");
        }

        var historial = await repository.GetBySolicitanteAsync(
            identidad,
            soloPendientes: false,
            cancellationToken);
        logger.LogInformation("Historial protegido consultado por {Solicitante}.", identidad);
        return historial.Select(ToDto).ToList();
    }

    private static bool EsAdministrador(string rol) =>
        string.Equals(rol, "Administrador", StringComparison.OrdinalIgnoreCase);

    private static bool EsEstandar(string rol) =>
        string.Equals(rol, "Estándar", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(rol, "Estandar", StringComparison.OrdinalIgnoreCase);

    private static bool EsPropietario(SolicitudPermiso solicitud, string usuario) =>
        string.Equals(solicitud.Solicitante, usuario?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static SolicitudPermisoDto ToDto(SolicitudPermiso solicitud) => new(
        solicitud.Id,
        solicitud.Solicitante,
        solicitud.PermisoSolicitado,
        solicitud.Estado.ToString(),
        solicitud.Aprobador,
        solicitud.FechaCreacion,
        solicitud.FechaResolucion,
        solicitud.MotivoRechazo);
}
