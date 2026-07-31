using System.Security.Claims;
using AqlanDentalPro.API.Configuration;
using AqlanDentalPro.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AqlanDentalPro.UnitTests.Authorization;

public class ClinicalAuthorizationPolicyTests
{
    public static TheoryData<UserRole> ClinicalRoles =>
        new()
        {
            UserRole.Admin,
            UserRole.Orthodontist,
            UserRole.GeneralDentist,
            UserRole.OralSurgeon,
        };

    public static TheoryData<UserRole> NonClinicalRoles =>
        new()
        {
            UserRole.Reception,
            UserRole.Accountant,
            UserRole.Assistant,
            UserRole.BranchManager,
            UserRole.Patient,
        };

    [Theory]
    [MemberData(nameof(ClinicalRoles))]
    public async Task ClinicalRead_AllowsOnlyClinicalRolesAndAdmin(UserRole role)
    {
        var result = await AuthorizeAsync(role, AuthorizationPolicyNames.ClinicalRead);

        result.Succeeded.Should().BeTrue($"{role} must be able to read clinical records");
    }

    [Theory]
    [MemberData(nameof(ClinicalRoles))]
    public async Task ClinicalWrite_AllowsOnlyClinicalRolesAndAdmin(UserRole role)
    {
        var result = await AuthorizeAsync(role, AuthorizationPolicyNames.ClinicalWrite);

        result.Succeeded.Should().BeTrue($"{role} must be able to write clinical records");
    }

    [Theory]
    [MemberData(nameof(NonClinicalRoles))]
    public async Task ClinicalRead_BlocksNonClinicalRoles(UserRole role)
    {
        var result = await AuthorizeAsync(role, AuthorizationPolicyNames.ClinicalRead);

        result.Succeeded.Should().BeFalse($"{role} must receive 403 before a clinical action executes");
    }

    [Theory]
    [MemberData(nameof(NonClinicalRoles))]
    public async Task ClinicalWrite_BlocksNonClinicalRoles(UserRole role)
    {
        var result = await AuthorizeAsync(role, AuthorizationPolicyNames.ClinicalWrite);

        result.Succeeded.Should().BeFalse($"{role} must receive 403 before a clinical mutation executes");
    }

    [Theory]
    [InlineData(AuthorizationPolicyNames.ClinicalRead)]
    [InlineData(AuthorizationPolicyNames.ClinicalWrite)]
    public async Task ClinicalPolicies_BlockUnauthenticatedUsers(string policy)
    {
        var services = BuildServices();
        var authorization = services.GetRequiredService<IAuthorizationService>();
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await authorization.AuthorizeAsync(anonymous, resource: null, policy);

        result.Succeeded.Should().BeFalse();
    }

    private static async Task<AuthorizationResult> AuthorizeAsync(UserRole role, string policy)
    {
        var services = BuildServices();
        var authorization = services.GetRequiredService<IAuthorizationService>();
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, role.ToString())],
            authenticationType: "test");

        return await authorization.AuthorizeAsync(
            new ClaimsPrincipal(identity),
            resource: null,
            policy);
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationPolicies();
        return services.BuildServiceProvider();
    }
}
