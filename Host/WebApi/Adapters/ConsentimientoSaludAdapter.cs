using AccessControl.Application.Interfaces;
using HabitTracker.Application.Interfaces;

namespace WebApi.Adapters;

/// <summary>
/// Puerto <see cref="IConsentimientoSalud"/> de HabitTracker hacia los consentimientos de Control de Acceso. Es código de
/// composición del Host: Control de Acceso no conoce a HabitTracker (RD-03) y HabitTracker no lee sus tablas.
/// </summary>
public sealed class ConsentimientoSaludAdapter(IConsentimientoService consentimientos) : IConsentimientoSalud
{
    public async Task<bool> TieneConsentimientoDatosSaludAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        // Sin ningún consentimiento guardado (cuenta anterior a la función) equivale a no haberlo dado.
        var vigente = await consentimientos.ObtenerAsync(usuarioId);
        return vigente?.DatosSalud == true;
    }
}
