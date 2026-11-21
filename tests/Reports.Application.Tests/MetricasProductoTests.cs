using Reports.Application.DTOs;
using Reports.Application.Interfaces;
using Reports.Application.Services;

namespace Reports.Application.Tests;

/// <summary>
/// Las métricas del Administrador y su regla de privacidad (P6): lo que cuenta personas se oculta con una cohorte de menos de 5.
/// Con una fuente de conteos escrita a mano, sin API ni base de datos (RD-12).
/// </summary>
public class MetricasProductoTests
{
    private static readonly DateTime Ahora = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

    private sealed class RelojFijo(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc, TimeSpan.Zero);
    }

    private sealed class Fuente(ConteosProducto conteos) : IFuenteMetricas
    {
        public DateTime? PedidoPara { get; private set; }
        public Task<ConteosProducto> ObtenerAsync(DateTime ahoraUtc, CancellationToken cancellationToken = default)
        {
            PedidoPara = ahoraUtc;
            return Task.FromResult(conteos);
        }
    }

    private static ConteosProducto Conteos(
        int activas = 20, int cohorte = 10, int retenidas = 7, params PlanIniciadoDto[] planes) =>
        new(CuentasNuevas7d: 3, EliminacionesEnCurso: 1, SugerenciasNuevas: 2, PersonasActivas30d: activas,
            CohorteRetencion: cohorte, PersonasRetenidas: retenidas, RegistrosPorCorreo7d: 4, PlanesIniciados: planes);

    private static Task<MetricasProductoDto> Obtener(ConteosProducto conteos) =>
        new MetricasProductoService(new Fuente(conteos), new RelojFijo(Ahora)).ObtenerAsync();

    [Fact]
    [Trait("Requirement", "RF-REP-04")]
    public async Task RF_REP_04_ConCohortesSuficientes_SeMuestraTodo_YLaRetencionEsUnPorcentaje()
    {
        var m = await Obtener(Conteos(activas: 20, cohorte: 10, retenidas: 7, new PlanIniciadoDto("caminar", 9), new PlanIniciadoDto("agua", 5)));

        Assert.Equal(Ahora, m.GeneradoEn);
        Assert.Equal((20, 70), (m.UsuariosActivos30d, m.Retencion30d));
        Assert.Equal(["caminar", "agua"], m.PlanesMasIniciados.Select(p => p.Slug));
        Assert.Equal(MetricasProductoService.CohorteMinima, m.CohorteMinima);
    }

    [Theory]
    [InlineData(4, null)]
    [InlineData(5, 5)]
    [InlineData(0, null)]
    [Trait("Requirement", "RF-REP-04")]
    public async Task RF_REP_04_LasPersonasActivas_SeOcultanConMenosDeCinco_ElBordeEsExacto(int activas, int? esperado)
    {
        Assert.Equal(esperado, (await Obtener(Conteos(activas: activas))).UsuariosActivos30d);
    }

    [Theory]
    [InlineData(4, 4, null)]  // cohorte de 4: nulo aunque todas se hayan quedado
    [InlineData(5, 5, 100)]
    [InlineData(5, 0, 0)]     // 0 % es un dato real, no un faltante
    [InlineData(6, 4, 67)]    // 66,67 → 67
    [InlineData(8, 1, 13)]    // 12,5 → 13 (redondeo hacia afuera de cero)
    [InlineData(0, 0, null)]  // sin cohorte no hay a quién medir (y no divide por cero)
    [Trait("Requirement", "RF-REP-04")]
    public async Task RF_REP_04_LaRetencion_SeOcultaConCohorteMenorDeCinco_YSeRedondea(int cohorte, int retenidas, int? esperado)
    {
        Assert.Equal(esperado, (await Obtener(Conteos(cohorte: cohorte, retenidas: retenidas))).Retencion30d);
    }

    [Fact]
    [Trait("Requirement", "RF-REP-04")]
    public async Task RF_REP_04_UnPlanConMenosDeCincoPersonas_NoSeListaYElResto_Si()
    {
        var m = await Obtener(Conteos(planes: [new PlanIniciadoDto("caminar", 5), new PlanIniciadoDto("yoga", 4), new PlanIniciadoDto("agua", 1)]));

        var visible = Assert.Single(m.PlanesMasIniciados);
        Assert.Equal(new PlanIniciadoDto("caminar", 5), visible);
    }

    [Fact]
    [Trait("Requirement", "RF-REP-04")]
    public async Task RF_REP_04_LosDatosDeOperacion_NoSeOcultanNuncaAunqueSeanPocos()
    {
        var m = await Obtener(Conteos(activas: 1, cohorte: 1, retenidas: 0));

        Assert.Equal((3, 1, 2, 4), (m.CuentasNuevas7d, m.EliminacionesEnCurso, m.SugerenciasNuevas, m.RegistrosPorCorreo7d));
        Assert.Null(m.UsuariosActivos30d);
        Assert.Null(m.Retencion30d);
        Assert.Empty(m.PlanesMasIniciados);
    }

    [Fact]
    [Trait("Requirement", "RD-11")]
    public async Task RD_11_LasMetricasSeCalculanConLaHoraUtcDelReloj()
    {
        var fuente = new Fuente(Conteos());

        var m = await new MetricasProductoService(fuente, new RelojFijo(Ahora)).ObtenerAsync();

        Assert.Equal(Ahora, fuente.PedidoPara);
        Assert.Equal(DateTimeKind.Utc, m.GeneradoEn.Kind);
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public void RD_06_LaRespuestaNoTieneNingunCampoQueIdentifiqueAUnaPersona()
    {
        // La forma del DTO es el contrato de privacidad: solo números, nunca ids, correos ni nombres.
        var tipos = typeof(MetricasProductoDto).GetProperties().Select(p => p.PropertyType).Concat(typeof(PlanIniciadoDto).GetProperties().Select(p => p.PropertyType));
        Assert.DoesNotContain(tipos, t => t == typeof(Guid) || t == typeof(Guid?));
        Assert.DoesNotContain(
            typeof(MetricasProductoDto).GetProperties().Concat(typeof(PlanIniciadoDto).GetProperties()),
            p => p.Name == "Id" || p.Name.EndsWith("Id", StringComparison.Ordinal)
                 || new[] { "email", "nombre" }.Any(x => p.Name.Contains(x, StringComparison.OrdinalIgnoreCase)));
    }
}
