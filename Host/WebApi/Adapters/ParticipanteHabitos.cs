using AccessControl.Application.Interfaces;
using HabitTracker.Application.Services;

namespace WebApi.Adapters;

/// <summary>
/// Hábitos y registros de avance en la descarga de datos y en la baja de cuenta. Es código de composición del Host: llama al
/// servicio de Application de HabitTracker; Control de Acceso no conoce a este módulo (RD-03).
/// </summary>
public sealed class ParticipanteHabitos(HabitoService servicio) : IParticipanteDatosUsuario
{
    public string Seccion => "habitos";

    public async Task<object> ExportarAsync(Guid usuarioId, string correo, CancellationToken cancelacion = default) =>
        await servicio.ConsultarHabitosAsync(usuarioId, cancelacion);

    public async Task EliminarAsync(Guid usuarioId, string correo, CancellationToken cancelacion = default) =>
        await servicio.EliminarDatosDeUsuarioAsync(usuarioId, cancelacion);
}
