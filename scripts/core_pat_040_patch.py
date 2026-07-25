from pathlib import Path

helper = Path("backend/src/AqlanDentalPro.Infrastructure/Services/ActivePatientWriteGuard.cs")
helper.write_text('''using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// Canonical guard for creating records that belong to a patient.
/// The AppDbContext global soft-delete filter means this query accepts only
/// an existing, active patient and rejects missing or archived files.
/// </summary>
public static class ActivePatientWriteGuard
{
    public const string ErrorMessage = "لا يمكن إضافة سجل لمريض غير موجود أو مؤرشف";

    public static Task<bool> ExistsAsync(
        AppDbContext db,
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        if (patientId == Guid.Empty) return Task.FromResult(false);

        return db.Patients
            .AsNoTracking()
            .AnyAsync(patient => patient.Id == patientId, cancellationToken);
    }

    public static async Task EnsureAsync(
        AppDbContext db,
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        if (!await ExistsAsync(db, patientId, cancellationToken))
            throw new ArgumentException(ErrorMessage);
    }
}
''', encoding="utf-8")

# API controllers return a stable Arabic 400 before creating any dependent row.
replacements = [
    (
        Path("backend/src/AqlanDentalPro.API/Controllers/LabOrdersController.cs"),
        '''        var denied = await DenyIfDoctorCannotAccess(req.PatientId);
        if (denied is not null) return denied;

        // CON-02 FIX:''',
        '''        var denied = await DenyIfDoctorCannotAccess(req.PatientId);
        if (denied is not null) return denied;

        if (!await ActivePatientWriteGuard.ExistsAsync(db, req.PatientId))
            return BadRequest(new { message = ActivePatientWriteGuard.ErrorMessage });

        // CON-02 FIX:'''
    ),
    (
        Path("backend/src/AqlanDentalPro.API/Controllers/ClinicalPhotosController.cs"),
        '''        var denied = await DenyIfDoctorCannotAccess(req.PatientId);
        if (denied is not null) return denied;

        // CLIN-25: If OrthoCaseId is provided''',
        '''        var denied = await DenyIfDoctorCannotAccess(req.PatientId);
        if (denied is not null) return denied;

        if (!await ActivePatientWriteGuard.ExistsAsync(db, req.PatientId))
            return BadRequest(new { message = ActivePatientWriteGuard.ErrorMessage });

        // CLIN-25: If OrthoCaseId is provided'''
    ),
    (
        Path("backend/src/AqlanDentalPro.API/Controllers/ClinicalPhotosController.cs"),
        '''        var denied = await DenyIfDoctorCannotAccess(req.PatientId);
        if (denied is not null) return denied;

        // Codex review (PR #357):''',
        '''        var denied = await DenyIfDoctorCannotAccess(req.PatientId);
        if (denied is not null) return denied;

        if (!await ActivePatientWriteGuard.ExistsAsync(db, req.PatientId))
            return BadRequest(new { message = ActivePatientWriteGuard.ErrorMessage });

        // Codex review (PR #357):'''
    ),
]

for file, old, new in replacements:
    text = file.read_text(encoding="utf-8")
    if old not in text:
        raise SystemExit(f"target not found in {file}: {old[:80]}")
    file.write_text(text.replace(old, new, 1), encoding="utf-8")

# Infrastructure/application services enforce the same rule for every caller,
# not only the current HTTP controllers.
service_replacements = [
    (
        Path("backend/src/AqlanDentalPro.Infrastructure/Services/PaymentService.cs"),
        '''    public async Task<PaymentDto> CreatePaymentAsync(CreatePaymentRequest req)
    {
        // Require active open cashier session''',
        '''    public async Task<PaymentDto> CreatePaymentAsync(CreatePaymentRequest req)
    {
        await ActivePatientWriteGuard.EnsureAsync(db, req.PatientId);

        // Require active open cashier session'''
    ),
    (
        Path("backend/src/AqlanDentalPro.Infrastructure/Services/ContractService.cs"),
        '''    public async Task<ContractDetailDto> CreateContractAsync(CreateContractRequest req)
    {
        // YOLO-S2: validate the optional package link''',
        '''    public async Task<ContractDetailDto> CreateContractAsync(CreateContractRequest req)
    {
        await ActivePatientWriteGuard.EnsureAsync(db, req.PatientId);

        // YOLO-S2: validate the optional package link'''
    ),
    (
        Path("backend/src/AqlanDentalPro.Infrastructure/Services/OrthoService.cs"),
        '''    public async Task<OrthoCaseDetailDto> CreateAsync(CreateOrthoCaseRequest req)
    {
        const int maxRetries = 3;''',
        '''    public async Task<OrthoCaseDetailDto> CreateAsync(CreateOrthoCaseRequest req)
    {
        await ActivePatientWriteGuard.EnsureAsync(db, req.PatientId);

        const int maxRetries = 3;'''
    ),
]

for file, old, new in service_replacements:
    text = file.read_text(encoding="utf-8")
    if old not in text:
        raise SystemExit(f"service target not found in {file}")
    file.write_text(text.replace(old, new, 1), encoding="utf-8")

test = Path("backend/tests/AqlanDentalPro.UnitTests/Patients/ActivePatientWriteGuardTests.cs")
test.parent.mkdir(parents=True, exist_ok=True)
test.write_text('''using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AqlanDentalPro.UnitTests.Patients;

public class ActivePatientWriteGuardTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"active-patient-guard-{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task ExistsAsync_AcceptsAnActivePatient()
    {
        await using var db = CreateDb();
        var patient = new Patient
        {
            PatientNumber = "PT-ACTIVE",
            FirstName = "Active",
            LastName = "Patient",
            IsActive = true,
        };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var exists = await ActivePatientWriteGuard.ExistsAsync(db, patient.Id);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_RejectsAnArchivedPatientThroughTheGlobalFilter()
    {
        await using var db = CreateDb();
        var patient = new Patient
        {
            PatientNumber = "PT-ARCHIVED",
            FirstName = "Archived",
            LastName = "Patient",
            IsActive = false,
        };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var exists = await ActivePatientWriteGuard.ExistsAsync(db, patient.Id);

        exists.Should().BeFalse();
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("11111111-1111-1111-1111-111111111111")]
    public async Task EnsureAsync_RejectsMissingOrEmptyPatientIds(string value)
    {
        await using var db = CreateDb();
        var patientId = Guid.Parse(value);

        var action = () => ActivePatientWriteGuard.EnsureAsync(db, patientId);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage(ActivePatientWriteGuard.ErrorMessage);
    }
}
''', encoding="utf-8")

contract = Path("backend/tests/AqlanDentalPro.UnitTests/Patients/PatientDependentWriteGuardContractTests.cs")
contract.write_text('''using FluentAssertions;
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
''', encoding="utf-8")

Path("scripts/core_pat_040_patch.py").unlink(missing_ok=True)
Path(".github/workflows/core-pat-040-bootstrap.yml").unlink(missing_ok=True)
