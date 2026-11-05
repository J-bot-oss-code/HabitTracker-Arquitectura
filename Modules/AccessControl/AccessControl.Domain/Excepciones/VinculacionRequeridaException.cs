namespace AccessControl.Domain.Excepciones;

/// <summary>
/// El correo de la cuenta de Google ya tiene una cuenta con contraseña. No se vincula sola: hay que confirmar la
/// contraseña (POST /api/auth/google/vincular). Se responde 409. Quien llega aquí controla ese correo en Google
/// (el proveedor lo verificó), así que decir que la cuenta existe no filtra nada a un tercero.
/// </summary>
public sealed class VinculacionRequeridaException : ControlAccesoException
{
    public VinculacionRequeridaException()
        : base("vinculacion_requerida", "Ya existe una cuenta con este correo. Confirma tu contraseña para vincular Google.") { }
}
