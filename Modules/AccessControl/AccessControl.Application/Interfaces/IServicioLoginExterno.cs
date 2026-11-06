using AccessControl.Application.DTOs;

namespace AccessControl.Application.Interfaces;

public interface IServicioLoginExterno
{
    /// <summary>
    /// Entra con Google: inicia sesión si la identidad ya está vinculada, crea la cuenta si el correo es nuevo (exige los
    /// consentimientos) y pide confirmar la contraseña si el correo ya tiene una cuenta (VinculacionRequeridaException).
    /// </summary>
    Task<ResultadoLoginExterno> IniciarConGoogleAsync(string idToken, ConsentimientosRegistroDto? consentimientos);

    /// <summary>Vincula Google a la cuenta con contraseña de ese mismo correo, previa confirmación de la contraseña (RF-CA-19 aplica).</summary>
    Task<ResultadoLoginExterno> VincularGoogleAsync(string idToken, string password);

    Task<VinculosDto> ListarVinculosAsync(Guid usuarioId);

    /// <summary>Quita el vínculo. False si no existía. Falla si es la única forma de entrar de la cuenta.</summary>
    Task<bool> DesvincularAsync(Guid usuarioId, string proveedor);
}
