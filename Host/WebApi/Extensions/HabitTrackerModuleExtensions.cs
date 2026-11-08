using HabitTracker.Application.Interfaces;
using HabitTracker.Application.Services;
using HabitTracker.Infrastructure.DependencyInjection;
using HabitTracker.Infrastructure.Persistence;
using HabitTracker.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IHabitoService, HabitoService>();

        // Plan del día: usa la API externa de clima (la clave llega por la variable OPENWEATHER_API_KEY).
        services.AddScoped<IPlanDelDiaService, PlanDelDiaService>();
        services.AddServicioClimaOpenWeather(configuration);

        return services;
    }
}