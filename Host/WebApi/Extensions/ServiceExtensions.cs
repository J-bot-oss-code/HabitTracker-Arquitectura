using AccessControl.Application.Interfaces;
using AccessControl.Application.Service;
using AccessControl.Domain.Interfaces;
using AccessControl.Infrastructure.Persistence;
using AccessControl.Infrastructure.Repositories;
using AccessControl.Infrastructure.Security;
using AccessControl.Infrastructure.Services;
using AccessControl.Infrastructure.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        services.AddHostedService<EmailSenderWorker>();

        return services;
    }
}
