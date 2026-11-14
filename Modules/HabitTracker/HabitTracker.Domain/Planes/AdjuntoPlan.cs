namespace HabitTracker.Domain.Planes;

/// <summary>
/// Un archivo de la persona (del Manejador de documentos) vinculado a uno de sus planes. El archivo sigue siendo suyo y sigue en
/// «Mis archivos»; este vínculo solo dice que el plan lo usa. Mientras el plan está en curso, eliminar el archivo pide un
/// permiso por documento (<see cref="PermisosDocumentos"/>).
/// </summary>
public sealed class AdjuntoPlan
{
    public const int MaximoPorPlan = 10;

    private AdjuntoPlan() { } // EF Core

    public AdjuntoPlan(Guid planUsuarioId, Guid usuarioId, Guid documentoId, DateTime ahoraUtc)
    {
        if (planUsuarioId == Guid.Empty || usuarioId == Guid.Empty || documentoId == Guid.Empty)
            throw new ArgumentException("El plan, la persona y el documento son obligatorios.");
        if (ahoraUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("La fecha debe ser UTC.", nameof(ahoraUtc));

        Id = Guid.NewGuid();
        PlanUsuarioId = planUsuarioId;
        UsuarioId = usuarioId;
        DocumentoId = documentoId;
        AdjuntadoEnUtc = ahoraUtc;
    }

    public Guid Id { get; private set; }
    public Guid PlanUsuarioId { get; private set; }
    public Guid UsuarioId { get; private set; }
    public Guid DocumentoId { get; private set; }
    public DateTime AdjuntadoEnUtc { get; private set; }
}

/// <summary>Las claves de permiso de los documentos. Una por documento, para que el Administrador vea cuál se pide.</summary>
public static class PermisosDocumentos
{
    public const string Prefijo = "Documents.DeleteLinked.";

    public static string EliminarVinculado(Guid documentoId) => Prefijo + documentoId.ToString("N");
}
