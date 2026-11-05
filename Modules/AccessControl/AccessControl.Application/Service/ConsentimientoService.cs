using AccessControl.Application.DTOs;
using AccessControl.Application.Interfaces;
using AccessControl.Domain.Entities;
using AccessControl.Domain.Excepciones;
using AccessControl.Domain.Interfaces;

namespace AccessControl.Application.Service;

public sealed class ConsentimientoService : IConsentimientoService
{
    private readonly IConsentimientoRepository _repositorio;
    private readonly IVersionesLegales _versiones;

    public ConsentimientoService(IConsentimientoRepository repositorio, IVersionesLegales versiones)
    {
        _repositorio = repositorio;
        _versiones = versiones;
    }

    public async Task<ConsentimientosDto?> ObtenerAsync(Guid usuarioId)
    {
        var vigente = await _repositorio.ObtenerVigenteAsync(usuarioId);
        return vigente is null ? null : Mapear(vigente);
    }

    public async Task<ConsentimientosDto> GuardarAsync(Guid usuarioId, GuardarConsentimientosRequest solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (usuarioId == Guid.Empty || solicitud.DatosSalud is null || solicitud.Marketing is null
            || solicitud.CompartirCumplimiento is null || solicitud.EstadisticasAnonimas is null)
        {
            throw new ArgumentException("Los datos de consentimiento no son válidos.", nameof(solicitud));
        }

        var anterior = await _repositorio.ObtenerVigenteAsync(usuarioId);

        // Se acepta la versión vigente de cada documento o la que la persona ya había aceptado.
        if (!VersionAceptable(solicitud.TerminosVersion, _versiones.Terminos, anterior?.TerminosVersion)
            || !VersionAceptable(solicitud.PrivacidadVersion, _versiones.Privacidad, anterior?.PrivacidadVersion))
        {
            throw new ConsentimientoNoValidoException();
        }

        var ahora = DateTime.UtcNow;

        // La fecha de aceptación es la de las VERSIONES: solo se renueva cuando cambian, no al mover un interruptor.
        var mismasVersiones = anterior is not null
            && anterior.TerminosVersion == solicitud.TerminosVersion
            && anterior.PrivacidadVersion == solicitud.PrivacidadVersion;
        var aceptadoEn = mismasVersiones ? anterior!.AceptadoEn : ahora;

        var nuevo = new ConsentimientoUsuario(
            usuarioId,
            solicitud.TerminosVersion,
            solicitud.PrivacidadVersion,
            solicitud.DatosSalud.Value,
            solicitud.Marketing.Value,
            solicitud.CompartirCumplimiento.Value,
            solicitud.EstadisticasAnonimas.Value,
            aceptadoEn,
            ahora,
            ConsentimientoUsuario.OrigenActualizacion);

        await _repositorio.AgregarAsync(nuevo);
        return Mapear(nuevo);
    }

    public async Task<bool> RequiereAceptarDocumentosAsync(Guid usuarioId)
    {
        var vigente = await _repositorio.ObtenerVigenteAsync(usuarioId);
        return vigente is null || !vigente.EsDeLasVersiones(_versiones.Terminos, _versiones.Privacidad);
    }

    private static bool VersionAceptable(string enviada, string vigente, string? previa) =>
        enviada == vigente || (previa is not null && enviada == previa);

    private static ConsentimientosDto Mapear(ConsentimientoUsuario c) => new(
        c.TerminosVersion,
        c.PrivacidadVersion,
        // SQL Server devuelve las fechas sin zona (Unspecified): se marcan como UTC para que el JSON lleve la "Z".
        DateTime.SpecifyKind(c.AceptadoEn, DateTimeKind.Utc),
        c.DatosSalud,
        c.Marketing,
        c.CompartirCumplimiento,
        c.EstadisticasAnonimas);
}
