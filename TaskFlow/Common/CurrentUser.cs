namespace TaskFlow.Common;

// Usuario autenticado de la petición actual. Los servicios lo usan para filtrar todo
// por dueño; así un usuario nunca ve ni modifica datos de otro.
public interface ICurrentUser
{
    int UserId { get; }
}

// Lee el claim "sub" (subject = id del usuario) del JWT ya validado por el middleware de
// autenticación. IHttpContextAccessor da acceso al HttpContext desde un servicio.
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public const string SubjectClaim = "sub";

    public int UserId
    {
        get
        {
            var sub = accessor.HttpContext?.User.FindFirst(SubjectClaim)?.Value;
            // Solo se llama desde endpoints con [Authorize]; si falta es un error de programación.
            return int.TryParse(sub, out var id)
                ? id
                : throw new InvalidOperationException("No hay un usuario autenticado en la petición.");
        }
    }
}
