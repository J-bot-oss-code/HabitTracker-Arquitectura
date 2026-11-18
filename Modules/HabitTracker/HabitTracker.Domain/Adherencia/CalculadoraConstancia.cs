namespace HabitTracker.Domain.Adherencia;

/// <summary>Un día local en que hubo avance en el plan, y si todo lo hecho ese día fue la versión mínima.</summary>
public sealed record AvanceDelDia(DateOnly Dia, bool EsVersionMinima);

/// <summary>Un tramo de pausa del plan en días locales. <c>Hasta</c> nulo = la pausa sigue abierta.</summary>
public sealed record PausaEnDias(DateOnly Desde, DateOnly? Hasta);

/// <summary>La constancia de un plan en un rango de fechas.</summary>
public sealed record ConstanciaEnRango(int DiasEnRango, int DiasConAvance, int DiasSoloMinimo, decimal? PorcentajeConstancia);

/// <summary>
/// Responde «¿qué tan constante fui en este plan durante este período?» (RF-REP-02): de los días del rango en que el plan estaba
/// en marcha, en cuántos hubo algún avance (completo o la versión mínima) y en cuántos solo la mínima. Es pura: no conoce la base
/// de datos ni el reloj, y no recibe peso, medidas ni datos de salud (P4). Los días que cuentan van del inicio del plan hasta
/// su fin, hoy o el final del rango (lo que llegue primero), sin los días de pausa: una pausa justificada no resta constancia (P3).
/// </summary>
public static class CalculadoraConstancia
{
    public static ConstanciaEnRango Calcular(
        DateOnly desde, DateOnly hasta, DateOnly inicioPlan, DateOnly finPlan, DateOnly hoy,
        IEnumerable<PausaEnDias> pausas, IEnumerable<AvanceDelDia> avances)
    {
        ArgumentNullException.ThrowIfNull(pausas);
        ArgumentNullException.ThrowIfNull(avances);

        var primero = desde > inicioPlan ? desde : inicioPlan;
        var ultimo = new[] { hasta, finPlan, hoy }.Min();
        if (primero > ultimo)
        {
            return new ConstanciaEnRango(0, 0, 0, null);
        }

        var tramos = pausas.ToList();
        var porDia = avances.GroupBy(a => a.Dia).ToDictionary(g => g.Key, g => g.All(a => a.EsVersionMinima));

        int enRango = 0, conAvance = 0, soloMinimo = 0;
        for (var dia = primero; dia <= ultimo; dia = dia.AddDays(1))
        {
            if (tramos.Any(p => dia >= p.Desde && (p.Hasta is null || dia <= p.Hasta)))
            {
                continue; // un día de pausa no cuenta ni a favor ni en contra
            }

            enRango++;
            if (porDia.TryGetValue(dia, out var soloLaMinima))
            {
                conAvance++;
                if (soloLaMinima)
                {
                    soloMinimo++;
                }
            }
        }

        decimal? porcentaje = enRango == 0 ? null : Math.Round(conAvance * 100m / enRango, 1);
        return new ConstanciaEnRango(enRango, conAvance, soloMinimo, porcentaje);
    }
}
