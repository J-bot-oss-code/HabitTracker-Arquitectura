using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Permissions.Application.Interfaces;
using Permissions.Infrastructure.Persistence;
using Permissions.Infrastructure.Repositories;

namespace Permissions.Infrastructure.DependencyInjection;

public static class PermissionsInfrastructureExtensions
{
    public static IServiceCollection AddPermissionsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AccessControl");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "No se encontró la cadena de conexión 'ConnectionStrings:AccessControl'.");
        }

        services.AddDbContext<PermissionsDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<ISolicitudPermisoRepository, SolicitudPermisoRepository>();

        return services;
    }
}
