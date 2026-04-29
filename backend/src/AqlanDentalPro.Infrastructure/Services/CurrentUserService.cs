using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Enums;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace AqlanDentalPro.Infrastructure.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var sub = Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? Principal?.FindFirstValue("sub");
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public string? Username => Principal?.FindFirstValue(ClaimTypes.Name)
                               ?? Principal?.FindFirstValue("unique_name");

    public UserRole? Role
    {
        get
        {
            var role = Principal?.FindFirstValue(ClaimTypes.Role);
            return Enum.TryParse<UserRole>(role, out var r) ? r : null;
        }
    }

    public Guid? BranchId
    {
        get
        {
            var bid = Principal?.FindFirstValue("branchId");
            return Guid.TryParse(bid, out var id) ? id : null;
        }
    }

    public bool IsAdmin => Role == UserRole.Admin;
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
}
