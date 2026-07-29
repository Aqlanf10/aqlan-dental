using AqlanDentalPro.API.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ortho;

/// <summary>
/// Route-guard regression tests for the ortho-surgical route-access fix: an OralSurgeon
/// reaching /ortho/{orthoCaseId}?tab=surgical (from /surgery's pending-review list, the
/// reciprocal link on /surgery/[id], or the patient file's ortho-surgical sub-tab) needs
/// GetById (the case header the page shell depends on to render at all) to admit them,
/// while every OTHER endpoint on this controller — including the richer GetOverview
/// projection (diagnosis, treatment plans, financial contract summary) — must remain
/// Admin/Orthodontist-only via the unchanged class-level OrthoAccess policy.
///
/// [Authorize] enforcement itself only runs inside the ASP.NET Core MVC pipeline (not when
/// calling a controller method directly in-process), so this is a reflection-based
/// attribute check — the established pattern in this codebase (see
/// DailyOperationsRouteGuardTests) for policies that can't be exercised via UnitTests
/// (no Testcontainers/HTTP pipeline in CI).
/// </summary>
public class OrthoCasesControllerGetByIdAccessTests
{
    [Fact]
    public void OrthoCasesController_ClassLevelPolicy_IsStillOrthoAccess()
    {
        var attr = typeof(OrthoCasesController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        attr.Should().NotBeNull("OrthoCasesController must have a class-level [Authorize] attribute");
        attr!.Policy.Should().Be("OrthoAccess",
            "the class-level policy must remain Admin/Orthodontist-only — the GetById override below " +
            "must be the ONLY endpoint widened, not a blanket relaxation");
    }

    [Fact]
    public void GetById_OverridesToOrthoSurgicalAccess_SoOralSurgeonCanRenderThePageShell()
    {
        var method = typeof(OrthoCasesController).GetMethod(nameof(OrthoCasesController.GetById));
        method.Should().NotBeNull();

        var authorize = method!.GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        authorize.Should().NotBeNull(
            "GetById needs a per-action [Authorize] override — without it, the class-level " +
            "OrthoAccess policy blocks OralSurgeon, and /ortho/[id]/page.tsx blanks the entire " +
            "page to \"الحالة غير موجودة\" when this call 403s, stranding the surgeon before " +
            "they ever see the shared ortho-surgical tab");
        authorize!.Policy.Should().Be("OrthoSurgicalAccess",
            "must widen to exactly Admin/Orthodontist/OralSurgeon — not a broader or narrower policy");
    }

    [Theory]
    [InlineData(nameof(OrthoCasesController.GetOverview))]
    [InlineData(nameof(OrthoCasesController.GetClinicalExam))]
    [InlineData(nameof(OrthoCasesController.GetDiagnosis))]
    public void RicherEndpoints_HaveNoPerActionOverride_StayOrthoAccessOnlyViaClassLevelPolicy(string methodName)
    {
        var method = typeof(OrthoCasesController).GetMethod(methodName);
        method.Should().NotBeNull($"{methodName} must exist on OrthoCasesController");

        var authorize = method!.GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        authorize.Should().BeNull(
            $"{methodName} exposes richer clinical/financial data than the GetById header and must " +
            "NOT be widened to OralSurgeon — it must keep relying solely on the class-level OrthoAccess policy");
    }
}
