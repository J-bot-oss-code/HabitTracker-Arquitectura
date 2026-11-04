using DocumentManager.Application.Interfaces;
using DocumentManager.Application.Services;
using DocumentManager.Infrastructure.Persistence;
using DocumentManager.Infrastructure.Repositories;
using DocumentManager.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentManager.Infrastructure.DependencyInjection;

public static class DocumentManagerInfrastructureExtensions
{
    public static IServiceCollection AddDocumentManagerModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AccessControl");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "No se encontró la cadena de conexión 'ConnectionStrings:AccessControl'.");
        }

        // En Docker se sobrescribe con la ruta del volumen (variable Documentos__RutaAlmacenamiento).
        // El valor por defecto queda FUERA de bin/: un clean o un publish no debe borrar los archivos
        // mientras la base de datos conserva sus metadatos (RD-09).
        var rutaAlmacenamiento = configuration["Documentos:RutaAlmacenamiento"];
        if (string.IsNullOrWhiteSpace(rutaAlmacenamiento))
        {
            rutaAlmacenamiento = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HabitTracker",
                "documentos");
        }

        services.AddDbContext<DocumentManagerDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IDocumentoRepository, DocumentoRepository>();
        services.AddSingleton<IAlmacenamientoArchivos>(_ => new AlmacenamientoArchivosLocal(rutaAlmacenamiento));
        services.AddScoped<IDocumentoService, DocumentoService>();

        return services;
    }
}
