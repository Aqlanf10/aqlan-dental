using System.Security.Claims;
using AqlanDentalPro.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Authorization;

/// <summary>
/// Tests for the StaffOnly authorization policy (TD-009 security fix).
/// Validates that Patient portal JWTs are blocked from staff endpoints
/// while staff/admin/reception JWTs continue to work.
///
/// The StaffOnly policy is defined as:
///   policy.RequireAuthenticatedUser()
///         .RequireAssertion(ctx => !ctx.User.IsInRole(nameof(UserRole.Patient)));
///
/// These tests verify the core assertion logic directly.
/// </summary>
public class StaffOnlyPolicyTests
{
    // ─── Patient Role Is Blocked ────────────────────────────────────────────

    [Fact]
    public void StaffOnly_PatientRole_IsBlocked()
    {
        // Arrange — Patient portal JWT has role="Patient"
        var user = CreatePatientUser();

        // Act — this is the exact assertion from the StaffOnly policy
        var isNotPatient = !user.IsInRole(nameof(UserRole.Patient));
        var isAuthenticated = user.Identity?.IsAuthenticated == true;

        // Assert
        isAuthenticated.Should().BeTrue("Patient portal JWTs are authenticated");
        isNotPatient.Should().BeFalse("Patient portal JWTs must fail the StaffOnly assertion");
    }

    [Fact]
    public void StaffOnly_PatientRole_WithPortalClaim_IsBlocked()
    {
        // Arrange — Patient portal JWT with portal="true" claim
        var user = CreatePatientUser(withPortalClaim: true);

        // Act
        var isNotPatient = !user.IsInRole(nameof(UserRole.Patient));

        // Assert
        isNotPatient.Should().BeFalse("Patient portal JWTs with portal claim must still fail StaffOnly");
    }

    // ─── Staff Roles Are Allowed ────────────────────────────────────────────

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Orthodontist)]
    [InlineData(UserRole.GeneralDentist)]
    [InlineData(UserRole.OralSurgeon)]
    [InlineData(UserRole.Reception)]
    [InlineData(UserRole.Accountant)]
    [InlineData(UserRole.Assistant)]
    [InlineData(UserRole.BranchManager)]
    public void StaffOnly_StaffRoles_AreAllowed(UserRole role)
    {
        // Arrange
        var user = CreateStaffUser(role);

        // Act — exact assertion from the StaffOnly policy
        var isNotPatient = !user.IsInRole(nameof(UserRole.Patient));
        var isAuthenticated = user.Identity?.IsAuthenticated == true;

        // Assert
        isAuthenticated.Should().BeTrue($"{role} JWTs are authenticated");
        isNotPatient.Should().BeTrue($"{role} JWTs must pass the StaffOnly assertion");
    }

    // ─── Unauthenticated Users Are Blocked ──────────────────────────────────

    [Fact]
    public void StaffOnly_UnauthenticatedUser_IsBlocked()
    {
        // Arrange — no authentication type means not authenticated
        var user = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var isAuthenticated = user.Identity?.IsAuthenticated == true;
        var isNotPatient = !user.IsInRole(nameof(UserRole.Patient));

        // Assert — RequireAuthenticatedUser would block first
        isAuthenticated.Should().BeFalse("Unauthenticated users must be blocked by RequireAuthenticatedUser");
    }

    // ─── PatientAccess Policy Still Works ───────────────────────────────────

    [Fact]
    public void PatientAccess_PatientRole_IsAllowed()
    {
        // Arrange
        var user = CreatePatientUser();

        // Act — PatientAccess uses RequireRole("Patient")
        var isPatient = user.IsInRole("Patient");

        // Assert
        isPatient.Should().BeTrue("Patient portal JWTs must pass the PatientAccess policy");
    }

    [Fact]
    public void PatientAccess_AdminRole_IsForbidden()
    {
        // Arrange
        var user = CreateStaffUser(UserRole.Admin);

        // Act
        var isPatient = user.IsInRole("Patient");

        // Assert
        isPatient.Should().BeFalse("Admin JWTs must fail the PatientAccess policy");
    }

    // ─── AdminOnly Policy Still Works ───────────────────────────────────────

    [Fact]
    public void AdminOnly_AdminRole_IsAllowed()
    {
        // Arrange
        var user = CreateStaffUser(UserRole.Admin);

        // Act
        var isAdmin = user.IsInRole(nameof(UserRole.Admin));

        // Assert
        isAdmin.Should().BeTrue("Admin JWTs must pass the AdminOnly policy");
    }

    [Fact]
    public void AdminOnly_PatientRole_IsForbidden()
    {
        // Arrange
        var user = CreatePatientUser();

        // Act
        var isAdmin = user.IsInRole(nameof(UserRole.Admin));

        // Assert
        isAdmin.Should().BeFalse("Patient portal JWTs must fail the AdminOnly policy");
    }

    [Fact]
    public void AdminOnly_ReceptionRole_IsForbidden()
    {
        // Arrange
        var user = CreateStaffUser(UserRole.Reception);

        // Act
        var isAdmin = user.IsInRole(nameof(UserRole.Admin));

        // Assert
        isAdmin.Should().BeFalse("Reception JWTs must fail the AdminOnly policy");
    }

    // ─── AdminOrReception Policy Still Works ────────────────────────────────

    [Fact]
    public void AdminOrReception_AdminRole_IsAllowed()
    {
        var user = CreateStaffUser(UserRole.Admin);
        var isAdminOrReception = user.IsInRole(nameof(UserRole.Admin)) || user.IsInRole(nameof(UserRole.Reception));
        isAdminOrReception.Should().BeTrue();
    }

    [Fact]
    public void AdminOrReception_ReceptionRole_IsAllowed()
    {
        var user = CreateStaffUser(UserRole.Reception);
        var isAdminOrReception = user.IsInRole(nameof(UserRole.Admin)) || user.IsInRole(nameof(UserRole.Reception));
        isAdminOrReception.Should().BeTrue();
    }

    [Fact]
    public void AdminOrReception_PatientRole_IsForbidden()
    {
        var user = CreatePatientUser();
        var isAdminOrReception = user.IsInRole(nameof(UserRole.Admin)) || user.IsInRole(nameof(UserRole.Reception));
        isAdminOrReception.Should().BeFalse("Patient portal JWTs must fail AdminOrReception policy");
    }

    // ─── Role Claim Uses Correct String Format ──────────────────────────────

    [Fact]
    public void UserRole_Patient_ToString_MatchesClaimValue()
    {
        // Verify that nameof(UserRole.Patient) produces "Patient" which matches the claim value
        nameof(UserRole.Patient).Should().Be("Patient");
    }

    [Fact]
    public void UserRole_Staff_ToString_MatchesExpectedValues()
    {
        // Verify all staff role names match their expected string representations
        nameof(UserRole.Admin).Should().Be("Admin");
        nameof(UserRole.Orthodontist).Should().Be("Orthodontist");
        nameof(UserRole.GeneralDentist).Should().Be("GeneralDentist");
        nameof(UserRole.OralSurgeon).Should().Be("OralSurgeon");
        nameof(UserRole.Reception).Should().Be("Reception");
        nameof(UserRole.Accountant).Should().Be("Accountant");
        nameof(UserRole.Assistant).Should().Be("Assistant");
        nameof(UserRole.BranchManager).Should().Be("BranchManager");
    }

    // ─── Helper Methods ─────────────────────────────────────────────────────

    private static ClaimsPrincipal CreatePatientUser(bool withPortalClaim = false)
    {
        // Replicate the claims structure of a patient portal JWT
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, "Patient"),
            new("patientId", Guid.NewGuid().ToString()),
            new("mustChangePassword", "false")
        };

        if (withPortalClaim)
        {
            claims.Add(new Claim("portal", "true"));
        }

        var identity = new ClaimsIdentity(claims, "TestAuthType");
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal CreateStaffUser(UserRole role)
    {
        // Replicate the claims structure of a staff JWT
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, $"test_{role.ToString().ToLower()}"),
            new(ClaimTypes.Role, role.ToString()),
            new("branchId", Guid.NewGuid().ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        return new ClaimsPrincipal(identity);
    }
}
