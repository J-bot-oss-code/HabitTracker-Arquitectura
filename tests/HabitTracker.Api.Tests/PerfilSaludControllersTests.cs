using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using HabitTracker.Application.DTOs;
using HabitTracker.Application.Services;
using HabitTracker.Domain.Excepciones;
using HabitTracker.Domain.Perfil;
using HabitTracker.Domain.Salud;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using WebApi.Extensions;
using WebApi.Controllers;
using WebApi.Middlewares;

namespace HabitTracker.Api.Tests;

/// <summary>Controllers de perfil, salud y progreso corporal sin levantar la API (RD-12).</summary>
public class PerfilSaludControllersTests
{
    private static readonly Guid UsuarioId = Guid.NewGuid();

    [Theory]
    [InlineData(typeof(ProfileController))]
    [InlineData(typeof(ProgressController))]
    [Trait("Requirement", "RF-NEG-06")]
    [Trait("Requirement", "RD-06")]
    public void RF_NEG_06_ControllersDePerfilYProgreso_ExigenRolEstandar(Type controller)
    {
        var authorize = controller.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal("Estandar", authorize.Roles);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_02_Perfil_ElNombreYLaIdentidadSalenDelToken()
    {
        var perfiles = new PerfilesFalsos();
        var controlador = new ProfileController(perfiles, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Usuario() } },
        };

        var resultado = await controlador.Obtener(CancellationToken.None);

        var dto = Assert.IsType<PerfilDto>(Assert.IsType<OkObjectResult>(resultado).Value);
        Assert.Equal("Ana Pérez", dto.Nombre);
        Assert.Equal(UsuarioId, perfiles.UltimoUsuario);
    }

    [Theory]
    [InlineData("{}", false, null)]
    [InlineData("{\"objetivoKg\":null}", true, null)]
    [InlineData("{\"objetivoKg\":65.5}", true, 65.5)]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Peso_ObjetivoOmitidoNullONumero_SeDistinguen(string json, bool cambiar, double? objetivo)
    {
        var request = JsonSerializer.Deserialize<RegistrarPesoRequest>(
            json.Replace("{", "{\"fecha\":\"2026-11-08T12:00:00Z\",\"pesoKg\":70,").Replace(",}", "}"), Opciones())!;

        var (cambia, valor) = ProgressController.LeerObjetivo(request.ObjetivoKg);

        Assert.Equal(cambiar, cambia);
        Assert.Equal((decimal?)objetivo, valor);
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_Peso_ObjetivoQueNoEsNumero_Rechaza()
    {
        var request = JsonSerializer.Deserialize<RegistrarPesoRequest>(
            "{\"fecha\":\"2026-11-08T12:00:00Z\",\"pesoKg\":70,\"objetivoKg\":\"mucho\"}", Opciones())!;

        Assert.Throws<ArgumentException>(() => ProgressController.LeerObjetivo(request.ObjetivoKg));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Perfil_ElJsonDelFrontendSeLee_HorasYEnumsComoTexto()
    {
        // Lo que envía un input type=time ("06:30") y los enums como texto, igual que configura Program.cs.
        var request = JsonSerializer.Deserialize<GuardarPerfilRequest>(
            "{\"perfilLaboral\":\"TurnoNocturno\",\"zonaHoraria\":\"America/Santo_Domingo\",\"avisoSaludAceptado\":true," +
            "\"horaDespertar\":\"06:30\",\"sexo\":\"Mujer\",\"actividad\":\"MuyAlta\"}", Opciones())!;

        Assert.Equal(PerfilLaboral.TurnoNocturno, request.PerfilLaboral);
        Assert.Equal(new TimeOnly(6, 30), request.HoraDespertar);
        Assert.Equal(Sexo.Mujer, request.Sexo);
        Assert.Equal(NivelActividad.MuyAlta, request.Actividad);
    }

    [Theory]
    [InlineData(typeof(GuardarPerfilRequest))]
    [InlineData(typeof(PerfilDto))]
    [InlineData(typeof(DatosPerfil))]
    [InlineData(typeof(PerfilUsuario))]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Perfil_ElPlanDeInteresSalioDelContrato(Type tipo)
    {
        // Desde la Fase 4b los planes que sigue la persona viven en PlanUsuario (api/my-plans), no en el perfil.
        var nombres = tipo.GetProperties().Select(p => p.Name).ToArray();

        Assert.DoesNotContain("MetaSlug", nombres);
        Assert.DoesNotContain("InicioPlan", nombres);
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_Perfil_UnClienteAntiguoQueEnviaMetaSlug_SeLeeSinFallar()
    {
        var request = JsonSerializer.Deserialize<GuardarPerfilRequest>(
            "{\"perfilLaboral\":\"Remoto\",\"zonaHoraria\":\"America/Santo_Domingo\",\"avisoSaludAceptado\":true," +
            "\"metaSlug\":\"dormir-mejor\"}", Opciones())!;

        Assert.Equal(PerfilLaboral.Remoto, request.PerfilLaboral);
    }

    [Fact]
    [Trait("Requirement", "RD-08")]
    public async Task RD_08_SinConsentimientoDeSalud_Responde409ConCodigo()
    {
        var (status, cuerpo) = await EjecutarAsync(new ConsentimientoSaludRequeridoException());

        Assert.Equal(409, status);
        Assert.Equal("consentimiento_salud_requerido", cuerpo.GetProperty("codigo").GetString());
    }

    [Fact]
    [Trait("Requirement", "RD-08")]
    public async Task RD_08_PerfilNoEncontrado_Responde404ConCodigo()
    {
        var (status, cuerpo) = await EjecutarAsync(
            new RecursoNoEncontradoException(RecursoNoEncontradoException.PerfilNoEncontrado, "Aún no completaste tu perfil."));

        // RequierePerfil del frontend lleva al onboarding solo con un 404 que trae ProblemDetails.
        Assert.Equal(404, status);
        Assert.Equal("perfil_no_encontrado", cuerpo.GetProperty("codigo").GetString());
    }

    [Theory]
    [InlineData("1e20")]
    [InlineData("300.4")]
    [InlineData("29.99")]
    [Trait("Requirement", "RD-07")]
    public void RD_07_Peso_FueraDeRangoOEnorme_EsUnRechazoDeValidacionYNoUnaExcepcion(string peso)
    {
        // MVC valida los atributos de los parámetros del record. Con [Range(int, int)] sobre un decimal, 1e20 lanzaba
        // OverflowException (500) y 300.4 pasaba por redondeo.
        var valor = decimal.Parse(peso, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
        var rangos = new[]
        {
            RangoDe(typeof(RegistrarPesoRequest), nameof(RegistrarPesoRequest.PesoKg)),
            RangoDe(typeof(GuardarPerfilRequest), nameof(GuardarPerfilRequest.PesoKg)),
        };

        foreach (var rango in rangos)
        {
            var excepcion = Record.Exception(() => rango.IsValid(valor));
            Assert.Null(excepcion);
            Assert.False(rango.IsValid(valor));
        }
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_Peso_DentroDeRango_EsValido()
    {
        Assert.True(RangoDe(typeof(RegistrarPesoRequest), nameof(RegistrarPesoRequest.PesoKg)).IsValid(72.35m));
    }

    private static RangeAttribute RangoDe(Type request, string parametro) =>
        request.GetConstructors().Single().GetParameters().Single(p => p.Name == parametro).GetCustomAttribute<RangeAttribute>()!;

    [Fact]
    [Trait("Requirement", "RD-09")]
    public void RD_09_ProteccionDeDatos_FueraDeDevelopmentSinRutaDeClaves_NoArranca()
    {
        var configuracion = new ConfigurationBuilder().Build();

        var excepcion = Record.Exception(() =>
            new ServiceCollection().AddProteccionDeDatos(configuracion, Entorno(Environments.Production)));

        Assert.IsType<InvalidOperationException>(excepcion);
        Assert.Contains(ProteccionDeDatosExtensions.VariableRutaClaves, excepcion.Message);
    }

    [Fact]
    [Trait("Requirement", "RD-09")]
    public void RD_09_ProteccionDeDatos_ConRuta_GuardaLasClavesAhiYNoLasPierdeAlReiniciar()
    {
        // Arrange
        var ruta = Path.Combine(Path.GetTempPath(), "ht-claves-" + Guid.NewGuid().ToString("N"));
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [ProteccionDeDatosExtensions.VariableRutaClaves] = ruta })
            .Build();
        try
        {
            string cifrado;
            using (var primera = new ServiceCollection().AddProteccionDeDatos(configuracion, Entorno(Environments.Production)).BuildServiceProvider())
                cifrado = primera.GetRequiredService<IDataProtectionProvider>().CreateProtector("prueba").Protect("dato");

            // Act: "reinicio" de la aplicación con la misma ruta.
            using var segunda = new ServiceCollection().AddProteccionDeDatos(configuracion, Entorno(Environments.Production)).BuildServiceProvider();
            var descifrado = segunda.GetRequiredService<IDataProtectionProvider>().CreateProtector("prueba").Unprotect(cifrado);

            // Assert
            Assert.Equal("dato", descifrado);
            Assert.NotEmpty(Directory.GetFiles(ruta, "*.xml"));
        }
        finally
        {
            if (Directory.Exists(ruta))
                Directory.Delete(ruta, recursive: true);
        }
    }

    private static IHostEnvironment Entorno(string nombre) => new EntornoFalso { EnvironmentName = nombre };

    private sealed class EntornoFalso : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "HabitTrackerNexus";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static JsonSerializerOptions Opciones() =>
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static ClaimsPrincipal Usuario() => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, UsuarioId.ToString()),
        new Claim(ClaimTypes.Role, "Estandar"),
        new Claim("name", "Ana Pérez"),
    ], "test"));

    private static async Task<(int Status, JsonElement Cuerpo)> EjecutarAsync(Exception excepcion)
    {
        var contexto = new DefaultHttpContext();
        contexto.Response.Body = new MemoryStream();
        await new GlobalExceptionMiddleware(_ => throw excepcion, NullLogger<GlobalExceptionMiddleware>.Instance).InvokeAsync(contexto);
        contexto.Response.Body.Position = 0;
        var cuerpo = await JsonDocument.ParseAsync(contexto.Response.Body);
        return (contexto.Response.StatusCode, cuerpo.RootElement.Clone());
    }

    private sealed class PerfilesFalsos : IPerfilService
    {
        public Guid? UltimoUsuario { get; private set; }

        public Task<PerfilDto> ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken = default)
        {
            UltimoUsuario = usuarioId;
            var perfil = PerfilUsuario.Crear(usuarioId, new DatosPerfil(PerfilLaboral.Remoto, "America/Santo_Domingo", true), DateTime.UtcNow);
            return Task.FromResult(PerfilDto.Desde(perfil));
        }

        public Task<PerfilDto> GuardarAsync(Guid usuarioId, DatosPerfil datos, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
