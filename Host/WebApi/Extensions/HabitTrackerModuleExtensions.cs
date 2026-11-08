using HabitTracker.Application.Interfaces;
using HabitTracker.Application.Services;
using HabitTracker.Infrastructure.DependencyInjection;
using HabitTracker.Infrastructure.Persistence;
using HabitTracker.Infrastructure.Repositories;
using HabitTracker.Infrastructure.Seguridad;
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

        // Perfil, cuestionario de salud (cifrado con Data Protection) y progreso corporal (Fase 2 de HabitTracker v2).
        // El consentimiento lo resuelve un adaptador del Host (IConsentimientoSalud), registrado en Program.cs.
        services.AddScoped<ICifradorDatos, CifradorDataProtection>();
        services.AddScoped<IPerfilUsuarioRepository, PerfilUsuarioRepository>();
        services.AddScoped<ICuestionarioSaludRepository, CuestionarioSaludRepository>();
        services.AddScoped<IProgresoCorporalRepository, ProgresoCorporalRepository>();
        services.AddScoped<IPerfilService, PerfilService>();
        services.AddScoped<ICuestionarioSaludService, CuestionarioSaludService>();
        services.AddScoped<IProgresoCorporalService, ProgresoCorporalService>();
        services.AddScoped<IDatosPerfilSaludService, DatosPerfilSaludService>();

        // Plan del día: usa la API externa de clima (la clave llega por la variable OPENWEATHER_API_KEY).
        services.AddScoped<IPlanDelDiaService, PlanDelDiaService>();
        services.AddServicioClimaOpenWeather(configuration);

        return services;
    }
}