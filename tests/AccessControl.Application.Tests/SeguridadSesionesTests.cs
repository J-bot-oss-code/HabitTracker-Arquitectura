using AccessControl.Application.Interfaces;
using AccessControl.Application.Service;
using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using WebApi.Extensions;

namespace AccessControl.Application.Tests;

/// <summary>SEG-08 (aviso al Administrador en cada inicio de sesión) y SEG-10 (respuestas de la API sin caché), sin API ni base (RD-12).</summary>
public class SeguridadSesionesTests
{
    private static (GestorSesiones Gestor, Mock<IEmailQueue> Cola) Crear(Usuario usuario)
    {
        var usuarios = new Mock<IUsuarioRepository>();
        usuarios.Setup(u => u.GetByIdAsync(usuario.Id)).ReturnsAsync(usuario);
        var cola = new Mock<IEmailQueue>();
        var gestor = new GestorSesiones(
            Mock.Of<ISesionRepository>(), usuarios.Object, Mock.Of<ITokenBlacklist>(), Mock.Of<IJwtProvider>(),
            OpcionesSesion.PorDefecto, Mock.Of<ILogger<GestorSesiones>>(), cola.Object);
        return (gestor, cola);
    }

    private static Usuario Cuenta(Rol rol)
    {
        var usuario = new Usuario("Persona", new Email("admin@ejemplo.com"), "hash", rol);
        usuario.Activar();
        return usuario;
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task SEG_08_CadaInicioDeSesionDeUnAdministradorLeLlegaPorCorreo_SinDatosSecretos()
    {
        var admin = Cuenta(Rol.Administrador);
        var (gestor, cola) = Crear(admin);

        await gestor.IniciarAsync(admin.Id, "Mozilla/5.0 (Windows NT 10.0) Chrome/131.0", "203.0.113.7");

        cola.Verify(c => c.EncolarCorreoAsync(
            "admin@ejemplo.com", It.IsAny<string>(), It.IsAny<string>(),
            It.Is<string>(m => m.Contains("Chrome en Windows") && !m.Contains("203.0.113.7") && !m.Contains("hash"))), Times.Once);
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task SEG_08_UnEstandarNoRecibeElAviso()
    {
        var persona = Cuenta(Rol.Estandar);
        var (gestor, cola) = Crear(persona);

        await gestor.IniciarAsync(persona.Id, null, null);

        cola.VerifyNoOtherCalls();
    }

    [Theory]
    [Trait("Requirement", "RD-08")]
    [InlineData("/api/profile/health", null, "no-store")]
    [InlineData("/api/auth/login", null, "no-store")]
    [InlineData("/api/plans", "public,max-age=300", "public,max-age=300")] // el catálogo conserva su caché
    [InlineData("/assets/app.js", null, null)]                            // fuera de la API no se toca
    public void SEG_10_LaApiNoSeGuardaEnCache_SalvoLoQueFijaLaSuya(string ruta, string? propia, string? esperado)
    {
        var contexto = new DefaultHttpContext();
        contexto.Request.Path = ruta;
        if (propia is not null) contexto.Response.Headers.CacheControl = propia;

        SinCacheEnApiExtensions.AplicarSinCache(contexto);

        Assert.Equal(esperado, contexto.Response.Headers.CacheControl.FirstOrDefault());
    }
}
