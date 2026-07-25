using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Patients;

public class PatientDependentWriteGuardContractTests
{
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "backend")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found");
    }

    [Theory]
    [InlineData("backend/src/AqlanDentalPro.API/Controllers/LabOrdersController.cs")]
    [InlineData("backend/src/AqlanDentalPro.API/Controllers/ClinicalPhotosController.cs")]
    public void BodyBasedControllers_UseTheCanonicalActivePatientGuard(string relativePath)
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath));

        source.Should().Contain("ActivePatientWriteGuard.ExistsAsync");
        source.Should().Contain("ActivePatientWriteGuard.ErrorMessage");
    }

    [Theory]
    [InlineData("backend/src/AqlanDentalPro.Infrastructure/Services/PaymentService.cs")]
    [InlineData("backend/src/AqlanDentalPro.Infrastructure/Services/ContractService.cs")]
    [InlineData("backend/src/AqlanDentalPro.Infrastructure/Services/OrthoService.cs")]
    public void ServiceWrites_EnforceTheCanonicalGuardForEveryCaller(string relativePath)
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath));

        source.Should().Contain("ActivePatientWriteGuard.EnsureAsync(db, req.PatientId)");
    }
}
