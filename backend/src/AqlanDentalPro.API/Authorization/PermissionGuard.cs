using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Authorization;

public static class PermissionGuard
{
    public static async Task<bool> HasAsync(
        AppDbContext db,
        ICurrentUserService currentUser,
        string resource,
        string action)
    {
        if (currentUser.Role == UserRole.Admin)
            return true;

        var role = currentUser.Role?.ToString();
        if (string.IsNullOrWhiteSpace(role))
            return false;

        var permission = await db.RolePermissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Role == role && p.Resource == resource);

        if (permission is null)
            return false;

        return action switch
        {
            "view" => permission.CanView,
            "create" => permission.CanCreate,
            "edit" => permission.CanEdit,
            "delete" => permission.CanDelete,
            "export" => permission.CanExport,
            "approve" => permission.CanApprove,
            _ => false
        };
    }
}
