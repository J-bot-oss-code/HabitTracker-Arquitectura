using HabitTracker.Application.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace HabitTracker.Infrastructure.Seguridad;

/// <summary>
/// <see cref="ICifradorDatos"/> con ASP.NET Core Data Protection (AES-256-CBC + HMACSHA256, con rotación de claves). Las
/// claves las configura el Host (<c>DATAPROTECTION_KEYS_PATH</c>); si se pierden, los datos cifrados no se pueden leer,
/// por eso el directorio va en un volumen persistente con respaldo.
/// </summary>
public sealed class CifradorDataProtection(IDataProtectionProvider proveedor) : ICifradorDatos
{
    private const string Prefijo = "HabitTracker.";

    public string Cifrar(string texto, string proposito)
    {
        ArgumentNullException.ThrowIfNull(texto);
        return Protector(proposito).Protect(texto);
    }

    public string Descifrar(string cifrado, string proposito)
    {
        ArgumentException.ThrowIfNullOrEmpty(cifrado);
        return Protector(proposito).Unprotect(cifrado);
    }

    private IDataProtector Protector(string proposito)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(proposito);
        return proveedor.CreateProtector(Prefijo + proposito);
    }
}
