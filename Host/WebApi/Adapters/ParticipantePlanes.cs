using AccessControl.Application.Interfaces;
using HabitTracker.Application.Services;

namespace WebApi.Adapters;

/// <summary>
/// Los planes, sus pausas y los logros de la persona en la descarga de datos y en la baja de cuenta. Es código de composición
/// del Host: Control de Acceso no conoce a este módulo (RD-03).
/// </summary>
public sealed class ParticipantePlanes(IDatosPlanesService servicio) : IParticipanteDatosUsuario
{
    public string Seccion => "planes";

    public async Task<object> ExportarAsync(Guid usuarioId, string correo, CancellationToken cancelacion = default) =>
        await servicio.ExportarAsync(usuarioId, cancelacion);

    public async Task EliminarAsync(Guid usuarioId, string correo, CancellationToken cancelacion = default) =>
        await servicio.EliminarAsync(usuarioId, cancelacion);
}
