using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data;
using TaskFlow.Services;

namespace TaskFlow.Common;

// Métodos de extensión para que Program.cs se lea como un índice y cada bloque de
// configuración viva junto a su explicación.
public static class ServiceCollectionExtensions
{
    // Núcleo de la aplicación: opciones, reloj, DbContext y servicios de negocio.
    public static IServiceCollection AddTaskFlowCore(this IServiceCollection services, IConfiguration configuration)
    {
        // Opciones tipadas desde appsettings, validadas al arrancar.
        services.AddOptions<ClockOptions>()
            .Bind(configuration.GetSection(ClockOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<RestDayOptions>()
            .Bind(configuration.GetSection(RestDayOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Reloj del sistema como singleton: servicios y DbContext piden TimeProvider en vez
        // de DateTime.UtcNow, así en las pruebas se sustituye por un reloj controlable.
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAppClock, AppClock>();

        // DbContext Scoped (una instancia por request). Si falta la cadena de conexión,
        // falla al arrancar en lugar de fallar en la primera consulta.
        var connectionString = configuration.GetConnectionString("TaskFlow")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'TaskFlow'.");
        services.AddDbContext<TaskFlowDbContext>(options => options.UseSqlServer(connectionString));

        // Servicios de negocio: Scoped porque dependen del DbContext (también Scoped).
        services.AddScoped<IActivityService, ActivityService>();
        services.AddScoped<IRestDayService, RestDayService>();
        services.AddScoped<IDailyRecordService, DailyRecordService>();
        services.AddScoped<ICalendarService, CalendarService>();

        return services;
    }

    // CORS: el navegador bloquea llamadas de un origen (esquema+host+puerto) distinto al de la
    // API salvo que la API lo permita con encabezados Access-Control-*. Solo aplica a frontends
    // que llaman desde el navegador (SPA); un frontend server-side (MVC) no lo necesita.
    public static IServiceCollection AddTaskFlowCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>()?.AllowedOrigins ?? [];

        services.AddCors(options => options.AddPolicy(CorsOptions.PolicyName, policy =>
            policy.WithOrigins(origins)   // lista explícita: nunca AllowAnyOrigin en una API con datos
                  .AllowAnyHeader()
                  .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
                  .WithExposedHeaders("Location"))); // para que el cliente lea el Location del 201

        return services;
    }

    // Rate limiting (.NET 7+): ventana fija por IP. Protege contra abuso o bucles del cliente.
    public static IServiceCollection AddTaskFlowRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var limit = configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>()?.PermitLimitPerMinute
            ?? new RateLimitOptions().PermitLimitPerMinute;

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Una "partición" por IP: cada cliente tiene su propio contador.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            // El 429 también sale como ProblemDetails.
            options.OnRejected = async (context, ct) =>
            {
                var problems = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problems.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Demasiadas solicitudes",
                        Detail = $"Límite de {limit} solicitudes por minuto. Intenta más tarde."
                    }
                });
            };
        });

        return services;
    }

    // Encabezados de seguridad básicos para una API JSON.
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";     // no adivinar el tipo de contenido
            headers["X-Frame-Options"] = "DENY";               // no se puede incrustar en iframes
            headers["Referrer-Policy"] = "no-referrer";
            await next();
        });
}
