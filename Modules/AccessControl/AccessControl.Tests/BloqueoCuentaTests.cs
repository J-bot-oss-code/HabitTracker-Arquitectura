using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Application.DTOs;
using AccessControl.Application.Interfaces;
using AccessControl.Application.Service;
using AccessControl.Domain.Entities;
using AccessControl.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AccessControl.Tests;

public sealed class BloqueoCuentaTests
{
    [Fact]
    public void QuintoFalloBloqueaLaCuentaDuranteQuinceMinutos()
    {
        var bloqueo = new BloqueoCuenta();
        var antesDelQuintoFallo = DateTime.UtcNow;

        for (var intento = 0; intento < 4; intento++)
        {
            bloqueo.RegistrarFallo();
        }

        Assert.False(bloqueo.EstaBloqueado());

        bloqueo.RegistrarFallo();

        Assert.True(bloqueo.EstaBloqueado());
        Assert.Equal(5, bloqueo.IntentosFallidos);
        Assert.InRange(
            bloqueo.BloqueadoHasta!.Value,
            antesDelQuintoFallo.AddMinutes(15),
            DateTime.UtcNow.AddMinutes(15));
    }

    [Fact]
    public void FallosDuranteElBloqueoNoAumentanElContadorNiRenuevanElPlazo()
    {
        var bloqueo = new BloqueoCuenta();
        for (var intento = 0; intento < 5; intento++)
        {
            bloqueo.RegistrarFallo();
        }

        var vencimientoOriginal = bloqueo.BloqueadoHasta;

        for (var intento = 0; intento < 3; intento++)
        {
            bloqueo.RegistrarFallo();
        }

        Assert.Equal(5, bloqueo.IntentosFallidos);
        Assert.Equal(vencimientoOriginal, bloqueo.BloqueadoHasta);
    }

    [Fact]
    public void RestablecerLimpiaLosIntentosYElBloqueo()
    {
        var bloqueo = new BloqueoCuenta();
        for (var intento = 0; intento < 5; intento++)
        {
            bloqueo.RegistrarFallo();
        }

        bloqueo.Restablecer();

        Assert.Equal(0, bloqueo.IntentosFallidos);
        Assert.Null(bloqueo.BloqueadoHasta);
        Assert.False(bloqueo.EstaBloqueado());
    }

    [Fact]
    public async Task ContraseñaCorrectaSeRechazaDuranteElBloqueoTrasCincoFallos()
    {
        var usuario = new Usuario("Ana", new Email("ana@example.com"), "hash");
        for (var intento = 0; intento < 5; intento++)
        {
            usuario.RegistrarIntentoFallido();
        }

        var servicio = CrearServicio(usuario);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servicio.AutenticarUsuarioAsync(new LoginRequestDto("ana@example.com", "ClaveValida123")));

        Assert.Contains("bloqueada", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(5, usuario.Bloqueo.IntentosFallidos);
    }

    private static UsuarioService CrearServicio(Usuario usuario) => new(
        new RepositorioFalso(usuario),
        new PasswordHasherFalso(),
        new ColaFalsa(),
        new AuditoriaFalsa(),
        new JwtFalso(),
        new BlacklistFalsa(),
        NullLogger<UsuarioService>.Instance);

    private sealed class RepositorioFalso(Usuario usuario) : IUsuarioRepository
    {
        public Task<Usuario?> GetByCorreoAsync(Email correo) => Task.FromResult<Usuario?>(usuario);
        public Task<Usuario?> GetByIdAsync(Guid id) => Task.FromResult<Usuario?>(null);
        public Task<Usuario?> GetByNameAsync(string nombreCompleto) => Task.FromResult<Usuario?>(null);
        public Task<Usuario?> GetByTokenRecuperacionAsync(string token) => Task.FromResult<Usuario?>(null);
        public Task<IEnumerable<Usuario>> ObtenerTodosAsync() => Task.FromResult<IEnumerable<Usuario>>([]);
        public Task<bool> CorreoExistsAsync(Email correo) => Task.FromResult(false);
        public Task SaveAsync(Usuario usuario) => Task.CompletedTask;
        public Task UpdateAsync(Usuario usuario) => Task.CompletedTask;
    }

    private sealed class PasswordHasherFalso : IPasswordHasher
    {
        public string Hash(string password) => "hash";
        public bool verificar(string hash, string passwordPlana) => true;
    }

    private sealed class ColaFalsa : IEmailQueue
    {
        public Task EncolarCorreoRecuperacionAsync(string correoDestino, string codigoGenerado) => Task.CompletedTask;
        public Task EncolarCorreoActivacionAsync(string correo, string token) => Task.CompletedTask;
    }

    private sealed class AuditoriaFalsa : IAuditoriaService
    {
        public Task RegistroAuditoriaAsync(Guid usuarioId, string nombreUsuarioResponsable, string accion,
            string entidadAfectada, string identificador, string valorAnterior, string valorNuevo) => Task.CompletedTask;
    }

    private sealed class JwtFalso : IJwtProvider
    {
        public string Generar(Usuario usuario) => "token";
    }

    private sealed class BlacklistFalsa : ITokenBlacklist
    {
        public void InvalidarToken(string token) { }
        public bool EstaInvalidado(string token) => false;
        public void RevocarSesionesAnteriores(Guid usuarioId) { }
        public bool SesionVigente(Guid usuarioId, DateTimeOffset emitidoEn) => true;
    }
}
