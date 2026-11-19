using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Permissions.Domain.Entities;
using Permissions.Infrastructure.Persistence;
using Permissions.Infrastructure.Repositories;

namespace Permissions.Infrastructure.Tests;

/// <summary>
/// Reporte del Core: solicitudes de permiso por estado y período (RF-REP-01, 03 y 05), con la agrupación hecha por la base de
/// datos (SQLite en memoria, sin SQL Server, RD-12).
/// </summary>
public sealed class SolicitudPermisoReportesTests : IDisposable
{
    private static readonly DateTime Semana1 = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);  // lunes
    private static readonly DateTime Semana2 = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);  // lunes siguiente

    private readonly SqliteConnection _conexion = new("DataSource=:memory:");

    public SolicitudPermisoReportesTests()
    {
        _conexion.Open();
        using var contexto = NuevoContexto();
        contexto.Database.EnsureCreated();
    }

    public void Dispose() => _conexion.Dispose();

    private PermissionsDbContext NuevoContexto() =>
        new(new DbContextOptionsBuilder<PermissionsDbContext>().UseSqlite(_conexion).Options);

    /// <summary>Crea una solicitud en la fecha indicada y, si se pide, la deja Aplicada o Rechazada.</summary>
    private async Task Sembrar(string solicitante, string permiso, DateTime creada, string? resolucion = null)
    {
        var solicitud = new SolicitudPermiso(solicitante, permiso);
        if (resolucion == "Aplicada") solicitud.Aprobar("admin@ejemplo.com", creada.AddHours(1));
        if (resolucion == "Rechazada") solicitud.Rechazar("admin@ejemplo.com", "No", creada.AddHours(1));

        await using var contexto = NuevoContexto();
        await new SolicitudPermisoRepository(contexto).AddAsync(solicitud);
        // La fecha de creación la fija el constructor (ahora): se acomoda para probar los rangos.
        await contexto.SolicitudesPermisos.Where(s => s.Id == solicitud.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.FechaCreacion, creada));
    }

    private async Task SembrarEscenario()
    {
        // Semana 1: ana pide 2 (una Aplicada, una Rechazada) y beto 1 (Pendiente). Semana 2: ana pide 2 Pendientes y beto 1 Aplicada.
        await Sembrar("ana@ejemplo.com", "Habits.AdvancedReconfig", Semana1, "Aplicada");
        await Sembrar("ana@ejemplo.com", "Reports.View", Semana1, "Rechazada");
        await Sembrar("beto@ejemplo.com", "Docs.Share", Semana1);
        await Sembrar("ana@ejemplo.com", "Docs.Share", Semana2);
        await Sembrar("ana@ejemplo.com", "Habits.Extra", Semana2);
        await Sembrar("beto@ejemplo.com", "Reports.View", Semana2, "Aplicada");
    }

    private static Dictionary<string, int> Por(IReadOnlyList<Permissions.Application.Interfaces.ConteoPorEstado> filas) =>
        filas.ToDictionary(f => f.Estado, f => f.Cantidad);

    [Fact]
    [Trait("Requirement", "RF-REP-01")]
    public async Task RF_REP_01_DevuelveUnaFilaPorEstadoConSuCantidad_NoElListadoDeSolicitudes()
    {
        await SembrarEscenario();
        await using var contexto = NuevoContexto();

        var filas = await new SolicitudPermisoReportes(contexto).ContarPorEstadoAsync(Semana1.Date, Semana2.Date.AddDays(7), null);

        Assert.Equal(new Dictionary<string, int> { ["Pendiente"] = 3, ["Aplicada"] = 2, ["Rechazada"] = 1 }, Por(filas));
    }

    [Fact]
    [Trait("Requirement", "RF-REP-03")]
    public async Task RF_REP_03_ElResultadoTieneMenosFilasQueLasSolicitudesQueResume()
    {
        await SembrarEscenario();
        await using var contexto = NuevoContexto();

        var filas = await new SolicitudPermisoReportes(contexto).ContarPorEstadoAsync(Semana1.Date, Semana2.Date.AddDays(7), null);

        Assert.True(filas.Count < 6);
        Assert.Equal(6, filas.Sum(f => f.Cantidad));
    }

    [Fact]
    [Trait("Requirement", "RF-REP-05")]
    public async Task RF_REP_05_DosRangosDistintosDanResultadosDistintosSobreLosMismosDatos()
    {
        await SembrarEscenario();
        await using var contexto = NuevoContexto();
        var reporte = new SolicitudPermisoReportes(contexto);

        var primera = Por(await reporte.ContarPorEstadoAsync(Semana1.Date, Semana1.Date.AddDays(7), null));
        var segunda = Por(await reporte.ContarPorEstadoAsync(Semana2.Date, Semana2.Date.AddDays(7), null));

        Assert.Equal(new Dictionary<string, int> { ["Pendiente"] = 1, ["Aplicada"] = 1, ["Rechazada"] = 1 }, primera);
        Assert.Equal(new Dictionary<string, int> { ["Pendiente"] = 2, ["Aplicada"] = 1 }, segunda);
        Assert.NotEqual(primera, segunda);
    }

    [Fact]
    [Trait("Requirement", "RF-REP-05")]
    public async Task RF_REP_05_UnRangoSinSolicitudesDevuelveCeroFilas()
    {
        await SembrarEscenario();
        await using var contexto = NuevoContexto();

        var filas = await new SolicitudPermisoReportes(contexto).ContarPorEstadoAsync(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), null);

        Assert.Empty(filas);
    }

    [Fact]
    [Trait("Requirement", "RF-REP-05")]
    public async Task RF_REP_05_ElRangoIncluyeElDiaDeInicioYExcluyeElLimiteSuperior()
    {
        await Sembrar("ana@ejemplo.com", "Docs.Share", new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc));   // justo al empezar el día 2
        await Sembrar("ana@ejemplo.com", "Reports.View", new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc)); // justo al empezar el día 3
        await using var contexto = NuevoContexto();

        var filas = await new SolicitudPermisoReportes(contexto).ContarPorEstadoAsync(
            new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc), null);

        Assert.Equal(1, Assert.Single(filas).Cantidad);
    }

    [Fact]
    [Trait("Requirement", "RF-REP-04")]
    public async Task RF_REP_04_ConUnSolicitanteSoloCuentaLasSuyas_SinDistinguirMayusculas()
    {
        await SembrarEscenario();
        await using var contexto = NuevoContexto();
        var reporte = new SolicitudPermisoReportes(contexto);

        var deAna = await reporte.ContarPorEstadoAsync(Semana1.Date, Semana2.Date.AddDays(7), "ANA@Ejemplo.com");
        var deBeto = await reporte.ContarPorEstadoAsync(Semana1.Date, Semana2.Date.AddDays(7), "beto@ejemplo.com");

        Assert.Equal(4, deAna.Sum(f => f.Cantidad));
        Assert.Equal(2, deBeto.Sum(f => f.Cantidad));
        Assert.Equal(6, deAna.Sum(f => f.Cantidad) + deBeto.Sum(f => f.Cantidad));
    }
}
