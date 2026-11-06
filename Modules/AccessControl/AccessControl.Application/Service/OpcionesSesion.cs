namespace AccessControl.Application.Service;

/// <summary>
/// Configuración de las sesiones (sección "Sesion"). El token de acceso dura poco (JwtSettings:MinutosExpiracion) y el
/// refresh token, que vive en una cookie HttpOnly, permite renovarlo sin volver a pedir la contraseña.
/// </summary>
public sealed class OpcionesSesion
{
    public const string Seccion = "Sesion";

    /// <summary>Nombre de la cookie del refresh token. Solo se envía a /api/auth.</summary>
    public const string NombreCookie = "habittracker_refresh";

    /// <summary>Ruta a la que el navegador envía la cookie: el resto de la API nunca la recibe.</summary>
    public const string RutaCookie = "/api/auth";

    /// <summary>Cabecera que deben llevar las peticiones que se autentican solo con la cookie (protección CSRF).</summary>
    public const string CabeceraCsrf = "X-HabitTracker-Csrf";

    public const int MaximoDiasRefresh = 90;

    /// <summary>Cuánto dura un refresh token sin usarse. Cada renovación empieza el plazo de nuevo.</summary>
    public int DiasRefresh { get; set; } = 14;

    /// <summary>
    /// Solo falso en desarrollo local sin HTTPS. SameSite=None exige cookie segura.
    /// </summary>
    public bool CookieSegura { get; set; } = true;

    /// <summary>
    /// Strict cuando el frontend y la API comparten sitio (localhost, o un mismo dominio); None cuando viven en dominios
    /// distintos (por ejemplo, el frontend en Vercel y la API en otro host).
    /// </summary>
    public string CookieSameSite { get; set; } = "Strict";

    public TimeSpan DuracionRefresh => TimeSpan.FromDays(DiasRefresh);

    public static OpcionesSesion PorDefecto { get; } = new();

    /// <summary>Una configuración inválida detiene el arranque de la aplicación (RD-07).</summary>
    public void Validar()
    {
        if (DiasRefresh < 1 || DiasRefresh > MaximoDiasRefresh)
        {
            throw new InvalidOperationException($"Sesion:DiasRefresh debe estar entre 1 y {MaximoDiasRefresh}.");
        }

        if (!string.Equals(CookieSameSite, "Strict", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(CookieSameSite, "Lax", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(CookieSameSite, "None", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Sesion:CookieSameSite debe ser Strict, Lax o None.");
        }

        if (string.Equals(CookieSameSite, "None", StringComparison.OrdinalIgnoreCase) && !CookieSegura)
        {
            throw new InvalidOperationException("Sesion:CookieSameSite=None exige Sesion:CookieSegura=true.");
        }
    }
}
