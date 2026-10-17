using Microsoft.EntityFrameworkCore;
using TaskFlow.Common;
using TaskFlow.Data;
using TaskFlow.DTOs;

namespace TaskFlow.Services;

public interface IUserAdminService
{
    Task<IReadOnlyList<AdminUserResponse>> GetAllAsync(CancellationToken ct);
    Task<Result<AdminUserResponse>> UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct);
}

// Gestión de usuarios para administradores. No hay borrado: un usuario con historial se
// desactiva (no puede entrar y sus tokens se invalidan) y sus datos se conservan.
public sealed class UserAdminService(TaskFlowDbContext db, ICurrentUser currentUser) : IUserAdminService
{
    public async Task<IReadOnlyList<AdminUserResponse>> GetAllAsync(CancellationToken ct) =>
        await db.Users
            .AsNoTracking()
            .OrderBy(u => u.DisplayName)
            // u.Activities.Count() se traduce a una subconsulta COUNT en el mismo SELECT.
            .Select(u => new AdminUserResponse(
                u.Id, u.Email, u.DisplayName, u.Role, u.IsActive, u.CreatedAt, u.LastLoginAt,
                u.Activities.Count()))
            .ToListAsync(ct);

    public async Task<Result<AdminUserResponse>> UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct)
    {
        // Un admin no puede quitarse el rol ni desactivarse a sí mismo: evita quedarse sin admins.
        if (id == currentUser.UserId)
            return Error.Conflict("CannotModifySelf",
                "No puedes cambiar tu propio rol ni desactivar tu propia cuenta.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
            return Error.NotFound("UserNotFound", $"No existe el usuario {id}.");

        if (!Enum.IsDefined(request.Role!.Value))
            return Error.Validation("InvalidRole", "Rol inválido.");

        user.Role = request.Role.Value;
        user.IsActive = request.IsActive!.Value;
        await db.SaveChangesAsync(ct);

        var activityCount = await db.Activities.CountAsync(a => a.UserId == id, ct);
        return new AdminUserResponse(user.Id, user.Email, user.DisplayName, user.Role, user.IsActive,
            user.CreatedAt, user.LastLoginAt, activityCount);
    }
}
