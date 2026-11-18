using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Application.Interfaces;
using Notifications.Application.Services;
using Notifications.Infrastructure.Persistence;
using Notifications.Infrastructure.Repositories;

namespace Notifications.Infrastructure.DependencyInjection;

public static class NotificationsInfrastructureExtensions
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AccessControl");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "No se encontró la cadena de conexión 'ConnectionStrings:AccessControl'.");
        }

        services.AddDbContext<NotificationsDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<INotificacionRepository, NotificacionRepository>();
        services.AddScoped<IPreferenciaNotificacionesRepository, PreferenciaNotificacionesRepository>();
        services.AddScoped<INotificacionService, NotificacionService>();

        return services;
    }
}
