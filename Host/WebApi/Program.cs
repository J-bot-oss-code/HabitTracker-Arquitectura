using WebApi.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Módulo de Control de Acceso (Clean Architecture).
builder.Services.AddAccessControlModule(builder.Configuration);

// Controladores y OpenAPI.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
