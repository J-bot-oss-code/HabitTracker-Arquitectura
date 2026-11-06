namespace AccessControl.Domain.Entities;

/// <summary>
/// Vínculo entre una cuenta y una identidad de un proveedor externo (Google). Lo que identifica a la persona en el
/// proveedor es el Subject (su id estable), nunca el correo: el correo puede cambiar, el Subject no.
/// </summary>
public class LoginExterno
{
    public const string ProveedorGoogle = "google";
    public const int LongitudMaximaSubject = 255;

    private static readonly string[] ProveedoresValidos = [ProveedorGoogle];

    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string Proveedor { get; private set; } = null!;
    public string Subject { get; private set; } = null!;
    public DateTime VinculadoEn { get; private set; }

    private LoginExterno() { } // Constructor privado para EF Core

    public LoginExterno(Guid usuarioId, string proveedor, string subject, DateTime ahora)
    {
        if (usuarioId == Guid.Empty)
        {
            throw new ArgumentException("El usuario no es válido.", nameof(usuarioId));
        }

        if (string.IsNullOrWhiteSpace(proveedor) || !ProveedoresValidos.Contains(proveedor))
        {
            throw new ArgumentException("El proveedor no es válido.", nameof(proveedor));
        }

        if (string.IsNullOrWhiteSpace(subject) || subject.Length > LongitudMaximaSubject)
        {
            throw new ArgumentException("El identificador del proveedor no es válido.", nameof(subject));
        }

        Id = Guid.NewGuid();
        UsuarioId = usuarioId;
        Proveedor = proveedor;
        Subject = subject;
        VinculadoEn = ahora;
    }
}
