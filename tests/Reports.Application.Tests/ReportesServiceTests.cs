using Reports.Application.DTOs;
using Reports.Application.Interfaces;
using Reports.Application.Services;

namespace Reports.Application.Tests;

/// <summary>
/// Validación del rango y alcance por rol de los reportes (RF-REP-04 y RF-REP-05), con dobles escritos a mano (RD-12): el módulo no
/// agrega nada por su cuenta, así que aquí se comprueba lo que pide a sus fuentes y lo que se le niega a cada rol.
/// </summary>
public class ReportesServiceTests
{
    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly Guid Admin = Guid.NewGuid();

    private sealed class Solicitudes : IFuenteSolicitudes
    {
        public List<(DateTime Desde, DateTime Hasta, string? Solicitante)> Llamadas { get; } = [];
        public Task<IReadOnlyList<FilaPermisosDto>> ContarPorEstadoAsync(DateTime desdeUtc, DateTime hastaUtcExclusivo, string? solicitante, CancellationToken c = default)
        {
            Llamadas.Add((desdeUtc, hastaUtcExclusivo, solicitante));
            return Task.FromResult<IReadOnlyList<FilaPermisosDto>>([new("Pendiente", 2), new("Aplicada", 1)]);
        }
    }

    private sealed class Constancia : IFuenteConstancia
    {
        public List<string> Llamadas { get; } = [];
        public Task<IReadOnlyList<FilaConstanciaDto>> PersonalAsync(Guid usuarioId, DateOnly desde, DateOnly hasta, CancellationToken c = default)
        {
            Llamadas.Add($"personal:{usuarioId}");
            return Task.FromResult<IReadOnlyList<FilaConstanciaDto>>([new("Calma", 1, 3, 1, 5, 60m)]);
        }

        public Task<IReadOnlyList<FilaConstanciaDto>> GlobalAsync(DateOnly desde, DateOnly hasta, CancellationToken c = default)
        {
            Llamadas.Add("global");
            return Task.FromResult<IReadOnlyList<FilaConstanciaDto>>([new("Calma", 4, 3.5m, 1m, 5m, 70m)]);
        }
    }

    private static (ReportesService Servicio, Solicitudes Solicitudes, Constancia Constancia) Crear()
    {
        var solicitudes = new Solicitudes();
        var constancia = new Constancia();
        return (new ReportesService(solicitudes, constancia), solicitudes, constancia);
    }

    private static ConsultaReporte Estandar(string? desde = "2026-11-02", string? hasta = "2026-11-08") =>
        new(Ana, "ana@ejemplo.com", EsAdministrador: false, Fecha(desde), Fecha(hasta));

    private static ConsultaReporte Administrador(string? desde = "2026-11-02", string? hasta = "2026-11-08") =>
        new(Admin, "admin@ejemplo.com", EsAdministrador: true, Fecha(desde), Fecha(hasta));

    private static DateOnly? Fecha(string? texto) => texto is null ? null : DateOnly.Parse(texto);

    // ---------- RF-REP-04: el rol decide el alcance ----------

    [Fact]
    [Trait("Requirement", "RF-REP-04")]
    public async Task RF_REP_04_UnEstandarSoloCuentaSusSolicitudes_ElAdministradorLasDeTodos()
    {
        var (servicio, solicitudes, _) = Crear();

        var deAna = await servicio.PermisosPorEstadoAsync(Estandar());
        var deTodos = await servicio.PermisosPorEstadoAsync(Administrador());

        Assert.Equal("ana@ejemplo.com", solicitudes.Llamadas[0].Solicitante);
        Assert.Null(solicitudes.Llamadas[1].Solicitante);
        Assert.Equal("propio", deAna.Alcance);
        Assert.Equal("todos", deTodos.Alcance);
    }

    [Fact]
    [Trait("Requirement", "RF-REP-04")]
    public async Task RF_REP_04_UnEstandarNoPuedeElegirLaPersonaDelReporte_ElCorreoSaleDeSuSesion()
    {
        var (servicio, solicitudes, _) = Crear();

        // Aunque la consulta lleve un correo ajeno en otro campo, el servicio solo usa el de la sesión de quien consulta.
        await servicio.PermisosPorEstadoAsync(new ConsultaReporte(Ana, "ana@ejemplo.com", false, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 8)));

        Assert.Equal("ana@ejemplo.com", Assert.Single(solicitudes.Llamadas).Solicitante);
    }

    [Fact]
    [Trait("Requirement", "RF-REP-04")]
    public async Task RF_REP_04_LaConstanciaDeUnEstandarEsLaSuya_YLaDelAdministradorEsElAgregado()
    {
        var (servicio, _, constancia) = Crear();

        var personal = await servicio.ConstanciaPorPlanAsync(Estandar());
        var global = await servicio.ConstanciaPorPlanAsync(Administrador());

        Assert.Equal([$"personal:{Ana}", "global"], constancia.Llamadas);
        Assert.Equal("propio", personal.Alcance);
        Assert.Equal("todos", global.Alcance);
        Assert.Equal(1, Assert.Single(personal.Filas).Personas);
        Assert.Equal(4, Assert.Single(global.Filas).Personas);
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_SinIdentidadValida_NingunReporteSeEjecuta()
    {
        var (servicio, solicitudes, constancia) = Crear();
        var sinSesion = new ConsultaReporte(Guid.Empty, "ana@ejemplo.com", false, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 8));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => servicio.PermisosPorEstadoAsync(sinSesion));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => servicio.ConstanciaPorPlanAsync(sinSesion));

        Assert.Empty(solicitudes.Llamadas);
        Assert.Empty(constancia.Llamadas);
    }

    // ---------- RF-REP-05: el rango ----------

    [Fact]
    [Trait("Requirement", "RF-REP-05")]
    public async Task RF_REP_05_ElRangoVaDelInicioDelDesdeAlInicioDelDiaSiguienteAlHasta_ParaIncluirElUltimoDia()
    {
        var (servicio, solicitudes, _) = Crear();

        var reporte = await servicio.PermisosPorEstadoAsync(Administrador("2026-11-02", "2026-11-08"));

        var (desde, hasta, _) = Assert.Single(solicitudes.Llamadas);
        Assert.Equal(new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc), desde);
        Assert.Equal(new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc), hasta);
        Assert.Equal(new DateOnly(2026, 11, 2), reporte.Desde);
        Assert.Equal(new DateOnly(2026, 11, 8), reporte.Hasta);
    }

    [Theory]
    [InlineData(null, "2026-11-08")]
    [InlineData("2026-11-02", null)]
    [InlineData(null, null)]
    [InlineData("2026-11-08", "2026-11-02")]
    [InlineData("1999-01-01", "2026-11-02")]
    [InlineData("2025-01-01", "2026-11-02")]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_UnRangoQueFaltaEstaInvertidoOEsMasLargoQueUnAnio_SeRechazaSinConsultarNada(string? desde, string? hasta)
    {
        var (servicio, solicitudes, constancia) = Crear();

        await Assert.ThrowsAsync<ArgumentException>(() => servicio.PermisosPorEstadoAsync(Administrador(desde, hasta)));
        await Assert.ThrowsAsync<ArgumentException>(() => servicio.ConstanciaPorPlanAsync(Estandar(desde, hasta)));

        Assert.Empty(solicitudes.Llamadas);
        Assert.Empty(constancia.Llamadas);
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_ElRangoMaximoDe366DiasSeAcepta_YUnDiaMasSeRechaza()
    {
        var (servicio, _, _) = Crear();

        await servicio.PermisosPorEstadoAsync(Administrador("2026-01-01", "2027-01-01")); // 366 días
        await Assert.ThrowsAsync<ArgumentException>(() => servicio.PermisosPorEstadoAsync(Administrador("2026-01-01", "2027-01-02")));
    }

    // ---------- RF-REP-01 y RF-REP-03: valores agrupados ----------

    [Fact]
    [Trait("Requirement", "RF-REP-03")]
    public async Task RF_REP_03_ElReporteDelCoreDevuelveElTotalYUnaFilaPorEstado_NoElListado()
    {
        var (servicio, _, _) = Crear();

        var reporte = await servicio.PermisosPorEstadoAsync(Administrador());

        Assert.Equal(2, reporte.Filas.Count);
        Assert.Equal(3, reporte.Total);
    }

    [Fact]
    [Trait("Requirement", "RF-REP-02")]
    public async Task RF_REP_02_ElReporteDelNegocioDeclaraLaPreguntaQueResponde()
    {
        var (servicio, _, _) = Crear();

        var reporte = await servicio.ConstanciaPorPlanAsync(Estandar());

        Assert.Equal("¿Qué tan constante fui en cada plan durante este período?", reporte.Pregunta);
    }
}
