namespace TaskFlow.Common;

// Patrón Result: los servicios devuelven éxito o un Error de negocio en lugar de lanzar
// excepciones para casos esperados (404, 409...). Las excepciones quedan para lo inesperado,
// que atrapa el GlobalExceptionHandler.
public class Result
{
    protected Result(Error? error) => Error = error;

    public Error? Error { get; }
    public bool IsSuccess => Error is null;

    public static Result Success() => new(null);

    // Permite "return Error.NotFound(...)" en métodos que devuelven Result.
    public static implicit operator Result(Error error) => new(error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value) : base(null) => _value = value;
    private Result(Error error) : base(error) { }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("No se puede leer Value de un resultado con error.");

    // Conversiones implícitas: "return response;" o "return Error.Conflict(...);".
    public static implicit operator Result<T>(T value) => new(value);
    public static implicit operator Result<T>(Error error) => new(error);
}
