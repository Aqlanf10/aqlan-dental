using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Visits;

/// <summary>
/// Unit tests for the per-patient access + listing + delete behavior of <see cref="VisitsController"/>.
/// Covers:
///   - GetVisit (GET /api/visits/{id}) — 404 for unknown id, 403 for cross-patient doctor,
///     200 for own patient, admin bypass.
///   - GetVisits (GET /api/visits) — pagination + patientId filter, pageSize clamping,
///     empty result shape.
///   - DeleteVisit (DELETE /api/visits/{id}) — 404, 400 for already-deleted, cross-patient 403,
///     soft-delete + cascade to prescriptions and general treatments (H6 fix).
///
/// Mirror of the TEST-02 <c>SurgeryControllerAccessTests</c> shape.
/// </summary>
public class VisitsControllerAccessTests : IDisposable
{
    private readonly AppDbContext _db;

    public VisitsControllerAccessTests()
    {
        _db = VisitsTestData.CreateDb();
    }

    public void Dispose() => _db.Dispose();

    // ── GetVisit (GET /api/visits/{id}) ──────────────────────────────────────────

    [Fact]
    public async Task GetVisit_UnknownId_Returns404_Arabic()
    {
        var controller = BuildControllerAsAdmin();

        var result = await controller.GetVisit(Guid.NewGuid());

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(404);
        ExtractMessage(notFound.Value).Should().Be("الزيارة غير موجودة");
    }

    [Fact]
    public async Task GetVisit_SamePatientDoctor_Returns200()
    {
        var seeded = await SeedVisitAsync();

        var accessMock = new Mock<IPatientAccessService>();
        VisitsTestData.SetupDoctorWithAccess(accessMock, seeded.PatientId);
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.GeneralDentist);

        var controller = VisitsTestData.BuildController(_db, accessMock, currentUserMock);

        var result = await controller.GetVisit(seeded.VisitId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);

        dynamic payload = ok.Value!;
        ((Guid)payload.Id).Should().Be(seeded.VisitId);
        ((string?)payload.VisitType).Should().Be("معاينة");
    }

    [Fact]
    public async Task GetVisit_CrossPatientDoctor_Returns403_Arabic()
    {
        // Doctor B tries to view a visit belonging to Doctor A's patient.
        var seeded = await SeedVisitAsync();

        var accessMock = new Mock<IPatientAccessService>();
        VisitsTestData.SetupDoctorWithAccess(accessMock); // no accessible patients
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.GeneralDentist);

        var controller = VisitsTestData.BuildController(_db, accessMock, currentUserMock);

        var result = await controller.GetVisit(seeded.VisitId);

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(403);
        ExtractMessage(statusResult.Value).Should().Be("غير مصرح لك بعرض بيانات هذا المريض");
    }

    [Fact]
    public async Task GetVisit_AdminRole_BypassesAccessCheck_Returns200()
    {
        var seeded = await SeedVisitAsync();
        var controller = BuildControllerAsAdmin();

        var result = await controller.GetVisit(seeded.VisitId);

        result.Should().BeOfType<OkObjectResult>();
    }

    // ── GetVisits (GET /api/visits) — listing, filtering, pagination ─────────────

    [Fact]
    public async Task GetVisits_WithPatientIdFilter_ReturnsOnlyMatching()
    {
        var patientA = VisitsTestData.BuildPatient();
        var patientB = VisitsTestData.BuildPatient("خالد", "العولقي");
        _db.Patients.AddRange(patientA, patientB);

        _db.Visits.AddRange(
            VisitsTestData.BuildVisit(patientA.Id),
            VisitsTestData.BuildVisit(patientA.Id),
            VisitsTestData.BuildVisit(patientB.Id));
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var result = await controller.GetVisits(patientId: patientA.Id, page: 1, pageSize: 50);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic payload = ok.Value!;
        var data = (IEnumerable<dynamic>)payload.data;
        data.Count().Should().Be(2, "two visits belong to patientA");
        foreach (var row in data)
            ((Guid)row.PatientId).Should().Be(patientA.Id);
    }

    [Fact]
    public async Task GetVisits_Paginates_WithPageSizeClampedTo100()
    {
        var patient = VisitsTestData.BuildPatient();
        _db.Patients.Add(patient);
        for (var i = 0; i < 5; i++)
            _db.Visits.Add(VisitsTestData.BuildVisit(patient.Id));
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var result = await controller.GetVisits(patientId: null, page: 1, pageSize: 2);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic payload = ok.Value!;
        ((int)payload.total).Should().Be(5);
        ((int)payload.page).Should().Be(1);
        ((int)payload.pageSize).Should().Be(2);
        ((IEnumerable<dynamic>)payload.data).Count().Should().Be(2);
    }

    [Fact]
    public async Task GetVisits_PageSizeOver100_ClampedTo100()
    {
        var controller = BuildControllerAsAdmin();

        var result = await controller.GetVisits(patientId: null, page: 1, pageSize: 500);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic payload = ok.Value!;
        ((int)payload.pageSize).Should().Be(100, "pageSize must be clamped to a maximum of 100");
    }

    [Fact]
    public async Task GetVisits_PageSizeUnder1_NormalizedTo1()
    {
        var controller = BuildControllerAsAdmin();

        var result = await controller.GetVisits(patientId: null, page: 1, pageSize: 0);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic payload = ok.Value!;
        ((int)payload.pageSize).Should().Be(1, "pageSize must be normalized to 1 when below 1");
    }

    [Fact]
    public async Task GetVisits_Empty_ReturnsZeroTotal_AndEmptyData()
    {
        var controller = BuildControllerAsAdmin();

        var result = await controller.GetVisits(patientId: null, page: 1, pageSize: 50);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic payload = ok.Value!;
        ((int)payload.total).Should().Be(0);
        ((IEnumerable<dynamic>)payload.data).Should().BeEmpty();
    }

    // ── DeleteVisit (DELETE /api/visits/{id}) ────────────────────────────────────

    [Fact]
    public async Task DeleteVisit_UnknownId_Returns404_Arabic()
    {
        var controller = BuildControllerAsAdmin();

        var result = await controller.DeleteVisit(Guid.NewGuid());

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(404);
        ExtractMessage(notFound.Value).Should().Be("الزيارة غير موجودة");
    }

    [Fact]
    public async Task DeleteVisit_AlreadySoftDeleted_Returns400_Arabic()
    {
        var seeded = await SeedVisitAsync();
        var visit = await _db.Visits.FirstAsync(v => v.Id == seeded.VisitId);
        visit.IsActive = false;
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var result = await controller.DeleteVisit(seeded.VisitId);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.StatusCode.Should().Be(400);
        ExtractMessage(bad.Value).Should().Be("الزيارة محذوفة بالفعل");
    }

    [Fact]
    public async Task DeleteVisit_CrossPatientDoctor_Returns403_Arabic_DoesNotMutate()
    {
        var seeded = await SeedVisitAsync();

        var accessMock = new Mock<IPatientAccessService>();
        VisitsTestData.SetupDoctorWithAccess(accessMock); // no accessible patients
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.GeneralDentist);

        var controller = VisitsTestData.BuildController(_db, accessMock, currentUserMock);

        var result = await controller.DeleteVisit(seeded.VisitId);

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(403);
        ExtractMessage(statusResult.Value).Should().Be("غير مصرح لك بعرض بيانات هذا المريض");

        _db.ChangeTracker.Clear();
        var stored = await _db.Visits.SingleOrDefaultAsync(v => v.Id == seeded.VisitId);
        stored.Should().NotBeNull();
        stored!.IsActive.Should().BeTrue("a denied Delete must NOT mutate the persisted entity");
        stored.DeletedAt.Should().BeNull();
    }

    [Fact]
    public async Task DeleteVisit_AdminRole_SoftDeletes_Visit_AndCascadesToPrescriptionsAndTreatments()
    {
        // H6 fix: soft-deleting a visit must cascade to its active prescriptions and general
        // treatments so they don't appear in patient records as orphans.
        var seeded = await SeedVisitAsync();
        var prescription = new Prescription
        {
            PatientId = seeded.PatientId,
            VisitId = seeded.VisitId,
            Diagnosis = "تسوس",
            Notes = "وصفة",
            IsActive = true,
            CreatedAt = VisitsTestData.FixedNowUtc,
            UpdatedAt = VisitsTestData.FixedNowUtc
        };
        var treatment = new GeneralTreatment
        {
            PatientId = seeded.PatientId,
            VisitId = seeded.VisitId,
            TreatmentType = "حشو",
            Cost = 3000m,
            IsActive = true,
            CreatedAt = VisitsTestData.FixedNowUtc,
            UpdatedAt = VisitsTestData.FixedNowUtc
        };
        _db.Prescriptions.Add(prescription);
        _db.GeneralTreatments.Add(treatment);
        await _db.SaveChangesAsync();

        var adminUserId = Guid.NewGuid();
        var accessMock = new Mock<IPatientAccessService>();
        VisitsTestData.SetupNonDoctor(accessMock);
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(adminUserId);
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.Admin);
        currentUserMock.SetupGet(c => c.IsAdmin).Returns(true);

        var controller = VisitsTestData.BuildController(_db, accessMock, currentUserMock);

        var result = await controller.DeleteVisit(seeded.VisitId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);

        // The message includes the cascade counts: "تم حذف الزيارة و1 وصفة طبية و1 علاج مرتبط"
        ExtractMessage(ok.Value).Should().Contain("تم حذف الزيارة");
        ExtractMessage(ok.Value).Should().Contain("1 وصفة طبية");
        ExtractMessage(ok.Value).Should().Contain("1 علاج مرتبط");

        _db.ChangeTracker.Clear();

        var storedVisit = await _db.Visits.IgnoreQueryFilters().FirstAsync(v => v.Id == seeded.VisitId);
        storedVisit.IsActive.Should().BeFalse("the visit must be soft-deleted");
        storedVisit.DeletedAt.Should().NotBeNull();
        storedVisit.DeletedBy.Should().Be(adminUserId,
            "DeletedBy must record the user who performed the delete");

        var storedRx = await _db.Prescriptions.IgnoreQueryFilters().FirstAsync(p => p.Id == prescription.Id);
        storedRx.IsActive.Should().BeFalse("H6: the linked prescription must be cascade-soft-deleted");
        storedRx.DeletedAt.Should().NotBeNull();

        var storedTx = await _db.GeneralTreatments.IgnoreQueryFilters().FirstAsync(t => t.Id == treatment.Id);
        storedTx.IsActive.Should().BeFalse("H6: the linked general treatment must be cascade-soft-deleted");
        storedTx.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteVisit_NoCascadeChildren_ReturnsSimpleSuccessMessage()
    {
        var seeded = await SeedVisitAsync();
        var controller = BuildControllerAsAdmin();

        var result = await controller.DeleteVisit(seeded.VisitId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ExtractMessage(ok.Value).Should().Be("تم حذف الزيارة بنجاح",
            "when there are no cascade children, the message is the simple success variant");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<(Guid VisitId, Guid PatientId)> SeedVisitAsync()
    {
        var patient = VisitsTestData.BuildPatient();
        _db.Patients.Add(patient);
        await _db.SaveChangesAsync();

        var visit = VisitsTestData.BuildVisit(patient.Id);
        _db.Visits.Add(visit);
        await _db.SaveChangesAsync();

        return (visit.Id, patient.Id);
    }

    private VisitsController BuildControllerAsAdmin()
    {
        var accessMock = new Mock<IPatientAccessService>();
        VisitsTestData.SetupNonDoctor(accessMock);
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.Admin);
        currentUserMock.SetupGet(c => c.IsAdmin).Returns(true);
        currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        return VisitsTestData.BuildController(_db, accessMock, currentUserMock);
    }

    private static string ExtractMessage(object? value)
    {
        value.Should().NotBeNull("the 4xx response must carry a payload");
        var prop = value!.GetType().GetProperty("message");
        prop.Should().NotBeNull("the 4xx response must carry a 'message' field (Arabic)");
        return (string)prop!.GetValue(value)!;
    }
}
