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
