using AccessControl.Application.Interfaces;
using HabitTracker.Application.Sugerencias;

namespace WebApi.Adapters;

/// <summary>
/// Las quejas y mejoras que envió la persona, en la descarga de datos y en la baja de cuenta. Es código de composición del Host:
/// Control de Acceso no conoce a este módulo (RD-03).
/// </summary>
public sealed class ParticipanteSugerencias(ISugerenciasService servicio) : IParticipanteDatosUsuario
{
    public string Seccion => "sugerencias";

    public async Task<object> ExportarAsync(Guid usuarioId, string correo, CancellationToken cancelacion = default) =>
        await servicio.ExportarAsync(usuarioId, cancelacion);

    public async Task EliminarAsync(Guid usuarioId, string correo, CancellationToken cancelacion = default) =>
        await servicio.EliminarAsync(usuarioId, cancelacion);
}
