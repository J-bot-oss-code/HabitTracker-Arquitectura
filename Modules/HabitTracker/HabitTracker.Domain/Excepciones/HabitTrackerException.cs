namespace HabitTracker.Domain.Excepciones;

/// <summary>
/// Base de los rechazos del módulo de negocio con un código estable (por ejemplo <c>fecha_futura</c>). El middleware de la
/// API la traduce a <c>ProblemDetails</c> con la extensión <c>codigo</c>, para que el cliente no dependa del texto del
/// mensaje (RD-08). Hereda de <see cref="InvalidOperationException"/>, como <c>ControlAccesoException</c>.
/// </summary>
public abstract class HabitTrackerException : InvalidOperationException
{
    protected HabitTrackerException(string codigo, string mensaje) : base(mensaje)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            throw new ArgumentException("El código del error es obligatorio.", nameof(codigo));

        Codigo = codigo;
    }

    /// <summary>Código estable en snake_case que viaja en <c>ProblemDetails.codigo</c>.</summary>
    public string Codigo { get; }
}

/// <summary>El recurso pedido no existe (HTTP 404). Ejemplo: <c>habito_no_encontrado</c>.</summary>
public sealed class RecursoNoEncontradoException(string codigo, string mensaje) : HabitTrackerException(codigo, mensaje)
{
    public const string HabitoNoEncontrado = "habito_no_encontrado";
    /// <summary>La persona aún no completó el onboarding: el frontend la lleva a <c>/bienvenida</c> (RequierePerfil).</summary>
    public const string PerfilNoEncontrado = "perfil_no_encontrado";
    /// <summary>No hay un plan activo del catálogo con ese slug.</summary>
    public const string PlanNoEncontrado = "plan_no_encontrado";
    /// <summary>No existe un plan en curso o terminado de esta persona con ese id (o es de otra persona).</summary>
    public const string PlanUsuarioNoEncontrado = "plan_usuario_no_encontrado";
    /// <summary>El archivo no existe, fue eliminado o no es de esta persona (para el vínculo con un plan).</summary>
    public const string DocumentoNoEncontrado = "documento_no_encontrado";
}

/// <summary>Una regla de negocio rechaza la operación (HTTP 400). Ejemplos: <c>fecha_futura</c>, <c>meta_no_encontrada</c>.</summary>
public sealed class ReglaNegocioException(string codigo, string mensaje) : HabitTrackerException(codigo, mensaje)
{
    public const string FechaFutura = "fecha_futura";
    public const string MetaNoEncontrada = "meta_no_encontrada";
    /// <summary>La operación necesita el perfil (por ejemplo, fijar un peso objetivo) y todavía no existe.</summary>
    public const string PerfilRequerido = "perfil_requerido";
    /// <summary>El plan no es para la persona (edad, sexo, intensidad, alérgenos…). Lo decide <c>ReglasSeguridadPlanes</c>.</summary>
    public const string PlanNoApto = "plan_no_apto";
    /// <summary>Faltan bloques del cuestionario de salud que el plan necesita.</summary>
    public const string CuestionarioIncompleto = "cuestionario_incompleto";
    public const string LimitePlanes = "limite_planes";
    public const string PlanYaActivo = "plan_ya_activo";
    public const string LimiteExtensiones = "limite_extensiones";
    public const string TransicionPlanInvalida = "transicion_plan_invalida";
    public const string PlanAunNoTermina = "plan_aun_no_termina";
    public const string AjusteNoDisponible = "ajuste_no_disponible";
    public const string VersionMinimaNoDisponible = "version_minima_no_disponible";
    /// <summary>Solo se adjuntan archivos a un plan en curso.</summary>
    public const string PlanNoEnCurso = "plan_no_en_curso";
    public const string AdjuntoDuplicado = "adjunto_duplicado";
    public const string LimiteAdjuntos = "limite_adjuntos";
}

/// <summary>
/// Falta el consentimiento <c>datosSalud</c> para guardar un dato de salud (HTTP 409, <c>consentimiento_salud_requerido</c>).
/// El cliente lo pide y repite: es el contrato que ya esperaban los mocks del frontend.
/// </summary>
public sealed class ConsentimientoSaludRequeridoException()
    : HabitTrackerException(ConsentimientoSaludRequerido, "Falta el consentimiento para usar tus datos de salud.")
{
    public const string ConsentimientoSaludRequerido = "consentimiento_salud_requerido";
}

/// <summary>
/// La operación necesita un permiso aprobado por un Administrador (HTTP 409, <c>documento_requiere_permiso</c>): quitar o
/// eliminar un archivo que está en un plan en curso. El cliente ofrece pedir el permiso.
/// </summary>
public sealed class PermisoRequeridoException()
    : HabitTrackerException(DocumentoRequierePermiso, "Este archivo está en un plan en curso: pide permiso para eliminarlo.")
{
    public const string DocumentoRequierePermiso = "documento_requiere_permiso";
}
