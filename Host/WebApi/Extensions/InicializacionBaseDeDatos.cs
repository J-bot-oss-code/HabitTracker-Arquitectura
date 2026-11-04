using AccessControl.Application.Interfaces;
using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Domain.Interfaces;
using AccessControl.Infrastructure.Persistence;
using DocumentManager.Infrastructure.Persistence;
using HabitTracker.Domain.Entities;
using HabitTracker.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Permissions.Infrastructure.Persistence;

namespace WebApi.Extensions;

/// <summary>
/// Deja la aplicación lista al arrancar, sin pasos manuales (Práctica 4): aplica las migraciones de los
/// cuatro módulos (la primera crea la base si no existe) y siembra los datos mínimos para poder probar.
/// Solo actúa si BaseDeDatos:MigrarAlIniciar es true, así que en desarrollo local no cambia nada.
///
/// Nota de diseño: es código de COMPOSICIÓN del Host. Por eso conoce a la vez AccessControl y HabitTracker,
/// algo que ningún módulo puede hacer (RD-03). Pensado para UNA sola réplica de la aplicación.
/// </summary>
public static class InicializacionBaseDeDatos
{
    /// <summary>
    /// Id fijo y público de la Meta de demostración. Crear un hábito exige un metaId y todavía no existe un
    /// endpoint que liste metas; con un Id conocido el README puede indicar qué enviar.
    /// </summary>
    public static readonly Guid MetaDemostracionId = new("5f1c0a00-0000-4000-8000-000000000002");

    private static readonly Guid CategoriaDemostracionId = new("5f1c0a00-0000-4000-8000-000000000001");

    public const int LongitudMinimaClaveAdministrador = 12;

    private const int IntentosMaximos = 12;
    private static readonly TimeSpan EsperaEntreIntentos = TimeSpan.FromSeconds(5);

    public static async Task InicializarBaseDeDatosAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("BaseDeDatos:MigrarAlIniciar"))
        {
            return;
        }

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("InicializacionBaseDeDatos");

        // Con Docker, SQL Server puede aceptar conexiones unos segundos después de arrancar el contenedor.
        await EjecutarConReintentosAsync(() => MigrarAsync(app), IntentosMaximos, EsperaEntreIntentos, logger);
        await SembrarMetaDemostracionAsync(app, logger);

        using var scope = app.Services.CreateScope();
        await SembrarAdministradorAsync(
            scope.ServiceProvider.GetRequiredService<IUsuarioRepository>(),
            scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
            app.Configuration["ADMIN_CORREO"],
            app.Configuration["ADMIN_CLAVE"],
            logger);
    }

    /// <summary>
    /// Reintenta SOLO si el fallo es de conexión (SQL Server aún arrancando). Cualquier otro error, como una
    /// migración rota o un permiso insuficiente, se relanza de inmediato: reintentarlo solo retrasaría el aviso.
    /// </summary>
    public static async Task EjecutarConReintentosAsync(Func<Task> operacion, int intentosMaximos, TimeSpan espera, ILogger logger)
    {
        for (var intento = 1; ; intento++)
        {
            try
            {
                await operacion();
                return;
            }
            catch (Exception ex) when (EsErrorTransitorio(ex) && intento < intentosMaximos)
            {
                logger.LogWarning(ex, "La base de datos aún no está lista (intento {Intento}/{Maximo}).", intento, intentosMaximos);
                await Task.Delay(espera);
            }
        }
    }

    public static bool EsErrorTransitorio(Exception ex) => ex switch
    {
        TimeoutException => true,
        // -2 timeout, 53 y 10061 servidor inalcanzable, 4060 base aún inexistente, 18456 login mientras arranca,
        // 233 y 258 conexión cerrada, 40613 base no disponible.
        SqlException sql => sql.Number is -2 or 53 or 10060 or 10061 or 4060 or 18456 or 233 or 258 or 40613,
        _ => false
    };

    private static async Task MigrarAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var sp = scope.ServiceProvider;

        // Orden fijo. Cada contexto lleva solo sus migraciones; el primero crea la base de datos.
        await sp.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<PermissionsDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<HabitTrackerDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<DocumentManagerDbContext>().Database.MigrateAsync();
    }

    private static async Task SembrarMetaDemostracionAsync(WebApplication app, ILogger logger)
    {
        using var scope = app.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<HabitTrackerDbContext>();

        if (await contexto.Metas.AnyAsync(m => m.Id == MetaDemostracionId))
        {
            return;
        }

        if (!await contexto.Categorias.AnyAsync(c => c.Id == CategoriaDemostracionId))
        {
            contexto.Categorias.Add(new Categoria
            {
                Id = CategoriaDemostracionId,
                Nombre = "Salud",
                Descripcion = "Categoría de demostración"
            });
        }

        contexto.Metas.Add(new Meta
        {
            Id = MetaDemostracionId,
            Titulo = "Meta de demostración",
            EstadoActual = "Activa",
            // Fecha lejana para que la meta de demostración no caduque con el tiempo.
            FechaLimite = new DateTime(2099, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            CategoriaId = CategoriaDemostracionId
        });

        await contexto.SaveChangesAsync();
        logger.LogInformation("Meta de demostración creada.");
    }

    /// <summary>
    /// Crea el Administrador inicial si las variables de entorno lo piden (RD-10) y todavía no existe.
    /// Falla de inmediato si la clave es débil o el correo no es válido: una cuenta con todos los permisos
    /// no debe nacer con una contraseña trivial. Nunca escribe la clave en el log.
    /// </summary>
    public static async Task SembrarAdministradorAsync(
        IUsuarioRepository repositorio,
        IPasswordHasher hasher,
        string? correo,
        string? clave,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrWhiteSpace(clave))
        {
            logger.LogInformation("ADMIN_CORREO / ADMIN_CLAVE no definidos: no se crea un Administrador inicial.");
            return;
        }

        if (clave.Length < LongitudMinimaClaveAdministrador)
        {
            throw new InvalidOperationException(
                $"ADMIN_CLAVE debe tener al menos {LongitudMinimaClaveAdministrador} caracteres.");
        }

        Email email;
        try
        {
            email = new Email(correo);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("ADMIN_CORREO no es un correo válido.");
        }

        if (await repositorio.CorreoExistsAsync(email))
        {
            return;
        }

        // La contraseña se guarda con hash (RD-05). Se usa el mismo flujo de activación del dominio
        // para que la cuenta nazca activa sin saltarse sus reglas.
        var administrador = new Usuario("Administrador", email, hasher.Hash(clave), Rol.Administrador);
        const string tokenInterno = "siembra-inicial";
        administrador.EstablecerTokenActivacion(tokenInterno, DateTime.UtcNow.AddMinutes(5));
        administrador.CompletarActivacion(tokenInterno);

        await repositorio.SaveAsync(administrador);
        logger.LogInformation("Administrador inicial creado: {Correo}.", email.Valor);
    }
}
