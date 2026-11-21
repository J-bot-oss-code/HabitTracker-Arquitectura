using HabitTracker.Application.Notificaciones;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using WebApi.Extensions;

namespace HabitTracker.Api.Tests;

/// <summary>
/// El chequeo de <c>/health</c> del proceso de recordatorios: un proceso parado marca «Degraded» (la API sigue respondiendo 200),
/// y uno apagado a propósito no es un problema (RD-08, RD-12). Sin levantar la API.
/// </summary>
public class SaludRecordatoriosTests
{
    private static readonly DateTime Inicio = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    private sealed class RelojManual(DateTime utc) : TimeProvider
    {
        public DateTime Ahora { get; set; } = utc;
        public override DateTimeOffset GetUtcNow() => new(Ahora, TimeSpan.Zero);
    }

    private static Task<HealthCheckResult> Comprobar(bool habilitado, RelojManual reloj, LatidoRecordatorios latido) =>
        new SaludDelServicioExtensions.RecordatoriosVivos(
            new OpcionesRecordatorios { Habilitado = habilitado, IntervaloMinutos = 5 }, latido, reloj)
            .CheckHealthAsync(new HealthCheckContext());

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public async Task RF_NEG_07_RecienArrancado_EstaSano()
    {
        var reloj = new RelojManual(Inicio);

        Assert.Equal(HealthStatus.Healthy, (await Comprobar(true, reloj, new LatidoRecordatorios(reloj))).Status);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public async Task RF_NEG_07_SinNingunaPasadaTrasElPlazo_EstaDegradado()
    {
        var reloj = new RelojManual(Inicio);
        var latido = new LatidoRecordatorios(reloj);

        reloj.Ahora = Inicio.AddMinutes(20);

        Assert.Equal(HealthStatus.Degraded, (await Comprobar(true, reloj, latido)).Status);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public async Task RF_NEG_07_UnaPasadaCorrectaReciente_LoDejaSano_YUnaPasadaVieja_NoLoEs()
    {
        var reloj = new RelojManual(Inicio);
        var latido = new LatidoRecordatorios(reloj);
        latido.PasadaCorrecta(Inicio.AddMinutes(30));

        reloj.Ahora = Inicio.AddMinutes(40);
        Assert.Equal(HealthStatus.Healthy, (await Comprobar(true, reloj, latido)).Status);

        reloj.Ahora = Inicio.AddMinutes(60);
        Assert.Equal(HealthStatus.Degraded, (await Comprobar(true, reloj, latido)).Status);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public async Task RF_NEG_07_ConLosRecordatoriosApagadosAPropositoSiempreEstaSano()
    {
        var reloj = new RelojManual(Inicio);
        var latido = new LatidoRecordatorios(reloj);
        reloj.Ahora = Inicio.AddDays(2);

        Assert.Equal(HealthStatus.Healthy, (await Comprobar(false, reloj, latido)).Status);
    }

    [Fact]
    [Trait("Requirement", "RD-08")]
    public async Task RD_08_ElResultadoNoDaDetallesNiMensajes()
    {
        var reloj = new RelojManual(Inicio);
        var latido = new LatidoRecordatorios(reloj);
        reloj.Ahora = Inicio.AddHours(5);

        var resultado = await Comprobar(true, reloj, latido);

        Assert.Equal(HealthStatus.Degraded, resultado.Status);
        Assert.Null(resultado.Description);
        Assert.Null(resultado.Exception);
        Assert.Empty(resultado.Data);
    }
}
