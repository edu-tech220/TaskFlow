using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Common;
using TaskFlow.Data;
using TaskFlow.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------- Servicios (contenedor de inyección de dependencias) ----------

builder.Services.AddControllers()
    // Enums como texto en JSON ("Completed" en lugar de 1). También acepta números al leer.
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Genera el documento OpenAPI nativo de .NET (Microsoft.AspNetCore.OpenApi).
builder.Services.AddOpenApi();

// ProblemDetails para todos los errores (RFC 9457) + manejador global de excepciones.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Opciones tipadas desde appsettings, validadas al arrancar.
builder.Services.AddOptions<ClockOptions>()
    .Bind(builder.Configuration.GetSection(ClockOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<RestDayOptions>()
    .Bind(builder.Configuration.GetSection(RestDayOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Reloj del sistema como singleton. Los servicios y el DbContext piden TimeProvider en vez
// de usar DateTime.UtcNow, así en las pruebas se sustituye por un reloj falso.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IAppClock, AppClock>();

// Registra TaskFlowDbContext como Scoped (una instancia por request).
// Si falta la cadena de conexión, falla al arrancar en lugar de fallar en la primera consulta.
var connectionString = builder.Configuration.GetConnectionString("TaskFlow")
    ?? throw new InvalidOperationException("Falta la cadena de conexión 'TaskFlow'.");

builder.Services.AddDbContext<TaskFlowDbContext>(options =>
    options.UseSqlServer(connectionString));

// Servicios de negocio: Scoped porque dependen del DbContext (también Scoped).
builder.Services.AddScoped<IActivityService, ActivityService>();

var app = builder.Build();

// ---------- Pipeline HTTP (middleware, en orden) ----------

// Primero: atrapa excepciones de todo lo que viene después y las convierte en ProblemDetails.
app.UseExceptionHandler();
// Respuestas vacías 4xx/5xx (p. ej. 404 de ruta inexistente) también como ProblemDetails.
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    // Expone el documento en /openapi/v1.json.
    app.MapOpenApi();

    // Swagger UI solo como visor de ese documento (en /swagger).
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/openapi/v1.json", "TaskFlow v1"));
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Hace visible la clase Program generada para WebApplicationFactory en las pruebas de integración.
public partial class Program;
