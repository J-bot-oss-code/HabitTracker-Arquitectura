namespace AccessControl.Application.DTOs;

/// <summary>Refresh token recién emitido. El valor solo existe aquí: la base de datos guarda únicamente su hash.</summary>
public record SesionEmitida(string RefreshToken, DateTime ExpiraEn);

/// <summary>Resultado de renovar: el usuario con su token de acceso nuevo y el refresh token que sustituye al anterior.</summary>
public record SesionRenovada(UsuarioResponseDto Usuario, string RefreshToken, DateTime ExpiraEn);

/// <summary>Una sesión abierta del usuario. <c>Actual</c> marca la del dispositivo que hace la consulta.</summary>
public record SesionActivaDto(Guid Id, string Dispositivo, DateTime CreadaEn, DateTime UltimoUsoEn, bool Actual);
