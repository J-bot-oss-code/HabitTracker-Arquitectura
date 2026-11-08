using HabitTracker.Application.DTOs;
using HabitTracker.Application.Interfaces;
using HabitTracker.Domain.Excepciones;
using HabitTracker.Domain.Perfil;
using HabitTracker.Domain.Progreso;
using HabitTracker.Domain.Salud;

namespace HabitTracker.Application.Services;

public interface IPerfilService
{
    /// <summary>Perfil de la persona; 404 <c>perfil_no_encontrado</c> si aún no hizo el onboarding.</summary>
    Task<PerfilDto> ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Crea o reemplaza el perfil. Si trae peso o altura exige el consentimiento <c>datosSalud</c> (409).</summary>
    Task<PerfilDto> GuardarAsync(Guid usuarioId, DatosPerfil datos, CancellationToken cancellationToken = default);
}

public interface ICuestionarioSaludService
{
    /// <summary>Bloques respondidos y su evaluación. Sin cuestionario devuelve los bloques vacíos (la evaluación usa el perfil).</summary>
    Task<CuestionarioSaludDto> ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    Task<CuestionarioSaludDto> GuardarObjetivosAsync(Guid usuarioId, BloqueObjetivos bloque, CancellationToken cancellationToken = default);

    Task<CuestionarioSaludDto> GuardarSeguridadAsync(Guid usuarioId, BloqueSeguridad bloque, CancellationToken cancellationToken = default);

    Task<CuestionarioSaludDto> GuardarCondicionesAsync(Guid usuarioId, BloqueCondiciones bloque, CancellationToken cancellationToken = default);

    /// <summary>Evaluación de salud de la persona (la usarán el recomendador y la inscripción en planes).</summary>
    Task<EvaluacionSalud> EvaluarAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}

public interface IProgresoCorporalService
{
    Task<PesosDto> ObtenerPesosAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Un peso por día UTC: el nuevo reemplaza al del mismo día. El último peso pasa al perfil.</summary>
    Task<PesosDto> RegistrarPesoAsync(Guid usuarioId, RegistrarPesoCommand command, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MedicionDto>> ObtenerMedicionesAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Una medida por día UTC y zona: la nueva reemplaza a la del mismo día.</summary>
    Task<MedicionDto> RegistrarMedicionAsync(Guid usuarioId, RegistrarMedicionCommand command, CancellationToken cancellationToken = default);
}

/// <summary>Descarga y baja de cuenta de los datos de perfil y salud (lo usa un adaptador del Host).</summary>
public interface IDatosPerfilSaludService
{
    Task<DatosPerfilSaludDto> ExportarAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Borra perfil, cuestionario, pesos y medidas. Inmediato e idempotente.</summary>
    Task EliminarAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}

/// <summary>Reglas compartidas por los servicios de perfil y salud.</summary>
internal static class Guardas
{
    public static void ExigirIdentidad(Guid usuarioId)
    {
        if (usuarioId == Guid.Empty)
            throw new UnauthorizedAccessException("Identidad de usuario inválida.");
    }

    /// <summary>Un dato de salud solo se guarda con el consentimiento vigente (409 si falta).</summary>
    public static async Task ExigirConsentimientoAsync(IConsentimientoSalud consentimiento, Guid usuarioId, CancellationToken ct)
    {
        if (!await consentimiento.TieneConsentimientoDatosSaludAsync(usuarioId, ct))
            throw new ConsentimientoSaludRequeridoException();
    }

    public static DateTime Ahora(TimeProvider reloj) => reloj.GetUtcNow().UtcDateTime;
}

public sealed class PerfilService(
    IPerfilUsuarioRepository perfiles,
    IConsentimientoSalud consentimiento,
    IUnitOfWork unitOfWork,
    TimeProvider? reloj = null) : IPerfilService
{
    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    public async Task<PerfilDto> ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        Guardas.ExigirIdentidad(usuarioId);
        var perfil = await perfiles.ObtenerAsync(usuarioId, cancellationToken)
            ?? throw new RecursoNoEncontradoException(RecursoNoEncontradoException.PerfilNoEncontrado, "Aún no completaste tu perfil.");
        return PerfilDto.Desde(perfil);
    }

    public async Task<PerfilDto> GuardarAsync(Guid usuarioId, DatosPerfil datos, CancellationToken cancellationToken = default)
    {
        Guardas.ExigirIdentidad(usuarioId);
        ArgumentNullException.ThrowIfNull(datos);

        // Nivel 1 (jornada, zona, aviso) no pide consentimiento: un plan Básico se empieza sin datos de salud (P1).
        if (datos.TraeDatosDeSalud)
            await Guardas.ExigirConsentimientoAsync(consentimiento, usuarioId, cancellationToken);

        var ahora = Guardas.Ahora(_reloj);
        var perfil = await perfiles.ObtenerAsync(usuarioId, cancellationToken);
        if (perfil is null)
        {
            perfil = PerfilUsuario.Crear(usuarioId, datos, ahora);
            await perfiles.AgregarAsync(perfil, cancellationToken);
        }
        else
        {
            perfil.Actualizar(datos, ahora);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return PerfilDto.Desde(perfil);
    }
}

public sealed class CuestionarioSaludService(
    ICuestionarioSaludRepository cuestionarios,
    IPerfilUsuarioRepository perfiles,
    IConsentimientoSalud consentimiento,
    IUnitOfWork unitOfWork,
    TimeProvider? reloj = null) : ICuestionarioSaludService
{
    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    public async Task<CuestionarioSaludDto> ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        Guardas.ExigirIdentidad(usuarioId);
        var cuestionario = await cuestionarios.ObtenerAsync(usuarioId, cancellationToken);
        var perfil = await perfiles.ObtenerAsync(usuarioId, cancellationToken);
        var consiente = await consentimiento.TieneConsentimientoDatosSaludAsync(usuarioId, cancellationToken);
        return ADto(cuestionario, perfil, consiente);
    }

    public Task<CuestionarioSaludDto> GuardarObjetivosAsync(Guid usuarioId, BloqueObjetivos bloque, CancellationToken cancellationToken = default) =>
        GuardarAsync(usuarioId, (c, ahora) => c.GuardarObjetivos(bloque, ahora), cancellationToken);

    public Task<CuestionarioSaludDto> GuardarSeguridadAsync(Guid usuarioId, BloqueSeguridad bloque, CancellationToken cancellationToken = default) =>
        GuardarAsync(usuarioId, (c, ahora) => c.GuardarSeguridad(bloque, ahora), cancellationToken);

    public Task<CuestionarioSaludDto> GuardarCondicionesAsync(Guid usuarioId, BloqueCondiciones bloque, CancellationToken cancellationToken = default) =>
        GuardarAsync(usuarioId, (c, ahora) => c.GuardarCondiciones(bloque, ahora), cancellationToken);

    public async Task<EvaluacionSalud> EvaluarAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        Guardas.ExigirIdentidad(usuarioId);
        var cuestionario = await cuestionarios.ObtenerAsync(usuarioId, cancellationToken);
        var perfil = await perfiles.ObtenerAsync(usuarioId, cancellationToken);
        var consiente = await consentimiento.TieneConsentimientoDatosSaludAsync(usuarioId, cancellationToken);
        return EvaluadorSalud.Evaluar(cuestionario, perfil, consiente);
    }

    private async Task<CuestionarioSaludDto> GuardarAsync(
        Guid usuarioId, Action<CuestionarioSalud, DateTime> cambiar, CancellationToken cancellationToken)
    {
        Guardas.ExigirIdentidad(usuarioId);
        await Guardas.ExigirConsentimientoAsync(consentimiento, usuarioId, cancellationToken);

        var cuestionario = await cuestionarios.ObtenerAsync(usuarioId, cancellationToken) ?? new CuestionarioSalud(usuarioId);
        cambiar(cuestionario, Guardas.Ahora(_reloj));
        await cuestionarios.GuardarAsync(cuestionario, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var perfil = await perfiles.ObtenerAsync(usuarioId, cancellationToken);
        return ADto(cuestionario, perfil, usaDatosDeSalud: true); // acaba de comprobarse el consentimiento
    }

    internal static CuestionarioSaludDto ADto(CuestionarioSalud? c, PerfilUsuario? perfil, bool usaDatosDeSalud) => new(
        c?.Version ?? CuestionarioSalud.VersionActual,
        c?.Objetivos, c?.ObjetivosRespondidoEn,
        c?.Seguridad, c?.SeguridadRespondidoEn,
        c?.Condiciones, c?.CondicionesRespondidoEn,
        EvaluadorSalud.Evaluar(c, perfil, usaDatosDeSalud),
        usaDatosDeSalud);
}

public sealed class ProgresoCorporalService(
    IProgresoCorporalRepository progreso,
    IPerfilUsuarioRepository perfiles,
    IConsentimientoSalud consentimiento,
    IUnitOfWork unitOfWork,
    TimeProvider? reloj = null) : IProgresoCorporalService
{
    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    public async Task<PesosDto> ObtenerPesosAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        Guardas.ExigirIdentidad(usuarioId);
        var perfil = await perfiles.ObtenerAsync(usuarioId, cancellationToken);
        var pesos = await progreso.PesosAsync(usuarioId, cancellationToken);
        return APesosDto(perfil, pesos);
    }

    public async Task<PesosDto> RegistrarPesoAsync(Guid usuarioId, RegistrarPesoCommand command, CancellationToken cancellationToken = default)
    {
        Guardas.ExigirIdentidad(usuarioId);
        ArgumentNullException.ThrowIfNull(command);
        await Guardas.ExigirConsentimientoAsync(consentimiento, usuarioId, cancellationToken);

        var ahora = Guardas.Ahora(_reloj);
        var perfil = await perfiles.ObtenerAsync(usuarioId, cancellationToken);
        if (command.CambiarObjetivo && perfil is null)
            throw new ReglaNegocioException(ReglaNegocioException.PerfilRequerido, "Completa tu perfil antes de fijar un peso objetivo.");

        FechasCorporales.Validar(command.Fecha, ahora);
        var dia = FechasCorporales.DiaUtc(command.Fecha);
        var guardados = await progreso.PesosAsync(usuarioId, cancellationToken);
        // El peso del perfil es el del día más reciente: un peso atrasado no lo pisa.
        var esElMasReciente = guardados.All(p => FechasCorporales.DiaUtc(p.Fecha) <= dia);

        var existente = guardados.FirstOrDefault(p => FechasCorporales.DiaUtc(p.Fecha) == dia);
        if (existente is not null)
            existente.Corregir(command.PesoKg);
        else
            await progreso.AgregarPesoAsync(new RegistroPeso(usuarioId, command.Fecha, command.PesoKg, ahora), cancellationToken);

        if (perfil is not null)
        {
            if (esElMasReciente)
                perfil.SincronizarPeso(command.PesoKg, ahora);
            if (command.CambiarObjetivo)
                perfil.FijarObjetivoPeso(command.ObjetivoKg, ahora);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return APesosDto(perfil, await progreso.PesosAsync(usuarioId, cancellationToken));
    }

    public async Task<IReadOnlyList<MedicionDto>> ObtenerMedicionesAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        Guardas.ExigirIdentidad(usuarioId);
        var mediciones = await progreso.MedicionesAsync(usuarioId, cancellationToken);
        return mediciones.OrderBy(m => m.Fecha).Select(AMedicionDto).ToList();
    }

    public async Task<MedicionDto> RegistrarMedicionAsync(Guid usuarioId, RegistrarMedicionCommand command, CancellationToken cancellationToken = default)
    {
        Guardas.ExigirIdentidad(usuarioId);
        ArgumentNullException.ThrowIfNull(command);
        await Guardas.ExigirConsentimientoAsync(consentimiento, usuarioId, cancellationToken);

        var ahora = Guardas.Ahora(_reloj);
        if (!Enum.IsDefined(command.Zona))
            throw new ArgumentException("La zona no es válida.", nameof(command.Zona));

        FechasCorporales.Validar(command.Fecha, ahora);
        var existente = await progreso.MedicionDelDiaAsync(usuarioId, command.Zona, FechasCorporales.DiaUtc(command.Fecha), cancellationToken);
        MedicionCorporal medicion;
        if (existente is not null)
        {
            existente.Corregir(command.Valor);
            medicion = existente;
        }
        else
        {
            medicion = new MedicionCorporal(usuarioId, command.Fecha, command.Zona, command.Valor, ahora);
            await progreso.AgregarMedicionAsync(medicion, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return AMedicionDto(medicion);
    }

    internal static PesosDto APesosDto(PerfilUsuario? perfil, IEnumerable<RegistroPeso> pesos) =>
        new(perfil?.ObjetivoPesoKg, pesos.OrderBy(p => p.Fecha).Select(p => new PesoDto(p.Id, p.Fecha, p.PesoKg)).ToList());

    internal static MedicionDto AMedicionDto(MedicionCorporal m) => new(m.Id, m.Fecha, m.Zona, m.Valor);
}

public sealed class DatosPerfilSaludService(
    IPerfilUsuarioRepository perfiles,
    ICuestionarioSaludRepository cuestionarios,
    IProgresoCorporalRepository progreso) : IDatosPerfilSaludService
{
    public async Task<DatosPerfilSaludDto> ExportarAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        Guardas.ExigirIdentidad(usuarioId);
        var perfil = await perfiles.ObtenerAsync(usuarioId, cancellationToken);
        var cuestionario = await cuestionarios.ObtenerAsync(usuarioId, cancellationToken);
        var pesos = await progreso.PesosAsync(usuarioId, cancellationToken);
        var mediciones = await progreso.MedicionesAsync(usuarioId, cancellationToken);

        return new DatosPerfilSaludDto(
            perfil is null ? null : PerfilDto.Desde(perfil),
            // En la descarga la evaluación es informativa: refleja las respuestas guardadas.
            cuestionario is null ? null : CuestionarioSaludService.ADto(cuestionario, perfil, usaDatosDeSalud: true),
            ProgresoCorporalService.APesosDto(perfil, pesos).Registros,
            mediciones.OrderBy(m => m.Fecha).Select(ProgresoCorporalService.AMedicionDto).ToList());
    }

    public async Task EliminarAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        Guardas.ExigirIdentidad(usuarioId);
        await progreso.EliminarDeUsuarioAsync(usuarioId, cancellationToken);
        await cuestionarios.EliminarDeUsuarioAsync(usuarioId, cancellationToken);
        await perfiles.EliminarDeUsuarioAsync(usuarioId, cancellationToken);
    }
}
