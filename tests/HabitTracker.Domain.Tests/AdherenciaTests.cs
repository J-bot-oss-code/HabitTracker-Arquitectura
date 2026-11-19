using System.Reflection;
using HabitTracker.Domain.Adherencia;
using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Planes;

namespace HabitTracker.Domain.Tests;

/// <summary>Fase 4 de HabitTracker v2: progreso tolerante, sugeridor de ajustes y logros (dominio puro, P3 y P4).</summary>
public class AdherenciaTests
{
    // República Dominicana no cambia de hora: el día local es el UTC menos 4 horas.
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/Santo_Domingo");
    private static readonly DateTime Inicio = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc); // lunes 08:00 local
    private static readonly Guid TareaA = Guid.NewGuid();
    private static readonly Guid TareaB = Guid.NewGuid();
    private static readonly Guid Semanal = Guid.NewGuid();

    private static readonly TareaDelPlan DiariaA = new(TareaA, Periodicidad.Diaria);
    private static readonly TareaDelPlan DiariaB = new(TareaB, Periodicidad.Diaria);

    /// <summary>Un avance de la tarea el día <paramref name="dia"/> del plan, a las 10:00 locales.</summary>
    private static RegistroDeAvance Reg(Guid tarea, int dia, bool minima = false, int horaUtc = 14) =>
        new(tarea, Inicio.Date.AddDays(dia).AddHours(horaUtc), minima);

    private static ParametrosProgreso Parametros(
        int diaDeHoy, int semanas = 4, DateTime? modoSuaveHasta = null, params PausaDelPlan[] pausas) => new(
        Inicio,
        Inicio.AddDays(semanas * 7),
        semanas,
        Zona,
        Inicio.Date.AddDays(diaDeHoy).AddHours(20), // 16:00 locales del día de hoy
        modoSuaveHasta,
        pausas);

    private static ResultadoProgreso Calcular(int hoy, IEnumerable<RegistroDeAvance> registros,
        IReadOnlyList<TareaDelPlan>? tareas = null, int semanas = 4, DateTime? modoSuave = null, params PausaDelPlan[] pausas) =>
        CalculadoraProgreso.Calcular(tareas ?? [DiariaA], [.. registros], Parametros(hoy, semanas, modoSuave, pausas));

    // ---------- Racha ----------

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Progreso_UnDiaConAvanceEnLaUnicaTareaDiaria_EsCompletoYEmpiezaLaRacha()
    {
        var r = Calcular(0, [Reg(TareaA, 0)]);

        Assert.Equal(1, r.Racha);
        Assert.Equal(1, r.RachaMasLarga);
        Assert.True(r.CumplidoHoy);
        Assert.Equal(new Cumplimiento(1, 0, 1), r.Siete);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Progreso_UnDiaSinRegistro_NoCortaLaRacha()
    {
        // Días 0 y 1 cumplidos, el 2 sin registro y el 3 cumplido: sigue la misma racha.
        var r = Calcular(3, [Reg(TareaA, 0), Reg(TareaA, 1), Reg(TareaA, 3)]);

        Assert.Equal(3, r.Racha);
        Assert.Equal(3, r.RachaMasLarga);
        Assert.Empty(r.Regresos);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Progreso_DosDiasSeguidosSinRegistro_CortanLaRachaYElRegresoSeAnota()
    {
        var r = Calcular(4, [Reg(TareaA, 0), Reg(TareaA, 1), Reg(TareaA, 4)]);

        Assert.Equal(1, r.Racha);
        Assert.Equal(2, r.RachaMasLarga);
        Assert.Equal([DateOnly.FromDateTime(Inicio.AddDays(4))], r.Regresos);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Progreso_HoySinAvanceTodaviaNoSeDaPorFallado()
    {
        var r = Calcular(2, [Reg(TareaA, 0), Reg(TareaA, 1)]);

        Assert.Equal(2, r.Racha);
        Assert.Equal(0, r.DiasSinRegistroSeguidos);
        Assert.False(r.CumplidoHoy);
        // Hoy sin avance tampoco cuenta contra el cumplimiento.
        Assert.Equal(2, r.Siete.DiasElegibles);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Progreso_DosDiasSinRegistroYHoyPendiente_LaRachaQuedaEnCero()
    {
        var r = Calcular(3, [Reg(TareaA, 0)]);

        Assert.Equal(0, r.Racha);
        Assert.Equal(1, r.RachaMasLarga);
        Assert.Equal(2, r.DiasSinRegistroSeguidos);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Progreso_UnaPausaJustificadaNoCortaLaRachaNiCuentaComoDiasSinRegistro()
    {
        // Pausa del día 2 a las 10:00 locales hasta el día 6 a las 05:00 locales; el día 6 hay avance.
        var pausa = new PausaDelPlan(Inicio.Date.AddDays(2).AddHours(14), Inicio.Date.AddDays(6).AddHours(9));

        var r = Calcular(6, [Reg(TareaA, 0), Reg(TareaA, 1), Reg(TareaA, 6)], pausas: pausa);

        Assert.Equal(3, r.Racha);
        Assert.Empty(r.Regresos);
        Assert.Equal(0, r.DiasSinRegistroUltimos7);
        // Los días de pausa sin avance no avanzan el programa: 3 días del plan, no 7.
        Assert.Equal(3, r.DiasTranscurridos);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Progreso_UnDiaDePausaConAvanceSeEvaluaNormalmente()
    {
        var pausa = new PausaDelPlan(Inicio.Date.AddDays(1).AddHours(14), null);

        var r = Calcular(1, [Reg(TareaA, 0), Reg(TareaA, 1)], pausas: pausa);

        Assert.Equal(2, r.Racha);
        Assert.Equal(new Cumplimiento(2, 0, 2), r.Siete);
    }

    // ---------- Versión mínima, modo suave, parcial ----------

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Progreso_LaVersionMinimaCuentaComoDiaCumplidoPeroNoComoCompleto()
    {
        IReadOnlyList<TareaDelPlan> tareas = [DiariaA, DiariaB];

        var r = Calcular(1, [Reg(TareaA, 0), Reg(TareaB, 0, minima: true), Reg(TareaA, 1), Reg(TareaB, 1)], tareas);

        Assert.Equal(2, r.Racha);
        Assert.Equal(new Cumplimiento(1, 1, 2), r.Siete);
        Assert.Equal(50m, r.Siete.PorcentajeCompleto);
        Assert.Equal(100m, r.Siete.PorcentajeConMinimo);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Progreso_EnModoSuaveLaVersionMinimaCuentaComoCompleta()
    {
        IReadOnlyList<TareaDelPlan> tareas = [DiariaA, DiariaB];
        var modoSuave = Inicio.Date.AddDays(5);

        var r = Calcular(0, [Reg(TareaA, 0), Reg(TareaB, 0, minima: true)], tareas, modoSuave: modoSuave);

        Assert.Equal(new Cumplimiento(1, 0, 1), r.Siete);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Progreso_AvanceEnUnaSolaTareaEsParcial_CumpleElDiaPeroNoEsCompleto()
    {
        IReadOnlyList<TareaDelPlan> tareas = [DiariaA, DiariaB];

        var r = Calcular(0, [Reg(TareaA, 0)], tareas);

        Assert.Equal(1, r.Racha);
        Assert.Equal(new Cumplimiento(0, 0, 1), r.Siete);
    }

    // ---------- Planes solo semanales: se miden por semanas (P3) ----------

    // Tres salidas por semana: la semana del lunes 2 al domingo 8 son los días 0 a 6.
    private static readonly IReadOnlyList<TareaDelPlan> TresPorSemana = [new(Semanal, Periodicidad.Semanal, 3)];

    private static ResultadoProgreso Semanales(int hoy, params int[] diasConSesion) =>
        Calcular(hoy, diasConSesion.Select(d => Reg(Semanal, d)), TresPorSemana);

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_ProgresoSemanal_UnaSemanaConLaMetaCumplidaEsCompletaYLaRachaSeCuentaEnSemanas()
    {
        var r = Semanales(5, 0, 2, 4); // lunes, miércoles y viernes

        Assert.Equal(UnidadSeguimiento.Semanas, r.Unidad);
        Assert.Equal(1, r.Racha);
        Assert.Equal(new Cumplimiento(1, 0, 1), r.Siete);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_ProgresoSemanal_LosDiasVaciosEntreSesionesSonLoEsperado_NoCortanNiPidenAjustes()
    {
        // Sesiones los días 0, 2, 4, 7 y 9: entre una y otra siempre hay días sin registro.
        var r = Semanales(10, 0, 2, 4, 7, 9);

        Assert.Equal(2, r.Racha);
        Assert.Equal(0, r.DiasSinRegistroSeguidos);
        Assert.Equal(0, r.DiasSinRegistroUltimos7);
        Assert.Null(SugeridorAjuste.Sugerir(r, EstadoPlanUsuario.Activo, 0, false));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_ProgresoSemanal_UnaSemanaVaciaNoCorta_DosSeguidasSi()
    {
        // Semana 1 completa, semana 2 vacía y semana 3 con sesiones: sigue la racha.
        var tolera = Semanales(16, 0, 2, 4, 14, 16);
        // Semana 1 completa, semanas 2 y 3 vacías y hoy (semana 4) todavía sin sesión: se cortó.
        var corta = Semanales(22, 0, 2, 4);

        Assert.Equal(2, tolera.Racha);
        Assert.Equal(0, corta.Racha);
        Assert.Equal(1, corta.RachaMasLarga);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_ProgresoSemanal_UnaSemanaEnteraSinSesion_PidePocoAPoco_YElRegresoSeAnota()
    {
        var sinSesion = Semanales(11, 0, 2, 4); // pasaron 7 días desde la última sesión (día 4)
        var volvio = Semanales(23, 0, 2, 4, 22);

        Assert.Equal(7, sinSesion.DiasSinRegistroSeguidos);
        Assert.Equal(7, sinSesion.DiasSinRegistroUltimos7);
        Assert.NotNull(SugeridorAjuste.Sugerir(sinSesion, EstadoPlanUsuario.Activo, 0, false));
        Assert.Equal([DateOnly.FromDateTime(Inicio.AddDays(22))], volvio.Regresos);
        Assert.Equal(1, volvio.Racha);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_ProgresoSemanal_UnaSemanaConMenosSesionesQueLaMetaEsParcial_YCumpleLaSemana()
    {
        var r = Semanales(8, 0, 2); // 2 de 3 sesiones; la semana ya cerró

        Assert.Equal(1, r.Racha);
        Assert.Equal(new Cumplimiento(0, 0, 1), r.Siete);
        Assert.Empty(r.SemanasCelebradas);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_ProgresoSemanal_SeCelebraLaSemanaCerradaConLaMetaCumplida()
    {
        Assert.Equal([1], Semanales(8, 0, 2, 4).SemanasCelebradas);
        Assert.Empty(Semanales(5, 0, 2, 4).SemanasCelebradas); // la semana sigue en curso
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_ProgresoSemanal_ElModoSuaveHaceCompletasLasSesionesMinimas()
    {
        var suave = Inicio.Date.AddDays(7);
        var minimas = new[] { 0, 2, 4 }.Select(d => Reg(Semanal, d, minima: true)).ToList();

        var sin = Calcular(5, minimas, TresPorSemana);
        var con = Calcular(5, minimas, TresPorSemana, modoSuave: suave);

        Assert.Equal(new Cumplimiento(0, 1, 1), sin.Siete);
        Assert.Equal(new Cumplimiento(1, 0, 1), con.Siete);
    }

    // ---------- Duración del plan y pausas ----------

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Progreso_UnPlanDeCuatroSemanasDura28DiasNoVeintinueve()
    {
        // Hoy es el día 29 (pasado el fin): el plan tiene exactamente 28 días y la semana 4 cierra.
        var r = Calcular(29, Enumerable.Range(0, 28).Select(d => Reg(TareaA, d)), semanas: 4);

        Assert.Equal(28, r.DiasTranscurridos);
        Assert.Equal(4, r.SemanaActual);
        Assert.Equal([1, 2, 3, 4], r.SemanasCelebradas);
        Assert.Equal(0, r.DiasRestantes);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Progreso_LosDiasExcluidosPorUnaPausaSonLosQueReanudarAgregaAlFinal()
    {
        // Pausa del día 2 (10:00 local) al día 4 (05:00 local): 2 días locales fuera, y Reanudar agrega esos mismos 2.
        var inicioPausa = Inicio.Date.AddDays(2).AddHours(14);
        var finPausa = Inicio.Date.AddDays(4).AddHours(9);
        var pausa = new PausaDelPlan(inicioPausa, finPausa);

        var r = Calcular(4, [Reg(TareaA, 0), Reg(TareaA, 4)], pausas: pausa);

        Assert.Equal(2, PlanUsuario.DiasDePausa(inicioPausa, finPausa, Zona));
        // Días 0, 1 y 4 cuentan; el 2 y el 3 no.
        Assert.Equal(3, r.DiasTranscurridos);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Progreso_ElDiaEnQueSeReanudaYaCuentaYNoEsDePausa()
    {
        // Pausa del día 2 al día 4 (se reanudó el 4 sin registrar nada ese día): el 2 y el 3 son de pausa; el 4 ya no.
        var pausa = new PausaDelPlan(Inicio.Date.AddDays(2).AddHours(14), Inicio.Date.AddDays(4).AddHours(14));

        var r = Calcular(6, [Reg(TareaA, 0), Reg(TareaA, 5)], pausas: pausa);

        // Cuentan los días 0, 1, 4, 5 y 6.
        Assert.Equal(5, r.DiasTranscurridos);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_ProgresoSemanal_UnLunesSinSesionesTodaviaMuestraLaSemanaQueAcabaDeCerrar()
    {
        // Hoy es lunes (día 7) y la semana nueva aún está vacía: el resumen de 7 días sigue siendo el de la semana cerrada.
        var r = Semanales(7, 0, 2, 4);

        Assert.Equal(new Cumplimiento(1, 0, 1), r.Siete);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Progreso_UnaTareaDiariaPausadaOCompletadaNoDejaTodosLosDiasParciales()
    {
        // La persona dejó de seguir la tarea B: el plan solo mide la A (el que la llama le pasa solo las tareas que sigue).
        var r = Calcular(1, [Reg(TareaA, 0), Reg(TareaA, 1)], [DiariaA]);

        Assert.Equal(new Cumplimiento(2, 0, 2), r.Siete);
    }

    // ---------- Día local ----------

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    [Trait("Requirement", "RD-11")]
    public void RD_11_Progreso_ElDiaEsElLocalNoElUtc_UnAvanceDeLaNocheNoSeVaAlDiaSiguiente()
    {
        // 02:00 UTC del día 1 son las 22:00 del día 0 en Santo Domingo.
        var r = Calcular(0, [Reg(TareaA, 1, horaUtc: 2)]);

        Assert.True(r.CumplidoHoy);
        Assert.Equal(1, r.Racha);
    }

    // ---------- Semanas, tiempo restante y celebración ----------

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Progreso_SemanaActualYDiasRestantes()
    {
        var r = Calcular(9, Enumerable.Range(0, 10).Select(d => Reg(TareaA, d)), semanas: 3);

        Assert.Equal(2, r.SemanaActual);
        Assert.Equal(3, r.SemanasTotales);
        Assert.Equal(21 - 9, r.DiasRestantes);
        Assert.Equal(10, r.DiasTranscurridos);
    }

    [Theory]
    [InlineData(5, true)]
    [InlineData(4, false)]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Progreso_UnaSemanaCerradaConCincoDiasCumplidosSeCelebra(int diasCumplidos, bool celebrada)
    {
        // Hoy es el día 8, así que la semana 1 (días 0 a 6) ya cerró.
        var r = Calcular(8, Enumerable.Range(0, diasCumplidos).Select(d => Reg(TareaA, d)), semanas: 3);

        Assert.Equal(celebrada ? new[] { 1 } : [], r.SemanasCelebradas);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Progreso_LaSemanaEnCursoTodaviaNoSeCelebra()
    {
        var r = Calcular(6, Enumerable.Range(0, 7).Select(d => Reg(TareaA, d)), semanas: 3);

        Assert.Empty(r.SemanasCelebradas);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Progreso_DiasSinRegistroDeLosUltimosSiete()
    {
        var r = Calcular(6, [Reg(TareaA, 0), Reg(TareaA, 3)]);

        // Días 1, 2, 4 y 5 sin registro; el 6 es hoy y no cuenta todavía.
        Assert.Equal(4, r.DiasSinRegistroUltimos7);
    }

    // ---------- Sugeridor de ajustes ----------

    private static ResultadoProgreso ConDiasSinRegistro(int dias) =>
        Calcular(0, [Reg(TareaA, 0)]) with { DiasSinRegistroUltimos7 = dias };

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Sugeridor_CuatroDiasSinRegistro_OfreceModoSuaveExtenderOPausar()
    {
        var sugerencia = SugeridorAjuste.Sugerir(ConDiasSinRegistro(4), EstadoPlanUsuario.Activo, 0, enModoSuave: false);

        Assert.NotNull(sugerencia);
        Assert.Equal([TipoAjuste.ModoSuave, TipoAjuste.Extender, TipoAjuste.Pausar], sugerencia.Opciones);
        Assert.Equal(4, sugerencia.DiasSinRegistro);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Sugeridor_TresDiasSinRegistro_NoSugiereNada()
    {
        Assert.Null(SugeridorAjuste.Sugerir(ConDiasSinRegistro(3), EstadoPlanUsuario.Activo, 0, false));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Sugeridor_NoOfreceLoQueYaNoSePuede()
    {
        var sinExtender = SugeridorAjuste.Sugerir(ConDiasSinRegistro(5), EstadoPlanUsuario.Activo, PlanUsuario.MaximoExtensiones, true);

        Assert.Equal([TipoAjuste.Pausar], sinExtender!.Opciones);
    }

    [Theory]
    [InlineData(EstadoPlanUsuario.Pausado)]
    [InlineData(EstadoPlanUsuario.Completado)]
    [InlineData(EstadoPlanUsuario.Cancelado)]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Sugeridor_SoloUnPlanActivoSeAjusta(EstadoPlanUsuario estado)
    {
        Assert.Null(SugeridorAjuste.Sugerir(ConDiasSinRegistro(6), estado, 0, false));
    }

    // ---------- Logros ----------

    private static EntradaLogros Entrada(int avances = 0, int completados = 0, int racha = 0, bool regreso = false,
        bool mitad = false, bool plan = false) => new(avances, completados, racha, regreso, mitad, plan);

    private static readonly IReadOnlySet<ClaveLogro> Ninguno = new HashSet<ClaveLogro>();

    [Theory]
    [InlineData(1, 0, 0, ClaveLogro.PrimerAvance)]
    [InlineData(0, 0, 7, ClaveLogro.PrimeraSemana)]
    [InlineData(25, 0, 0, ClaveLogro.VeinticincoAvances)]
    [InlineData(0, 0, 21, ClaveLogro.Constancia21)]
    [InlineData(0, 0, 30, ClaveLogro.Constancia30)]
    [InlineData(0, 1, 0, ClaveLogro.MetaCumplida)]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Logros_CadaUmbralGanaSuLogro(int avances, int completados, int racha, ClaveLogro esperado)
    {
        Assert.Contains(esperado, CalculadoraLogros.Evaluar(Entrada(avances, completados, racha), Ninguno));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Logros_ElRegresoLaMitadYElPlanCompletadoSeGanan()
    {
        var ganados = CalculadoraLogros.Evaluar(Entrada(regreso: true, mitad: true, plan: true), Ninguno);

        Assert.Equal([ClaveLogro.Regreso, ClaveLogro.MitadDelPlan, ClaveLogro.PlanCompletado], ganados);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Logros_UnLogroYaObtenidoNoSeVuelveAGanar_Ni_UnoSinMerito()
    {
        var yaTiene = new HashSet<ClaveLogro> { ClaveLogro.PrimerAvance };

        var ganados = CalculadoraLogros.Evaluar(Entrada(avances: 3, racha: 6), yaTiene);

        Assert.Empty(ganados);
    }

    [Fact]
    [Trait("Requirement", "RD-12")]
    public void RD_12_Logros_PorReflexion_NingunaFirmaNiLaEntradaMencionanPesoMedidasNiImc()
    {
        // P4: se motiva la conducta, nunca el cuerpo. Si alguien añade un dato corporal a la calculadora, esta prueba falla.
        var prohibidos = new[] { "peso", "medicion", "medida", "imc", "kg", "cuerpo", "grasa", "RegistroPeso", "MedicionCorporal" };
        var tipos = new List<string>();
        foreach (var metodo in typeof(CalculadoraLogros).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            tipos.Add(metodo.Name);
            tipos.AddRange(metodo.GetParameters().SelectMany(p => new[] { p.Name!, p.ParameterType.ToString() }));
        }

        tipos.AddRange(typeof(EntradaLogros).GetProperties().SelectMany(p => new[] { p.Name, p.PropertyType.ToString() }));

        foreach (var texto in tipos)
            foreach (var palabra in prohibidos)
                Assert.DoesNotContain(palabra, texto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_LogroUsuario_SeFelicitaUnaSolaVez()
    {
        var ahora = new DateTime(2026, 11, 9, 15, 0, 0, DateTimeKind.Utc);
        var logro = new LogroUsuario(Guid.NewGuid(), ClaveLogro.PrimerAvance, ahora);

        Assert.Null(logro.VistoEn);
        logro.MarcarVisto(ahora);
        logro.MarcarVisto(ahora.AddDays(1));

        Assert.Equal(ahora, logro.VistoEn);
    }
}
