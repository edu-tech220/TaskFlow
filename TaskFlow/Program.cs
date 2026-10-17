using System.Text.Json.Serialization;
using TaskFlow.Common;

var builder = WebApplication.CreateBuilder(args);

// No anunciar "Server: Kestrel" en las respuestas.
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

// ---------- Servicios (contenedor de inyección de dependencias) ----------

builder.Services.AddControllers()
    // Enums como texto en JSON ("Completed" en lugar de 1). También acepta números al leer.
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Genera el documento OpenAPI nativo de .NET (Microsoft.AspNetCore.OpenApi).
builder.Services.AddOpenApi();

// ProblemDetails para todos los errores (RFC 9457) + manejador global de excepciones.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Opciones, reloj, DbContext y servicios de negocio (Common/ServiceCollectionExtensions.cs).
builder.Services.AddTaskFlowCore(builder.Configuration);
builder.Services.AddTaskFlowCors(builder.Configuration);
builder.Services.AddTaskFlowRateLimiting(builder.Configuration);

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
else
{
    // HSTS: el navegador recordará usar solo HTTPS con este host. No en desarrollo para
    // no "envenenar" localhost.
    app.UseHsts();
}

app.UseSecurityHeaders();
app.UseHttpsRedirection();
app.UseCors(CorsOptions.PolicyName);   // antes de los endpoints para responder preflight OPTIONS
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Hace visible la clase Program generada para WebApplicationFactory en las pruebas de integración.
public partial class Program;
