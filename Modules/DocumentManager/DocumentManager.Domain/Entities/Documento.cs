using DocumentManager.Domain.Results;

namespace DocumentManager.Domain.Entities;

/// <summary>
/// Registro de un archivo subido por un usuario. El archivo físico vive en el
/// almacenamiento; esta entidad guarda sus metadatos (RF-DOC-01) y la marca de
/// eliminado (RF-DOC-04: el borrado es lógico, nunca físico).
/// </summary>
public class Documento
{
    public const int LongitudMaximaNombre = 255;
    public const int LongitudMaximaTipo = 150;
    public const int LongitudMaximaCorreo = 256;

    public Guid Id { get; private set; }
    public string NombreOriginal { get; private set; } = string.Empty;
    public string TipoContenido { get; private set; } = string.Empty;
    public long TamanoBytes { get; private set; }
    public DateTime FechaSubidaUtc { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string CorreoUsuario { get; private set; } = string.Empty;

    /// <summary>
    /// Nombre con el que el archivo se guarda en disco. Es un GUID generado aquí,
    /// nunca el nombre que escribió el usuario: así un nombre como "../../x" no puede
    /// convertirse en una ruta del sistema de archivos.
    /// </summary>
    public string NombreAlmacenado { get; private set; } = string.Empty;

    public bool Eliminado { get; private set; }
    public DateTime? FechaEliminacionUtc { get; private set; }

    private Documento()
    {
    }

    public Documento(string nombreOriginal, string tipoContenido, long tamanoBytes, Guid usuarioId, string correoUsuario)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombreOriginal);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoContenido);
        ArgumentException.ThrowIfNullOrWhiteSpace(correoUsuario);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tamanoBytes);

        if (usuarioId == Guid.Empty)
        {
            throw new ArgumentException("El usuario que sube el documento es obligatorio.", nameof(usuarioId));
        }

        ValidarLongitud(nombreOriginal, LongitudMaximaNombre, nameof(nombreOriginal));
        ValidarLongitud(tipoContenido, LongitudMaximaTipo, nameof(tipoContenido));
        ValidarLongitud(correoUsuario, LongitudMaximaCorreo, nameof(correoUsuario));

        Id = Guid.NewGuid();
        NombreOriginal = nombreOriginal.Trim();
        TipoContenido = tipoContenido.Trim();
        TamanoBytes = tamanoBytes;
        UsuarioId = usuarioId;
        CorreoUsuario = correoUsuario.Trim();
        NombreAlmacenado = Guid.NewGuid().ToString("N");
        FechaSubidaUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Regla de acceso en un solo punto (RD-06): solo el dueño o un Administrador.
    /// La usan por igual la consulta, la descarga y la eliminación.
    /// Solo mira la propiedad: no considera <see cref="Eliminado"/>. El servicio de
    /// aplicación trata un documento eliminado como inexistente (404) antes de llegar aquí.
    /// </summary>
    public bool PuedeAcceder(Guid usuarioId, bool esAdministrador) =>
        esAdministrador || usuarioId == UsuarioId;

    /// <summary>
    /// Único punto donde cambia la marca de eliminado (RD-04). No borra nada:
    /// el registro y el archivo siguen existiendo (RF-DOC-04).
    /// </summary>
    public DomainResult Eliminar(DateTime fechaEliminacionUtc)
    {
        if (fechaEliminacionUtc.Kind != DateTimeKind.Utc)
        {
            return DomainResult.Failure(DomainError.FechaEliminacionDebeSerUtc);
        }

        if (Eliminado)
        {
            return DomainResult.Failure(DomainError.DocumentoYaEliminado);
        }

        Eliminado = true;
        FechaEliminacionUtc = fechaEliminacionUtc;

        return DomainResult.Success();
    }

    private static void ValidarLongitud(string valor, int longitudMaxima, string parametro)
    {
        if (valor.Trim().Length > longitudMaxima)
        {
            throw new ArgumentException($"El valor no puede superar {longitudMaxima} caracteres.", parametro);
        }
    }
}
