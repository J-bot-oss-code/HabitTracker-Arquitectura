using AccessControl.Application.Interfaces;
using HabitTracker.Application.Services;

namespace WebApi.Adapters;

/// <summary>
/// Perfil, cuestionario de salud, pesos y medidas en la descarga de datos y en la baja de cuenta. En la descarga el
/// cuestionario va descifrado (es de su dueño); en la baja se borra todo de inmediato.
/// </summary>
public sealed class ParticipantePerfilSalud(IDatosPerfilSaludService servicio) : IParticipanteDatosUsuario
{
    public string Seccion => "perfilYSalud";

    public async Task<object> ExportarAsync(Guid usuarioId, string correo, CancellationToken cancelacion = default) =>
        await servicio.ExportarAsync(usuarioId, cancelacion);

    public Task EliminarAsync(Guid usuarioId, string correo, CancellationToken cancelacion = default) =>
        servicio.EliminarAsync(usuarioId, cancelacion);
}
