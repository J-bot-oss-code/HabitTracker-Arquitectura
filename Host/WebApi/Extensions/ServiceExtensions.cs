using System.Text;
using AccessControl.Application.Interfaces;
using AccessControl.Application.Service;
using AccessControl.Domain.Interfaces;
using AccessControl.Infrastructure.Persistence;
using AccessControl.Infrastructure.Repositories;
using AccessControl.Infrastructure.Security;
using AccessControl.Infrastructure.Services;
using AccessControl.Infrastructure.Workers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace WebApi.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddAccessControlModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AccessControl");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("No se encontró la cadena de conexión 'ConnectionStrings:AccessControl'.");
        }

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<IUsuarioService, UsuarioService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IEmailQueue, DatabaseEmailQueue>();
        services.AddScoped<IAuditoriaService, AuditoriaService>();
        services.AddScoped<IJwtProvider, JwtProvider>();
        services.AddScoped<ITokenBlacklist, TokenBlacklistService>();

        // Sesiones: token de acceso corto + refresh token rotativo en cookie HttpOnly. Una configuración inválida detiene el arranque.
        var opcionesSesion = new OpcionesSesion();
        configuration.GetSection(OpcionesSesion.Seccion).Bind(opcionesSesion);
        opcionesSesion.Validar();
        services.AddSingleton(opcionesSesion);
        services.AddScoped<ISesionRepository, SesionRepository>();
        services.AddScoped<IGestorSesiones, GestorSesiones>();

        // Inicio de sesión con Google: el navegador obtiene un ID token y la API lo valida (sin secreto de cliente ni
        // redirecciones). Sin Google:ClientId los endpoints responden 503 y el resto de la API sigue funcionando.
        services.AddScoped<ILoginExternoRepository, LoginExternoRepository>();
        services.AddScoped<IValidadorIdTokenGoogle, ValidadorIdTokenGoogle>();
        services.AddScoped<IServicioLoginExterno, ServicioLoginExterno>();

        // Baja de cuenta (periodo de gracia, desactivación) y descarga de datos. Cada módulo de negocio participa con un
        // adaptador del Host (IParticipanteDatosUsuario); Control de Acceso no los conoce (RD-03).
        var opcionesPrivacidad = new OpcionesPrivacidad();
        configuration.GetSection(OpcionesPrivacidad.Seccion).Bind(opcionesPrivacidad);
        opcionesPrivacidad.Validar();
        services.AddSingleton(opcionesPrivacidad);
        services.AddScoped<IServicioPrivacidad, ServicioPrivacidad>();
        services.AddScoped<IServicioBajaCuentas, ServicioBajaCuentas>();
        services.AddHostedService<BajaCuentasWorker>();

        // Consentimiento legal versionado (Ley 172-13 / GDPR). Las versiones vigentes salen de Legal:*; una versión vacía
        // detiene el arranque.
        services.AddSingleton<IVersionesLegales>(new VersionesLegalesConfiguracion(configuration));
        services.AddScoped<IConsentimientoRepository, ConsentimientoRepository>();
        services.AddScoped<IConsentimientoService, ConsentimientoService>();

        // Política de contraseñas (RF-CA-14): 12+ caracteres con letra y número, y sin aparecer en filtraciones conocidas.
        // La consulta a Pwned Passwords usa k-anonimato (solo viaja un prefijo de 5 caracteres del hash) y, si la API
        // falla o tarda más de 3 s, la contraseña se permite. Se puede apagar con Seguridad:ContrasenasFiltradas:Habilitado=false.
        services.AddScoped<IPoliticaContrasena, PoliticaContrasena>();
        if (configuration.GetValue("Seguridad:ContrasenasFiltradas:Habilitado", true))
        {
            var urlBase = configuration["Seguridad:ContrasenasFiltradas:UrlBase"] ?? "https://api.pwnedpasswords.com/";
            services.AddHttpClient<IVerificadorContrasenasFiltradas, VerificadorHaveIBeenPwned>(cliente =>
            {
                cliente.BaseAddress = new Uri(urlBase.EndsWith('/') ? urlBase : urlBase + "/");
                cliente.Timeout = TimeSpan.FromSeconds(3);
                cliente.DefaultRequestHeaders.Add("Add-Padding", "true");
                cliente.DefaultRequestHeaders.UserAgent.ParseAdd("HabitTrackerNexus");
            });
        }
        else
        {
            services.AddSingleton<IVerificadorContrasenasFiltradas, VerificadorDesactivado>();
        }

        // Misma validación que usa el emisor de tokens: si la clave falta o es corta, la aplicación no arranca.
        var jwt = new JwtSettings();
        configuration.GetSection(JwtSettings.Seccion).Bind(jwt);
        jwt.Validar();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Emisor,
                    ValidAudience = jwt.Audiencia,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Clave)),
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorization();

        services.AddHostedService<EmailSenderWorker>();

        return services;
    }
}
