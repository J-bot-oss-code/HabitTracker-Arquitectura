using AccessControl.Domain.Entities;
using AccessControl.Infrastructure.Persistence;
using AccessControl.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AccessControl.Infrastructure.Tests;

/// <summary>Consulta de la cola por el Administrador (RF-NOT-11) y pasada manual del procesador (RF-NOT-09), sobre SQLite en memoria.</summary>
public sealed class ColaCorreosAdminTests : IDisposable
{
    private readonly SqliteConnection _conexion = new("DataSource=:memory:");

    public ColaCorreosAdminTests()
    {
        _conexion.Open();
        using var contexto = NuevoContexto();
        contexto.Database.EnsureCreated();
    }

    public void Dispose() => _conexion.Dispose();

    private ApplicationDbContext NuevoContexto() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_conexion).Options);

    private sealed class EnviadorFalso(bool falla = false) : IEnviadorCorreo
    {
        public Task EnviarAsync(string destinatario, string asunto, string cuerpoHtml, CancellationToken cancellationToken) =>
            falla ? throw new InvalidOperationException("Servidor de correo apagado.") : Task.CompletedTask;
    }

    private ColaCorreosAdmin Servicio(ApplicationDbContext contexto, bool fallaElEnvio = false)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Correo:EsperaReintentoSegundos"] = "0" }).Build();
        var procesador = new ProcesadorColaCorreos(contexto, new EnviadorFalso(fallaElEnvio), config, NullLogger<ProcesadorColaCorreos>.Instance);
        return new ColaCorreosAdmin(contexto, procesador);
    }

    private void Sembrar(int pendientes, int enviados = 0)
    {
        using var contexto = NuevoContexto();
        for (var i = 0; i < pendientes; i++)
            contexto.CorreosEnCola.Add(new CorreoEnCola($"p{i}@ejemplo.com", "Pendiente", "<p>x</p>"));
        for (var i = 0; i < enviados; i++)
        {
            var c = new CorreoEnCola($"e{i}@ejemplo.com", "Enviado", "<p>x</p>");
            c.MarcarProcesando();
            c.MarcarEnviado();
            contexto.CorreosEnCola.Add(c);
        }
        contexto.SaveChanges();
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-11")]
    public async Task RF_NOT_11_ElAdministradorVeEstadoIntentosFechasYUltimoError_SinElCuerpo()
    {
        Sembrar(pendientes: 1);
        using (var contexto = NuevoContexto())
            await Servicio(contexto, fallaElEnvio: true).ProcesarAsync();

        using var lectura = NuevoContexto();
        var correo = Assert.Single(await Servicio(lectura).ListarAsync(null));

        Assert.Equal("Fallido", correo.Estado);
        Assert.Equal(1, correo.Intentos);
        Assert.NotNull(correo.FechaUltimoIntento);
        Assert.Contains("apagado", correo.UltimoError);
        Assert.Null(correo.FechaEnvio);
        Assert.DoesNotContain(typeof(AccessControl.Application.DTOs.CorreoColaDto).GetProperties(), p => p.Name == "Cuerpo");
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-11")]
    public async Task RF_NOT_11_ElFiltroPorEstadoDevuelveSoloEsosCorreos()
    {
        Sembrar(pendientes: 2, enviados: 1);
        using var contexto = NuevoContexto();
        var servicio = Servicio(contexto);

        Assert.Equal(2, (await servicio.ListarAsync("Pendiente")).Count);
        Assert.Single(await servicio.ListarAsync("enviado"));
        Assert.Equal(3, (await servicio.ListarAsync(null)).Count);
        Assert.Empty(await servicio.ListarAsync("Fallido"));
    }

    [Theory]
    [InlineData("Cualquiera")]
    [InlineData("9")]
    [InlineData("1")]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_UnEstadoQueNoEsUnNombreValido_SeRechaza(string estado)
    {
        using var contexto = NuevoContexto();

        await Assert.ThrowsAsync<ArgumentException>(() => Servicio(contexto).ListarAsync(estado));
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-11")]
    public async Task RF_NOT_11_LaConsultaTieneUnTopeDeCorreos()
    {
        Sembrar(pendientes: ColaCorreosAdmin.MaximoDeCorreos + 5);
        using var contexto = NuevoContexto();

        Assert.Equal(ColaCorreosAdmin.MaximoDeCorreos, (await Servicio(contexto).ListarAsync(null)).Count);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-09")]
    public async Task RF_NOT_09_ProcesarDesdeLaApiEnviaLosPendientesYDevuelveLosConteos()
    {
        Sembrar(pendientes: 2);
        using var contexto = NuevoContexto();

        var resultado = await Servicio(contexto).ProcesarAsync();

        Assert.Equal(2, resultado.Enviados);
        Assert.Equal(0, resultado.Fallidos);
        using var lectura = NuevoContexto();
        Assert.Equal(2, (await Servicio(lectura).ListarAsync("Enviado")).Count);
    }
}
