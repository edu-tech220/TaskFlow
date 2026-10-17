namespace TaskFlow.Common;

// Tipo de error de negocio. El controlador lo traduce a código HTTP:
// Validation => 400, Unauthorized => 401, Forbidden => 403, NotFound => 404, Conflict => 409.
public enum ErrorType
{
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict
}

// Error de negocio esperado (no una excepción). Code es estable para que el frontend
// pueda reaccionar a casos concretos ("MonthlyRestLimitReached") sin parsear el mensaje.
public sealed record Error(ErrorType Type, string Code, string Message)
{
    public static Error Validation(string code, string message) => new(ErrorType.Validation, code, message);
    public static Error NotFound(string code, string message) => new(ErrorType.NotFound, code, message);
    public static Error Conflict(string code, string message) => new(ErrorType.Conflict, code, message);
    public static Error Unauthorized(string code, string message) => new(ErrorType.Unauthorized, code, message);
    public static Error Forbidden(string code, string message) => new(ErrorType.Forbidden, code, message);
}
