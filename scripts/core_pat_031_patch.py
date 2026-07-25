from pathlib import Path

controller = Path("backend/src/AqlanDentalPro.API/Controllers/PatientsController.cs")
text = controller.read_text(encoding="utf-8")
old = '''    private static List<string> BuildMedicalAlerts(MedicalHistory? history)
    {
        if (history == null) return [];

        var alerts = new List<string>();
        void Add(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                alerts.Add($"{label}: {value.Trim()}");
        }

        Add("حساسية أدوية", history.DrugAllergies);
        Add("أمراض مزمنة", history.ChronicDiseases);
        Add("أدوية حالية", history.CurrentMedications);
        if (history.BleedingDisorders) alerts.Add("اضطرابات نزف");
        if (history.TmjProblems) alerts.Add("مشكلات مفصل الفك");

        var pregnancy = history.IsPregnant?.Trim();
        if (!string.IsNullOrWhiteSpace(pregnancy)
            && !pregnancy.Equals("لا", StringComparison.OrdinalIgnoreCase)
            && !pregnancy.Equals("no", StringComparison.OrdinalIgnoreCase)
            && !pregnancy.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            alerts.Add($"حمل: {pregnancy}");
        }

        return alerts.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
'''
new = '''    internal static List<string> BuildMedicalAlerts(MedicalHistory? history)
    {
        if (history == null) return [];

        var alerts = new List<string>();
        void Add(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                alerts.Add($"{label}: {value.Trim()}");
        }

        Add("حساسية أدوية", history.DrugAllergies);
        Add("أمراض مزمنة", history.ChronicDiseases);
        Add("أدوية حالية", history.CurrentMedications);
        if (history.BleedingDisorders) alerts.Add("اضطرابات نزف");
        if (history.TmjProblems) alerts.Add("مشكلات مفصل الفك");

        // CORE-PAT-031: IsPregnant is a tri-state value (Yes / No / N/A).
        // Treat only explicit affirmative values as a clinical alert. The old
        // negative-list logic classified N/A (لا ينطبق) and unknown legacy values
        // as pregnancy, creating a false safety warning in the patient summary.
        var pregnancy = history.IsPregnant?.Trim();
        if (pregnancy != null && (
            pregnancy.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || pregnancy.Equals("true", StringComparison.OrdinalIgnoreCase)
            || pregnancy.Equals("نعم", StringComparison.OrdinalIgnoreCase)
            || pregnancy.Equals("حامل", StringComparison.OrdinalIgnoreCase)
            || pregnancy.Equals("يوجد حمل", StringComparison.OrdinalIgnoreCase)))
        {
            alerts.Add("حمل");
        }

        return alerts.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
'''
if old not in text:
    raise SystemExit("BuildMedicalAlerts target block not found")
controller.write_text(text.replace(old, new), encoding="utf-8")

test = Path("backend/tests/AqlanDentalPro.UnitTests/Patients/PatientMedicalAlertsTests.cs")
test.parent.mkdir(parents=True, exist_ok=True)
test.write_text('''using AqlanDentalPro.API.Controllers;
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
''', encoding="utf-8")

Path("scripts/core_pat_031_patch.py").unlink(missing_ok=True)
Path(".github/workflows/core-pat-031-bootstrap.yml").unlink(missing_ok=True)
