using Microsoft.AspNetCore.DataProtection;

namespace WebApi.Extensions;

/// <summary>
/// ASP.NET Core Data Protection para el cifrado en reposo (cuestionario de salud). Las claves deben persistir: si se
/// pierden, los datos cifrados no se pueden leer (RD-09). Por eso fuera de Development la ruta es obligatoria.
/// </summary>
public static class ProteccionDeDatosExtensions
{
    /// <summary>Variable de entorno con el directorio de las claves (un volumen con nombre en Docker).</summary>
    public const string VariableRutaClaves = "DATAPROTECTION_KEYS_PATH";

    public static IServiceCollection AddProteccionDeDatos(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment entorno)
    {
        var ruta = configuration[VariableRutaClaves];
        var proteccion = services.AddDataProtection().SetApplicationName("HabitTrackerNexus");

        if (!string.IsNullOrWhiteSpace(ruta))
        {
            proteccion.PersistKeysToFileSystem(new DirectoryInfo(ruta));
        }
        else if (!entorno.IsDevelopment())
        {
            // En Development se usa la carpeta del perfil del usuario (persistente en el equipo). En cualquier otro entorno
            // las claves por defecto podrían vivir dentro del contenedor y perderse al recrearlo.
            throw new InvalidOperationException(
                $"Falta {VariableRutaClaves}: el directorio persistente de las claves de cifrado de los datos de salud.");
        }

        return services;
    }
}
