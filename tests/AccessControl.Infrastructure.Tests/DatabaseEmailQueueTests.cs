using AccessControl.Domain.Entities;
using AccessControl.Infrastructure.Persistence;
using AccessControl.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AccessControl.Infrastructure.Tests;

/// <summary>Encolar correos (RF-NOT-08): quedan pendientes en la tabla; la operación no espera al servidor de correo.</summary>
public sealed class DatabaseEmailQueueTests : IDisposable
{
    private readonly SqliteConnection _conexion = new("DataSource=:memory:");

    public DatabaseEmailQueueTests()
    {
        _conexion.Open();
        using var contexto = NuevoContexto();
        contexto.Database.EnsureCreated();
    }

    public void Dispose() => _conexion.Dispose();

    private ApplicationDbContext NuevoContexto() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_conexion).Options);

    private static DatabaseEmailQueue Cola(ApplicationDbContext contexto) =>
        new(contexto, new ConfigurationBuilder().Build(), NullLogger<DatabaseEmailQueue>.Instance);

    [Fact]
    [Trait("Requirement", "RF-NOT-08")]
    public async Task RF_NOT_08_UnCorreoInformativoQuedaPendienteEnLaCola_SinEnviarseEnLaOperacion()
    {
        using var contexto = NuevoContexto();

        await Cola(contexto).EncolarCorreoAsync("ana@ejemplo.com", "Tu solicitud fue aprobada", "Solicitud aprobada", "Ya puedes reconfigurar tus hábitos.");

        using var lectura = NuevoContexto();
        var correo = lectura.CorreosEnCola.Single();
        Assert.Equal(EstadoCorreo.Pendiente, correo.Estado);
        Assert.Equal(0, correo.Intentos);
        Assert.Equal("ana@ejemplo.com", correo.Destinatario);
        Assert.Contains("Tu solicitud fue aprobada", correo.Asunto);
        Assert.Contains("Ya puedes reconfigurar tus hábitos.", correo.Cuerpo);
        Assert.DoesNotContain("letter-spacing", correo.Cuerpo); // sin la caja del código
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_ElTextoDeUnCorreoInformativoSeEscapa_UnMotivoConHtmlNoSeInterpreta()
    {
        using var contexto = NuevoContexto();

        await Cola(contexto).EncolarCorreoAsync("ana@ejemplo.com", "Resultado", "Rechazada", "Motivo: <script>alert(1)</script>");

        using var lectura = NuevoContexto();
        var cuerpo = lectura.CorreosEnCola.Single().Cuerpo;
        Assert.DoesNotContain("<script>", cuerpo);
        Assert.Contains("&lt;script&gt;", cuerpo);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-08")]
    public async Task RF_NOT_08_ElCorreoDeActivacionSigueLlevandoLaCajaDelCodigo()
    {
        using var contexto = NuevoContexto();

        await Cola(contexto).EncolarCorreoActivacionAsync("ana@ejemplo.com", "K7Q2ZP");

        using var lectura = NuevoContexto();
        Assert.Contains("K7Q2ZP", lectura.CorreosEnCola.Single().Cuerpo);
        Assert.Contains("letter-spacing", lectura.CorreosEnCola.Single().Cuerpo);
    }
}
