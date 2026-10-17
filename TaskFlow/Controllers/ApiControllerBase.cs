using Microsoft.AspNetCore.Mvc;
using TaskFlow.Common;

namespace TaskFlow.Controllers;

// Base de todos los controladores de la API.
// [ApiController] activa: validación automática del modelo (400 con ValidationProblemDetails
// si fallan las DataAnnotations), binding inferido ([FromBody] para objetos complejos) y
// respuestas de error en formato ProblemDetails.
// Nota: no se usa [Produces("application/json")] aquí porque ese filtro reemplaza el
// Content-Type de TODAS las respuestas, incluido application/problem+json de los errores.
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    // Traduce un Error de negocio a ProblemDetails con el código HTTP correspondiente.
    // ProblemDetailsFactory agrega type, title, status y traceId de forma consistente.
    protected ObjectResult ErrorResult(Error error)
    {
        var (status, title) = error.Type switch
        {
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Recurso no encontrado"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Conflicto con el estado actual"),
            _ => (StatusCodes.Status400BadRequest, "Solicitud inválida")
        };

        var problem = ProblemDetailsFactory.CreateProblemDetails(
            HttpContext, statusCode: status, title: title, detail: error.Message);

        // "code" es una extensión de ProblemDetails: identificador estable para el cliente.
        problem.Extensions["code"] = error.Code;

        return new ObjectResult(problem)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" }
        };
    }
}
