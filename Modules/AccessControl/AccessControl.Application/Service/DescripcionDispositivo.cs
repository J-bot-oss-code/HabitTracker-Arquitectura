using AccessControl.Domain.Entities;

namespace AccessControl.Application.Service;

/// <summary>
/// Resume el User-Agent en «navegador en sistema» (p. ej. «Chrome en Windows») antes de guardar la sesión: basta para que la
/// persona reconozca sus sesiones y no se guarda la cadena completa (versiones exactas, modelo del equipo), que la identifica más.
/// </summary>
public static class DescripcionDispositivo
{
    public const string Desconocido = SesionUsuario.DispositivoDesconocido;

    // El orden importa: Edge y Opera dicen «Chrome», y Chrome dice «Safari».
    private static readonly (string Marca, string Nombre)[] Navegadores =
    [
        ("Edg", "Edge"), ("OPR", "Opera"), ("SamsungBrowser", "Samsung Internet"), ("Firefox", "Firefox"),
        ("FxiOS", "Firefox"), ("CriOS", "Chrome"), ("Chrome", "Chrome"), ("Safari", "Safari"),
    ];

    // iPhone y Android antes que Mac y Linux: sus User-Agent también los mencionan.
    private static readonly (string Marca, string Nombre)[] Sistemas =
    [
        ("iPhone", "iPhone"), ("iPad", "iPad"), ("Android", "Android"), ("Windows", "Windows"),
        ("Mac OS X", "Mac"), ("Macintosh", "Mac"), ("CrOS", "ChromeOS"), ("Linux", "Linux"),
    ];

    public static string De(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return Desconocido;
        }

        var navegador = Buscar(Navegadores, userAgent);
        var sistema = Buscar(Sistemas, userAgent);

        return (navegador, sistema) switch
        {
            (null, null) => Desconocido,
            (not null, null) => navegador,
            (null, not null) => $"Navegador en {sistema}",
            _ => $"{navegador} en {sistema}",
        };
    }

    private static string? Buscar((string Marca, string Nombre)[] lista, string userAgent) =>
        lista.FirstOrDefault(x => userAgent.Contains(x.Marca, StringComparison.Ordinal)).Nombre;
}
