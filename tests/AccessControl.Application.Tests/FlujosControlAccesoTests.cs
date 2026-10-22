using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AccessControl.Application.DTOs;
using AccessControl.Application.Interfaces;
using AccessControl.Application.Service;
using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Domain.Interfaces;
using AccessControl.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using WebApi.Controllers;

namespace AccessControl.Application.Tests;

public class FlujosControlAccesoTests
{
    [Fact]
    public async Task RF_CA_01_02_RegistroYActivacion_CreaCuentaInactivaYLaActivaConToken()
    {
        // Arrange
        Usuario? usuarioGuardado = null;
        string? tokenEnviado = null;
        var repositorio = new Mock<IUsuarioRepository>();
        repositorio
            .Setup(r => r.CorreoExistsAsync(It.IsAny<Email>()))
            .ReturnsAsync(false);
        repositorio
            .Setup(r => r.SaveAsync(It.IsAny<Usuario>()))
            .Callback<Usuario>(usuario => usuarioGuardado = usuario)
            .Returns(Task.CompletedTask);
        repositorio
            .Setup(r => r.GetByTokenRecuperacionAsync(It.IsAny<string>()))
            .Returns(() => Task.FromResult(usuarioGuardado));
        repositorio
            .Setup(r => r.UpdateAsync(It.IsAny<Usuario>()))
            .Returns(Task.CompletedTask);

        var colaCorreo = new Mock<IEmailQueue>();
        colaCorreo
            .Setup(q => q.EncolarCorreoActivacionAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, token) => tokenEnviado = token)
            .Returns(Task.CompletedTask);

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash("ClaveSegura123")).Returns("hash-guardado");
        var servicio = CrearServicio(
            repositorio.Object,
            hasher: hasher.Object,
            emailQueue: colaCorreo.Object);

        // Act
        var respuesta = await servicio.RegistrarUsuarioAsync(
            new RegistroRequestDto("Persona de prueba", "persona@ejemplo.com", "ClaveSegura123"));

        // Assert
        Assert.NotNull(usuarioGuardado);
        Assert.False(usuarioGuardado.Activo);
        Assert.Equal("hash-guardado", usuarioGuardado.PasswordHash);
        Assert.False(string.IsNullOrWhiteSpace(tokenEnviado));
        Assert.Equal(tokenEnviado, usuarioGuardado.TokenAcceso?.Valor);
        Assert.False(respuesta.Activo);

        // Act
        await servicio.ActivarCuentaAsync(tokenEnviado!);

        // Assert
        Assert.True(usuarioGuardado.Activo);
        Assert.Null(usuarioGuardado.TokenAcceso);
        repositorio.Verify(r => r.UpdateAsync(usuarioGuardado), Times.Once);
    }

    [Fact]
    public async Task RF_CA_03_LoginExitoso_EmiteJwtConIdentidadRolYExpiracion()
    {
        // Arrange
        var usuario = new Usuario(
            "Persona de prueba",
            new Email("persona@ejemplo.com"),
            "hash-guardado",
            Rol.Estandar);
        usuario.Activar();

        var repositorio = new Mock<IUsuarioRepository>();
        repositorio
            .Setup(r => r.GetByCorreoAsync(It.IsAny<Email>()))
            .ReturnsAsync(usuario);
        repositorio
            .Setup(r => r.UpdateAsync(usuario))
            .Returns(Task.CompletedTask);

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.verificar("hash-guardado", "ClaveSegura123")).Returns(true);

        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Clave"] = "una-clave-de-prueba-de-al-menos-32-caracteres",
                ["JwtSettings:Emisor"] = "habit-tracker-tests",
                ["JwtSettings:Audiencia"] = "habit-tracker-client",
                ["JwtSettings:MinutosExpiracion"] = "20"
            })
            .Build();
        var proveedorJwt = new JwtProvider(configuracion, Mock.Of<ILogger<JwtProvider>>());
        var servicio = CrearServicio(
            repositorio.Object,
            hasher.Object,
            jwtProvider: proveedorJwt);

        // Act
        var respuesta = await servicio.AutenticarUsuarioAsync(
            new LoginRequestDto("persona@ejemplo.com", "ClaveSegura123"));

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(respuesta.Token));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(respuesta.Token);
        Assert.Equal(usuario.Id.ToString(), jwt.Subject);
        Assert.Equal("persona@ejemplo.com", jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(Rol.Estandar.ToString(), jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
        Assert.True(jwt.ValidTo > DateTime.UtcNow);
        Assert.True(jwt.ValidTo <= DateTime.UtcNow.AddMinutes(21));
    }

    [Fact]
    public async Task RF_CA_05_06_AccesoAdministrador_DeniegaEstandarYAutorizaAdministrador()
    {
        // Arrange
        var authorize = Assert.Single(
            typeof(AdminUsuariosController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>());
        Assert.Equal("Administrador", authorize.Roles);
        var policy = new AuthorizationPolicyBuilder()
            .RequireRole(authorize.Roles!.Split(','))
            .Build();
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddAuthorization()
            .BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var estandar = CrearPrincipalConRol("Estandar");
        var administrador = CrearPrincipalConRol("Administrador");

        // Act
        var resultadoEstandar = await authorization.AuthorizeAsync(estandar, resource: null, policy);
        var resultadoAdministrador = await authorization.AuthorizeAsync(administrador, resource: null, policy);

        // Assert
        Assert.False(resultadoEstandar.Succeeded);
        Assert.Contains(resultadoEstandar.Failure!.FailedRequirements, requisito => requisito is RolesAuthorizationRequirement);
        Assert.True(resultadoAdministrador.Succeeded);

        var authentication = new Mock<IAuthenticationService>();
        authentication
            .Setup(service => service.ForbidAsync(
                It.IsAny<HttpContext>(),
                It.IsAny<string?>(),
                It.IsAny<AuthenticationProperties?>()))
            .Callback<HttpContext, string?, AuthenticationProperties?>(
                (context, _, _) => context.Response.StatusCode = StatusCodes.Status403Forbidden)
            .Returns(Task.CompletedTask);
        using var httpProvider = new ServiceCollection()
            .AddLogging()
            .AddSingleton(authentication.Object)
            .BuildServiceProvider();
        var contextoHttp = new DefaultHttpContext
        {
            RequestServices = httpProvider,
            User = estandar
        };
        var middlewareHandler = new AuthorizationMiddlewareResultHandler();

        // Act
        await middlewareHandler.HandleAsync(
            _ => Task.CompletedTask,
            contextoHttp,
            policy,
            PolicyAuthorizationResult.Forbid(resultadoEstandar.Failure));

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, contextoHttp.Response.StatusCode);
    }

    [Fact]
    public async Task RF_CA_09_10_11_12_Recuperacion_RestableceClaveYRevocaSesiones()
    {
        // Arrange
        var usuario = new Usuario(
            "Persona de prueba",
            new Email("persona@ejemplo.com"),
            "hash-anterior");
        string? codigoEnviado = null;

        var repositorio = new Mock<IUsuarioRepository>();
        repositorio
            .Setup(r => r.GetByCorreoAsync(It.IsAny<Email>()))
            .ReturnsAsync(usuario);
        repositorio
            .Setup(r => r.UpdateAsync(usuario))
            .Returns(Task.CompletedTask);

        var colaCorreo = new Mock<IEmailQueue>();
        colaCorreo
            .Setup(q => q.EncolarCorreoRecuperacionAsync("persona@ejemplo.com", It.IsAny<string>()))
            .Callback<string, string>((_, codigo) => codigoEnviado = codigo)
            .Returns(Task.CompletedTask);

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash("ClaveNueva123")).Returns("hash-nuevo");
        var blacklist = new Mock<ITokenBlacklist>();
        var servicio = CrearServicio(
            repositorio.Object,
            hasher.Object,
            colaCorreo.Object,
            blacklist.Object);

        // Act
        await servicio.IniciarRecuperacionAsync("persona@ejemplo.com");

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(codigoEnviado));
        Assert.NotNull(usuario.Recuperacion);
        Assert.True(usuario.Recuperacion.EsValido(codigoEnviado!));

        // Act
        await servicio.RestablecerPasswordAsync(
            "persona@ejemplo.com",
            codigoEnviado!,
            "ClaveNueva123");

        // Assert
        Assert.Equal("hash-nuevo", usuario.PasswordHash);
        Assert.False(usuario.Recuperacion.EsValido(codigoEnviado!));
        blacklist.Verify(b => b.RevocarSesionesAnteriores(usuario.Id), Times.Once);
        repositorio.Verify(r => r.UpdateAsync(usuario), Times.Exactly(2));

        var accionRecuperacion = typeof(AuthController).GetMethod(nameof(AuthController.RecuperarPassword))!;
        var accionRestablecimiento = typeof(AuthController).GetMethod(nameof(AuthController.RestablecerPassword))!;
        Assert.Contains(accionRecuperacion.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpPostAttribute), true)
            .Cast<Microsoft.AspNetCore.Mvc.HttpPostAttribute>(), atributo => atributo.Template == "forgot-password");
        Assert.Contains(accionRestablecimiento.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpPostAttribute), true)
            .Cast<Microsoft.AspNetCore.Mvc.HttpPostAttribute>(), atributo => atributo.Template == "reset-password");
    }

    private static UsuarioService CrearServicio(
        IUsuarioRepository repositorio,
        IPasswordHasher? hasher = null,
        IEmailQueue? emailQueue = null,
        ITokenBlacklist? blacklist = null,
        IJwtProvider? jwtProvider = null)
    {
        return new UsuarioService(
            repositorio,
            hasher ?? Mock.Of<IPasswordHasher>(),
            emailQueue ?? Mock.Of<IEmailQueue>(),
            Mock.Of<IAuditoriaService>(),
            jwtProvider ?? Mock.Of<IJwtProvider>(),
            blacklist ?? Mock.Of<ITokenBlacklist>(),
            Mock.Of<ILogger<UsuarioService>>());
    }

    private static ClaimsPrincipal CrearPrincipalConRol(string rol) =>
        new(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Role, rol) },
            authenticationType: "tests"));
}
