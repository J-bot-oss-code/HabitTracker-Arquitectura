using HabitTracker.Application.Interfaces;
using HabitTracker.Application.Services;
using HabitTracker.Infrastructure.Persistence;
using HabitTracker.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace WebApi.Extensions;

public static class HabitTrackerModuleExtensions
{
    public static IServiceCollection AddHabitTrackerModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AccessControl");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("No se encontró la cadena de conexión 'ConnectionStrings:AccessControl'.");
        }

        services.AddDbContext<HabitTrackerDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IHabitoRepository, HabitoRepository>();
        services.AddScoped<IMetaRepository, MetaRepository>();
        services.AddScoped<IUnitOfWork, HabitTrackerUnitOfWork>();
        services.AddScoped<HabitoService>();

        return services;
    }
}