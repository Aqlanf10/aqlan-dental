using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Application.Interfaces.Services;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Username { get; }
    UserRole? Role { get; }
    Guid? BranchId { get; }
    bool IsAdmin { get; }
    bool IsAuthenticated { get; }
    Guid? OriginalUserId { get; }  // null if not impersonating
    bool IsImpersonating { get; }
}
