using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Common;

// Patrón Options: cada sección de appsettings se enlaza a una clase tipada y se inyecta
// como IOptions<T>. ValidateDataAnnotations + ValidateOnStart (en Program.cs) hacen que
// una configuración inválida tumbe la app al arrancar, no a media petición.

public sealed class ClockOptions
{
    public const string SectionName = "Clock";

    // Id IANA de la zona en la que se calcula "hoy". .NET en Windows lo resuelve con ICU.
    [Required]
    public string TimeZoneId { get; set; } = "America/Mexico_City";
}

public sealed class RestDayOptions
{
    public const string SectionName = "RestDays";

    // Límite usado cuando un mes no tiene fila en MonthlyRestLimits.
    [Range(0, 10)]
    public int DefaultMonthlyLimit { get; set; } = 4;
}

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    // Quién emite el token y para quién es. La API valida ambos al recibirlo.
    [Required]
    public string Issuer { get; set; } = "TaskFlow";

    [Required]
    public string Audience { get; set; } = "TaskFlow";

    // Clave simétrica HMAC-SHA256. Es un SECRETO: va en User Secrets o variables de entorno,
    // nunca en appsettings del repo. 32+ caracteres = 256 bits como mínimo.
    [Required, MinLength(32, ErrorMessage = "Jwt:Key debe tener al menos 32 caracteres.")]
    public string Key { get; set; } = string.Empty;

    // Vida del token de acceso. Al vencer, el cliente vuelve a iniciar sesión.
    [Range(5, 7 * 24 * 60)]
    public int ExpiresMinutes { get; set; } = 480;
}

public sealed class CorsOptions
{
    public const string SectionName = "Cors";
    public const string PolicyName = "Frontend";

    // Orígenes (esquema + host + puerto) que pueden llamar a la API desde un navegador.
    public string[] AllowedOrigins { get; set; } = [];
}

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public const string AuthPolicy = "auth";

    // Peticiones permitidas por IP en cada ventana de 1 minuto.
    [Range(1, 100_000)]
    public int PermitLimitPerMinute { get; set; } = 300;

    // Límite más estricto para login/registro: frena ataques de fuerza bruta a contraseñas.
    [Range(1, 100_000)]
    public int AuthPermitLimitPerMinute { get; set; } = 10;
}
