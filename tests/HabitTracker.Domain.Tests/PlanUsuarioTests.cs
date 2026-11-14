using HabitTracker.Domain.Adherencia;
using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Excepciones;
using HabitTracker.Domain.Motivacion;
using HabitTracker.Domain.Perfil;
using HabitTracker.Domain.Planes;
using HabitTracker.Domain.Salud;

namespace HabitTracker.Domain.Tests;

/// <summary>Fase 4 de HabitTracker v2: el plan de la persona, su máquina de estados, el ancla y los mensajes.</summary>
public class PlanUsuarioTests
{
    private static readonly Guid Usuario = Guid.NewGuid();
    private static readonly DateTime Ahora = new(2026, 11, 9, 15, 0, 0, DateTimeKind.Utc);

    private static PlanPlantilla Plantilla(int semanas = 4) => new(new ContenidoPlan(
        "plan-prueba", "Plan de prueba", "Resumen.", "Descripción.", AreaPlan.Estres, NivelPlan.Basico, IntensidadPlan.Baja,
        SexoPlan.Todos, 16, 100, semanas, [], null, false, [], [], [], ["Uno", "Dos", "Tres"], "Aviso.", [],
        RequiereCuestionario: false, VersionContenido: 1,
        [new TareaPlantilla(1, "Respirar", "Inhala y exhala.", Periodicidad.Diaria, 5, "minutos", "3 respiraciones.", Ancla.AMediaTarde)]));

    private static PlanUsuario Plan(int semanas = 4) => PlanUsuario.Iniciar(Usuario, Plantilla(semanas), new TimeOnly(10, 0), Ahora);

    private static void AssertCodigo(string codigo, Action accion) =>
        Assert.Equal(codigo, Assert.Throws<ReglaNegocioException>(accion).Codigo);

    // ---------- Iniciar ----------

    [Fact]
    [Trait("Requirement", "RF-NEG-01")]
    public void RF_NEG_01_Plan_Iniciar_CopiaLaPlantillaYEmpiezaActivo()
    {
        var plan = Plan(semanas: 3);

        Assert.Equal(EstadoPlanUsuario.Activo, plan.Estado);
        Assert.Equal("plan-prueba", plan.Slug);
        Assert.Equal("Plan de prueba", plan.Nombre);
        Assert.Equal(OrigenPlan.Catalogo, plan.Origen);
        Assert.Equal(Ahora.AddDays(21), plan.FechaFinPrevista);
        Assert.Equal(3, plan.SemanasTotales);
        Assert.False(plan.RecordatoriosActivos);
        Assert.Equal(new TimeOnly(10, 0), plan.HoraRecordatorio);
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_Plan_Iniciar_ConIdentidadVaciaOFechaNoUtc_SeRechaza()
    {
        Assert.Throws<ArgumentException>(() => PlanUsuario.Iniciar(Guid.Empty, Plantilla(), null, Ahora));
        Assert.Throws<ArgumentException>(() => PlanUsuario.Iniciar(Usuario, Plantilla(), null, DateTime.SpecifyKind(Ahora, DateTimeKind.Local)));
    }

    // ---------- Máquina de estados ----------

    [Theory]
    [InlineData(EstadoPlanUsuario.Activo, EstadoPlanUsuario.Pausado, false, true)]
    [InlineData(EstadoPlanUsuario.Pausado, EstadoPlanUsuario.Activo, false, true)]
    [InlineData(EstadoPlanUsuario.Activo, EstadoPlanUsuario.Cancelado, false, true)]
    [InlineData(EstadoPlanUsuario.Pausado, EstadoPlanUsuario.Cancelado, false, true)]
    [InlineData(EstadoPlanUsuario.Activo, EstadoPlanUsuario.Completado, true, true)]
    [InlineData(EstadoPlanUsuario.Activo, EstadoPlanUsuario.Completado, false, false)]
    [InlineData(EstadoPlanUsuario.Activo, EstadoPlanUsuario.Activo, false, false)]
    [InlineData(EstadoPlanUsuario.Pausado, EstadoPlanUsuario.Pausado, false, false)]
    [Trait("Requirement", "RF-NEG-03")]
    [Trait("Requirement", "RD-04")]
    public void RF_NEG_03_Transiciones_TablaDeTransicionesPermitidas(
        EstadoPlanUsuario actual, EstadoPlanUsuario nuevo, bool llegoElFin, bool permitida)
    {
        Assert.Equal(permitida, TransicionesPlanUsuario.EsTransicionValida(actual, nuevo, llegoElFin));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-04")]
    public void RF_NEG_04_Transiciones_PausadoACompletado_EstaProhibidoYElEstadoNoCambia()
    {
        var plan = Plan();
        plan.Pausar(MotivoPausa.Viaje, Ahora);

        AssertCodigo(ReglaNegocioException.TransicionPlanInvalida, () => plan.Completar(Ahora.AddDays(60)));

        Assert.Equal(EstadoPlanUsuario.Pausado, plan.Estado);
    }

    [Theory]
    [InlineData(EstadoPlanUsuario.Completado)]
    [InlineData(EstadoPlanUsuario.Cancelado)]
    [Trait("Requirement", "RF-NEG-05")]
    public void RF_NEG_05_Transiciones_UnEstadoTerminalNoTieneSalida(EstadoPlanUsuario terminal)
    {
        foreach (var destino in Enum.GetValues<EstadoPlanUsuario>())
            AssertCodigo(ReglaNegocioException.TransicionPlanInvalida,
                () => TransicionesPlanUsuario.Validar(terminal, destino, llegoFechaFin: true));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-05")]
    public void RF_NEG_05_Plan_UnPlanCancelado_NoSeReanudaNiSePausaNiSeExtiende()
    {
        var plan = Plan();
        plan.Cancelar(Ahora);

        AssertCodigo(ReglaNegocioException.TransicionPlanInvalida, () => plan.Reanudar(Ahora, TimeZoneInfo.Utc));
        AssertCodigo(ReglaNegocioException.TransicionPlanInvalida, () => plan.Pausar(MotivoPausa.Otro, Ahora));
        AssertCodigo(ReglaNegocioException.AjusteNoDisponible, plan.Extender);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-09")]
    public void RF_NEG_09_Transiciones_LaMaquinaDelPlanNoDependeDeLaDelHabito()
    {
        // Mismos nombres de estado, máquinas distintas: pausar el plan no cambia lo que permite la del hábito.
        Assert.False(TransicionesHabito.EsTransicionValida(EstadoHabito.Planificado, EstadoHabito.Pausado));
        Assert.True(TransicionesPlanUsuario.EsTransicionValida(EstadoPlanUsuario.Activo, EstadoPlanUsuario.Pausado));
        Assert.Equal(4, Enum.GetValues<EstadoPlanUsuario>().Length);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-03")]
    public void RF_NEG_03_Plan_Completar_SoloAlLlegarALaFechaDeFin()
    {
        var plan = Plan(semanas: 2);

        AssertCodigo(ReglaNegocioException.PlanAunNoTermina, () => plan.Completar(Ahora.AddDays(13)));
        Assert.Equal(EstadoPlanUsuario.Activo, plan.Estado);

        plan.Completar(Ahora.AddDays(14));
        Assert.Equal(EstadoPlanUsuario.Completado, plan.Estado);
    }

    // ---------- Pausa y reanudación ----------

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Plan_PausarYReanudar_RegistraElPeriodoYAgregaLosDiasPausadosAlFinal()
    {
        var plan = Plan(semanas: 4);
        var finOriginal = plan.FechaFinPrevista;

        plan.Pausar(MotivoPausa.Enfermedad, Ahora.AddDays(2));
        Assert.Equal(MotivoPausa.Enfermedad, plan.MotivoPausa);
        plan.Reanudar(Ahora.AddDays(5).AddHours(3), TimeZoneInfo.Utc);

        var pausa = Assert.Single(plan.Pausas);
        Assert.Equal(Ahora.AddDays(2), pausa.InicioUtc);
        Assert.Equal(Ahora.AddDays(5).AddHours(3), pausa.FinUtc);
        Assert.Equal(EstadoPlanUsuario.Activo, plan.Estado);
        Assert.Null(plan.MotivoPausa);
        // 3 días y 3 horas de pausa: se agregan los 3 días completos.
        Assert.Equal(finOriginal.AddDays(3), plan.FechaFinPrevista);
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_Plan_Pausar_ConUnMotivoQueNoExiste_SeRechaza()
    {
        Assert.Throws<ArgumentException>(() => Plan().Pausar((MotivoPausa)99, Ahora));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Plan_Cancelar_DesdeUnaPausa_CierraElPeriodo()
    {
        var plan = Plan();
        plan.Pausar(MotivoPausa.Trabajo, Ahora);

        plan.Cancelar(Ahora.AddDays(1));

        Assert.Equal(EstadoPlanUsuario.Cancelado, plan.Estado);
        Assert.NotNull(plan.Pausas[0].FinUtc);
        Assert.Null(plan.MotivoPausa);
    }

    // ---------- Ajustes ----------

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Plan_Extender_SumaUnaSemanaComoMaximoDosVecesYNoEsUnaTransicion()
    {
        var plan = Plan(semanas: 4);
        var fin = plan.FechaFinPrevista;

        plan.Extender();
        plan.Extender();

        Assert.Equal(2, plan.Extensiones);
        Assert.Equal(6, plan.SemanasTotales);
        Assert.Equal(fin.AddDays(14), plan.FechaFinPrevista);
        Assert.Equal(EstadoPlanUsuario.Activo, plan.Estado);
        AssertCodigo(ReglaNegocioException.LimiteExtensiones, plan.Extender);
        Assert.Equal(2, plan.Extensiones);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Plan_Ajustar_SoloUnPlanActivo()
    {
        var plan = Plan();
        plan.Pausar(MotivoPausa.Otro, Ahora);

        AssertCodigo(ReglaNegocioException.AjusteNoDisponible, plan.Extender);
        AssertCodigo(ReglaNegocioException.AjusteNoDisponible, () => plan.ActivarModoSuave(Ahora));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Plan_ModoSuave_DuraSieteDias()
    {
        var plan = Plan();

        plan.ActivarModoSuave(Ahora);

        Assert.True(plan.EnModoSuave(Ahora.AddDays(7)));
        Assert.False(plan.EnModoSuave(Ahora.AddDays(7).AddMinutes(1)));
        Assert.False(Plan().EnModoSuave(Ahora));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Plan_Recordatorio_UnPlanTerminadoNoLoAdmite()
    {
        var plan = PlanUsuario.Iniciar(Usuario, Plantilla(), null, Ahora);
        plan.Cancelar(Ahora);

        var ex = Assert.Throws<ReglaNegocioException>(() => plan.ConfigurarRecordatorio(new TimeOnly(7, 30), activos: true));
        Assert.Equal(ReglaNegocioException.TransicionPlanInvalida, ex.Codigo);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Plan_Recordatorio_ActivarExigeUnaHora()
    {
        var plan = PlanUsuario.Iniciar(Usuario, Plantilla(), null, Ahora);

        Assert.Throws<ArgumentException>(() => plan.ConfigurarRecordatorio(null, activos: true));
        plan.ConfigurarRecordatorio(new TimeOnly(7, 30), activos: true);

        Assert.True(plan.RecordatoriosActivos);
        Assert.Equal(new TimeOnly(7, 30), plan.HoraRecordatorio);
        plan.ConfigurarRecordatorio(null, activos: false);
        Assert.False(plan.RecordatoriosActivos);
        Assert.Equal(new TimeOnly(7, 30), plan.HoraRecordatorio);
    }

    // ---------- Hábito que nace de una tarea ----------

    [Fact]
    [Trait("Requirement", "RF-NEG-01")]
    public void RF_NEG_01_Habito_DesdeTareaDePlan_CopiaLaTareaYEmpiezaActivo()
    {
        var plan = Plan();
        var tarea = Plantilla().Tareas[0];

        var habito = Habito.DesdeTareaDePlan(Usuario, plan.Id, tarea);

        Assert.Equal(plan.Id, habito.PlanUsuarioId);
        Assert.Equal(EstadoHabito.Activo, habito.Estado);
        Assert.Equal("Respirar", habito.Nombre);
        Assert.Equal("Inhala y exhala.", habito.Descripcion);
        Assert.Equal("3 respiraciones.", habito.VersionMinima);
        Assert.Equal(Ancla.AMediaTarde, habito.Ancla);
        Assert.Equal(5m, habito.CantidadObjetivo);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Habito_VersionMinima_SoloExisteEnUnHabitoDeUnPlan()
    {
        var suelto = new Habito(Usuario, "Leer", Periodicidad.Diaria, 10, "páginas");
        suelto.Activar(Usuario);
        var deplan = Habito.DesdeTareaDePlan(Usuario, Guid.NewGuid(), Plantilla().Tareas[0]);

        deplan.RegistrarAvance(Usuario, 1, Ahora, esVersionMinima: true);

        Assert.True(Assert.Single(deplan.Registros).EsVersionMinima);
        AssertCodigo(ReglaNegocioException.VersionMinimaNoDisponible,
            () => suelto.RegistrarAvance(Usuario, 1, Ahora, esVersionMinima: true));
        Assert.Empty(suelto.Registros);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Habito_PausadoPorElPlan_VuelveConElPlan_PeroUnoPausadoAManoNo()
    {
        var delPlan = Habito.DesdeTareaDePlan(Usuario, Guid.NewGuid(), Plantilla().Tareas[0]);
        var aMano = Habito.DesdeTareaDePlan(Usuario, Guid.NewGuid(), Plantilla().Tareas[0]);

        delPlan.PausarPorPlan(Usuario);
        aMano.Pausar(Usuario); // la persona lo dejó en pausa a propósito
        Assert.True(delPlan.PausadoPorPlan);
        Assert.False(aMano.PausadoPorPlan);

        delPlan.ReanudarPorPlan(Usuario);
        aMano.ReanudarPorPlan(Usuario);

        Assert.Equal(EstadoHabito.Activo, delPlan.Estado);
        Assert.False(delPlan.PausadoPorPlan);
        Assert.Equal(EstadoHabito.Pausado, aMano.Estado);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Habito_SiLaPersonaCambiaElEstadoEnPersona_ElPlanYaNoEsQuienLoPauso()
    {
        var habito = Habito.DesdeTareaDePlan(Usuario, Guid.NewGuid(), Plantilla().Tareas[0]);
        habito.PausarPorPlan(Usuario);

        habito.Activar(Usuario);
        habito.Pausar(Usuario);

        Assert.Equal(EstadoHabito.Pausado, habito.Estado);
        Assert.False(habito.PausadoPorPlan);
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public void RD_06_Habito_PausarPorPlan_SoloElDueno()
    {
        var habito = Habito.DesdeTareaDePlan(Usuario, Guid.NewGuid(), Plantilla().Tareas[0]);

        Assert.Throws<UnauthorizedAccessException>(() => habito.PausarPorPlan(Guid.NewGuid()));
        Assert.Equal(EstadoHabito.Activo, habito.Estado);
    }

    // ---------- Pausa: días locales y reglas ----------

    [Fact]
    [Trait("Requirement", "RD-11")]
    public void RD_11_Plan_Reanudar_CuentaLosDiasDeLaPausaEnDiasLocalesNoEnHoras()
    {
        var santoDomingo = TimeZoneInfo.FindSystemTimeZoneById("America/Santo_Domingo");
        var plan = Plan(semanas: 4);
        var fin = plan.FechaFinPrevista;

        // De las 22:00 del lunes local (02:00 UTC del martes) a las 08:00 del martes local: 10 horas, pero 1 día local.
        plan.Pausar(MotivoPausa.Otro, new DateTime(2026, 11, 10, 2, 0, 0, DateTimeKind.Utc));
        plan.Reanudar(new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc), santoDomingo);

        // Empezó el lunes 9 (22:00 local) y reanudó el martes 10 (08:00 local): la pausa cubre 1 día local, no 0 por ser < 24 h.
        Assert.Equal(fin.AddDays(1), plan.FechaFinPrevista);
        // Las mismas 54 horas dan 2 días en UTC.
        Assert.Equal(2, PlanUsuario.DiasDePausa(Ahora, Ahora.AddDays(2).AddHours(6), TimeZoneInfo.Utc));
        // Y 3 horas dentro del mismo día local no son ningún día.
        Assert.Equal(0, PlanUsuario.DiasDePausa(
            new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 10, 15, 0, 0, DateTimeKind.Utc), santoDomingo));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-04")]
    public void RF_NEG_04_Plan_UnPlanQueYaLlegoASuFinNoSePausa_SeCompletaOSeExtiende()
    {
        var plan = Plan(semanas: 2);

        AssertCodigo(ReglaNegocioException.TransicionPlanInvalida, () => plan.Pausar(MotivoPausa.Viaje, Ahora.AddDays(14)));

        Assert.Equal(EstadoPlanUsuario.Activo, plan.Estado);
        Assert.Empty(plan.Pausas);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Plan_ModoSuave_NoSeReaplicaMientrasEstaActivo()
    {
        var plan = Plan();
        plan.ActivarModoSuave(Ahora);

        AssertCodigo(ReglaNegocioException.AjusteNoDisponible, () => plan.ActivarModoSuave(Ahora.AddDays(3)));
        plan.ActivarModoSuave(Ahora.AddDays(8)); // ya terminó el anterior

        Assert.Equal(Ahora.AddDays(15), plan.ModoSuaveHasta);
    }

    // ---------- ResolvedorAncla ----------

    private static JornadaPersona Jornada(PerfilLaboral laboral, string? entrada = null, string? salida = null,
        string? despertar = null, string? dormir = null) => new(
        laboral,
        entrada is null ? null : TimeOnly.Parse(entrada),
        salida is null ? null : TimeOnly.Parse(salida),
        despertar is null ? null : TimeOnly.Parse(despertar),
        dormir is null ? null : TimeOnly.Parse(dormir));

    [Theory]
    [InlineData(Ancla.AlDespertar, "06:30")]
    [InlineData(Ancla.AlLlegarAlTrabajo, "08:00")]
    [InlineData(Ancla.AMediaManana, "10:15")]
    [InlineData(Ancla.DespuesDelAlmuerzo, "13:00")]
    [InlineData(Ancla.AMediaTarde, "14:45")]
    [InlineData(Ancla.AlSalirDelTrabajo, "17:00")]
    [InlineData(Ancla.AntesDeCenar, "19:30")]
    [InlineData(Ancla.AntesDeDormir, "22:00")]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Ancla_JornadaDeOficinaConValoresPorDefecto(Ancla ancla, string esperada)
    {
        Assert.Equal(TimeOnly.Parse(esperada), ResolvedorAncla.Resolver(ancla, Jornada(PerfilLaboral.OficinaDiurna)));
    }

    [Theory]
    [InlineData(Ancla.AlDespertar, "17:00")]
    [InlineData(Ancla.AlLlegarAlTrabajo, "22:00")]
    [InlineData(Ancla.AMediaManana, "00:00")]
    [InlineData(Ancla.DespuesDelAlmuerzo, "02:30")]
    [InlineData(Ancla.AMediaTarde, "04:00")]
    [InlineData(Ancla.AlSalirDelTrabajo, "06:00")]
    [InlineData(Ancla.AntesDeCenar, "04:30")]
    [InlineData(Ancla.AntesDeDormir, "07:00")]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Ancla_ElTurnoNocturnoTieneSusPropiosValores(Ancla ancla, string esperada)
    {
        Assert.Equal(TimeOnly.Parse(esperada), ResolvedorAncla.Resolver(ancla, Jornada(PerfilLaboral.TurnoNocturno)));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Ancla_LasHorasQueLaPersonaDioMandanSobreLosValoresPorDefecto()
    {
        var jornada = Jornada(PerfilLaboral.OficinaDiurna, "09:00", "18:00", "07:00", "23:00");

        Assert.Equal(new TimeOnly(9, 0), ResolvedorAncla.Resolver(Ancla.AlLlegarAlTrabajo, jornada));
        Assert.Equal(new TimeOnly(18, 0), ResolvedorAncla.Resolver(Ancla.AlSalirDelTrabajo, jornada));
        Assert.Equal(new TimeOnly(7, 0), ResolvedorAncla.Resolver(Ancla.AlDespertar, jornada));
        Assert.Equal(new TimeOnly(22, 30), ResolvedorAncla.Resolver(Ancla.AntesDeDormir, jornada));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Ancla_TodaAnclaYJornadaDanUnaHora_ConEntradaIgualASalida()
    {
        foreach (var laboral in Enum.GetValues<PerfilLaboral>())
            foreach (var ancla in Enum.GetValues<Ancla>())
                _ = ResolvedorAncla.Resolver(ancla, Jornada(laboral));

        // Entrada igual a salida: no dice nada de la duración, se toma como 8 horas.
        Assert.Equal(new TimeOnly(12, 0), ResolvedorAncla.Resolver(Ancla.AMediaManana, Jornada(PerfilLaboral.Remoto, "10:00", "10:00")));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResolvedorAncla.Resolver((Ancla)99, Jornada(PerfilLaboral.Remoto)));
    }

    // ---------- Selector de mensajes ----------

    private static List<MensajeMotivacion> Catalogo() =>
    [
        .. Enum.GetValues<CategoriaMensaje>().SelectMany(c => Enumerable.Range(1, 4).Select(i => new MensajeMotivacion(c, $"{c} {i}"))),
    ];

    private static SituacionMensaje Situacion(int dias = 10, int racha = 3, int sinRegistro = 0, bool volvio = false,
        int semanas = 4, bool terminado = false) => new(dias, racha, sinRegistro, volvio, semanas, terminado);

    [Theory]
    [InlineData(CategoriaMensaje.Cierre, 10, 3, 0, false, true)]
    [InlineData(CategoriaMensaje.Regreso, 10, 0, 2, false, false)]
    [InlineData(CategoriaMensaje.Regreso, 10, 0, 5, true, false)]
    [InlineData(CategoriaMensaje.Recaida, 10, 1, 0, true, false)]
    [InlineData(CategoriaMensaje.Hito, 12, 7, 0, false, false)]
    [InlineData(CategoriaMensaje.Hito, 14, 5, 0, false, false)]
    [InlineData(CategoriaMensaje.Inicio, 1, 1, 0, false, false)]
    [InlineData(CategoriaMensaje.Constancia, 9, 3, 0, false, false)]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Mensajes_ElTonoDependeDelMomentoYConDosDiasSinRegistroEsDeRegreso(
        CategoriaMensaje esperada, int dias, int racha, int sinRegistro, bool volvio, bool terminado)
    {
        Assert.Equal(esperada,
            SelectorMensajesMotivacion.Categorizar(Situacion(dias, racha, sinRegistro, volvio, semanas: 4, terminado)));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Mensajes_ElMismoDiaDaElMismoMensajeYDosDiasSeguidosNuncaRepiten()
    {
        var catalogo = Catalogo();
        var usuario = Guid.NewGuid();
        var dia = new DateOnly(2026, 11, 9);

        var hoy = SelectorMensajesMotivacion.Elegir(catalogo, CategoriaMensaje.Constancia, usuario, dia);

        Assert.Equal(hoy, SelectorMensajesMotivacion.Elegir(catalogo, CategoriaMensaje.Constancia, usuario, dia));
        for (var i = 0; i < 200; i++)
        {
            var d = dia.AddDays(i);
            Assert.NotEqual(
                SelectorMensajesMotivacion.Elegir(catalogo, CategoriaMensaje.Constancia, usuario, d),
                SelectorMensajesMotivacion.Elegir(catalogo, CategoriaMensaje.Constancia, usuario, d.AddDays(1)));
        }
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Mensajes_ElHashEsEstableYPersonasDistintasNoRecibenSiempreElMismo()
    {
        var catalogo = Catalogo();
        var dia = new DateOnly(2026, 11, 9);
        var fijo = new Guid("11111111-2222-3333-4444-555555555555");

        // Un Guid fijo da siempre el mismo mensaje: el hash no depende del proceso (a diferencia de string.GetHashCode).
        // El índice se verificó aparte con FNV-1a sobre los bytes del Guid: (2685261573 + 739928) % 4 = 1.
        Assert.Equal("Constancia 2", SelectorMensajesMotivacion.Elegir(catalogo, CategoriaMensaje.Constancia, fijo, dia));
        var distintos = Enumerable.Range(0, 40)
            .Select(_ => SelectorMensajesMotivacion.Elegir(catalogo, CategoriaMensaje.Constancia, Guid.NewGuid(), dia))
            .Distinct().Count();
        Assert.True(distintos > 1);
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_Mensajes_SinMensajesEnLaCategoria_NoDevuelveNada()
    {
        var soloInicio = Catalogo().Where(m => m.Categoria == CategoriaMensaje.Inicio).ToList();

        Assert.Null(SelectorMensajesMotivacion.Elegir(soloInicio, CategoriaMensaje.Cierre, Guid.NewGuid(), new DateOnly(2026, 11, 9)));
        Assert.Null(SelectorMensajesMotivacion.Elegir([], CategoriaMensaje.Inicio, Guid.NewGuid(), new DateOnly(2026, 11, 9)));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Mensajes_SituacionDesdeElProgreso_DetectaElRegresoDeHoy()
    {
        var hoy = new DateOnly(2026, 11, 9);
        var progreso = CalculadoraProgreso.Calcular([new TareaDelPlan(Guid.NewGuid(), Periodicidad.Diaria)], [],
            new ParametrosProgreso(Ahora.AddDays(-1), Ahora.AddDays(27), 4, TimeZoneInfo.Utc, Ahora, null, []));

        var situacion = SituacionMensaje.Desde(progreso with { Regresos = [hoy], HoyLocal = hoy }, planTerminado: false);

        Assert.True(situacion.VolvioHoy);
        Assert.False(SituacionMensaje.Desde(progreso, true).VolvioHoy);
    }
}
