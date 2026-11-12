using HabitTracker.Domain.Perfil;

namespace HabitTracker.Domain.Planes;

/// <summary>La jornada de la persona: lo que el resolvedor necesita para traducir un ancla a una hora.</summary>
public sealed record JornadaPersona(
    PerfilLaboral PerfilLaboral,
    TimeOnly? HoraEntrada,
    TimeOnly? HoraSalida,
    TimeOnly? HoraDespertar,
    TimeOnly? HoraDormir)
{
    public static JornadaPersona Desde(PerfilUsuario perfil)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        return new JornadaPersona(perfil.PerfilLaboral, perfil.HoraEntradaTrabajo, perfil.HoraSalidaTrabajo,
            perfil.HoraDespertar, perfil.HoraDormir);
    }
}

/// <summary>
/// Traduce un ancla ("al llegar al trabajo", "antes de dormir") a una hora local sugerida según la jornada de la persona
/// (servicio de dominio puro). Es el único punto que lo hace: la inscripción lo usa hoy y los recordatorios por correo
/// (Fase 5) lo usarán mañana. Las horas que la persona dio mandan; sin ellas hay valores por defecto por jornada, y el
/// turno nocturno tiene los suyos (se despierta por la tarde y duerme de día).
/// </summary>
public static class ResolvedorAncla
{
    private static readonly TimeSpan DuracionPorDefecto = TimeSpan.FromHours(8);

    public static TimeOnly Resolver(Ancla ancla, JornadaPersona jornada)
    {
        ArgumentNullException.ThrowIfNull(jornada);
        if (!Enum.IsDefined(ancla))
            throw new ArgumentOutOfRangeException(nameof(ancla), "Ancla no válida.");

        var nocturna = jornada.PerfilLaboral is PerfilLaboral.TurnoNocturno or PerfilLaboral.TurnosRotativos;
        var entrada = jornada.HoraEntrada ?? (nocturna ? new TimeOnly(22, 0) : new TimeOnly(8, 0));
        var salida = jornada.HoraSalida ?? (nocturna ? new TimeOnly(6, 0) : new TimeOnly(17, 0));
        var despertar = jornada.HoraDespertar ?? (nocturna ? new TimeOnly(17, 0) : new TimeOnly(6, 30));
        var dormir = jornada.HoraDormir ?? (nocturna ? new TimeOnly(7, 30) : new TimeOnly(22, 30));
        var duracion = DuracionDeLaJornada(entrada, salida);

        return ancla switch
        {
            Ancla.AlDespertar => despertar,
            Ancla.AlLlegarAlTrabajo => entrada,
            Ancla.AMediaManana => entrada.Add(duracion * 0.25),
            Ancla.DespuesDelAlmuerzo => entrada.Add(duracion * 0.5).Add(TimeSpan.FromMinutes(30)),
            Ancla.AMediaTarde => entrada.Add(duracion * 0.75),
            Ancla.AlSalirDelTrabajo => salida,
            Ancla.AntesDeCenar => dormir.Add(TimeSpan.FromHours(-3)),
            Ancla.AntesDeDormir => dormir.Add(TimeSpan.FromMinutes(-30)),
            _ => throw new ArgumentOutOfRangeException(nameof(ancla), "Ancla no válida."),
        };
    }

    // La resta de TimeOnly va hacia adelante y envuelve la medianoche: de 22:00 a 06:00 son 8 h. Una entrada igual a la
    // salida no dice nada de la duración: se toma como 8 h.
    private static TimeSpan DuracionDeLaJornada(TimeOnly entrada, TimeOnly salida)
    {
        var duracion = salida - entrada;
        return duracion == TimeSpan.Zero ? DuracionPorDefecto : duracion;
    }
}
