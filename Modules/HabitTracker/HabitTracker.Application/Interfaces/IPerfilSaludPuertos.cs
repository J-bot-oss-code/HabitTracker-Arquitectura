using HabitTracker.Domain.Perfil;
using HabitTracker.Domain.Progreso;
using HabitTracker.Domain.Salud;

namespace HabitTracker.Application.Interfaces;

/// <summary>
/// Puerto hacia Control de Acceso: ¿la persona dio el consentimiento <c>datosSalud</c>? Lo implementa un adaptador del Host
/// (HabitTracker no lee tablas ajenas y el Core no conoce a este módulo, RD-03).
/// </summary>
public interface IConsentimientoSalud
{
    Task<bool> TieneConsentimientoDatosSaludAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Cifrado de datos sensibles en reposo (cuestionario de salud; notas en la Fase 9). Cada tipo de dato usa su propio
/// <paramref name="proposito"/>: un texto cifrado para un propósito no se descifra con otro.
/// </summary>
public interface ICifradorDatos
{
    string Cifrar(string texto, string proposito);

    /// <summary>Lanza <see cref="System.Security.Cryptography.CryptographicException"/> si el texto no es válido o la clave no coincide.</summary>
    string Descifrar(string cifrado, string proposito);
}

public interface IPerfilUsuarioRepository
{
    Task<PerfilUsuario?> ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    Task AgregarAsync(PerfilUsuario perfil, CancellationToken cancellationToken = default);

    /// <summary>Borra el perfil (baja de cuenta). Inmediato e idempotente.</summary>
    Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}

public interface ICuestionarioSaludRepository
{
    Task<CuestionarioSalud?> ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Inserta o actualiza el cuestionario; los bloques se guardan cifrados. Se confirma con <see cref="IUnitOfWork"/>.</summary>
    Task GuardarAsync(CuestionarioSalud cuestionario, CancellationToken cancellationToken = default);

    Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}

public interface IProgresoCorporalRepository
{
    Task<IReadOnlyList<RegistroPeso>> PesosAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    Task AgregarPesoAsync(RegistroPeso registro, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MedicionCorporal>> MedicionesAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    Task<MedicionCorporal?> MedicionDelDiaAsync(Guid usuarioId, ZonaMedicion zona, DateTime diaUtc, CancellationToken cancellationToken = default);

    Task AgregarMedicionAsync(MedicionCorporal medicion, CancellationToken cancellationToken = default);

    /// <summary>Borra pesos y medidas de la persona (baja de cuenta). Inmediato e idempotente.</summary>
    Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}
