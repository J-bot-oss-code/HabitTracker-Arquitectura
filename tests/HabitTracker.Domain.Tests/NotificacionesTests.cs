using HabitTracker.Domain.Notificaciones;

namespace HabitTracker.Domain.Tests;

/// <summary>Fase 5 de HabitTracker v2: cuándo y cuánto se escribe a la persona (dominio puro, sin reloj ni base de datos).</summary>
public class NotificacionesTests
{
    private static readonly TimeZoneInfo SantoDomingo = TimeZoneInfo.FindSystemTimeZoneById("America/Santo_Domingo");
    private static readonly TimeZoneInfo NuevaYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    private static DateTime Utc(int a, int m, int d, int h, int min = 0) => new(a, m, d, h, min, 0, DateTimeKind.Utc);

    // ---------- CalculadoraProximoEnvio ----------

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    [Trait("Requirement", "RD-11")]
    public void RF_NEG_07_ProximoEnvio_SantoDomingo_EsLaHoraLocalMasCuatro()
    {
        // 12:00 UTC = 08:00 local; el recordatorio de las 10:15 locales es hoy a las 14:15 UTC.
        var envio = CalculadoraProximoEnvio.ProximoEnvioUtc(new TimeOnly(10, 15), SantoDomingo, Utc(2026, 11, 13, 12));

        Assert.Equal(Utc(2026, 11, 13, 14, 15), envio);
        Assert.Equal(DateTimeKind.Utc, envio.Kind);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public void RF_NEG_07_ProximoEnvio_SiLaHoraYaPaso_EsMañana()
    {
        var envio = CalculadoraProximoEnvio.ProximoEnvioUtc(new TimeOnly(8, 0), SantoDomingo, Utc(2026, 11, 13, 12, 1));

        Assert.Equal(Utc(2026, 11, 14, 12), envio);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public void RF_NEG_07_ProximoEnvio_ExactamenteAhora_EsMañana_NuncaElMismoInstante()
    {
        var envio = CalculadoraProximoEnvio.ProximoEnvioUtc(new TimeOnly(8, 0), SantoDomingo, Utc(2026, 11, 13, 12));

        Assert.Equal(Utc(2026, 11, 14, 12), envio);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    [Trait("Requirement", "RD-11")]
    public void RF_NEG_07_ProximoEnvio_NuevaYork_SiguePorElCambioDeHora()
    {
        var hora = new TimeOnly(8, 0);

        // Con horario de verano (EDT, UTC-4) y sin él (EST, UTC-5): la misma hora local es otro instante UTC.
        var enVerano = CalculadoraProximoEnvio.ProximoEnvioUtc(hora, NuevaYork, Utc(2026, 10, 20, 20));
        var enInvierno = CalculadoraProximoEnvio.ProximoEnvioUtc(hora, NuevaYork, Utc(2026, 11, 3, 20));
        // El 1-nov el cambio ocurre a las 02:00 locales: las 08:00 de ese día ya son EST aunque «hoy» (31-oct) fuera EDT.
        var cruzandoElCambio = CalculadoraProximoEnvio.ProximoEnvioUtc(hora, NuevaYork, Utc(2026, 10, 31, 20));

        Assert.Equal(Utc(2026, 10, 21, 12), enVerano);
        Assert.Equal(Utc(2026, 11, 4, 13), enInvierno);
        Assert.Equal(Utc(2026, 11, 1, 13), cruzandoElCambio);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public void RF_NEG_07_ProximoEnvio_HoraQueNoExiste_SeCorreUnaHora()
    {
        // 8-mar-2026 a las 02:30 no existe en Nueva York (saltan de 02:00 a 03:00): se envía a las 03:30 locales (EDT).
        var envio = CalculadoraProximoEnvio.ProximoEnvioUtc(new TimeOnly(2, 30), NuevaYork, Utc(2026, 3, 8, 3));

        Assert.Equal(Utc(2026, 3, 8, 7, 30), envio);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public void RF_NEG_07_ProximoEnvio_HoraQueSeRepite_CuentaLaPrimeraOcurrencia()
    {
        // 1-nov-2026 a la 01:30 ocurre dos veces en Nueva York (EDT y luego EST): vale la primera, 05:30 UTC.
        var envio = CalculadoraProximoEnvio.ProximoEnvioUtc(new TimeOnly(1, 30), NuevaYork, Utc(2026, 11, 1, 3));

        Assert.Equal(Utc(2026, 11, 1, 5, 30), envio);
    }

    [Fact]
    [Trait("Requirement", "RD-11")]
    public void RD_11_ProximoEnvio_ExigeUtc()
    {
        Assert.Throws<ArgumentException>(() =>
            CalculadoraProximoEnvio.ProximoEnvioUtc(new TimeOnly(8, 0), SantoDomingo, new DateTime(2026, 11, 13, 12, 0, 0, DateTimeKind.Local)));
    }

    // ---------- CalculadoraCadencia ----------

    [Theory]
    [InlineData(0, ModoResumen.Diario)]
    [InlineData(2, ModoResumen.Diario)]
    [InlineData(3, ModoResumen.DiarioConRegreso)]
    [InlineData(6, ModoResumen.DiarioConRegreso)]
    [InlineData(7, ModoResumen.Semanal)]
    [InlineData(20, ModoResumen.Semanal)]
    [InlineData(21, ModoResumen.Detenido)]
    [InlineData(400, ModoResumen.Detenido)]
    [Trait("Requirement", "RF-NEG-07")]
    public void RF_NEG_07_Cadencia_CambiaDeModoA_3_7_Y_21_Dias(int dias, ModoResumen esperado)
    {
        Assert.Equal(esperado, CalculadoraCadencia.Evaluar(dias, avisoRetomarYaEnviado: false).Modo);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public void RF_NEG_07_Cadencia_ElAvisoRetomar_SaleUnaSolaVez()
    {
        Assert.True(CalculadoraCadencia.Evaluar(21, avisoRetomarYaEnviado: false).EnviarAvisoRetomar);
        Assert.False(CalculadoraCadencia.Evaluar(21, avisoRetomarYaEnviado: true).EnviarAvisoRetomar);
        Assert.False(CalculadoraCadencia.Evaluar(40, avisoRetomarYaEnviado: true).EnviarAvisoRetomar);
        // Antes del tope nunca se manda.
        Assert.False(CalculadoraCadencia.Evaluar(20, avisoRetomarYaEnviado: false).EnviarAvisoRetomar);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public void RF_NEG_07_Cadencia_SoloReduce_ConMasDiasNuncaHayMasEnvios()
    {
        var anterior = ModoResumen.Diario;
        for (var dias = 0; dias <= 90; dias++)
        {
            var modo = CalculadoraCadencia.Evaluar(dias, avisoRetomarYaEnviado: true).Modo;
            Assert.True(modo >= anterior, $"A los {dias} días el modo volvió a más envíos.");
            anterior = modo;
        }
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_Cadencia_RechazaDiasNegativosYUmbralesAbsurdos()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CalculadoraCadencia.Evaluar(-1, false));
        Assert.Throws<ArgumentException>(() => CalculadoraCadencia.Evaluar(5, false, new UmbralesCadencia(7, 3, 21)));
        Assert.Throws<ArgumentException>(() => CalculadoraCadencia.Evaluar(5, false, new UmbralesCadencia(0, 7, 21)));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public void RF_NEG_07_Cadencia_LosUmbralesSePuedenConfigurar()
    {
        var u = new UmbralesCadencia(DiasParaRegreso: 2, DiasParaSemanal: 5, DiasParaRetomar: 10);

        Assert.Equal(ModoResumen.DiarioConRegreso, CalculadoraCadencia.Evaluar(2, false, u).Modo);
        Assert.Equal(ModoResumen.Semanal, CalculadoraCadencia.Evaluar(5, false, u).Modo);
        Assert.Equal(ModoResumen.Detenido, CalculadoraCadencia.Evaluar(10, false, u).Modo);
    }

    // ---------- Días sin actividad ----------

    [Fact]
    [Trait("Requirement", "RD-11")]
    public void RD_11_DiasSinActividad_CuentaDiasLocalesNoHorasUtc()
    {
        // Última actividad el 10-nov a las 23:30 locales (03:30 UTC del 11). El 13-nov a las 00:10 locales son 3 días locales,
        // aunque solo hayan pasado 48 horas y 40 minutos.
        var dias = CalculadoraCadencia.DiasSinActividad(Utc(2026, 11, 11, 3, 30), SantoDomingo, Utc(2026, 11, 13, 4, 10));

        Assert.Equal(3, dias);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public void RF_NEG_07_DiasSinActividad_SinActividadRegistrada_NoHayNadaQueReducir()
    {
        Assert.Equal(0, CalculadoraCadencia.DiasSinActividad(null, SantoDomingo, Utc(2026, 11, 13, 12)));
    }

    [Fact]
    [Trait("Requirement", "RD-11")]
    public void RD_11_DiasSinActividad_ExigeUtc_YNuncaDaNegativos()
    {
        Assert.Throws<ArgumentException>(() =>
            CalculadoraCadencia.DiasSinActividad(new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Unspecified), SantoDomingo, Utc(2026, 11, 13, 12)));
        // Una actividad «futura» por desfase de reloj no produce días negativos.
        Assert.Equal(0, CalculadoraCadencia.DiasSinActividad(Utc(2026, 11, 14, 12), SantoDomingo, Utc(2026, 11, 13, 12)));
    }

    // ---------- DecisorRecordatorio ----------

    // Santo Domingo es UTC-4 todo el año: las 8:00 locales son las 12:00 UTC.
    private static readonly TimeOnly Ocho = new(8, 0);
    private static readonly DateOnly Hoy = new(2026, 11, 20);

    [Theory]
    [Trait("Requirement", "RF-NEG-07")]
    [InlineData(11, 59, false)] // 7:59 local: todavía no
    [InlineData(12, 0, true)]   // 8:00 justo
    [InlineData(14, 30, true)]  // 10:30, dentro de la ventana de 3 h
    [InlineData(15, 0, true)]   // 11:00, el borde de la ventana
    [InlineData(15, 1, false)]  // 11:01, llegaría a destiempo
    public void RF_NEG_07_Recordatorio_SoloEntreLaHoraElegidaYTresHorasDespues(int hUtc, int minUtc, bool toca)
    {
        var tipo = DecisorRecordatorio.Decidir(Ocho, SantoDomingo, Utc(2026, 11, 20, hUtc, minUtc), null, 0);

        Assert.Equal(toca ? TipoRecordatorio.Diario : null, tipo);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public void RF_NEG_07_Recordatorio_UnoPorDia_SiYaSeEnvioHoyNoVuelveATocar()
    {
        Assert.Null(DecisorRecordatorio.Decidir(Ocho, SantoDomingo, Utc(2026, 11, 20, 13), Hoy, 0));
        Assert.Equal(TipoRecordatorio.Diario, DecisorRecordatorio.Decidir(Ocho, SantoDomingo, Utc(2026, 11, 20, 13), Hoy.AddDays(-1), 0));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public void RF_NEG_07_Recordatorio_UnaHoraTardiaNoSeMandaPasadaLaMedianoche()
    {
        // 23:00 local elegidas; a la 1:00 local del día siguiente (05:00 UTC) no se manda como si fuera de ese día.
        Assert.Null(DecisorRecordatorio.Decidir(new TimeOnly(23, 0), SantoDomingo, Utc(2026, 11, 21, 5), Hoy, 0));
    }

    [Theory]
    [Trait("Requirement", "RF-NEG-07")]
    [InlineData(0, TipoRecordatorio.Diario)]
    [InlineData(2, TipoRecordatorio.Diario)]
    [InlineData(3, TipoRecordatorio.Regreso)]
    [InlineData(6, TipoRecordatorio.Regreso)]
    [InlineData(7, TipoRecordatorio.Regreso)]   // pasa a semanal: el día del umbral sí
    [InlineData(8, null)]
    [InlineData(13, null)]
    [InlineData(14, TipoRecordatorio.Regreso)]  // y cada 7 días
    [InlineData(20, null)]
    [InlineData(21, TipoRecordatorio.AvisoRetomar)] // el único «¿seguimos?»
    [InlineData(22, null)]                      // después, nada hasta que vuelva
    [InlineData(28, null)]
    [InlineData(400, null)]
    public void RF_NEG_07_Recordatorio_LaCadenciaSoloReduce(int diasSinActividad, TipoRecordatorio? esperado)
    {
        Assert.Equal(esperado, DecisorRecordatorio.Decidir(Ocho, SantoDomingo, Utc(2026, 11, 20, 13), null, diasSinActividad));
    }

    [Fact]
    [Trait("Requirement", "RD-11")]
    public void RD_11_Recordatorio_ExigeUnaFechaUtc()
    {
        Assert.Throws<ArgumentException>(() =>
            DecisorRecordatorio.Decidir(Ocho, SantoDomingo, new DateTime(2026, 11, 20, 13, 0, 0, DateTimeKind.Local), null, 0));
    }
}
