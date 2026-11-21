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

    /// <summary>La persona activó su cuenta con el código del correo.</summary>
    public static Task ActivadaAsync(this IAuditoriaService? auditoria, Usuario usuario) =>
        RegistrarDelDuenoAsync(auditoria, usuario, "Activación de cuenta", "Pendiente de activación", Activo);

    /// <summary>La persona cambió su contraseña desde la app (nunca se guarda ninguna de las dos).</summary>
    public static Task ContrasenaCambiadaAsync(this IAuditoriaService? auditoria, Usuario usuario) =>
        RegistrarDelDuenoAsync(auditoria, usuario, "Cambio de contraseña",
            "Contraseña anterior (no se guarda)", "Contraseña nueva (no se guarda)");

    /// <summary>La persona vinculó (o desvinculó) su cuenta de Google.</summary>
    public static Task GoogleVinculadoAsync(this IAuditoriaService? auditoria, Usuario usuario, bool vinculado) =>
        vinculado
            ? RegistrarDelDuenoAsync(auditoria, usuario, "Vinculación de Google", "Sin Google", "Google vinculado")
            : RegistrarDelDuenoAsync(auditoria, usuario, "Desvinculación de Google", "Google vinculado", "Sin Google");

    private static Task RegistrarDelDuenoAsync(
        IAuditoriaService? auditoria, Usuario usuario, string accion, string anterior, string nuevo) =>
        auditoria is null
            ? Task.CompletedTask
            : auditoria.RegistroAuditoriaAsync(
                usuario.Id, usuario.Rol.ToString(), accion, "Usuario", usuario.Id.ToString(), anterior, nuevo);
}
