using HabitTracker.Domain.Adherencia;

namespace HabitTracker.Domain.Tests;

/// <summary>«¿Qué tan constante fui en este plan?» (RF-REP-02): pura, sin base de datos ni reloj (RD-12).</summary>
public class CalculadoraConstanciaTests
{
    private static readonly DateOnly Lunes = new(2026, 11, 2);
    private static DateOnly Dia(int desdeElLunes) => Lunes.AddDays(desdeElLunes);

    private static ConstanciaEnRango Calcular(
        int desde, int hasta, IEnumerable<AvanceDelDia> avances, int inicioPlan = 0, int finPlan = 27, int hoy = 27,
        IEnumerable<PausaEnDias>? pausas = null) =>
        CalculadoraConstancia.Calcular(Dia(desde), Dia(hasta), Dia(inicioPlan), Dia(finPlan), Dia(hoy), pausas ?? [], avances);

    [Fact]
    [Trait("Requirement", "RF-REP-02")]
    public void RF_REP_02_CuentaLosDiasConAvanceYElPorcentajeSobreLosDiasDelRango()
    {
        // Una semana (7 días): avances completos 3 días y la versión mínima otros 2.
        var avances = new[]
        {
            new AvanceDelDia(Dia(0), false), new AvanceDelDia(Dia(1), false), new AvanceDelDia(Dia(2), false),
            new AvanceDelDia(Dia(4), true), new AvanceDelDia(Dia(5), true),
        };

        var r = Calcular(0, 6, avances);

        Assert.Equal(new ConstanciaEnRango(7, 5, 2, 71.4m), r);
    }

    [Fact]
    [Trait("Requirement", "RF-REP-02")]
    public void RF_REP_02_UnDiaConAvancesCompletoYMinimoCuentaComoCompleto_NoComoSoloMinimo()
    {
        var avances = new[] { new AvanceDelDia(Dia(0), true), new AvanceDelDia(Dia(0), false) };

        var r = Calcular(0, 0, avances);

        Assert.Equal(1, r.DiasConAvance);
        Assert.Equal(0, r.DiasSoloMinimo);
    }

    [Fact]
    [Trait("Requirement", "RF-REP-05")]
    public void RF_REP_05_DosRangosDistintosDanResultadosDistintosConLosMismosAvances()
    {
        var avances = new[] { new AvanceDelDia(Dia(1), false), new AvanceDelDia(Dia(2), false), new AvanceDelDia(Dia(9), false) };

        var primera = Calcular(0, 6, avances);
        var segunda = Calcular(7, 13, avances);

        Assert.Equal(2, primera.DiasConAvance);
        Assert.Equal(1, segunda.DiasConAvance);
        Assert.NotEqual(primera, segunda);
    }

    [Fact]
    [Trait("Requirement", "RF-REP-02")]
    public void RF_REP_02_LosDiasAnterioresAlInicioDelPlanYPosterioresAHoyNoCuentan()
    {
        // El plan empezó el día 3 y hoy es el día 5: del rango 0..13 solo cuentan 3, 4 y 5.
        var r = Calcular(0, 13, [new AvanceDelDia(Dia(3), false)], inicioPlan: 3, hoy: 5);

        Assert.Equal(new ConstanciaEnRango(3, 1, 0, 33.3m), r);
    }

    [Fact]
    [Trait("Requirement", "RF-REP-02")]
    public void RF_REP_02_UnPlanQueYaTerminoSoloCuentaHastaSuFin()
    {
        var r = Calcular(0, 27, [], inicioPlan: 0, finPlan: 6, hoy: 27);

        Assert.Equal(7, r.DiasEnRango);
    }

    [Fact]
    [Trait("Requirement", "RF-REP-02")]
    public void RF_REP_02_LosDiasDePausaNoCuentanNiAFavorNiEnContra()
    {
        // Pausa del día 2 al 4 (3 días) y otra abierta desde el 6: del rango 0..6 cuentan 0, 1, 5 (el 6 es de la pausa abierta).
        var pausas = new[] { new PausaEnDias(Dia(2), Dia(4)), new PausaEnDias(Dia(6), null) };
        var avances = new[] { new AvanceDelDia(Dia(1), false), new AvanceDelDia(Dia(3), false) }; // el del día 3 cae en pausa

        var r = Calcular(0, 6, avances, pausas: pausas);

        Assert.Equal(new ConstanciaEnRango(3, 1, 0, 33.3m), r);
    }

    [Fact]
    [Trait("Requirement", "RF-REP-02")]
    public void RF_REP_02_SinDiasEnElRangoNoHayPorcentaje_NoUnaDivisionPorCero()
    {
        // El rango termina antes de que empezara el plan.
        var r = Calcular(0, 3, [new AvanceDelDia(Dia(1), false)], inicioPlan: 10);

        Assert.Equal(new ConstanciaEnRango(0, 0, 0, null), r);
    }

    [Fact]
    [Trait("Requirement", "RF-REP-03")]
    public void RF_REP_03_ElResultadoEsUnaFilaPorPlan_NoUnaPorAvance()
    {
        var avances = Enumerable.Range(0, 20).Select(i => new AvanceDelDia(Dia(i), false)).ToList();

        var r = Calcular(0, 19, avances);

        Assert.Equal(20, r.DiasConAvance);
        Assert.Equal(100m, r.PorcentajeConstancia);
    }

    [Fact]
    [Trait("Requirement", "RD-12")]
    public void RD_12_LaCalculadoraNoRecibeDatosDeSaludNiPesoNiMedidas()
    {
        var parametros = typeof(CalculadoraConstancia).GetMethod(nameof(CalculadoraConstancia.Calcular))!.GetParameters();
        var nombres = string.Join(' ', parametros.Select(p => p.Name + " " + p.ParameterType.Name)).ToLowerInvariant();

        Assert.DoesNotContain("peso", nombres);
        Assert.DoesNotContain("medida", nombres);
        Assert.DoesNotContain("salud", nombres);
        Assert.DoesNotContain("imc", nombres);
    }
}
