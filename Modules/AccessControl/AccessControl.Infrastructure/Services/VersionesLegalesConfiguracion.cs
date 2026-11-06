using AccessControl.Application.Interfaces;
using AccessControl.Application.Service;
using Microsoft.Extensions.Configuration;

namespace AccessControl.Infrastructure.Services;

/// <summary>Versiones legales vigentes leídas de Legal:VersionTerminos y Legal:VersionPrivacidad (por defecto, las del frontend).</summary>
public sealed class VersionesLegalesConfiguracion : IVersionesLegales
{
    public string Terminos { get; }
    public string Privacidad { get; }

    public VersionesLegalesConfiguracion(IConfiguration configuracion)
    {
        Terminos = Leer(configuracion, "Legal:VersionTerminos", VersionesLegales.PorDefecto.Terminos);
        Privacidad = Leer(configuracion, "Legal:VersionPrivacidad", VersionesLegales.PorDefecto.Privacidad);
    }

    private static string Leer(IConfiguration configuracion, string clave, string porDefecto)
    {
        var valor = configuracion[clave] ?? porDefecto;

        // La columna admite 20 caracteres: una versión vacía o más larga es un error de configuración, no un dato de usuario.
        if (string.IsNullOrWhiteSpace(valor) || valor.Length > 20)
        {
            throw new InvalidOperationException($"La configuración {clave} debe tener entre 1 y 20 caracteres.");
        }

        return valor.Trim();
    }
}
