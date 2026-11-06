namespace AccessControl.Application.DTOs;

/// <summary>
/// Respuesta a la solicitud de baja. <c>BorradoEn</c> es la fecha en que la cuenta se borrará de forma definitiva si no se
/// cancela (modo "eliminar") o nulo (modo "desactivar").
/// </summary>
public record BajaSolicitadaDto(string Modo, DateTime? BorradoEn);

public record CuentaExportadaDto(
    Guid Id,
    string Nombre,
    string Correo,
    string Rol,
    bool Activo,
    DateTime FechaCreacion,
    bool TienePassword,
    DateTime? DesactivadaPorUsuarioEn,
    DateTime? BajaProgramadaPara);

public record ConsentimientoExportadoDto(
    string TerminosVersion,
    string PrivacidadVersion,
    bool DatosSalud,
    bool Marketing,
    bool CompartirCumplimiento,
    bool EstadisticasAnonimas,
    DateTime AceptadoEn,
    DateTime ActualizadoEn,
    string Origen);

public record SesionExportadaDto(Guid Id, string Dispositivo, string? Ip, DateTime CreadaEn, DateTime UltimoUsoEn, DateTime ExpiraEn);

/// <summary>
/// Todo lo que el sistema guarda de una persona (derecho de acceso y portabilidad, Ley 172-13 y GDPR). Nunca incluye la
/// contraseña ni su hash, ni los tokens de las sesiones. <c>Modulos</c> trae una sección por cada módulo que guarda datos suyos.
/// </summary>
public record ExportacionDatosDto(
    DateTime GeneradoEn,
    CuentaExportadaDto Cuenta,
    IReadOnlyList<ConsentimientoExportadoDto> Consentimientos,
    IReadOnlyList<VinculoDto> Vinculos,
    IReadOnlyList<SesionExportadaDto> Sesiones,
    IReadOnlyDictionary<string, object?> Modulos);
