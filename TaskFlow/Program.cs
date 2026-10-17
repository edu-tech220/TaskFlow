using Microsoft.EntityFrameworkCore;
using TaskFlow.Data;

var builder = WebApplication.CreateBuilder(args);

// ---------- Servicios (contenedor de inyección de dependencias) ----------

builder.Services.AddControllers();

// Genera el documento OpenAPI nativo de .NET (Microsoft.AspNetCore.OpenApi).
builder.Services.AddOpenApi();

// Reloj del sistema como singleton. Los servicios y el DbContext piden TimeProvider en vez
// de usar DateTime.UtcNow, así en las pruebas se sustituye por un reloj falso.
builder.Services.AddSingleton(TimeProvider.System);

// Registra TaskFlowDbContext como Scoped (una instancia por request).
// Si falta la cadena de conexión, falla al arrancar en lugar de fallar en la primera consulta.
var connectionString = builder.Configuration.GetConnectionString("TaskFlow")
    ?? throw new InvalidOperationException("Falta la cadena de conexión 'TaskFlow'.");

builder.Services.AddDbContext<TaskFlowDbContext>(options =>
    options.UseSqlServer(connectionString));

var app = builder.Build();

// ---------- Pipeline HTTP (middleware, en orden) ----------

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
