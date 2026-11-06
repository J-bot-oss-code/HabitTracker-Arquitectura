using AccessControl.Application.Interfaces;

namespace AccessControl.Application.Service;

/// <summary>
/// Versiones vigentes por defecto. Coinciden con las que muestra el frontend (VERSION_TERMINOS y VERSION_PRIVACIDAD en
/// frontend/src/features/publico/legal.ts). Se cambian con Legal:VersionTerminos y Legal:VersionPrivacidad.
/// </summary>
public sealed record VersionesLegales(string Terminos, string Privacidad) : IVersionesLegales
{
    public static VersionesLegales PorDefecto { get; } = new("v2.1", "v1.4");
}
