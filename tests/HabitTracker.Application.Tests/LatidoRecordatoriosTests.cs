using HabitTracker.Application.Notificaciones;

namespace HabitTracker.Application.Tests;

/// <summary>El latido del proceso de recordatorios: cuándo se considera parado (RD-11, RD-12), sin reloj ni worker reales.</summary>
public class LatidoRecordatoriosTests
{
    private static readonly DateTime Inicio = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Cada5 = TimeSpan.FromMinutes(5);

    private sealed class RelojFijo(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc, TimeSpan.Zero);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public void RF_NEG_07_SinPasadas_ElPuntoDePartidaEsElArranque_ConElMargenDeLaPrimeraPasada()
    {
        // Intervalo de 5 min: tolera 3 pasadas (15 min) + 2 min de margen = 17 min desde el arranque.
        Assert.True(LatidoRecordatorios.EstaVivo(Inicio.AddMinutes(17), Inicio, null, Cada5));
        Assert.False(LatidoRecordatorios.EstaVivo(Inicio.AddMinutes(17).AddSeconds(1), Inicio, null, Cada5));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public void RF_NEG_07_ConUnaPasadaCorrecta_ElPlazoSeMideDesdeEsaPasada()
    {
        var ultima = Inicio.AddHours(3);

        Assert.True(LatidoRecordatorios.EstaVivo(ultima.AddMinutes(17), Inicio, ultima, Cada5));
        Assert.False(LatidoRecordatorios.EstaVivo(ultima.AddMinutes(18), Inicio, ultima, Cada5));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public void RF_NEG_07_UnaSolaPasadaQueFalta_NoDaAviso()
    {
        var ultima = Inicio;

        // Una pasada perdida (10 min) y dos (14 min) todavía caben en la tolerancia.
        Assert.True(LatidoRecordatorios.EstaVivo(ultima.AddMinutes(10), Inicio, ultima, Cada5));
        Assert.True(LatidoRecordatorios.EstaVivo(ultima.AddMinutes(14), Inicio, ultima, Cada5));
    }

    [Fact]
    [Trait("Requirement", "RD-11")]
    public void RD_11_ElLatidoGuardaLaUltimaPasadaEnUtc_YRechazaOtraFecha()
    {
        var latido = new LatidoRecordatorios(new RelojFijo(Inicio));
        Assert.Null(latido.UltimaPasadaCorrectaUtc);
        Assert.Equal(Inicio, latido.IniciadoEnUtc);

        latido.PasadaCorrecta(Inicio.AddMinutes(5));

        Assert.Equal(Inicio.AddMinutes(5), latido.UltimaPasadaCorrectaUtc);
        Assert.Equal(DateTimeKind.Utc, latido.UltimaPasadaCorrectaUtc!.Value.Kind);
        Assert.Throws<ArgumentException>(() => latido.PasadaCorrecta(DateTime.Now));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public void RF_NEG_07_UnaPasadaNuevaReemplazaALaAnterior()
    {
        var latido = new LatidoRecordatorios(new RelojFijo(Inicio));

        latido.PasadaCorrecta(Inicio.AddMinutes(5));
        latido.PasadaCorrecta(Inicio.AddMinutes(10));

        Assert.Equal(Inicio.AddMinutes(10), latido.UltimaPasadaCorrectaUtc);
    }
}
