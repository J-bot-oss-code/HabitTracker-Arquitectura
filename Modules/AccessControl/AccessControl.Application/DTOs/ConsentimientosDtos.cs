using System.ComponentModel.DataAnnotations;

namespace AccessControl.Application.DTOs;

/// <summary>Lo que se acepta al registrarse: las versiones vigentes de los dos documentos y, opcional, recibir correos de novedades.</summary>
public record ConsentimientosRegistroDto(
    [param: Required, StringLength(20)] string TerminosVersion,
    [param: Required, StringLength(20)] string PrivacidadVersion,
    [param: Required] bool? Marketing);

/// <summary>Consentimientos vigentes de un usuario. AceptadoEn (UTC) lo fija siempre el servidor.</summary>
public record ConsentimientosDto(
    string? TerminosVersion,
    string? PrivacidadVersion,
    DateTime? AceptadoEn,
    bool DatosSalud,
    bool Marketing,
    bool CompartirCumplimiento,
    bool EstadisticasAnonimas);

/// <summary>Actualización de los consentimientos. Todos los campos son obligatorios: no se asume "falso" si falta uno.</summary>
public record GuardarConsentimientosRequest(
    [param: Required, StringLength(20)] string TerminosVersion,
    [param: Required, StringLength(20)] string PrivacidadVersion,
    [param: Required] bool? DatosSalud,
    [param: Required] bool? Marketing,
    [param: Required] bool? CompartirCumplimiento,
    [param: Required] bool? EstadisticasAnonimas);
