namespace TaskFlow.Enums;

// Rol del usuario. Se guarda como int; en el JWT viaja como texto ("User" / "Admin")
// en el claim "role", que es lo que revisa [Authorize(Roles = "Admin")].
public enum UserRole
{
    User = 1,
    Admin = 2
}
