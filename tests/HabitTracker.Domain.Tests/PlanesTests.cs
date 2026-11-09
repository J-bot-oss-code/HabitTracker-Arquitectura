using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Perfil;
using HabitTracker.Domain.Planes;
using HabitTracker.Domain.Salud;

namespace HabitTracker.Domain.Tests;

/// <summary>Fase 3 de HabitTracker v2: plan del catálogo, reglas de seguridad y recomendador (dominio puro).</summary>
public class PlanesTests
{
    private static readonly Guid Usuario = Guid.NewGuid();
    private static readonly DateTime Ahora = new(2026, 11, 9, 15, 0, 0, DateTimeKind.Utc);

    // ---------- Constructores de datos ----------

    private static TareaPlantilla Tarea(int orden = 1, string unidad = "veces", decimal cantidad = 1,
        Periodicidad periodicidad = Periodicidad.Diaria) =>
        new(orden, $"Tarea {orden}", "Instrucciones claras.", periodicidad, cantidad, unidad, "Lo mínimo.", Ancla.AMediaManana);

    private static ContenidoPlan Basico(string slug = "plan-basico", AreaPlan area = AreaPlan.Estres) => new(
        slug, "Plan básico", "Resumen.", "Descripción.", area, NivelPlan.Basico, IntensidadPlan.Baja, SexoPlan.Todos,
        16, 100, 3, [], null, false, [], [], [], ["Uno", "Dos", "Tres"], "No sustituye el consejo de tu médico.", [],
        RequiereCuestionario: false, VersionContenido: 1, [Tarea()]);

    private static ContenidoPlan Regular(string slug = "plan-regular", int semanas = 4) => Basico(slug) with
    {
        Nivel = NivelPlan.Regular,
        Semanas = semanas,
        GuiaSemanal = [.. Enumerable.Range(1, semanas).Select(s => new SemanaGuia(s, $"Semana {s}.", null))],
    };

    private static PlanPlantilla Plan(ContenidoPlan contenido) => new(contenido);

    private static PerfilUsuario Perfil(PerfilLaboral laboral = PerfilLaboral.OficinaDiurna, int? edad = null, Sexo? sexo = null) =>
        PerfilUsuario.Crear(Usuario, new DatosPerfil(laboral, "America/Santo_Domingo", true, Edad: edad, Sexo: sexo), Ahora);

    private static CuestionarioSalud Cuestionario(
        IReadOnlyList<Objetivo>? objetivos = null,
        bool seguridadAfirmativa = false,
        IReadOnlyList<CondicionSalud>? condiciones = null,
        IReadOnlyList<Alergia>? alergias = null,
        Lugar lugar = Lugar.CasaSinEquipo,
        Experiencia experiencia = Experiencia.Principiante)
    {
        var c = new CuestionarioSalud(Usuario);
        c.GuardarObjetivos(new BloqueObjetivos(objetivos ?? [Objetivo.ReducirEstres], [], experiencia, lugar), Ahora);
        c.GuardarSeguridad(new BloqueSeguridad(seguridadAfirmativa, false, false, false, false, false, false), Ahora);
        if (condiciones is not null || alergias is not null)
            c.GuardarCondiciones(new BloqueCondiciones(condiciones ?? [CondicionSalud.Ninguna], alergias ?? [Alergia.Ninguna]), Ahora);
        return c;
    }

    private static SituacionPersona Persona(PerfilUsuario? perfil = null, CuestionarioSalud? cuestionario = null, bool consiente = true) =>
        SituacionPersona.Crear(perfil ?? Perfil(), cuestionario, consiente);

    // ---------- PlanPlantilla: invariantes del contenido ----------

    [Fact]
    [Trait("Requirement", "RF-NEG-01")]
    public void RF_NEG_01_PlanPlantilla_ContenidoValido_SeCreaActivoYConTareasOrdenadas()
    {
        var plan = Plan(Basico() with { Tareas = [Tarea(2), Tarea(1)] });

        Assert.True(plan.Activo);
        Assert.Equal([1, 2], plan.Tareas.Select(t => t.Orden));
        Assert.NotEqual(Guid.Empty, plan.Id);
    }

    [Theory]
    [InlineData(AreaPlan.ControlDePeso)]
    [InlineData(AreaPlan.Gimnasio)]
    [Trait("Requirement", "RF-NEG-01")]
    [Trait("Requirement", "RD-07")]
    public void RF_NEG_01_PlanPlantilla_AreaDePesoOGimnasioSinCuestionario_SeRechaza(AreaPlan area)
    {
        var contenido = Regular() with { Area = area, RequiereCuestionario = false };

        Assert.Throws<ArgumentException>(() => Plan(contenido));
        Assert.True(Plan(contenido with { RequiereCuestionario = true }).RequiereCuestionario);
    }

    public static TheoryData<string> DatosDeNivel2 => new() { "intensidad", "contraindicacion", "alergeno", "edadMinima", "edadMaxima", "sexo" };

    [Theory]
    [MemberData(nameof(DatosDeNivel2))]
    [Trait("Requirement", "RF-NEG-01")]
    [Trait("Requirement", "RD-07")]
    public void RF_NEG_01_PlanPlantilla_UsaDatosDeNivel2SinCuestionario_SeRechaza(string dato)
    {
        var contenido = dato switch
        {
            "intensidad" => Regular() with { Intensidad = IntensidadPlan.Media },
            "contraindicacion" => Regular() with { Contraindicaciones = [CondicionSalud.Embarazo] },
            "alergeno" => Regular() with { Alergenos = [Alergia.Mani] },
            "edadMinima" => Regular() with { EdadMinima = 60 },
            "edadMaxima" => Regular() with { EdadMaxima = 40 },
            _ => Regular() with { Sexo = SexoPlan.Mujer },
        };

        Assert.True(PlanPlantilla.NecesitaCuestionario(contenido));
        Assert.Throws<ArgumentException>(() => Plan(contenido));
    }

    [Theory]
    [InlineData(AreaPlan.ControlDePeso, IntensidadPlan.Baja)]
    [InlineData(AreaPlan.Estres, IntensidadPlan.Alta)]
    [Trait("Requirement", "RF-NEG-01")]
    public void RF_NEG_01_PlanPlantilla_BasicoDePesoODeIntensidadAlta_SeRechaza(AreaPlan area, IntensidadPlan intensidad)
    {
        var contenido = Basico() with { Area = area, Intensidad = intensidad, RequiereCuestionario = true };

        Assert.Throws<ArgumentException>(() => Plan(contenido));
    }

    [Theory]
    [InlineData(NivelPlan.Basico, 1)]
    [InlineData(NivelPlan.Basico, 5)]
    [InlineData(NivelPlan.Regular, 2)]
    [InlineData(NivelPlan.Regular, 13)]
    [Trait("Requirement", "RF-NEG-01")]
    public void RF_NEG_01_PlanPlantilla_SemanasFueraDelRangoDelNivel_SeRechaza(NivelPlan nivel, int semanas)
    {
        var contenido = (nivel == NivelPlan.Basico ? Basico() : Regular(semanas: 4)) with
        {
            Semanas = semanas,
            GuiaSemanal = nivel == NivelPlan.Basico ? [] : [.. Enumerable.Range(1, semanas).Select(s => new SemanaGuia(s, "Texto.", null))],
        };

        Assert.Throws<ArgumentException>(() => Plan(contenido));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-01")]
    public void RF_NEG_01_PlanPlantilla_BasicoConMasDeTresTareas_SeRechaza()
    {
        var contenido = Basico() with { Tareas = [Tarea(1), Tarea(2), Tarea(3), Tarea(4)] };

        Assert.Throws<ArgumentException>(() => Plan(contenido));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-01")]
    public void RF_NEG_01_PlanPlantilla_RegularSinGuiaParaCadaSemana_SeRechaza()
    {
        var sinUnaSemana = Regular(semanas: 4) with { GuiaSemanal = [new(1, "a", null), new(2, "b", null), new(3, "c", null)] };
        var desordenada = Regular(semanas: 2) with { Semanas = 3, GuiaSemanal = [new(1, "a", null), new(3, "c", null), new(2, "b", null)] };

        Assert.Throws<ArgumentException>(() => Plan(sinUnaSemana));
        Assert.Throws<ArgumentException>(() => Plan(desordenada));
    }

    [Theory]
    [InlineData("Con Mayusculas")]
    [InlineData("con espacio")]
    [InlineData("-guion-al-inicio")]
    [InlineData("")]
    [Trait("Requirement", "RD-07")]
    public void RD_07_PlanPlantilla_SlugInvalido_SeRechaza(string slug)
    {
        Assert.Throws<ArgumentException>(() => Plan(Basico(slug)));
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_PlanPlantilla_SinTresBeneficiosOSinAviso_SeRechaza()
    {
        Assert.Throws<ArgumentException>(() => Plan(Basico() with { Beneficios = ["Uno", "Dos"] }));
        Assert.Throws<ArgumentException>(() => Plan(Basico() with { AvisoSeguridad = "  " }));
    }

    [Theory]
    [InlineData("vasos", 20, true)]
    [InlineData("vasos", 21, false)]
    [InlineData("litros", 5, true)]
    [InlineData("litros", 5.5, false)]
    [Trait("Requirement", "RD-07")]
    public void RD_07_TareaPlantilla_AguaDiaria_RespetaElTopeDeHidratacionPrudente(string unidad, double cantidad, bool valida)
    {
        TareaPlantilla Crear() => Tarea(unidad: unidad, cantidad: (decimal)cantidad);

        if (valida)
            Assert.True(Crear().EsDeAgua);
        else
            Assert.Throws<ArgumentException>(Crear);
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_TareaPlantilla_SinVersionMinima_SeRechaza()
    {
        Assert.Throws<ArgumentException>(() =>
            new TareaPlantilla(1, "Agua", "Instrucciones.", Periodicidad.Diaria, 1, "vasos", " ", Ancla.AlDespertar));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-01")]
    public void RF_NEG_01_PlanPlantilla_ActualizarYRetirar_ConservanElSlug()
    {
        var plan = Plan(Basico());

        plan.Retirar();
        Assert.False(plan.Activo);

        plan.Actualizar(Basico() with { Nombre = "Nuevo nombre", VersionContenido = 2 });
        Assert.True(plan.Activo);
        Assert.Equal("Nuevo nombre", plan.Nombre);
        Assert.Equal(2, plan.VersionContenido);
        Assert.Throws<InvalidOperationException>(() => plan.Actualizar(Basico("otro-slug")));
    }

    // ---------- Variantes de "Mi día" ----------

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_VariantesMiDia_CadaPerfilLaboralTieneSuVariante()
    {
        foreach (var perfil in Enum.GetValues<PerfilLaboral>())
            Assert.Contains(VariantesMiDia.SlugPara(perfil), VariantesMiDia.Slugs);

        Assert.Equal(VariantesMiDia.TurnoNocturno, VariantesMiDia.SlugPara(PerfilLaboral.TurnosRotativos));
        Assert.Equal(VariantesMiDia.TrabajoFisico, VariantesMiDia.SlugPara(PerfilLaboral.TrabajoFisico));
        Assert.Equal(VariantesMiDia.Oficina, VariantesMiDia.SlugPara(PerfilLaboral.Remoto));
    }

    // ---------- Reglas de seguridad ----------

    [Theory]
    [InlineData(CondicionSalud.Embarazo)]
    [InlineData(CondicionSalud.HistorialTrastornoAlimentario)]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_ReglasSeguridad_PlanDePesoConEmbarazoOTrastornoAlimentario_NoEsApto(CondicionSalud condicion)
    {
        var plan = Plan(Regular() with { Area = AreaPlan.ControlDePeso, RequiereCuestionario = true });
        var persona = Persona(cuestionario: Cuestionario(condiciones: [condicion]));

        var evaluacion = ReglasSeguridadPlanes.Evaluar(plan, persona);

        Assert.False(evaluacion.EsApto);
        Assert.Contains(evaluacion.Razones, r => r.Motivo == MotivoNoApto.SinPlanesDePeso);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_ReglasSeguridad_MenorDeEdad_SinPlanesDePesoNiIntensidadAlta()
    {
        var peso = Plan(Regular() with { Area = AreaPlan.ControlDePeso, RequiereCuestionario = true });
        var alta = Plan(Regular() with { Intensidad = IntensidadPlan.Alta, RequiereCuestionario = true });
        var persona = Persona(Perfil(edad: 17));

        Assert.Contains(ReglasSeguridadPlanes.Evaluar(peso, persona).Razones, r => r.Motivo == MotivoNoApto.SinPlanesDePeso);
        Assert.Contains(ReglasSeguridadPlanes.Evaluar(alta, persona).Razones, r => r.Motivo == MotivoNoApto.IntensidadNoRecomendada);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_ReglasSeguridad_ConsultarMedico_SoloIntensidadBaja()
    {
        var media = Plan(Regular() with { Intensidad = IntensidadPlan.Media, RequiereCuestionario = true });
        var baja = Plan(Regular());
        var persona = Persona(cuestionario: Cuestionario(seguridadAfirmativa: true));

        Assert.Contains(ReglasSeguridadPlanes.Evaluar(media, persona).Razones, r => r.Motivo == MotivoNoApto.IntensidadNoRecomendada);
        var apto = ReglasSeguridadPlanes.Evaluar(baja, persona);
        Assert.True(apto.EsApto);
        Assert.Contains(ReglasSeguridadPlanes.AvisoConsultarMedico, apto.Advertencias);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_ReglasSeguridad_LesionDeRodilla_SinImpactoNiPiernas()
    {
        var conImpacto = Plan(Regular() with { Impacto = true });
        var piernas = Plan(Regular() with { Area = AreaPlan.Gimnasio, ZonasCorporales = [ZonaCorporal.GluteosYPiernas], RequiereCuestionario = true });
        var persona = Persona(cuestionario: Cuestionario(condiciones: [CondicionSalud.Rodilla]));

        Assert.Contains(ReglasSeguridadPlanes.Evaluar(conImpacto, persona).Razones, r => r.Motivo == MotivoNoApto.ImpactoNoRecomendado);
        Assert.Contains(ReglasSeguridadPlanes.Evaluar(piernas, persona).Razones, r => r.Motivo == MotivoNoApto.ZonaAEvitar);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_ReglasSeguridad_AlergenoOContraindicacionDelPlan_NoEsApto()
    {
        var conMani = Plan(Regular() with { Alergenos = [Alergia.Mani], RequiereCuestionario = true });
        var contraindicado = Plan(Regular() with { Contraindicaciones = [CondicionSalud.Asma], RequiereCuestionario = true });
        var persona = Persona(cuestionario: Cuestionario(condiciones: [CondicionSalud.Asma], alergias: [Alergia.Mani]));

        Assert.Contains(ReglasSeguridadPlanes.Evaluar(conMani, persona).Razones, r => r.Motivo == MotivoNoApto.ContieneAlergenos);
        Assert.Contains(ReglasSeguridadPlanes.Evaluar(contraindicado, persona).Razones, r => r.Motivo == MotivoNoApto.Contraindicado);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_ReglasSeguridad_FueraDeEdadOSexoDelPlan_NoEsApto()
    {
        var mayores = Plan(Regular() with { EdadMinima = 60, RequiereCuestionario = true });
        var mujeres = Plan(Regular() with { Sexo = SexoPlan.Mujer, RequiereCuestionario = true });
        var persona = Persona(Perfil(edad: 30, sexo: Sexo.Hombre));

        Assert.Contains(ReglasSeguridadPlanes.Evaluar(mayores, persona).Razones, r => r.Motivo == MotivoNoApto.FueraDeRangoDeEdad);
        Assert.Contains(ReglasSeguridadPlanes.Evaluar(mujeres, persona).Razones, r => r.Motivo == MotivoNoApto.SexoDelPlan);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_ReglasSeguridad_SinEdadNiSexo_AvisaSinExcluir()
    {
        var plan = Plan(Regular() with { EdadMinima = 60, Sexo = SexoPlan.Mujer, RequiereCuestionario = true });

        var evaluacion = ReglasSeguridadPlanes.Evaluar(plan, Persona());

        Assert.True(evaluacion.EsApto);
        Assert.Contains(ReglasSeguridadPlanes.AvisoCompletarEdad, evaluacion.Advertencias);
        Assert.Contains(ReglasSeguridadPlanes.AvisoCompletarSexo, evaluacion.Advertencias);
    }

    [Theory]
    [InlineData(CondicionSalud.Hipertension)]
    [InlineData(CondicionSalud.EnfermedadCardiaca)]
    [InlineData(CondicionSalud.EnfermedadRenal)]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_ReglasSeguridad_PlanConAguaYCondicion_AvisaDeConsultarAlMedico(CondicionSalud condicion)
    {
        var agua = Plan(Basico() with { Tareas = [Tarea(unidad: "vasos", cantidad: 8)] });
        var persona = Persona(cuestionario: Cuestionario(condiciones: [condicion]));

        var evaluacion = ReglasSeguridadPlanes.Evaluar(agua, persona);

        Assert.True(evaluacion.EsApto);
        Assert.Contains(ReglasSeguridadPlanes.AvisoAguaMedico, evaluacion.Advertencias);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_ReglasSeguridad_SinConsentimiento_NoUsaElCuestionarioPeroSiLaEdad()
    {
        var conMani = Plan(Regular() with { Alergenos = [Alergia.Mani], RequiereCuestionario = true });
        var peso = Plan(Regular() with { Area = AreaPlan.ControlDePeso, RequiereCuestionario = true });
        var cuestionario = Cuestionario(condiciones: [CondicionSalud.Embarazo], alergias: [Alergia.Mani]);

        Assert.True(ReglasSeguridadPlanes.Evaluar(conMani, Persona(cuestionario: cuestionario, consiente: false)).EsApto);
        Assert.False(ReglasSeguridadPlanes.Evaluar(peso, Persona(Perfil(edad: 16), cuestionario, consiente: false)).EsApto);
    }

    // ---------- Recomendador ----------

    private static IReadOnlyList<PlanPlantilla> CatalogoDePrueba() =>
    [
        Plan(Basico(VariantesMiDia.Oficina)),
        Plan(Basico(VariantesMiDia.TurnoNocturno)),
        Plan(Basico(VariantesMiDia.TrabajoFisico, AreaPlan.Hidratacion)),
        Plan(Basico("respirar")),
        Plan(Regular("descanso") with { Area = AreaPlan.Descanso }),
        Plan(Regular("peso") with { Area = AreaPlan.ControlDePeso, RequiereCuestionario = true }),
        Plan(Regular("gimnasio") with { Area = AreaPlan.Gimnasio, Intensidad = IntensidadPlan.Media, Lugar = Lugar.Gimnasio, RequiereCuestionario = true }),
    ];

    [Theory]
    [InlineData(PerfilLaboral.OficinaDiurna, VariantesMiDia.Oficina)]
    [InlineData(PerfilLaboral.Estudiante, VariantesMiDia.Oficina)]
    [InlineData(PerfilLaboral.TurnoNocturno, VariantesMiDia.TurnoNocturno)]
    [InlineData(PerfilLaboral.TurnosRotativos, VariantesMiDia.TurnoNocturno)]
    [InlineData(PerfilLaboral.TrabajoFisico, VariantesMiDia.TrabajoFisico)]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Recomendador_SinCuestionario_MiDiaDeSuJornadaEsElPrimero(PerfilLaboral laboral, string esperado)
    {
        var resultado = RecomendadorPlanes.Recomendar(CatalogoDePrueba(), Persona(Perfil(laboral)));

        Assert.False(resultado.PersonalizadoConSalud);
        Assert.Equal(esperado, resultado.Recomendados[0].Plan.Slug);
        // Las variantes de otras jornadas no se recomiendan.
        Assert.Single(resultado.Recomendados, r => VariantesMiDia.EsVariante(r.Plan.Slug));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Recomendador_SinCuestionario_LosQueNoPidenPreguntasVanAntes()
    {
        var resultado = RecomendadorPlanes.Recomendar(CatalogoDePrueba(), Persona());
        var slugs = resultado.Recomendados.Select(r => r.Plan.Slug).ToList();

        Assert.True(slugs.IndexOf("respirar") < slugs.IndexOf("peso"));
        Assert.True(slugs.IndexOf("descanso") < slugs.IndexOf("gimnasio"));
        Assert.Contains(RecomendadorPlanes.AvisoPreguntasDeSalud, resultado.Recomendados.Single(r => r.Plan.Slug == "peso").Advertencias);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Recomendador_ConCuestionario_AfinaPorObjetivosYAvisaDelGimnasio()
    {
        var cuestionario = Cuestionario(objetivos: [Objetivo.DormirMejor, Objetivo.GanarMasaMuscular]);

        var resultado = RecomendadorPlanes.Recomendar(CatalogoDePrueba(), Persona(cuestionario: cuestionario));
        var slugs = resultado.Recomendados.Select(r => r.Plan.Slug).ToList();

        Assert.True(resultado.PersonalizadoConSalud);
        Assert.Equal(VariantesMiDia.Oficina, slugs[0]);
        Assert.Equal("descanso", slugs[1]);
        Assert.True(slugs.IndexOf("descanso") < slugs.IndexOf("respirar"));
        var gimnasio = resultado.Recomendados.Single(r => r.Plan.Slug == "gimnasio");
        Assert.Contains(RecomendadorPlanes.AvisoNecesitaGimnasio, gimnasio.Advertencias);
        Assert.Contains(gimnasio.Motivos, m => m.Contains("masa muscular", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Recomendador_PlanNoApto_SeMuestraAparteConSuRazon()
    {
        var cuestionario = Cuestionario(condiciones: [CondicionSalud.Embarazo]);

        var resultado = RecomendadorPlanes.Recomendar(CatalogoDePrueba(), Persona(cuestionario: cuestionario));

        var noApto = Assert.Single(resultado.NoAptos);
        Assert.Equal("peso", noApto.Plan.Slug);
        Assert.DoesNotContain(resultado.Recomendados, r => r.Plan.Slug == "peso");
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Recomendador_IgnoraLosPlanesRetiradosYEsDeterminista()
    {
        var catalogo = CatalogoDePrueba();
        catalogo.Single(p => p.Slug == "respirar").Retirar();

        var primero = RecomendadorPlanes.Recomendar(catalogo, Persona());
        var segundo = RecomendadorPlanes.Recomendar(catalogo.Reverse(), Persona());

        Assert.DoesNotContain(primero.Recomendados, r => r.Plan.Slug == "respirar");
        Assert.Equal(primero.Recomendados.Select(r => r.Plan.Slug), segundo.Recomendados.Select(r => r.Plan.Slug));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Recomendador_CadaObjetivoApuntaAUnArea()
    {
        foreach (var objetivo in Enum.GetValues<Objetivo>())
            Assert.True(Enum.IsDefined(RecomendadorPlanes.AreaDe(objetivo)));
    }
}
