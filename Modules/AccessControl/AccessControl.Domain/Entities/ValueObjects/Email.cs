using System.ComponentModel.DataAnnotations;

namespace AccessControl.Domain.Entities.ValueObjects;

public sealed class Email
{
    private static readonly EmailAddressAttribute Validador = new();

    public string Valor { get; private set; } = string.Empty;

    private Email() { }

    public Email(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("El correo electrónico no puede estar vacío.", nameof(email));
        }

        var normalizado = email.Trim();
        if (normalizado.Length > 150 || !Validador.IsValid(normalizado))
        {
            throw new ArgumentException("El formato del correo electrónico no es válido.", nameof(email));
        }

        Valor = normalizado.ToLowerInvariant();
    }

    /// <summary>Correo apto para escribir en un log: "persona@ejemplo.com" se muestra como "p***@ejemplo.com".</summary>
    public string Enmascarado() => Enmascarar(Valor);

    /// <summary>
    /// Enmascara un correo sin necesidad de validarlo (sirve también para texto que aún no pasó por el objeto de valor).
    /// Un correo es un dato personal: los logs no deben guardarlo completo (RD-08).
    /// </summary>
    public static string Enmascarar(string? correo)
    {
        if (string.IsNullOrWhiteSpace(correo))
        {
            return "***";
        }

        var arroba = correo.IndexOf('@');
        return arroba <= 0
            ? "***"
            : $"{correo[0]}***{correo[arroba..]}";
    }

    public static implicit operator string(Email email) => email.Valor;

    public override bool Equals(object? obj) => obj is Email other && Valor == other.Valor;

    public override int GetHashCode() => Valor.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Valor;
}
