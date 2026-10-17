using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TaskFlow.Data;
using TaskFlow.Entities;
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
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserAdminService, UserAdminService>();

        return services;
    }

    // Autenticación con JWT Bearer + autorización "todo requiere login" por defecto.
    public static IServiceCollection AddTaskFlowAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Se lee aquí porque JwtBearer necesita la clave al configurarse.
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        if (jwt.Key.Length < 32)
            throw new InvalidOperationException(
                "Falta Jwt:Key (32+ caracteres). En desarrollo: dotnet user-secrets set \"Jwt:Key\" \"<clave larga>\"");

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Sin esto, el handler renombra "sub" y "role" a URIs largas de ClaimTypes.
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30), // tolerancia de reloj (default: 5 min)
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };

                options.Events = new JwtBearerEvents
                {
                    // Un JWT es válido hasta que expira aunque el usuario se desactive después.
                    // Esta verificación (una consulta por petición) lo corta de inmediato.
                    OnTokenValidated = async context =>
                    {
                        var sub = context.Principal?.FindFirst(CurrentUser.SubjectClaim)?.Value;
                        var db = context.HttpContext.RequestServices.GetRequiredService<TaskFlowDbContext>();
                        var active = int.TryParse(sub, out var id) &&
                                     await db.Users.AnyAsync(u => u.Id == id && u.IsActive);
                        if (!active)
                            context.Fail("Usuario inexistente o desactivado.");
                    }
                };
            });

        // La validación de vencimiento de Microsoft.IdentityModel usa DateTime.UtcNow. Para que
        // emitir y validar usen el MISMO reloj (y las pruebas puedan fijarlo), se reemplaza por
        // uno basado en TimeProvider. Configure<TimeProvider> inyecta el servicio del contenedor.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<TimeProvider>((options, clock) =>
            {
                var skew = options.TokenValidationParameters.ClockSkew;
                options.TokenValidationParameters.LifetimeValidator = (notBefore, expires, _, _) =>
                {
                    var now = clock.GetUtcNow().UtcDateTime;
                    return (notBefore is null || notBefore.Value <= now + skew)
                        && (expires is null || expires.Value > now - skew);
                };
            });

        // FallbackPolicy aplica a todo endpoint sin [Authorize] ni [AllowAnonymous]:
        // olvidar el atributo en un controlador nuevo no lo deja abierto por accidente.
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

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
        var settings = configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>() ?? new RateLimitOptions();
        var limit = settings.PermitLimitPerMinute;

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

            // Política con nombre para login/registro ([EnableRateLimiting("auth")]), por IP.
            options.AddPolicy(RateLimitOptions.AuthPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.AuthPermitLimitPerMinute,
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
                        Detail = "Demasiadas solicitudes en poco tiempo. Espera un minuto e intenta de nuevo."
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
