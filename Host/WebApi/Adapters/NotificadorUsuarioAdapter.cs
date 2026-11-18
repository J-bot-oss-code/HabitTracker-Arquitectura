using HabitTracker.Application.Interfaces;
using HabitTracker.Domain.Adherencia;
using Notifications.Application.Interfaces;
using Notifications.Domain;

namespace WebApi.Adapters;

/// <summary>
/// Conecta los avisos de HabitTracker con el motor de Notificaciones (RF-NOT-03). Código de composición del Host: ninguno de
/// los dos módulos conoce al otro (RD-03). El texto celebra la conducta y nunca habla de peso, cuerpo ni culpa (P3 y P4).
/// </summary>
public sealed class NotificadorUsuarioAdapter(INotificacionService notificaciones, IDirectorioUsuarios directorio) : INotificadorUsuario
{
    public async Task LogroObtenidoAsync(Guid usuarioId, ClaveLogro clave, CancellationToken cancellationToken = default)
    {
        var persona = await directorio.ObtenerPorIdAsync(usuarioId, cancellationToken);
        if (persona is null)
        {
            return; // la cuenta ya no existe o no está activa: nadie a quien felicitar
        }

        var (titulo, mensaje) = Texto(clave);
        await notificaciones.NotificarAsync([persona], TipoNotificacion.LogroObtenido, titulo, mensaje, cancellationToken);
    }

    // Una entrada por logro: si se añade una clave nueva, la prueba de cobertura obliga a darle su texto.
    public static (string Titulo, string Mensaje) Texto(ClaveLogro clave) => clave switch
    {
        ClaveLogro.PrimerAvance => ("Primer avance", "Registraste tu primer avance. Empezar es lo más difícil."),
        ClaveLogro.PrimeraSemana => ("Una semana de constancia", "Completaste una semana seguida. Cada día cuenta, también la versión mínima."),
        ClaveLogro.VeinticincoAvances => ("25 avances", "Ya van 25 avances registrados. Se nota tu constancia."),
        ClaveLogro.Constancia21 => ("21 días de constancia", "Tres semanas de constancia. Ya es parte de tu rutina."),
        ClaveLogro.Constancia30 => ("30 días de constancia", "Un mes entero de constancia. Gracias por quedarte."),
        ClaveLogro.MetaCumplida => ("Meta cumplida", "Cumpliste la meta de un hábito. Buen trabajo."),
        ClaveLogro.Regreso => ("Volviste", "Volver también cuenta. Qué bueno verte de nuevo por aquí."),
        ClaveLogro.MitadDelPlan => ("Mitad del plan", "Llegaste a la mitad de tu plan. Sigue a tu ritmo."),
        ClaveLogro.PlanCompletado => ("Plan completado", "Terminaste tu plan. Gracias por tu constancia."),
        _ => throw new ArgumentOutOfRangeException(nameof(clave), clave, "Logro sin texto de aviso."),
    };
}
