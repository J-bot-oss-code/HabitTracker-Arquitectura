namespace AccessControl.Domain.Excepciones;

/// <summary>
/// Base de las reglas de Control de Acceso que se rechazan con un código estable (por ejemplo "cuenta_no_activa").
/// El código permite a un cliente distinguir el motivo sin depender del texto del mensaje (RF-CA-15, RF-CA-19).
/// Los mensajes los escribimos nosotros y nunca incluyen datos internos: es seguro mostrarlos (RD-08).
/// Deriva de InvalidOperationException para que cualquier capa que ya la trate como "rechazo de negocio" siga funcionando.
/// </summary>
public abstract class ControlAccesoException : InvalidOperationException
{
    public string Codigo { get; }

    protected ControlAccesoException(string codigo, string mensaje) : base(mensaje)
    {
        Codigo = codigo;
    }
}
