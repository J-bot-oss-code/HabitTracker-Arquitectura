using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace AccessControl.Infrastructure.Persistence;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // RD-10: la cadena de conexión nunca se hardcodea. Se lee desde
        // Host/WebApi (appsettings.json + UserSecrets + variables de entorno).
        // En tiempo de diseño el cwd y el BaseDirectory varían según cómo se
        // invoque dotnet-ef, así que se busca Host/WebApi/appsettings.json
        // caminando hacia arriba desde ambas ubicaciones.
        var webApiPath = FindWebApiPath()
            ?? throw new InvalidOperationException("No se encontró Host/WebApi/appsettings.json.");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(webApiPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
            .AddUserSecrets<ApplicationDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("AccessControl");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "No se encontró la cadena de conexión 'ConnectionStrings:AccessControl'.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }

    private static string? FindWebApiPath()
    {
        var bases = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() };

        foreach (var b in bases)
        {
            var dir = new DirectoryInfo(Path.GetFullPath(b));

            for (var i = 0; i < 10 && dir != null; i++, dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "Host", "WebApi");
                if (File.Exists(Path.Combine(candidate, "appsettings.json")))
                {
                    return candidate;
                }

                if (dir.Name.Equals("WebApi", StringComparison.OrdinalIgnoreCase)
                    && File.Exists(Path.Combine(dir.FullName, "appsettings.json")))
                {
                    return dir.FullName;
                }
            }
        }

        return null;
    }
}
