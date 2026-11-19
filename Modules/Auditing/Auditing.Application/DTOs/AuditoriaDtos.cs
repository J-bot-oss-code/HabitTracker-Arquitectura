namespace Auditing.Application.DTOs;

/// <summary>
/// Lo que el Administrador pide al historial (RF-AUD-07). Todos los filtros son opcionales y se combinan: un registro tiene que
/// cumplirlos todos. <c>Desde</c> y <c>Hasta</c> son días UTC y ambos se incluyen.
/// </summary>
public sealed record ConsultaAuditoria(
    string? Usuario = null,
    string? Entidad = null,
    string? EntidadId = null,
    string? Accion = null,
    DateOnly? Desde = null,
    DateOnly? Hasta = null,
    int Pagina = 1,
    int Tamano = 50);

/// <summary>Un registro del historial tal como lo ve el Administrador.</summary>
public sealed record RegistroAuditoriaDto(
    Guid Id,
    DateTime Fecha,
    Guid UsuarioId,
    string Actor,
    string Accion,
    string Entidad,
    string EntidadId,
    string ValorAnterior,
    string ValorNuevo);

/// <summary>Una página del historial. <c>Total</c> cuenta todos los que cumplen los filtros, no solo los de esta página.</summary>
public sealed record PaginaAuditoriaDto(int Total, int Pagina, int Tamano, IReadOnlyList<RegistroAuditoriaDto> Registros);
