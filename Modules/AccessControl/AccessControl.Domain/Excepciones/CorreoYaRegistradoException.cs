namespace AccessControl.Domain.Excepciones;

/// <summary>Ya existe una cuenta con ese correo (RF-CA-01).</summary>
public sealed class CorreoYaRegistradoException : ControlAccesoException
{
    public CorreoYaRegistradoException() : base("correo_ya_registrado", "El correo ya esta registrado") { }
}
