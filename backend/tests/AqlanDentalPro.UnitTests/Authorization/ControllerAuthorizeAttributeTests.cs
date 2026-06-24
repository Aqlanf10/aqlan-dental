using System.Linq;
using AqlanDentalPro.API.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace AqlanDentalPro.UnitTests.Authorization;

/// <summary>
/// Authorization hygiene guard. ASP.NET Core combines controller- and
/// action-level [Authorize] additively (ALL policies must pass), so a class
/// policy is never weakened by a method-level attribute. These checks document
/// the effective policy of two endpoints the audit (C-12) flagged.
/// </summary>
public class ControllerAuthorizeAttributeTests
{
    private static AuthorizeAttribute[] MethodAuthorize(Type controller, string method)
    {
        var mi = controller.GetMethod(method);
        mi.Should().NotBeNull($"{controller.Name}.{method} must exist");
        return mi!.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>().ToArray();
    }

    private static AuthorizeAttribute[] ClassAuthorize(Type controller) =>
        controller.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().ToArray();

    [Fact]
    public void LeaveController_IsStaffOnly_WithNoRedundantBareMethodAuthorize()
    {
        // The class StaffOnly policy already blocks patient-portal JWTs on every
        // action (authorization is additive). Create must not carry a redundant,
        // misleading bare [Authorize] that appears to weaken the class policy.
        ClassAuthorize(typeof(LeaveController))
            .Should().Contain(a => a.Policy == "StaffOnly");
        MethodAuthorize(typeof(LeaveController), "Create")
            .Should().NotContain(a => string.IsNullOrEmpty(a.Policy),
                "a redundant bare [Authorize] on Create is misleading; the class StaffOnly already enforces staff-only access");
    }

    [Fact]
    public void UsersController_IsAdminOnly_AtClassLevel()
    {
        // UsersController is AdminOnly at the class level; because authorization is
        // additive that policy applies to every action including GetContacts, so
        // contacts are admin-scoped (patients blocked). Broadening contacts to all
        // staff for messaging would require relocating the endpoint off this
        // AdminOnly controller — out of scope here.
        ClassAuthorize(typeof(UsersController))
            .Should().Contain(a => a.Policy == "AdminOnly");
    }

    [Fact]
    public void UsersController_GetContacts_NoRedundantBareMethodAuthorize()
    {
        // C-12 follow-up: the original audit flagged GetContacts for "bare [Authorize]"
        // but the class-level AdminOnly policy already covers it (additive authorization).
        // The bare attribute was redundant/misleading and has been removed.
        MethodAuthorize(typeof(UsersController), "GetContacts")
            .Should().BeEmpty("the class-level AdminOnly policy is sufficient; no method-level [Authorize] is needed");
    }
}
