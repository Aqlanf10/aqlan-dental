using System.Reflection;
using AqlanDentalPro.API.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace AqlanDentalPro.UnitTests.ClinicQueue;

/// <summary>
/// Controller guard tests for Sprint 8A.
/// These tests intentionally use reflection only so they do not require a database.
/// They protect the most important security/privacy attributes on ClinicQueueController.
/// </summary>
public class ClinicQueueControllerGuardTests
{
    [Fact]
    public void ClinicQueueController_RequiresStaffOnlyPolicyByDefault()
    {
        var authorize = typeof(ClinicQueueController)
            .GetCustomAttributes<AuthorizeAttribute>()
            .SingleOrDefault();

        authorize.Should().NotBeNull();
        authorize!.Policy.Should().Be("StaffOnly");
    }

    [Fact]
    public void ClinicQueueController_UsesExpectedRoute()
    {
        var route = typeof(ClinicQueueController)
            .GetCustomAttributes<RouteAttribute>()
            .SingleOrDefault();

        route.Should().NotBeNull();
        route!.Template.Should().Be("api/clinic-queue");
    }

    [Fact]
    public void GetDisplay_AllowsAnonymousAccess()
    {
        var method = GetControllerMethod(nameof(ClinicQueueController.GetDisplay));

        method.GetCustomAttributes<AllowAnonymousAttribute>()
            .Should()
            .ContainSingle();
    }

    [Fact]
    public void GetRooms_AllowsAnonymousAccess()
    {
        var method = GetControllerMethod(nameof(ClinicQueueController.GetRooms));

        method.GetCustomAttributes<AllowAnonymousAttribute>()
            .Should()
            .ContainSingle();
    }

    [Theory]
    [InlineData(nameof(ClinicQueueController.GetTodayQueue))]
    [InlineData(nameof(ClinicQueueController.AddToQueue))]
    [InlineData(nameof(ClinicQueueController.CallPatient))]
    [InlineData(nameof(ClinicQueueController.EnterRoom))]
    [InlineData(nameof(ClinicQueueController.StartVisit))]
    [InlineData(nameof(ClinicQueueController.Complete))]
    [InlineData(nameof(ClinicQueueController.Cancel))]
    [InlineData(nameof(ClinicQueueController.ChangeRoom))]
    [InlineData(nameof(ClinicQueueController.MarkArrived))]
    public void StaffWorkflowMethods_DoNotAllowAnonymousAccess(string methodName)
    {
        var method = GetControllerMethod(methodName);

        method.GetCustomAttributes<AllowAnonymousAttribute>()
            .Should()
            .BeEmpty($"{methodName} is a staff workflow endpoint and must stay protected by the controller StaffOnly policy");
    }

    [Theory]
    [InlineData(nameof(ClinicQueueController.GetTodayQueue), typeof(HttpGetAttribute), "today")]
    [InlineData(nameof(ClinicQueueController.AddToQueue), typeof(HttpPostAttribute), null)]
    [InlineData(nameof(ClinicQueueController.CallPatient), typeof(HttpPostAttribute), "{id:guid}/call")]
    [InlineData(nameof(ClinicQueueController.EnterRoom), typeof(HttpPostAttribute), "{id:guid}/enter-room")]
    [InlineData(nameof(ClinicQueueController.StartVisit), typeof(HttpPostAttribute), "{id:guid}/start")]
    [InlineData(nameof(ClinicQueueController.Complete), typeof(HttpPostAttribute), "{id:guid}/complete")]
    [InlineData(nameof(ClinicQueueController.Cancel), typeof(HttpPostAttribute), "{id:guid}/cancel")]
    [InlineData(nameof(ClinicQueueController.ChangeRoom), typeof(HttpPatchAttribute), "{id:guid}/room")]
    [InlineData(nameof(ClinicQueueController.GetDisplay), typeof(HttpGetAttribute), "display")]
    [InlineData(nameof(ClinicQueueController.GetRooms), typeof(HttpGetAttribute), "rooms")]
    [InlineData(nameof(ClinicQueueController.MarkArrived), typeof(HttpPostAttribute), "arrive/{id:guid}")]
    public void ControllerMethods_HaveExpectedHttpAttributes(string methodName, Type attributeType, string? expectedTemplate)
    {
        var method = GetControllerMethod(methodName);
        var attribute = method.GetCustomAttributes()
            .SingleOrDefault(a => a.GetType() == attributeType);

        attribute.Should().NotBeNull($"{methodName} should keep its expected HTTP verb");

        var template = attribute switch
        {
            HttpGetAttribute get => get.Template,
            HttpPostAttribute post => post.Template,
            HttpPatchAttribute patch => patch.Template,
            _ => null
        };

        template.Should().Be(expectedTemplate);
    }

    [Fact]
    public void DisplayEndpoints_AreTheOnlyAnonymousEndpoints()
    {
        var anonymousMethods = typeof(ClinicQueueController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<AllowAnonymousAttribute>().Any())
            .Select(m => m.Name)
            .OrderBy(name => name)
            .ToArray();

        anonymousMethods.Should().Equal(
            nameof(ClinicQueueController.GetDisplay),
            nameof(ClinicQueueController.GetRooms));
    }

    private static MethodInfo GetControllerMethod(string methodName)
    {
        var method = typeof(ClinicQueueController).GetMethod(methodName);
        method.Should().NotBeNull($"ClinicQueueController should contain method {methodName}");
        return method!;
    }
}
