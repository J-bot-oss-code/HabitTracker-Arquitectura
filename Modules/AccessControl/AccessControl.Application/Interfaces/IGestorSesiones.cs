using AccessControl.Application.DTOs;

namespace AccessControl.Application.Interfaces;

/// <summary>
/// Único punto donde se abren, renuevan y revocan las sesiones (RD-04). Todo lo que debe cortar sesiones (cambio o
/// restablecimiento de contraseña, desactivación, cambio de rol, cierre de sesión) pasa por <see cref="RevocarTodasAsync"/>.
/// </summary>
public interface IGestorSesiones
{
    /// <summary>Abre una sesión nueva para quien acaba de autenticarse (login o Google) y emite su refresh token.</summary>
    Task<SesionEmitida> IniciarAsync(Guid usuarioId, string? dispositivo, string? ip);

    /// <summary>
    /// Canjea un refresh token por un token de acceso nuevo y un refresh token nuevo; el anterior queda revocado. Si el
    /// token ya se había usado se revoca toda su familia (señal de robo). Lanza SesionInvalidaException.
    /// </summary>
    Task<SesionRenovada> RenovarAsync(string? refreshToken);

    /// <summary>Cierra la sesión del refresh token indicado, solo si pertenece al usuario autenticado.</summary>
    Task CerrarAsync(Guid usuarioId, string? refreshToken);

    /// <summary>
    /// Revoca todas las sesiones del usuario: los refresh tokens y los tokens de acceso emitidos hasta ahora.
    /// <paramref name="motivo"/> es un texto fijo de nuestro código que queda en el log (nunca un dato de la persona).
    /// </summary>
    Task RevocarTodasAsync(Guid usuarioId, string motivo);

    Task<IReadOnlyList<SesionActivaDto>> ListarAsync(Guid usuarioId, string? refreshActual);

    /// <summary>Revoca una sesión concreta del usuario. False si no existe, no es suya o ya no estaba activa.</summary>
    Task<bool> RevocarAsync(Guid usuarioId, Guid sesionId);
}
