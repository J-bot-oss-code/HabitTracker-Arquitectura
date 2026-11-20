using AccessControl.Application.Interfaces;
using AccessControl.Domain.Entities;

namespace AccessControl.Application.Service;

/// <summary>
/// Texto de los registros de auditoría de cuentas que se repiten en más de un flujo (RF-AUD-04), en un solo lugar para que el
/// inicio de sesión con contraseña y con Google no puedan describir lo mismo de dos maneras.
/// </summary>
internal static class AuditoriaDeCuentas
{
    public const string Activo = "Activo";
    public const string Inactivo = "Inactivo";

    public static string Estado(bool activo) => activo ? Activo : Inactivo;

    /// <summary>La persona había desactivado su propia cuenta y la reactivó al demostrar que es la dueña (inicio de sesión).</summary>
    public static Task ReactivadaPorSuDuenoAsync(this IAuditoriaService? auditoria, Usuario usuario) =>
        auditoria is null
            ? Task.CompletedTask
            : auditoria.RegistroAuditoriaAsync(
                usuario.Id, usuario.Rol.ToString(), "Reactivación de cuenta", "Usuario", usuario.Id.ToString(),
                $"{Inactivo} (desactivada por su dueño)", Activo);
}
