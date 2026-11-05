namespace AccessControl.Application.DTOs;

/// <summary>Usuario devuelto por la API. En JSON los nombres viajan en camelCase (id, nombre, correo, rol, activo, token).</summary>
public record UsuarioResponseDto(
    Guid Id, string Nombre, string Correo, string Rol, bool Activo, string? Token = null, bool RequiereAceptarDocumentos = false);
