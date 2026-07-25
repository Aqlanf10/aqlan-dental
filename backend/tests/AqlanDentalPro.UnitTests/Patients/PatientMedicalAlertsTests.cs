using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Patients;

public class PatientMedicalAlertsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("No")]
    [InlineData("N/A")]
    [InlineData("لا ينطبق")]
    public void BuildMedicalAlerts_NonPregnantTriStateValues_DoNotCreatePregnancyAlert(string? value)
    {
        var history = new MedicalHistory { IsPregnant = value };

        var alerts = PatientsController.BuildMedicalAlerts(history);

        alerts.Should().NotContain(alert => alert.Contains("حمل"));
    }

    [Theory]
    [InlineData("Yes")]
    [InlineData("yes")]
    [InlineData("نعم")]
    [InlineData("حامل")]
    [InlineData("يوجد حمل")]
    public void BuildMedicalAlerts_AffirmativeValues_CreatePregnancyAlert(string value)
    {
        var history = new MedicalHistory { IsPregnant = value };

        var alerts = PatientsController.BuildMedicalAlerts(history);

        alerts.Should().Contain("حمل");
    }

    [Fact]
    public void BuildMedicalAlerts_PreservesOtherMedicalWarnings()
    {
        var history = new MedicalHistory
        {
            IsPregnant = "N/A",
            DrugAllergies = "Penicillin",
            BleedingDisorders = true,
        };

        var alerts = PatientsController.BuildMedicalAlerts(history);

        alerts.Should().Contain("حساسية أدوية: Penicillin");
        alerts.Should().Contain("اضطرابات نزف");
        alerts.Should().NotContain(alert => alert.Contains("حمل"));
    }
}
