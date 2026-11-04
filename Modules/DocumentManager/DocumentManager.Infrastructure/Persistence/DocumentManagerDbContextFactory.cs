using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DocumentManager.Infrastructure.Persistence;

/// <summary>Solo se usa en tiempo de diseño (dotnet ef migrations / database update).</summary>
public sealed class DocumentManagerDbContextFactory : IDesignTimeDbContextFactory<DocumentManagerDbContext>
{
    public DocumentManagerDbContext CreateDbContext(string[] args)
    {
        var webApiPath = FindWebApiPath()
            ?? throw new InvalidOperationException("No se encontró la configuración del proyecto Host/WebApi.");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(webApiPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("AccessControl");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "No se encontró la cadena de conexión 'ConnectionStrings:AccessControl'.");
        }

        var options = new DbContextOptionsBuilder<DocumentManagerDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new DocumentManagerDbContext(options);
    }

    private static string? FindWebApiPath()
    {
        var bases = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() };

        foreach (var basePath in bases)
        {
            var directory = new DirectoryInfo(Path.GetFullPath(basePath));
            for (var depth = 0; depth < 10 && directory is not null; depth++, directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "Host", "WebApi");
                if (File.Exists(Path.Combine(candidate, "appsettings.json")))
                {
                    return candidate;
                }

                if (directory.Name.Equals("WebApi", StringComparison.OrdinalIgnoreCase)
                    && File.Exists(Path.Combine(directory.FullName, "appsettings.json")))
                {
                    return directory.FullName;
                }
            }
        }

        return null;
    }
}
