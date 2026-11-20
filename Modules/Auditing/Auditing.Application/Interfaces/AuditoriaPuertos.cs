using Auditing.Application.DTOs;
using Auditing.Domain;

namespace Auditing.Application.Interfaces;

/// <summary>
/// Filtros ya validados y normalizados (el rango en instantes UTC, con el final exclusivo) que el repositorio traduce a SQL.
/// </summary>
public sealed record FiltroAuditoria(
    string? Usuario, string? Entidad, string? EntidadId, string? Accion, DateTime? DesdeUtc, DateTime? HastaUtcExclusivo);

public sealed record PaginaRegistros(int Total, IReadOnlyList<RegistroAuditoria> Registros);

/// <summary>
/// Almacén del historial. Solo agrega y consulta: no hay ninguna operación para editar ni para borrar (RF-AUD-06).
/// </summary>
public interface IRegistroAuditoriaRepository
{
    Task AgregarAsync(RegistroAuditoria registro, CancellationToken cancellationToken = default);

    /// <summary>Filtra, cuenta y pagina en la base de datos, de lo más reciente a lo más antiguo.</summary>
    Task<PaginaRegistros> ConsultarAsync(FiltroAuditoria filtro, int pagina, int tamano, CancellationToken cancellationToken = default);
}

public interface IRegistroAuditoriaService
{
    /// <summary>Guarda un registro con la fecha y hora de este momento (UTC). Falla si falta cualquier dato (RF-AUD-01).</summary>
    Task RegistrarAsync(
        Guid usuarioId, string usuario, string accion, string entidad, string entidadId,
        string valorAnterior, string valorNuevo, CancellationToken cancellationToken = default);

    /// <summary>Consulta filtrada y paginada (RF-AUD-07). Un filtro inválido lanza <see cref="ArgumentException"/>.</summary>
    Task<PaginaAuditoriaDto> ConsultarAsync(ConsultaAuditoria consulta, CancellationToken cancellationToken = default);
}
