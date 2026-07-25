using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ortho;

public class ActiveOrthoCaseConsumerContractTests
{
    [Fact]
    public void PatientSummaryDashboardAndJourney_UseCanonicalActiveCasesQuery()
    {
        var root = FindRepositoryRoot();
        var files = new[]
        {
            Path.Combine(root, "backend", "src", "AqlanDentalPro.API", "Controllers", "PatientsController.cs"),
            Path.Combine(root, "backend", "src", "AqlanDentalPro.Infrastructure", "Services", "DashboardService.cs"),
            Path.Combine(root, "backend", "src", "AqlanDentalPro.Infrastructure", "Services", "PatientJourneyService.cs"),
        };

        foreach (var file in files)
            File.ReadAllText(file).Should().Contain(".ActiveCases()", file);

        File.ReadAllText(files[2]).Should().NotContain(
            ".Where(o => o.PatientId == patientId && o.IsActive)",
            "soft-delete alone is not a clinical active-case definition");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null && !Directory.Exists(Path.Combine(current.FullName, "backend", "src")))
            current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found");
    }
}
