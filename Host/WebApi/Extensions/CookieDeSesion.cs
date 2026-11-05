using AccessControl.Application.Service;

namespace WebApi.Extensions;

/// <summary>
/// Cookie del refresh token: HttpOnly (el JavaScript de la página no puede leerla, así un XSS no la roba), Secure, y
/// restringida a /api/auth (el resto de la API nunca la recibe). Establecer y borrar usan exactamente los mismos
/// atributos: si difieren, el navegador no reconoce la cookie y no la borra.
/// </summary>
public static class CookieDeSesion
{
    public static string? Leer(HttpRequest peticion) =>
        peticion.Cookies.TryGetValue(OpcionesSesion.NombreCookie, out var valor) && !string.IsNullOrWhiteSpace(valor) ? valor : null;

    public static void Establecer(HttpResponse respuesta, OpcionesSesion opciones, string refreshToken, DateTime expiraEnUtc)
    {
        var cookie = Atributos(opciones);
        cookie.Expires = new DateTimeOffset(DateTime.SpecifyKind(expiraEnUtc, DateTimeKind.Utc));
        respuesta.Cookies.Append(OpcionesSesion.NombreCookie, refreshToken, cookie);
    }

    public static void Borrar(HttpResponse respuesta, OpcionesSesion opciones) =>
        respuesta.Cookies.Delete(OpcionesSesion.NombreCookie, Atributos(opciones));

    public static SameSiteMode SameSite(OpcionesSesion opciones) =>
        Enum.Parse<SameSiteMode>(opciones.CookieSameSite, ignoreCase: true);

    private static CookieOptions Atributos(OpcionesSesion opciones) => new()
    {
        HttpOnly = true,
        Secure = opciones.CookieSegura,
        SameSite = SameSite(opciones),
        Path = OpcionesSesion.RutaCookie,
        IsEssential = true
    };
}
