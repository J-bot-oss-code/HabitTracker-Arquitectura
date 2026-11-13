using HabitTracker.Domain.Excepciones;
using HabitTracker.Domain.Perfil;
using HabitTracker.Domain.Progreso;
using HabitTracker.Domain.Salud;

namespace HabitTracker.Domain.Tests;

/// <summary>Fase 2 de HabitTracker v2: perfil en dos niveles, cuestionario por bloques, evaluador de salud y progreso corporal.</summary>
public class PerfilSaludTests
{
    private static readonly Guid Usuario = Guid.NewGuid();
    private static readonly DateTime Ahora = new(2026, 11, 8, 15, 0, 0, DateTimeKind.Utc);
    private const string Zona = "America/Santo_Domingo";

    private static DatosPerfil Nivel1() => new(PerfilLaboral.OficinaDiurna, Zona, AvisoSaludAceptado: true);

    // ---------- Perfil ----------

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Perfil_Nivel1SinEdadNiPeso_SeCrea()
    {
        // Act
        var perfil = PerfilUsuario.Crear(Usuario, Nivel1(), Ahora);

        // Assert: P1, el primer valor no pide datos de salud.
        Assert.Equal(PerfilLaboral.OficinaDiurna, perfil.PerfilLaboral);
        Assert.Null(perfil.Edad);
        Assert.Null(perfil.PesoKg);
        Assert.False(Nivel1().TraeDatosDeSalud);
    }

    [Theory]
    [InlineData("Zona/Inexistente")]
    [InlineData("")]
    [Trait("Requirement", "RD-07")]
    public void RD_07_Perfil_ZonaHorariaInvalida_Rechaza(string zona)
    {
        var excepcion = Record.Exception(() => PerfilUsuario.Crear(Usuario, Nivel1() with { ZonaHoraria = zona }, Ahora));

        Assert.IsType<ArgumentException>(excepcion);
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_Perfil_SinAceptarAvisoDeSalud_Rechaza()
    {
        var excepcion = Record.Exception(() => PerfilUsuario.Crear(Usuario, Nivel1() with { AvisoSaludAceptado = false }, Ahora));

        Assert.IsType<ArgumentException>(excepcion);
    }

    [Theory]
    [InlineData(15, null, null)]
    [InlineData(101, null, null)]
    [InlineData(null, 119.0, null)]
    [InlineData(null, null, 301.0)]
    [Trait("Requirement", "RD-07")]
    public void RD_07_Perfil_DatosFueraDeRango_Rechaza(int? edad, double? altura, double? peso)
    {
        var datos = Nivel1() with { Edad = edad, AlturaCm = (decimal?)altura, PesoKg = (decimal?)peso };

        var excepcion = Record.Exception(() => PerfilUsuario.Crear(Usuario, datos, Ahora));

        Assert.IsType<ArgumentException>(excepcion);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Perfil_PesoOAltura_SonDatosDeSalud()
    {
        Assert.True((Nivel1() with { PesoKg = 70 }).TraeDatosDeSalud);
        Assert.True((Nivel1() with { AlturaCm = 170 }).TraeDatosDeSalud);
        Assert.False((Nivel1() with { Edad = 30, HoraDespertar = new TimeOnly(6, 30) }).TraeDatosDeSalud);
    }

    // ---------- Cuestionario ----------

    private static BloqueObjetivos Objetivos(params Objetivo[] objetivos) =>
        new(objetivos, [], Experiencia.Principiante, Lugar.CasaSinEquipo);

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_Cuestionario_MasDeTresObjetivos_Rechaza()
    {
        var cuestionario = new CuestionarioSalud(Usuario);

        var excepcion = Record.Exception(() => cuestionario.GuardarObjetivos(
            Objetivos(Objetivo.DormirMejor, Objetivo.Hidratarme, Objetivo.ReducirEstres, Objetivo.MasEnergia), Ahora));

        Assert.IsType<ArgumentException>(excepcion);
        Assert.Null(cuestionario.Objetivos);
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_Cuestionario_ZonasSinObjetivoDeDefinir_Rechaza()
    {
        var bloque = new BloqueObjetivos([Objetivo.DormirMejor], [ZonaCorporal.AbdomenYCore], Experiencia.Ninguna, Lugar.Gimnasio);

        Assert.IsType<ArgumentException>(Record.Exception(() => new CuestionarioSalud(Usuario).GuardarObjetivos(bloque, Ahora)));
    }

    [Theory]
    [InlineData(new[] { CondicionSalud.Ninguna, CondicionSalud.Asma }, new[] { Alergia.Ninguna })]
    [InlineData(new[] { CondicionSalud.Asma }, new[] { Alergia.Ninguna, Alergia.Gluten })]
    [InlineData(new[] { CondicionSalud.Asma, CondicionSalud.Asma }, new[] { Alergia.Ninguna })]
    [InlineData(new CondicionSalud[0], new[] { Alergia.Ninguna })]
    [Trait("Requirement", "RD-07")]
    public void RD_07_Cuestionario_NingunaCombinadaRepetidaOVacia_Rechaza(CondicionSalud[] condiciones, Alergia[] alergias)
    {
        var excepcion = Record.Exception(() => new CuestionarioSalud(Usuario).GuardarCondiciones(new BloqueCondiciones(condiciones, alergias), Ahora));

        Assert.IsType<ArgumentException>(excepcion);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Cuestionario_LosBloquesSeGuardanPorSeparadoConSuFecha()
    {
        // Arrange
        var cuestionario = new CuestionarioSalud(Usuario);

        // Act
        cuestionario.GuardarObjetivos(Objetivos(Objetivo.DormirMejor), Ahora);
        cuestionario.GuardarSeguridad(new BloqueSeguridad(false, false, false, false, false, false, false), Ahora.AddHours(1));

        // Assert
        Assert.NotNull(cuestionario.Objetivos);
        Assert.Equal(Ahora, cuestionario.ObjetivosRespondidoEn);
        Assert.Equal(Ahora.AddHours(1), cuestionario.SeguridadRespondidoEn);
        Assert.Null(cuestionario.Condiciones);
        Assert.Null(cuestionario.CondicionesRespondidoEn);
    }

    // ---------- Evaluador de salud ----------

    private static CuestionarioSalud ConCondiciones(params CondicionSalud[] condiciones)
    {
        var c = new CuestionarioSalud(Usuario);
        c.GuardarCondiciones(new BloqueCondiciones(condiciones, [Alergia.Ninguna]), Ahora);
        return c;
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Evaluador_SinCuestionario_EsNormalYSinRestricciones()
    {
        var evaluacion = EvaluadorSalud.Evaluar(null, null);

        Assert.Equal(NivelPrecaucion.Normal, evaluacion.Nivel);
        Assert.Empty(evaluacion.Restricciones);
        Assert.Equal(new CompletitudCuestionario(false, false, false), evaluacion.Completitud);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Evaluador_UnSiEnLaRevisionDeSeguridad_ConsultarMedicoYSoloIntensidadBaja()
    {
        var c = new CuestionarioSalud(Usuario);
        c.GuardarSeguridad(new BloqueSeguridad(false, true, false, false, false, false, false), Ahora);

        var evaluacion = EvaluadorSalud.Evaluar(c, null);

        Assert.Equal(NivelPrecaucion.ConsultarMedico, evaluacion.Nivel);
        Assert.Contains(RestriccionSalud.SoloIntensidadBaja, evaluacion.Restricciones);
    }

    [Theory]
    [InlineData(CondicionSalud.Embarazo, RestriccionSalud.SinRestriccionCalorica)]
    [InlineData(CondicionSalud.Embarazo, RestriccionSalud.SinIntensidadAlta)]
    [InlineData(CondicionSalud.Posparto, RestriccionSalud.SinRestriccionCalorica)]
    [InlineData(CondicionSalud.HistorialTrastornoAlimentario, RestriccionSalud.SinRestriccionCalorica)]
    [InlineData(CondicionSalud.Rodilla, RestriccionSalud.SinImpacto)]
    [InlineData(CondicionSalud.Osteoporosis, RestriccionSalud.SinImpacto)]
    [InlineData(CondicionSalud.Hipertension, RestriccionSalud.SinIntensidadAlta)]
    [InlineData(CondicionSalud.EnfermedadRenal, RestriccionSalud.SinIntensidadAlta)]
    [InlineData(CondicionSalud.EspaldaBaja, RestriccionSalud.EvitarZonaEspaldaBaja)]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Evaluador_CadaCondicion_DaSuRestriccion(CondicionSalud condicion, RestriccionSalud esperada)
    {
        var evaluacion = EvaluadorSalud.Evaluar(ConCondiciones(condicion), null);

        Assert.Contains(esperada, evaluacion.Restricciones);
        Assert.Equal(NivelPrecaucion.Precaucion, evaluacion.Nivel);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Evaluador_Menor18_SinRestriccionCaloricaNiIntensidadAlta()
    {
        var perfil = PerfilUsuario.Crear(Usuario, Nivel1() with { Edad = 16 }, Ahora);

        var evaluacion = EvaluadorSalud.Evaluar(null, perfil);

        Assert.Contains(RestriccionSalud.SinRestriccionCalorica, evaluacion.Restricciones);
        Assert.Contains(RestriccionSalud.SinIntensidadAlta, evaluacion.Restricciones);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Evaluador_ImcBajo_SinRestriccionCalorica()
    {
        var perfil = PerfilUsuario.Crear(Usuario, Nivel1() with { AlturaCm = 180, PesoKg = 55 }, Ahora); // IMC 17

        var evaluacion = EvaluadorSalud.Evaluar(null, perfil);

        Assert.Contains(RestriccionSalud.SinRestriccionCalorica, evaluacion.Restricciones);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Evaluador_Alergias_SinNinguna()
    {
        var c = new CuestionarioSalud(Usuario);
        c.GuardarCondiciones(new BloqueCondiciones([CondicionSalud.Ninguna], [Alergia.Mani, Alergia.Lactosa]), Ahora);

        var evaluacion = EvaluadorSalud.Evaluar(c, null);

        Assert.Equal([Alergia.Lactosa, Alergia.Mani], evaluacion.AlergenosAEvitar);
        Assert.Equal(NivelPrecaucion.Normal, evaluacion.Nivel);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Evaluador_SinConsentimiento_IgnoraCuestionarioEImcPeroNoLaEdad()
    {
        // Arrange: alerta de seguridad, IMC bajo y menor de edad.
        var c = new CuestionarioSalud(Usuario);
        c.GuardarSeguridad(new BloqueSeguridad(true, false, false, false, false, false, false), Ahora);
        var perfil = PerfilUsuario.Crear(Usuario, Nivel1() with { Edad = 17, AlturaCm = 180, PesoKg = 55 }, Ahora);

        // Act
        var evaluacion = EvaluadorSalud.Evaluar(c, perfil, usarDatosDeSalud: false);

        // Assert: los datos de salud dejan de usarse; la protección de menores se mantiene.
        Assert.Equal(NivelPrecaucion.Normal, evaluacion.Nivel);
        Assert.DoesNotContain(RestriccionSalud.SoloIntensidadBaja, evaluacion.Restricciones);
        Assert.Contains(RestriccionSalud.SinRestriccionCalorica, evaluacion.Restricciones);
        Assert.False(evaluacion.Completitud.Seguridad);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_BloquesFaltantes_PlanBasicoSinRequisitos_NoPideNada()
    {
        // P1: un plan Básico se empieza sin cuestionario.
        var faltan = EvaluadorSalud.BloquesFaltantesPara(new RequisitosSaludPlan(false, false, false), null);

        Assert.Empty(faltan);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_BloquesFaltantes_ExigeSoloLosQueElPlanNecesita()
    {
        // Arrange: ya respondió la seguridad.
        var c = new CuestionarioSalud(Usuario);
        c.GuardarSeguridad(new BloqueSeguridad(false, false, false, false, false, false, false), Ahora);

        // Act
        var requiere = EvaluadorSalud.BloquesFaltantesPara(new RequisitosSaludPlan(true, false, false), c);
        var conAlergenos = EvaluadorSalud.BloquesFaltantesPara(new RequisitosSaludPlan(false, true, false), c);

        // Assert
        Assert.Equal([BloqueCuestionario.Objetivos], requiere);
        Assert.Equal([BloqueCuestionario.Condiciones], conAlergenos);
    }

    // ---------- Progreso corporal ----------

    [Theory]
    [InlineData(29.9)]
    [InlineData(300.1)]
    [Trait("Requirement", "RD-07")]
    public void RD_07_Peso_FueraDeRango_Rechaza(double peso)
    {
        var excepcion = Record.Exception(() => new RegistroPeso(Usuario, Ahora, (decimal)peso, Ahora));

        Assert.IsType<ArgumentException>(excepcion);
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_Peso_FechaFutura_RechazaConCodigo()
    {
        var excepcion = Record.Exception(() => new RegistroPeso(Usuario, Ahora.AddDays(1), 70, Ahora));

        var regla = Assert.IsType<ReglaNegocioException>(excepcion);
        Assert.Equal(ReglaNegocioException.FechaFutura, regla.Codigo);
    }

    [Theory]
    [InlineData(ZonaMedicion.Cintura, 39.9)]
    [InlineData(ZonaMedicion.Brazo, 100.1)]
    [InlineData(ZonaMedicion.Grasa, 2.9)]
    [InlineData(ZonaMedicion.Pecho, 250.1)]
    [Trait("Requirement", "RD-07")]
    public void RD_07_Medicion_FueraDeLosLimitesDeSuZona_Rechaza(ZonaMedicion zona, double valor)
    {
        var excepcion = Record.Exception(() => new MedicionCorporal(Usuario, Ahora, zona, (decimal)valor, Ahora));

        Assert.IsType<ArgumentException>(excepcion);
    }
}
