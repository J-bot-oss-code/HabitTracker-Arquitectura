using AccessControl.Application.Interfaces;
using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Domain.Interfaces;
using AccessControl.Infrastructure.Persistence;
using Auditing.Infrastructure.Persistence;
using DocumentManager.Infrastructure.Persistence;
using Notifications.Infrastructure.Persistence;
using HabitTracker.Domain.Entities;
using HabitTracker.Infrastructure.Contenido;
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
    /// Id fijo y público de la Meta de demostración. Desde HabitTracker v2 la meta de un hábito es opcional; este Id
    /// conocido sigue sirviendo para los ejemplos del README y para probar un hábito con meta.
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
        if (VerificacionDelEntorno.DebeSembrarDemostracion(app.Configuration))
        {
            await SembrarMetaDemostracionAsync(app, logger);
        }

        using var scope = app.Services.CreateScope();
        await SembrarAdministradorAsync(
            scope.ServiceProvider.GetRequiredService<IUsuarioRepository>(),
            scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
            app.Configuration["ADMIN_CORREO"],
            app.Configuration["ADMIN_CLAVE"],
            logger);
    }

    /// <summary>Usuario de la base con el que entra la API en producción: solo lee y escribe datos, sin crear tablas ni administrar el servidor.</summary>
    public const string LoginDeLaApp = "habittracker_app";

    // Idempotente. CREATE/ALTER LOGIN no aceptan parámetros: la contraseña llega como @clave y QUOTENAME la entrecomilla en el
    // servidor (duplica las comillas), así que nunca se concatena texto del cliente en el SQL.
    private const string SqlLoginDeLaApp =
        "DECLARE @sql nvarchar(max); " +
        "IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'habittracker_app') " +
        "  SET @sql = N'CREATE LOGIN habittracker_app WITH PASSWORD = ' + QUOTENAME(@clave, '''') + N', CHECK_POLICY = ON'; " +
        "ELSE SET @sql = N'ALTER LOGIN habittracker_app WITH PASSWORD = ' + QUOTENAME(@clave, ''''); " +
        "EXEC (@sql); " +
        "IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'habittracker_app') " +
        "  CREATE USER habittracker_app FOR LOGIN habittracker_app; " +
        // Tras restaurar un respaldo en otro servidor el usuario queda huérfano (otro SID): se vuelve a enlazar con el login.
        "ELSE ALTER USER habittracker_app WITH LOGIN = habittracker_app; " +
        "ALTER ROLE db_datareader ADD MEMBER habittracker_app; " +
        "ALTER ROLE db_datawriter ADD MEMBER habittracker_app;";

    /// <summary>
    /// SEG-06: el paso «solo migrar» (con sa) deja creado el usuario de la API con permisos mínimos. Así, si la API se viera
    /// comprometida, no podría borrar tablas, leer otras bases ni ejecutar comandos del sistema (xp_cmdshell).
    /// </summary>
    public static async Task AsegurarLoginDeLaAppAsync(this WebApplication app)
    {
        var clave = ClaveDeLaApp(app.Configuration);
        using var scope = app.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await contexto.Database.ExecuteSqlRawAsync(SqlLoginDeLaApp, new SqlParameter("@clave", clave));
    }

    /// <summary>La contraseña del usuario de la API (DB_APP_PASSWORD): obligatoria y de hasta 128 caracteres (el límite de SQL Server).</summary>
    public static string ClaveDeLaApp(IConfiguration configuracion)
    {
        var clave = configuracion["DB_APP_PASSWORD"];
        if (string.IsNullOrWhiteSpace(clave) || clave.Length > 128)
        {
            throw new InvalidOperationException("Define DB_APP_PASSWORD (hasta 128 caracteres): es la contraseña con la que la API entra a la base, sin sa.");
        }
        return clave;
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

    /// <summary>
    /// Lleva el catálogo de planes embebido a la base (Fase 3). A diferencia de las migraciones, corre en cada arranque,
    /// también en desarrollo local, para que el catálogo siga al código. Un contenido inválido detiene el arranque.
    /// </summary>
    public static async Task SembrarCatalogoPlanesAsync(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("InicializacionBaseDeDatos");
        try
        {
            await EjecutarConReintentosAsync(async () =>
            {
                using var scope = app.Services.CreateScope();
                var resultado = await scope.ServiceProvider.GetRequiredService<SembradorCatalogoPlanes>().SembrarAsync();
                logger.LogInformation(
                    "Catálogo de planes: {Nuevos} nuevos, {Actualizados} actualizados y {Retirados} retirados.",
                    resultado.Nuevos, resultado.Actualizados, resultado.Retirados);
            }, IntentosMaximos, EsperaEntreIntentos, logger);
        }
        catch (Exception ex)
        {
            // El caso típico en local: falta aplicar la migración AgregarCatalogoPlanes (tabla inexistente).
            logger.LogCritical(ex,
                "No se pudo sembrar el catálogo de planes. Aplica las migraciones de HabitTrackerDbContext " +
                "(dotnet ef database update) o revisa catalogo-planes.json.");
            throw;
        }
    }

    private static async Task MigrarAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var sp = scope.ServiceProvider;

        // Orden fijo. Cada contexto lleva solo sus migraciones; el primero crea la base de datos.
        await sp.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<PermissionsDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<HabitTrackerDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<DocumentManagerDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<NotificationsDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<AuditingDbContext>().Database.MigrateAsync();
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

        // La misma política que cualquier contraseña (letras, números y el tope de BCrypt), no solo la longitud (SEG-15).
        try
        {
            AccessControl.Application.Service.PoliticaContrasena.SoloFormato.ValidarFormato(clave);
        }
        catch (AccessControl.Domain.Excepciones.ContrasenaNoValidaException)
        {
            throw new InvalidOperationException("ADMIN_CLAVE no cumple la política de contraseñas (letras y números, 72 bytes como mucho).");
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
        logger.LogInformation("Administrador inicial creado: {UsuarioId}.", administrador.Id);
    }
}
