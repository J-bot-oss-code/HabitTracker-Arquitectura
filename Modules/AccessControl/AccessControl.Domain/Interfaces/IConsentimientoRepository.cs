using AccessControl.Domain.Entities;

namespace AccessControl.Domain.Interfaces;

public interface IConsentimientoRepository
{
    /// <summary>El consentimiento vigente de un usuario (la última fila del historial), o null si nunca aceptó nada.</summary>
    Task<ConsentimientoUsuario?> ObtenerVigenteAsync(Guid usuarioId);

    /// <summary>Agrega una fila al historial. Nunca se modifica ni se borra una fila existente.</summary>
    Task AgregarAsync(ConsentimientoUsuario consentimiento);
}
