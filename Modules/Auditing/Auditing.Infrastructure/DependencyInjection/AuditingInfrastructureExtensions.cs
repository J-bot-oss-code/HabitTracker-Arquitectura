using Auditing.Application.Interfaces;
using Auditing.Application.Services;
using Auditing.Infrastructure.Persistence;
using Auditing.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Auditing.Infrastructure.DependencyInjection;

public static class AuditingInfrastructureExtensions
{
    public static IServiceCollection AddAuditingModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AccessControl");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "No se encontró la cadena de conexión 'ConnectionStrings:AccessControl'.");
        }

        services.AddDbContext<AuditingDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IRegistroAuditoriaRepository, RegistroAuditoriaRepository>();
        services.AddScoped<IRegistroAuditoriaService, RegistroAuditoriaService>();

        return services;
    }
}
