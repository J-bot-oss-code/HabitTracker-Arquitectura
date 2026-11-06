using AccessControl.Application.DTOs;
using AccessControl.Application.Interfaces;
using AccessControl.Domain.Excepciones;

namespace AccessControl.Application.Service;

/// <summary>Única regla de cuándo una aceptación de los documentos legales sirve para crear una cuenta (registro y Google).</summary>
public static class AceptacionLegal
{
    public static ConsentimientosRegistroDto Validar(IVersionesLegales versiones, ConsentimientosRegistroDto? consentimientos)
    {
        if (consentimientos is null
            || consentimientos.Marketing is null
            || consentimientos.TerminosVersion != versiones.Terminos
            || consentimientos.PrivacidadVersion != versiones.Privacidad)
        {
            throw new ConsentimientoNoValidoException();
        }

        return consentimientos;
    }
}
