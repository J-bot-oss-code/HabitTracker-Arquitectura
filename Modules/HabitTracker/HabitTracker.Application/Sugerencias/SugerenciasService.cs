using HabitTracker.Application.Interfaces;
using HabitTracker.Domain.Excepciones;
using HabitTracker.Domain.Sugerencias;

namespace HabitTracker.Application.Sugerencias;

public sealed record SugerenciaDto(Guid Id, TipoSugerencia Tipo, string Texto, DateTime CreadaEn, EstadoSugerencia Estado);

/// <summary>Lo que ve el Administrador: lo mismo, más quién la envió (solo su identificador: la cuenta vive en Control de Acceso).</summary>
public sealed record SugerenciaAdminDto(Guid Id, Guid UsuarioId, TipoSugerencia Tipo, string Texto, DateTime CreadaEn, EstadoSugerencia Estado);

/// <param name="Nuevas">Cuántas siguen sin leer, con cualquier filtro: el número del aviso del menú y del Resumen.</param>
public sealed record PaginaSugerenciasDto(int Total, int Pagina, int Tamano, int Nuevas, IReadOnlyList<SugerenciaAdminDto> Sugerencias);

public interface ISugerenciaRepository
{
    Task AgregarAsync(Sugerencia sugerencia, CancellationToken cancellationToken = default);
    Task<Sugerencia?> ObtenerAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Sugerencia>> ListarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);
    Task<int> ContarDesdeAsync(Guid usuarioId, DateTime desdeUtc, CancellationToken cancellationToken = default);

    /// <summary>Filtra, cuenta y pagina en la base, de la más reciente a la más antigua.</summary>
    Task<(int Total, IReadOnlyList<Sugerencia> Pagina)> ListarAsync(
        EstadoSugerencia? estado, int pagina, int tamano, CancellationToken cancellationToken = default);

    Task<int> ContarNuevasAsync(CancellationToken cancellationToken = default);
    Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}

public interface ISugerenciasService
{
    Task<SugerenciaDto> EnviarAsync(Guid usuarioId, TipoSugerencia tipo, string texto, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SugerenciaDto>> ListarMiasAsync(Guid usuarioId, CancellationToken cancellationToken = default);
    Task<PaginaSugerenciasDto> ListarAsync(EstadoSugerencia? estado, int pagina, int tamano, CancellationToken cancellationToken = default);
    Task<SugerenciaAdminDto> CambiarEstadoAsync(Guid id, EstadoSugerencia estado, CancellationToken cancellationToken = default);

    /// <summary>Para la descarga de datos y la baja de cuenta: lo que envió la persona.</summary>
    Task<IReadOnlyList<SugerenciaDto>> ExportarAsync(Guid usuarioId, CancellationToken cancellationToken = default);
    Task EliminarAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Quejas y propuestas de mejora (Estándar) y su bandeja (Administrador). La regla del texto y del estado vive en
/// <see cref="Sugerencia"/>; aquí, que nadie llene la bandeja: como mucho <see cref="MaximoPorDia"/> en 24 horas por persona.
/// </summary>
public sealed class SugerenciasService(ISugerenciaRepository sugerencias, IUnitOfWork unitOfWork, TimeProvider? reloj = null) : ISugerenciasService
{
    public const int MaximoPorDia = 5;
    public const int TamanoMaximo = 100;
    public const string CodigoLimite = "limite_sugerencias";
    public const string CodigoNoEncontrada = "sugerencia_no_encontrada";

    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    public async Task<SugerenciaDto> EnviarAsync(Guid usuarioId, TipoSugerencia tipo, string texto, CancellationToken cancellationToken = default)
    {
        ExigirIdentidad(usuarioId);
        var ahora = _reloj.GetUtcNow().UtcDateTime;
        var sugerencia = new Sugerencia(usuarioId, tipo, texto, ahora); // RD-07: se valida antes de tocar la base

        // shortcut: contar y luego insertar deja un hueco; dos envíos simultáneos pueden dejar 6 en vez de 5. Es un tope contra el
        // abuso, no un dato que se muestre, y el límite de peticiones por persona ya frena las ráfagas.
        if (await sugerencias.ContarDesdeAsync(usuarioId, ahora.AddDays(-1), cancellationToken) >= MaximoPorDia)
            throw new ReglaNegocioException(CodigoLimite, $"Ya nos escribiste {MaximoPorDia} veces hoy. Gracias: vuelve a escribirnos mañana.");

        await sugerencias.AgregarAsync(sugerencia, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Dto(sugerencia);
    }

    public async Task<IReadOnlyList<SugerenciaDto>> ListarMiasAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        ExigirIdentidad(usuarioId);
        return [.. (await sugerencias.ListarDeUsuarioAsync(usuarioId, cancellationToken)).Select(Dto)];
    }

    public async Task<PaginaSugerenciasDto> ListarAsync(EstadoSugerencia? estado, int pagina, int tamano, CancellationToken cancellationToken = default)
    {
        if (estado is { } e && !Enum.IsDefined(e))
            throw new ArgumentException("El estado no es válido.");
        if (pagina < 1)
            throw new ArgumentException("La página empieza en 1.");
        if (tamano is < 1 or > TamanoMaximo)
            throw new ArgumentException($"El tamaño de la página va de 1 a {TamanoMaximo}.");

        var (total, lista) = await sugerencias.ListarAsync(estado, pagina, tamano, cancellationToken);
        var nuevas = await sugerencias.ContarNuevasAsync(cancellationToken);
        return new PaginaSugerenciasDto(total, pagina, tamano, nuevas, [.. lista.Select(AdminDto)]);
    }

    public async Task<SugerenciaAdminDto> CambiarEstadoAsync(Guid id, EstadoSugerencia estado, CancellationToken cancellationToken = default)
    {
        var sugerencia = await sugerencias.ObtenerAsync(id, cancellationToken)
            ?? throw new RecursoNoEncontradoException(CodigoNoEncontrada, "La sugerencia no existe.");
        sugerencia.CambiarEstado(estado, _reloj.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return AdminDto(sugerencia);
    }

    public Task<IReadOnlyList<SugerenciaDto>> ExportarAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        ListarMiasAsync(usuarioId, cancellationToken);

    public async Task EliminarAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        ExigirIdentidad(usuarioId);
        await sugerencias.EliminarDeUsuarioAsync(usuarioId, cancellationToken);
    }

    private static void ExigirIdentidad(Guid usuarioId)
    {
        if (usuarioId == Guid.Empty)
            throw new UnauthorizedAccessException("Sesión inválida.");
    }

    private static SugerenciaDto Dto(Sugerencia s) =>
        new(s.Id, s.Tipo, s.Texto, DateTime.SpecifyKind(s.CreadaEnUtc, DateTimeKind.Utc), s.Estado);

    private static SugerenciaAdminDto AdminDto(Sugerencia s) =>
        new(s.Id, s.UsuarioId, s.Tipo, s.Texto, DateTime.SpecifyKind(s.CreadaEnUtc, DateTimeKind.Utc), s.Estado);
}
