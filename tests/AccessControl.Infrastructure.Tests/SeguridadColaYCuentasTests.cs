using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Infrastructure.Persistence;
using AccessControl.Infrastructure.Repositories;
using AccessControl.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AccessControl.Infrastructure.Tests;

/// <summary>Correcciones de la auditoría con EF Core y SQLite: topes de la cola (SEG-05), su purga (SEG-12) y cuentas pendientes (SEG-02).</summary>
public sealed class SeguridadColaYCuentasTests : IDisposable
{
    private readonly SqliteConnection _conexion = new("DataSource=:memory:");

    public SeguridadColaYCuentasTests()
    {
        _conexion.Open();
        using var contexto = NuevoContexto();
        contexto.Database.EnsureCreated();
    }

    public void Dispose() => _conexion.Dispose();

    private ApplicationDbContext NuevoContexto() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_conexion).Options);

    private DatabaseEmailQueue Cola(ApplicationDbContext contexto, int? topeDiario = null) =>
        new(contexto,
            new ConfigurationBuilder().AddInMemoryCollection(
                topeDiario is { } t ? new Dictionary<string, string?> { ["Correo:TopeDiario"] = t.ToString() } : []).Build(),
            NullLogger<DatabaseEmailQueue>.Instance);

    private int Encolados(string? destinatario = null)
    {
        using var lectura = NuevoContexto();
        return lectura.CorreosEnCola.Count(c => destinatario == null || c.Destinatario == destinatario);
    }

    // ---------- SEG-05: topes de envío ----------

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task SEG_05_ComoMuchoDiezCorreosPorHoraAlMismoDestinatario_ElOnceNoSeEncola()
    {
        for (var i = 0; i < 11; i++)
        {
            using var contexto = NuevoContexto();
            await Cola(contexto).EncolarCorreoRecuperacionAsync("victima@ejemplo.com", "123456");
        }

        Assert.Equal(10, Encolados("victima@ejemplo.com"));

        using (var contexto = NuevoContexto())
            await Cola(contexto).EncolarCorreoRecuperacionAsync("otra@ejemplo.com", "123456");
        Assert.Equal(1, Encolados("otra@ejemplo.com")); // el tope es por destinatario
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task SEG_05_ElTopeDiarioDejaUnaReservaParaActivacionYRecuperacion()
    {
        // Con tope 60 y reserva 50: los informativos se cortan a los 10; los críticos siguen hasta 60.
        for (var i = 0; i < 12; i++)
        {
            using var contexto = NuevoContexto();
            await Cola(contexto, topeDiario: 60).EncolarCorreoAsync($"p{i}@ejemplo.com", "Aviso", "Título", "Mensaje");
        }
        Assert.Equal(10, Encolados());

        for (var i = 0; i < 55; i++)
        {
            using var contexto = NuevoContexto();
            await Cola(contexto, topeDiario: 60).EncolarCorreoActivacionAsync($"a{i}@ejemplo.com", "123456");
        }
        Assert.Equal(60, Encolados()); // 10 informativos + 50 críticos, y el resto no
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task SEG_05_UnaColaVaciaEncolaSinProblemas()
    {
        using var contexto = NuevoContexto();
        await Cola(contexto).EncolarCorreoActivacionAsync("nueva@ejemplo.com", "123456");

        Assert.Equal(1, Encolados());
    }

    // ---------- SEG-12: purga de la cola ----------

    [Fact]
    [Trait("Requirement", "RD-09")]
    public async Task SEG_12_LaPurgaBorraSoloLosTerminadosDeMasDeCatorceDias()
    {
        using (var contexto = NuevoContexto())
        {
            var viejoEnviado = new CorreoEnCola("a@ejemplo.com", "Asunto", "Cuerpo");
            viejoEnviado.MarcarProcesando();
            viejoEnviado.MarcarEnviado();
            var viejoPendiente = new CorreoEnCola("b@ejemplo.com", "Asunto", "Cuerpo");
            var nuevoEnviado = new CorreoEnCola("c@ejemplo.com", "Asunto", "Cuerpo");
            nuevoEnviado.MarcarProcesando();
            nuevoEnviado.MarcarEnviado();
            contexto.CorreosEnCola.AddRange(viejoEnviado, viejoPendiente, nuevoEnviado);
            await contexto.SaveChangesAsync();
            await contexto.CorreosEnCola.Where(c => c.Destinatario != "c@ejemplo.com")
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.FechaCreacion, DateTime.UtcNow.AddDays(-15)));
        }

        using (var contexto = NuevoContexto())
            await new ProcesadorColaCorreos(contexto, new EnviadorQueNoEnvia(), new ConfigurationBuilder().Build(),
                NullLogger<ProcesadorColaCorreos>.Instance).ProcesarAsync(CancellationToken.None);

        using var lectura = NuevoContexto();
        var quedan = lectura.CorreosEnCola.Select(c => c.Destinatario).OrderBy(d => d).ToList();
        Assert.Equal(["b@ejemplo.com", "c@ejemplo.com"], quedan); // el pendiente viejo y el enviado reciente se quedan
    }

    private sealed class EnviadorQueNoEnvia : IEnviadorCorreo
    {
        public Task EnviarAsync(string destinatario, string asunto, string cuerpoHtml, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    // ---------- SEG-02: cuentas pendientes para purgar ----------

    [Fact]
    [Trait("Requirement", "RD-09")]
    public async Task SEG_02_SoloSeListanLasPendientesCreadasAntesDelPlazo()
    {
        var pendiente = new Usuario("Pendiente", new Email("pendiente@ejemplo.com"), "hash");
        pendiente.EstablecerTokenActivacion("123456", DateTime.UtcNow.AddHours(24));
        var activa = new Usuario("Activa", new Email("activa@ejemplo.com"), "hash");
        activa.Activar();
        using (var contexto = NuevoContexto())
        {
            var repo = new UsuarioRepository(contexto);
            await repo.SaveAsync(pendiente);
            await repo.SaveAsync(activa);
        }

        using var lectura = NuevoContexto();
        var repoLectura = new UsuarioRepository(lectura);

        Assert.Empty(await repoLectura.ObtenerPendientesSinActivarAsync(DateTime.UtcNow.AddHours(-48), 50)); // recién creada
        var vencidas = await repoLectura.ObtenerPendientesSinActivarAsync(DateTime.UtcNow.AddMinutes(1), 50);
        Assert.Equal([pendiente.Id], vencidas.Select(u => u.Id)); // nunca la activa
    }
}
