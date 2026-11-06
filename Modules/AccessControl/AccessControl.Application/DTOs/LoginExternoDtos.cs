namespace AccessControl.Application.DTOs;

/// <summary>Resultado de entrar con un proveedor externo. <c>CuentaNueva</c> indica que la cuenta se creó en esta petición.</summary>
public record ResultadoLoginExterno(UsuarioResponseDto Usuario, bool CuentaNueva);

public record VinculoDto(string Proveedor, DateTime VinculadoEn);

/// <summary>Formas de entrar de la cuenta. Sin contraseña no se puede desvincular el último proveedor.</summary>
public record VinculosDto(bool TienePassword, IReadOnlyList<VinculoDto> Vinculos);
