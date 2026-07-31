using AqlanDentalPro.API.Configuration;
using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.DTOs.Patients;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace AqlanDentalPro.UnitTests.Authorization;

public class ClinicalControllerGuardTests
{
    public static TheoryData<Type> ClinicalControllers =>
        new()
        {
            typeof(VisitsController),
            typeof(PrescriptionsController),
            typeof(DocumentsController),
            typeof(ClinicalPhotosController),
            typeof(RadiographsController),
        };

    public static TheoryData<Type, string> ClinicalWriteActions =>
        new()
        {
            { typeof(VisitsController), nameof(VisitsController.CreateVisit) },
            { typeof(VisitsController), nameof(VisitsController.UpdateVisit) },
            { typeof(VisitsController), nameof(VisitsController.DeleteVisit) },
            { typeof(PrescriptionsController), nameof(PrescriptionsController.Create) },
            { typeof(PrescriptionsController), nameof(PrescriptionsController.Delete) },
            { typeof(DocumentsController), nameof(DocumentsController.CreateDocument) },
            { typeof(DocumentsController), nameof(DocumentsController.UpdateDocument) },
            { typeof(DocumentsController), nameof(DocumentsController.DeleteDocument) },
            { typeof(ClinicalPhotosController), nameof(ClinicalPhotosController.AddPhoto) },
            { typeof(ClinicalPhotosController), nameof(ClinicalPhotosController.DeletePhoto) },
            { typeof(RadiographsController), nameof(RadiographsController.AddRadiograph) },
            { typeof(RadiographsController), nameof(RadiographsController.DeleteRadiograph) },
            { typeof(PatientsController), nameof(PatientsController.UpdateMedicalHistory) },
            { typeof(PatientsController), nameof(PatientsController.UpdateDentalHistory) },
        };

    public static TheoryData<string> PatientClinicalReadActions =>
        new()
        {
            nameof(PatientsController.GetMedicalHistory),
            nameof(PatientsController.GetDentalHistory),
        };

    [Theory]
    [MemberData(nameof(ClinicalControllers))]
    public void ClinicalController_RequiresClinicalReadForEveryDirectRoute(Type controller)
    {
        var policies = controller
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy);

        policies.Should().Contain(AuthorizationPolicyNames.ClinicalRead,
            "direct IDs and list routes must be rejected before clinical details are loaded");
        policies.Should().NotContain("StaffOnly");
    }

    [Theory]
    [MemberData(nameof(ClinicalWriteActions))]
    public void ClinicalMutation_RequiresClinicalWrite(Type controller, string action)
    {
        var method = controller.GetMethod(action);
        method.Should().NotBeNull();

        var policies = method!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy);

        policies.Should().Contain(AuthorizationPolicyNames.ClinicalWrite);

        var verbs = method.GetCustomAttributes(inherit: true).OfType<HttpMethodAttribute>();
        verbs.Should().NotContain(attribute =>
            attribute.HttpMethods.Contains("GET", StringComparer.OrdinalIgnoreCase));
    }

    [Theory]
    [MemberData(nameof(PatientClinicalReadActions))]
    public void PatientClinicalHistoryRead_RequiresClinicalRead(string action)
    {
        var method = typeof(PatientsController).GetMethod(action);
        method.Should().NotBeNull();

        method!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .Should().Contain(AuthorizationPolicyNames.ClinicalRead);
    }

    [Fact]
    public void OperationalPatientDto_DoesNotExposeClinicalHistory()
    {
        var properties = typeof(PatientOperationalDto)
            .GetProperties()
            .Select(property => property.Name);

        properties.Should().NotContain(nameof(PatientProfileDto.MedicalHistory));
        properties.Should().NotContain(nameof(PatientProfileDto.DentalHistory));
    }

    [Fact]
    public void LabUploadRoutes_RemainStaffOnly()
    {
        typeof(UploadsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .Should().Contain("StaffOnly",
                "closing clinical writes must not break authorized lab file uploads");
    }
}
