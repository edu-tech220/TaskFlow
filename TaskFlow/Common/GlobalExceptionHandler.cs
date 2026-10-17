using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace TaskFlow.Common;

// IExceptionHandler (.NET 8+): lo invoca app.UseExceptionHandler() para cualquier excepción
// no atrapada. Aquí se traduce a ProblemDetails (RFC 9457), el mismo formato que los errores
// de negocio, así el cliente siempre recibe errores con la misma forma.
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail) = Classify(exception);

        if (status >= 500)
            logger.LogError(exception, "Error no controlado en {Path}", httpContext.Request.Path);
        else
            logger.LogWarning(exception, "Petición rechazada por la base de datos en {Path}", httpContext.Request.Path);

        httpContext.Response.StatusCode = status;

        // IProblemDetailsService escribe el JSON con application/problem+json y agrega traceId.
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail }
        });
    }

    private static (int Status, string Title, string Detail) Classify(Exception exception)
    {
        // Las reglas se validan en los servicios, pero dos peticiones simultáneas pueden pasar
        // la validación a la vez. Entonces la BD es la última línea: sus errores se vuelven 409.
        var sqlError = exception switch
        {
            SqlException sql => sql,
            DbUpdateException { InnerException: SqlException sql } => sql,
            _ => null
        };

        return sqlError?.Number switch
        {
            // 2601/2627: violación de índice único o PK.
            2601 or 2627 => (StatusCodes.Status409Conflict, "Conflicto",
                "Ya existe un registro con esos datos."),
            // 547: violación de FK o CHECK.
            547 => (StatusCodes.Status409Conflict, "Conflicto",
                "La operación viola una restricción de integridad de la base de datos."),
            _ when exception is BadHttpRequestException bad => (bad.StatusCode, "Solicitud inválida", bad.Message),
            _ => (StatusCodes.Status500InternalServerError, "Error interno",
                "Ocurrió un error inesperado. Revisa los logs del servidor.")
        };
    }
}
