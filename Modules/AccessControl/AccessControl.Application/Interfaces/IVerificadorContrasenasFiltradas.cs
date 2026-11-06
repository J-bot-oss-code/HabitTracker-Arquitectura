namespace AccessControl.Application.Interfaces;

/// <summary>
/// Dice si una contraseña aparece en filtraciones de datos conocidas. La implementación nunca debe enviar la contraseña
/// ni su hash completo a un tercero (k-anonimato), y si el servicio externo falla debe responder "no filtrada": una caída
/// de un servicio ajeno no puede impedir que la gente se registre.
/// </summary>
public interface IVerificadorContrasenasFiltradas
{
    Task<bool> EstaFiltradaAsync(string password, CancellationToken cancellationToken = default);
}
