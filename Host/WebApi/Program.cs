using System.Text.Json.Serialization;
using WebApi.Adapters;
using WebApi.Extensions;
using WebApi.Middlewares;
using Auditing.Infrastructure.DependencyInjection;
using DocumentManager.Infrastructure.DependencyInjection;
using Notifications.Infrastructure.DependencyInjection;
using Permissions.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Módulo de Control de Acceso (Clean Architecture).
builder.Services.AddAccessControlModule(builder.Configuration);

// Módulo de Negocio: Rastreador de Hábitos (contexto propio).
builder.Services.AddHabitTrackerModule(builder.Configuration);

// Módulo de Gestión de Permisos (persistencia propia).
builder.Services.AddPermissionsInfrastructure(builder.Configuration);

// Módulo Manejador de Documentos (Pieza 3 del Core; persistencia y almacenamiento propios).
builder.Services.AddDocumentManagerModule(builder.Configuration);

// Módulo de Notificaciones (Pieza 4 del Core): bandeja interna y correo por la cola de Control de Acceso.
builder.Services.AddNotificationsModule(builder.Configuration);

// Módulo de Auditoría (Pieza 6 del Core): historial inmutable en su propia tabla; los demás módulos le escriben por puertos.
builder.Services.AddAuditingModule(builder.Configuration);

// Control de Acceso registra sus eventos (rol, estado, contraseña) por su puerto; este adaptador los guarda en el historial.
builder.Services.AddScoped<AccessControl.Application.Interfaces.IAuditoriaService, AuditoriaControlAccesoAdapter>();

// Notificaciones se conecta a Control de Acceso (usuarios y cola de correos) solo por puertos y estos adaptadores.
builder.Services.AddScoped<Notifications.Application.Interfaces.IDirectorioUsuarios, DirectorioUsuariosAdapter>();
builder.Services.AddScoped<Notifications.Application.Interfaces.IColaCorreos, ColaCorreosAdapter>();

// Los avisos de Permisos (solicitud creada y resuelta) llegan al motor de Notificaciones por este adaptador.
builder.Services.AddScoped<Permissions.Application.Interfaces.INotificadorPermisos, NotificadorPermisosAdapter>();

// Los logros que gana la persona en HabitTracker se le avisan por el mismo motor.
builder.Services.AddScoped<HabitTracker.Application.Interfaces.INotificadorUsuario, NotificadorUsuarioAdapter>();

// Reportes (Pieza 5 del Core) no tiene base de datos propia: agrega lo que le dan Permisos y HabitTracker por estos adaptadores.
builder.Services.AddScoped<Reports.Application.Interfaces.IFuenteSolicitudes, FuenteSolicitudesAdapter>();
builder.Services.AddScoped<Reports.Application.Interfaces.IFuenteConstancia, FuenteConstanciaAdapter>();
builder.Services.AddScoped<Reports.Application.Interfaces.IReportesService, Reports.Application.Services.ReportesService>();

// Adaptador de IPermisoService para HabitTracker (conecta con Gestión de Permisos).
builder.Services.AddScoped<HabitTracker.Application.Interfaces.IPermisoService, PermisoServiceAdapter>();

// Adaptador de IConsentimientoSalud para HabitTracker (consentimiento datosSalud de Control de Acceso).
builder.Services.AddScoped<HabitTracker.Application.Interfaces.IConsentimientoSalud, ConsentimientoSaludAdapter>();
// Archivos vinculados a planes: HabitTracker ve los documentos por un puerto y el Manejador de documentos consulta una guarda al eliminar.
builder.Services.AddScoped<HabitTracker.Application.Interfaces.IDocumentosUsuario, DocumentosUsuarioAdapter>();
builder.Services.AddScoped<DocumentManager.Application.Interfaces.IGuardaEliminacionDocumento, GuardaEliminacionDocumentoAdapter>();

// Cifrado en reposo de los datos de salud: claves persistentes en DATAPROTECTION_KEYS_PATH (obligatorio fuera de Development).
builder.Services.AddProteccionDeDatos(builder.Configuration, builder.Environment);

// Datos de cada módulo en la descarga de datos y en la baja de cuenta de Control de Acceso (sin que éste los conozca).
builder.Services.AddScoped<AccessControl.Application.Interfaces.IParticipanteDatosUsuario, ParticipanteHabitos>();
builder.Services.AddScoped<AccessControl.Application.Interfaces.IParticipanteDatosUsuario, ParticipantePerfilSalud>();
builder.Services.AddScoped<AccessControl.Application.Interfaces.IParticipanteDatosUsuario, ParticipantePlanes>();
builder.Services.AddScoped<AccessControl.Application.Interfaces.IParticipanteDatosUsuario, ParticipantePermisos>();
builder.Services.AddScoped<AccessControl.Application.Interfaces.IParticipanteDatosUsuario, ParticipanteDocumentos>();
builder.Services.AddScoped<AccessControl.Application.Interfaces.IParticipanteDatosUsuario, ParticipanteNotificaciones>();

// Límite de peticiones por IP en activación, login, registro y recuperación de contraseña (RD-06).
builder.Services.AddLimitacionDePeticiones(builder.Configuration);
builder.Services.AddReenvioDeCabeceras(builder.Configuration);
builder.Services.AddSaludDelServicio();

// CORS: solo los orígenes de Cors:OrigenesPermitidos (el frontend). Sin orígenes configurados no se permite ninguno.
builder.Services.AddCorsDelFrontend(builder.Configuration);

// Controladores y OpenAPI. Los enums viajan como texto ("Diaria", "Activo") y se aceptan también como número.
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Description = "Escribe 'Bearer' [espacio] y luego tu token JWT válido.\r\n\r\nEjemplo: \"Bearer eyJhbGciOiJIUzI1Ni...\""
    });

    c.AddSecurityRequirement(document => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        { new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document), new List<string>() }
    });
});

var app = builder.Build();

// Migraciones y datos semilla (solo si BaseDeDatos:MigrarAlIniciar=true; la composición de Docker lo activa).
await app.InicializarBaseDeDatosAsync();

// Catálogo de planes de HabitTracker (siempre: es contenido del código, no datos de demostración).
await app.SembrarCatalogoPlanesAsync();

// Middleware global de excepciones al inicio del pipeline (RD-07, RD-08).
app.UseMiddleware<GlobalExceptionMiddleware>();

// Configure the HTTP request pipeline.
// Swagger se muestra en Development y también cuando Swagger:Habilitado=true (el contenedor de la Práctica 4).
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Habilitado"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// El contenedor solo escucha HTTP (puerto 8080): redirigir a HTTPS sin un puerto HTTPS configurado
// no llevaría a ninguna parte. Fuera de Docker se mantiene como antes.
if (!app.Configuration.GetValue<bool>("Https:RedireccionDesactivada"))
{
    app.UseHttpsRedirection();
}

// Antes que todo lo que lee la IP del cliente (el limitador de peticiones); sin proxies configurados no hace nada.
app.UseReenvioDeCabeceras();

app.UseCorsDelFrontend(app.Configuration);

// Después de autenticar, para limitar por persona los endpoints con sesión; antes de la lista negra, para que un
// token inválido repetido no consulte la base sin tope.
app.UseAuthentication();
app.UseRateLimiter();
app.UseMiddleware<TokenBlacklistMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapSaludDelServicio();

app.Run();
