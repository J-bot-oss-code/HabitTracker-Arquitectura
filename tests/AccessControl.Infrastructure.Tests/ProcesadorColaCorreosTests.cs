using AccessControl.Domain.Entities;
using AccessControl.Infrastructure.Persistence;
using AccessControl.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AccessControl.Infrastructure.Tests;

/// <summary>
/// Cola de correos (RF-NOT-08 a 12) con el procesador real sobre SQLite en memoria y un enviador falso: sin SMTP, sin API y
/// sin SQL Server.
/// </summary>
public sealed class ProcesadorColaCorreosTests : IDisposable
{
    private readonly SqliteConnection _conexion = new("DataSource=:memory:");

    public ProcesadorColaCorreosTests()
    {
        _conexion.Open();
        using var contexto = NuevoContexto();
        contexto.Database.EnsureCreated();
    }

    public void Dispose() => _conexion.Dispose();

    private ApplicationDbContext NuevoContexto() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_conexion).Options);

    private sealed class EnviadorFalso : IEnviadorCorreo
    {
        public List<string> Entregados { get; } = [];
        public bool Falla { get; set; }

        public Task EnviarAsync(string destinatario, string asunto, string cuerpoHtml, CancellationToken cancellationToken)
        {
            if (Falla)
            {
                throw new InvalidOperationException("Servidor de correo apagado.");
            }

            Entregados.Add(destinatario);
            return Task.CompletedTask;
        }
    }

    private static IConfiguration Config(int esperaSegundos = 0, int minutosRecuperar = 10) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Correo:EsperaReintentoSegundos"] = esperaSegundos.ToString(),
            ["Correo:MinutosParaRecuperar"] = minutosRecuperar.ToString(),
        }).Build();

    private ProcesadorColaCorreos Procesador(ApplicationDbContext contexto, EnviadorFalso enviador, IConfiguration? config = null) =>
        new(contexto, enviador, config ?? Config(), NullLogger<ProcesadorColaCorreos>.Instance);

    private Guid Encolar(string destinatario = "ana@ejemplo.com")
    {
        using var contexto = NuevoContexto();
        var correo = new CorreoEnCola(destinatario, "Asunto", "<p>Hola</p>");
        contexto.CorreosEnCola.Add(correo);
        contexto.SaveChanges();
        return correo.Id;
    }

    private CorreoEnCola Leer(Guid id)
    {
        using var contexto = NuevoContexto();
        return contexto.CorreosEnCola.AsNoTracking().Single(c => c.Id == id);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-09")]
    public async Task RF_NOT_09_ElProcesadorEnviaLosPendientesSinQueNadieRepitaLaOperacion()
    {
        var id = Encolar();
        var enviador = new EnviadorFalso();
        using var contexto = NuevoContexto();

        var resultado = await Procesador(contexto, enviador).ProcesarAsync(CancellationToken.None);

        Assert.Equal(1, resultado.Enviados);
        var correo = Leer(id);
        Assert.Equal(EstadoCorreo.Enviado, correo.Estado);
        Assert.NotNull(correo.FechaEnvio);
        Assert.Equal(["ana@ejemplo.com"], enviador.Entregados);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-12")]
    public async Task RF_NOT_12_EjecutarElProcesadorDosVecesSeguidas_NoDuplicaElEnvio()
    {
        Encolar();
        var enviador = new EnviadorFalso();

        using (var c1 = NuevoContexto())
            await Procesador(c1, enviador).ProcesarAsync(CancellationToken.None);
        using var c2 = NuevoContexto();
        var segunda = await Procesador(c2, enviador).ProcesarAsync(CancellationToken.None);

        Assert.Equal(0, segunda.Enviados);
        Assert.Single(enviador.Entregados);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-12")]
    public async Task RF_NOT_12_ElReclamoEsAtomico_SoloUnaPasadaTomaElCorreo()
    {
        var id = Encolar();
        using var primera = NuevoContexto();
        using var segunda = NuevoContexto();
        // Las dos pasadas leyeron el correo como Pendiente antes de que cualquiera lo reclamara.
        var verPrimera = primera.CorreosEnCola.Single(c => c.Id == id);
        var verSegunda = segunda.CorreosEnCola.Single(c => c.Id == id);

        var ganaPrimera = await Procesador(primera, new EnviadorFalso()).ReclamarAsync(verPrimera, DateTime.UtcNow, CancellationToken.None);
        var ganaSegunda = await Procesador(segunda, new EnviadorFalso()).ReclamarAsync(verSegunda, DateTime.UtcNow, CancellationToken.None);

        Assert.True(ganaPrimera);
        Assert.False(ganaSegunda);
        Assert.Equal(EstadoCorreo.Procesando, Leer(id).Estado);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-10")]
    public async Task RF_NOT_10_UnCorreoQueFalla_SeReintentaElMaximoYQuedaFallidoConSuUltimoError()
    {
        var id = Encolar();
        var enviador = new EnviadorFalso { Falla = true };

        for (var i = 0; i < CorreoEnCola.MaxIntentos + 1; i++)
        {
            using var contexto = NuevoContexto();
            await Procesador(contexto, enviador).ProcesarAsync(CancellationToken.None);
        }

        var correo = Leer(id);
        Assert.Equal(EstadoCorreo.Fallido, correo.Estado);
        Assert.Equal(CorreoEnCola.MaxIntentos, correo.Intentos);
        Assert.Contains("apagado", correo.UltimoError);
        Assert.NotNull(correo.FechaUltimoIntento);
        Assert.Empty(enviador.Entregados);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-10")]
    public async Task RF_NOT_10_ElReintentoRespetaLaEsperaConfigurada()
    {
        var id = Encolar();
        var enviador = new EnviadorFalso { Falla = true };
        var config = Config(esperaSegundos: 3600);

        using (var c1 = NuevoContexto())
            await Procesador(c1, enviador, config).ProcesarAsync(CancellationToken.None);
        using var c2 = NuevoContexto();
        var segunda = await Procesador(c2, enviador, config).ProcesarAsync(CancellationToken.None);

        Assert.Equal(0, segunda.Fallidos);
        Assert.Equal(1, Leer(id).Intentos);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-10")]
    public void RF_NOT_10_ElUltimoErrorSeGuardaTruncado()
    {
        var correo = new CorreoEnCola("ana@ejemplo.com", "Asunto", "<p>Hola</p>");
        correo.MarcarProcesando();

        correo.RegistrarFallo(new string('x', 5000));

        Assert.Equal(CorreoEnCola.LongitudMaximaError, correo.UltimoError!.Length);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-12")]
    public async Task RF_NOT_12_UnCorreoQueQuedoEnProcesandoSeRecuperaYSeEnviaUnaSolaVez()
    {
        var id = Encolar();
        using (var contexto = NuevoContexto())
        {
            // Simula una caída: reclamado hace 20 minutos y nunca enviado.
            var correo = contexto.CorreosEnCola.Single(c => c.Id == id);
            await Procesador(contexto, new EnviadorFalso()).ReclamarAsync(correo, DateTime.UtcNow.AddMinutes(-20), CancellationToken.None);
        }
        var enviador = new EnviadorFalso();

        // La primera pasada lo recupera (cuenta un intento fallido); la siguiente lo envía; la tercera no repite nada.
        using (var c1 = NuevoContexto())
            Assert.Equal(1, (await Procesador(c1, enviador).ProcesarAsync(CancellationToken.None)).Recuperados);
        using (var c2 = NuevoContexto())
            Assert.Equal(1, (await Procesador(c2, enviador).ProcesarAsync(CancellationToken.None)).Enviados);
        using (var c3 = NuevoContexto())
            Assert.Equal(0, (await Procesador(c3, enviador).ProcesarAsync(CancellationToken.None)).Enviados);

        Assert.Single(enviador.Entregados);
        Assert.Equal(EstadoCorreo.Enviado, Leer(id).Estado);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-12")]
    public async Task RF_NOT_12_UnCorreoReclamadoHaceUnMomento_NoSeRecuperaNiSeDuplica()
    {
        var id = Encolar();
        using (var contexto = NuevoContexto())
        {
            var correo = contexto.CorreosEnCola.Single(c => c.Id == id);
            await Procesador(contexto, new EnviadorFalso()).ReclamarAsync(correo, DateTime.UtcNow.AddMinutes(-1), CancellationToken.None);
        }
        var enviador = new EnviadorFalso();

        using var otra = NuevoContexto();
        var resultado = await Procesador(otra, enviador).ProcesarAsync(CancellationToken.None);

        Assert.Equal(0, resultado.Recuperados);
        Assert.Empty(enviador.Entregados);
        Assert.Equal(EstadoCorreo.Procesando, Leer(id).Estado);
    }
}
