namespace WebApi.Extensions;

/// <summary>
/// Comprobaciones del entorno que se hacen al arrancar. Una imagen sin la base de zonas horarias hace que la zona de cada
/// persona caiga a UTC en silencio y falsee sus rachas y sus horas de envío: es preferible no arrancar y decirlo.
/// </summary>
public static class VerificacionDelEntorno
{
    /// <summary>Zonas que el producto necesita (el piloto es en República Dominicana).</summary>
    public static readonly string[] ZonasRequeridas = ["America/Santo_Domingo"];

    public static void ExigirZonasHorarias(IEnumerable<string>? zonas = null)
    {
        foreach (var id in zonas ?? ZonasRequeridas)
        {
            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                throw new InvalidOperationException(
                    $"El sistema no conoce la zona horaria «{id}». Instala tzdata en la imagen y revisa InvariantGlobalization.", ex);
            }
        }
    }

    /// <summary>La Meta de demostración (con Id público) solo se siembra si no se pide lo contrario: en producción se desactiva.</summary>
    public static bool DebeSembrarDemostracion(IConfiguration configuracion) =>
        configuracion.GetValue("BaseDeDatos:SembrarDemostracion", true);
}
