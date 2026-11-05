namespace AccessControl.Application.Interfaces;

/// <summary>Política de contraseñas del sistema: se aplica al registrarse, al restablecerla y al cambiarla (RF-CA-14, RD-07).</summary>
public interface IPoliticaContrasena
{
    /// <summary>Formato mínimo. Es rápido y no consulta nada externo: se ejecuta antes de tocar la base de datos.</summary>
    /// <exception cref="AccessControl.Domain.Excepciones.ContrasenaNoValidaException"/>
    void ValidarFormato(string? password);

    /// <summary>Rechaza las contraseñas que aparecen en filtraciones conocidas.</summary>
    /// <exception cref="AccessControl.Domain.Excepciones.ContrasenaFiltradaException"/>
    Task ValidarNoFiltradaAsync(string password, CancellationToken cancellationToken = default);
}
