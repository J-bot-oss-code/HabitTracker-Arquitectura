namespace AccessControl.Application.DTOs;

/// <summary>Un correo de la cola, como lo ve el Administrador (RF-NOT-11). No incluye el cuerpo: puede llevar códigos de activación.</summary>
public sealed record CorreoColaDto(
    Guid Id,
    string Destinatario,
    string Asunto,
    string Estado,
    int Intentos,
    DateTime FechaCreacion,
    DateTime? FechaUltimoIntento,
    DateTime? FechaEnvio,
    string? UltimoError);

/// <summary>Resultado de una pasada manual del procesador de la cola.</summary>
public sealed record ResultadoColaDto(int Enviados, int Fallidos, int Recuperados);
