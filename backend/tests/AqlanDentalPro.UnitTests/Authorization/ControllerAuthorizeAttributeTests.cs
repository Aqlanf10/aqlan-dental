using System.Linq;
using System.Reflection;
using AqlanDentalPro.API.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace AqlanDentalPro.UnitTests.Authorization;

/// <summary>
/// C-12 regression guard: a bare method-level [Authorize] (auth-only, no policy)
/// OVERRIDES the controller's class policy, so an authenticated patient-portal
/// JWT can reach a staff/admin endpoint. These reflection checks ensure the two
/// fixed endpoints keep a staff-scoped policy.
/// </summary>
public class ControllerAuthorizeAttributeTests
{
    private static AuthorizeAttribute[] MethodAuthorize(Type controller, string method)
    {
        var mi = controller.GetMethod(method);
        mi.Should().NotBeNull($"{controller.Name}.{method} must exist");
        return mi!.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>().ToArray();
    }

    [Fact]
    public void LeaveController_Create_DoesNotWeakenClassStaffOnlyWithBareAuthorize()
    {
        // Class is [Authorize(Policy="StaffOnly")]; Create must not re-declare a
        // bare [Authorize] (which would admit patient-portal JWTs).
        MethodAuthorize(typeof(LeaveController), "Create")
            .Should().NotContain(a => string.IsNullOrEmpty(a.Policy),
                "a bare [Authorize] on Create overrides the class StaffOnly policy and lets patient JWTs forge leave requests");
    }

    [Fact]
    public void UsersController_GetContacts_RequiresStaffOnly()
    {
        // GetContacts intentionally broadens the class AdminOnly to all staff for
        // messaging, but must exclude patient-portal JWTs — so StaffOnly, never bare.
        var attrs = MethodAuthorize(typeof(UsersController), "GetContacts");
        attrs.Should().NotBeEmpty();
        attrs.Should().OnlyContain(a => a.Policy == "StaffOnly",
            "GetContacts must be staff-scoped, not a bare [Authorize] that admits patient JWTs");
    }
}
