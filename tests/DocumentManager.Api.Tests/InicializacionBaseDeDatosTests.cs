using AccessControl.Application.Interfaces;
using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Domain.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WebApi.Extensions;

namespace DocumentManager.Api.Tests;

public class InicializacionBaseDeDatosTests
{
    private const string ClaveValida = "Ejemplo_Admin_2026!";

    // ---------- Indicador apagado ----------

    [Fact]
    [Trait("Requirement", "RD-09")]
    public async Task RD_09_SinElIndicador_NoMigraNiTocaLaBaseDeDatos()
    {
        // No se registra ningún DbContext: si el inicializador intentara migrar, resolver el servicio fallaría.
        var builder = WebApplication.CreateBuilder();
        await using var app = builder.Build();

        await app.InicializarBaseDeDatosAsync();
    }

    [Fact]
    [Trait("Requirement", "RD-09")]
    public async Task RD_09_IndicadorEnFalso_NoMigraNiTocaLaBaseDeDatos()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["BaseDeDatos:MigrarAlIniciar"] = "false";
        await using var app = builder.Build();

        await app.InicializarBaseDeDatosAsync();
    }

    // ---------- Reintentos ----------

    [Fact]
    [Trait("Requirement", "RD-09")]
    public async Task RD_09_Reintentos_UnFalloTransitorio_SeReintentaHastaTenerExito()
    {
        var llamadas = 0;

        await InicializacionBaseDeDatos.EjecutarConReintentosAsync(
            () => ++llamadas < 3 ? throw new TimeoutException() : Task.CompletedTask,
            intentosMaximos: 5, espera: TimeSpan.Zero, NullLogger.Instance);

        Assert.Equal(3, llamadas);
    }

    [Fact]
    [Trait("Requirement", "RD-09")]
    public async Task RD_09_Reintentos_UnErrorQueNoEsDeConexion_SeRelanzaSinReintentar()
    {
        var llamadas = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InicializacionBaseDeDatos.EjecutarConReintentosAsync(
                () => { llamadas++; throw new InvalidOperationException("migración rota"); },
                intentosMaximos: 5, espera: TimeSpan.Zero, NullLogger.Instance));

        Assert.Equal(1, llamadas);
    }

    [Fact]
    [Trait("Requirement", "RD-09")]
    public async Task RD_09_Reintentos_AgotadosLosIntentos_RelanzaElUltimoFallo()
    {
        var llamadas = 0;

        await Assert.ThrowsAsync<TimeoutException>(() =>
            InicializacionBaseDeDatos.EjecutarConReintentosAsync(
                () => { llamadas++; throw new TimeoutException(); },
                intentosMaximos: 3, espera: TimeSpan.Zero, NullLogger.Instance));

        Assert.Equal(3, llamadas);
    }

    // ---------- Administrador inicial ----------

    [Fact]
    [Trait("Requirement", "RD-05")]
    public async Task RD_05_Siembra_CreaUnAdministradorActivoConLaClaveEnHash()
    {
        var repo = new RepositorioDeUsuariosFalso();

        await InicializacionBaseDeDatos.SembrarAdministradorAsync(
            repo, new HasherFalso(), "Admin@Ejemplo.com", ClaveValida, NullLogger.Instance);

        var admin = Assert.Single(repo.Guardados);
        Assert.Equal(Rol.Administrador, admin.Rol);
        Assert.True(admin.Activo);
        Assert.Equal("admin@ejemplo.com", admin.Correo.Valor);
        Assert.NotEqual(ClaveValida, admin.PasswordHash);
    }

    [Fact]
    [Trait("Requirement", "RD-09")]
    public async Task RD_09_Siembra_EsIdempotente_NoDuplicaAlRepetirse()
    {
        var repo = new RepositorioDeUsuariosFalso();

        await InicializacionBaseDeDatos.SembrarAdministradorAsync(repo, new HasherFalso(), "admin@ejemplo.com", ClaveValida, NullLogger.Instance);
        await InicializacionBaseDeDatos.SembrarAdministradorAsync(repo, new HasherFalso(), "admin@ejemplo.com", ClaveValida, NullLogger.Instance);

        Assert.Single(repo.Guardados);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("admin@ejemplo.com", null)]
    [InlineData(null, ClaveValida)]
    [InlineData("", "")]
    [Trait("Requirement", "RD-10")]
    public async Task RD_10_Siembra_SinVariablesDeEntorno_NoCreaNada(string? correo, string? clave)
    {
        var repo = new RepositorioDeUsuariosFalso();

        await InicializacionBaseDeDatos.SembrarAdministradorAsync(repo, new HasherFalso(), correo, clave, NullLogger.Instance);

        Assert.Empty(repo.Guardados);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("corta123")]
    [InlineData("once_chars!")]
    [Trait("Requirement", "RD-10")]
    public async Task RD_10_Siembra_ClaveDebil_FallaDeInmediatoYNoCreaLaCuenta(string claveDebil)
    {
        var repo = new RepositorioDeUsuariosFalso();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InicializacionBaseDeDatos.SembrarAdministradorAsync(
                repo, new HasherFalso(), "admin@ejemplo.com", claveDebil, NullLogger.Instance));

        Assert.Empty(repo.Guardados);
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_Siembra_CorreoInvalido_FallaConUnMensajeControlado()
    {
        var repo = new RepositorioDeUsuariosFalso();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InicializacionBaseDeDatos.SembrarAdministradorAsync(
                repo, new HasherFalso(), "esto-no-es-un-correo", ClaveValida, NullLogger.Instance));

        Assert.Contains("ADMIN_CORREO", ex.Message);
    }

    [Fact]
    [Trait("Requirement", "RD-10")]
    public async Task RD_10_Siembra_NuncaEscribeLaClaveEnElLog()
    {
        var registro = new LoggerQueGuarda();
        var repo = new RepositorioDeUsuariosFalso();

        await InicializacionBaseDeDatos.SembrarAdministradorAsync(repo, new HasherFalso(), "admin@ejemplo.com", ClaveValida, registro);

        Assert.NotEmpty(registro.Mensajes);
        Assert.DoesNotContain(registro.Mensajes, m => m.Contains(ClaveValida));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_LaMetaDeDemostracion_TieneUnIdFijoYDocumentado()
    {
        Assert.Equal(new Guid("5f1c0a00-0000-4000-8000-000000000002"), InicializacionBaseDeDatos.MetaDemostracionId);
    }

    // ---------- Dobles ----------

    private sealed class HasherFalso : IPasswordHasher
    {
        public string Hash(string password) => "HASH::" + password.Length + "::" + new string(password.Reverse().ToArray());
        public bool Verificar(string hash, string passwordPlana) => hash == Hash(passwordPlana);
        public void SimularVerificacion(string passwordPlana) { }
    }

    private sealed class RepositorioDeUsuariosFalso : IUsuarioRepository
    {
        public List<Usuario> Guardados { get; } = [];

        public Task<Usuario?> GetByIdAsync(Guid id) => Task.FromResult(Guardados.FirstOrDefault(u => u.Id == id));
        public Task<Usuario?> GetByCorreoAsync(Email correo) => Task.FromResult(Guardados.FirstOrDefault(u => u.Correo.Equals(correo)));
        public Task SaveAsync(Usuario usuario) { Guardados.Add(usuario); return Task.CompletedTask; }
        public Task<bool> CorreoExistsAsync(Email correo) => Task.FromResult(Guardados.Any(u => u.Correo.Equals(correo)));
        public Task UpdateAsync(Usuario usuario) => Task.CompletedTask;
        public Task<int> ReservarIntentoActivacionAsync(Guid usuarioId) => Task.FromResult(0);
        public Task<IEnumerable<Usuario>> ObtenerTodosAsync() => Task.FromResult<IEnumerable<Usuario>>(Guardados);
        public Task<int> ContarAdministradoresActivosAsync() =>
            Task.FromResult(Guardados.Count(u => u.Rol == Rol.Administrador && u.Activo));
    }

    private sealed class LoggerQueGuarda : ILogger
    {
        public List<string> Mensajes { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Mensajes.Add(formatter(state, exception));
    }
}
