using System.Text.Json.Serialization;
using WebApi.Adapters;
using WebApi.Extensions;
using WebApi.Middlewares;
using DocumentManager.Infrastructure.DependencyInjection;
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

// Adaptador de IPermisoService para HabitTracker (conecta con Gestión de Permisos).
builder.Services.AddScoped<HabitTracker.Application.Interfaces.IPermisoService, PermisoServiceAdapter>();

// Límite de peticiones por IP en la activación de cuenta y su reenvío (RD-06). El código de activación tiene solo
// 6 dígitos: además del tope de 5 intentos por código, se frena a quien prueba códigos contra muchas cuentas.
var activacionesPorMinuto = builder.Configuration.GetValue<int?>("RateLimit:ActivacionPorMinuto") ?? 30;
builder.Services.AddRateLimiter(opciones =>
{
    opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opciones.AddPolicy("activacion", contexto =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = activacionesPorMinuto,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    opciones.OnRejected = async (contexto, cancelacion) =>
    {
        // Mismo formato de error que el resto de la API (RFC 7807) y sin detalles internos (RD-08).
        contexto.HttpContext.Response.ContentType = "application/problem+json";
        await contexto.HttpContext.Response.WriteAsync(
            "{\"title\":\"Demasiadas solicitudes.\",\"status\":429,\"detail\":\"Intente de nuevo en un minuto.\"}",
            cancelacion);
    };
});

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

app.UseRateLimiter();

app.UseAuthentication();
app.UseMiddleware<TokenBlacklistMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();
