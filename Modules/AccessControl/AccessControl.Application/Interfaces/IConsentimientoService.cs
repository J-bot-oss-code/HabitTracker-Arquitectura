using AccessControl.Application.DTOs;

namespace AccessControl.Application.Interfaces;

public interface IConsentimientoService
{
    /// <summary>Consentimientos vigentes del usuario, o null si nunca aceptó nada (las cuentas anteriores a esta función).</summary>
    Task<ConsentimientosDto?> ObtenerAsync(Guid usuarioId);

    /// <summary>
    /// Registra una nueva fila del historial. Solo se aceptan las versiones vigentes o las que el usuario ya había aceptado:
    /// cambiar un interruptor no debe obligarle a aceptar documentos nuevos que aún no ha visto.
    /// </summary>
    /// <exception cref="AccessControl.Domain.Excepciones.ConsentimientoNoValidoException"/>
    Task<ConsentimientosDto> GuardarAsync(Guid usuarioId, GuardarConsentimientosRequest solicitud);

    /// <summary>¿Debe volver a aceptar los documentos? Verdadero si nunca aceptó o si su última aceptación no es de las versiones vigentes.</summary>
    Task<bool> RequiereAceptarDocumentosAsync(Guid usuarioId);
}
