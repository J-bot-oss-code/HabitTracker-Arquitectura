using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using AccessControl.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace AccessControl.Infrastructure.Security;

/// <summary>
/// Comprueba una contraseña contra la base de filtraciones de Have I Been Pwned (API "Pwned Passwords") con k-anonimato:
/// a la API solo viaja el prefijo de 5 caracteres del hash SHA-1; la contraseña y el hash completo nunca salen del
/// servidor. La API devuelve todos los sufijos que comparten ese prefijo y la comparación se hace aquí.
/// Si la API falla, tarda o responde con error, la contraseña se PERMITE (y se avisa en el log): una caída de un servicio
/// ajeno no puede impedir que la gente se registre.
/// </summary>
public sealed class VerificadorHaveIBeenPwned : IVerificadorContrasenasFiltradas
{
    private readonly HttpClient _http;
    private readonly ILogger<VerificadorHaveIBeenPwned> _logger;

    public VerificadorHaveIBeenPwned(HttpClient http, ILogger<VerificadorHaveIBeenPwned> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<bool> EstaFiltradaAsync(string password, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var hash = Sha1Hexadecimal(password);
        var prefijo = hash[..5];
        var sufijo = hash[5..];

        try
        {
            using var respuesta = await _http.GetAsync($"range/{prefijo}", cancellationToken);
            if (!respuesta.IsSuccessStatusCode)
            {
                _logger.LogWarning("Pwned Passwords respondió {Estado}; se permite la contraseña.", (int)respuesta.StatusCode);
                return false;
            }

            var cuerpo = await respuesta.Content.ReadAsStringAsync(cancellationToken);
            return ContieneSufijo(cuerpo, sufijo);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // Falla de red o tiempo de espera agotado (no cancelación de quien llamó): fail-open.
            _logger.LogWarning(ex, "No se pudo consultar Pwned Passwords; se permite la contraseña.");
            return false;
        }
    }

    /// <summary>
    /// La respuesta son líneas "SUFIJO:CANTIDAD". Con la cabecera Add-Padding la API agrega líneas falsas con cantidad 0
    /// para que el tamaño no delate el prefijo: esas no cuentan como filtración.
    /// </summary>
    public static bool ContieneSufijo(string cuerpo, string sufijo)
    {
        foreach (var linea in cuerpo.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separador = linea.IndexOf(':');
            if (separador <= 0)
            {
                continue;
            }

            if (linea.AsSpan(0, separador).Equals(sufijo, StringComparison.OrdinalIgnoreCase)
                && long.TryParse(linea.AsSpan(separador + 1), out var cantidad)
                && cantidad > 0)
            {
                return true;
            }
        }

        return false;
    }

    // SHA-1 es el algoritmo que exige el protocolo de Pwned Passwords para el prefijo de k-anonimato. NO se usa para
    // guardar ni verificar contraseñas (eso lo hace bcrypt con PasswordHasher): solo calcula una clave de consulta.
    [SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms",
        Justification = "SHA-1 lo exige el protocolo de k-anonimato de Pwned Passwords; no protege ninguna contraseña almacenada.")]
    private static string Sha1Hexadecimal(string texto) =>
        Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(texto)));
}
