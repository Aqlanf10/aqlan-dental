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

namespace AqlanDentalPro.UnitTests.Surgery;

/// <summary>
/// SEC-ROUTE access-control tests for <see cref="SurgeryController"/> route-id-bound mutations.
///
/// Coverage map:
///   - Update_ByDoctorWithoutAccess_Returns403:
///       PUT /api/surgery-cases/{id} — fetches by {id} route, never checked the caller owns the patient.
///       SEC-ROUTE fix: explicit DenyIfDoctorCannotAccess(surgery.PatientId) after fetch, before
///       mutation. 403 + Arabic message + audit LogAsync + no mutation persisted.
///   - UpsertPreop_ByDoctorWithoutAccess_Returns403:
///       PUT /api/surgery-cases/{id}/preop — same pattern.
///   - UpsertPostop_ByDoctorWithoutAccess_Returns403:
///       PUT /api/surgery-cases/{id}/postop — same pattern.
///   - CreateReferral_ByDoctorWithoutAccess_Returns403:
///       POST /api/surgery-cases/{id}/referrals — same pattern.
///   - ApproveOperativeReport_ByDoctorWithoutAccess_Returns403:
///       PUT /api/surgery-cases/{id}/operative/approve — patient resolved via parent surgery case.
///
/// Mirrors TEST-02/TEST-13 access-test patterns (DocumentsControllerAccessTests,
/// LabOrdersControllerRouteAccessTests). Found by SEC-DOCS (PR #506) out-of-scope audit.
/// </summary>
public class SurgeryControllerRouteAccessTests : IDisposable
{
    private readonly AppDbContext _db;

    public SurgeryControllerRouteAccessTests()
    {
        _db = SurgeryTestData.CreateDb();
    }

    public void Dispose() => _db.Dispose();

    // ── Update (PUT /api/surgery-cases/{id}) — SEC-ROUTE access ──────────────────

    [Fact]
    public async Task Update_ByDoctorWithoutAccess_Returns403()
    {
        // SEC-ROUTE: Update now resolves PatientId from the fetched surgery and calls
        // DenyIfDoctorCannotAccess(surgery.PatientId). A cross-patient doctor gets 403 +
        // Arabic message + audit log + NO mutation. The fetched surgery is returned
        // unchanged from the DB.
        var seeded = await SeedSurgeryAsync();
        var originalSurgeryType = (await _db.SurgeryCases.FindAsync(seeded.SurgeryId))!.SurgeryType;

        var accessMock = new Mock<IPatientAccessService>();
        SurgeryTestData.SetupDoctorWithAccess(accessMock); // no accessible patients
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.OralSurgeon);
        var auditMock = new Mock<IAuditService>();

        var controller = SurgeryTestData.BuildController(_db, accessMock, currentUserMock, auditMock);

        var result = await controller.Update(seeded.SurgeryId, new UpdateSurgeryCaseRequest
        {
            SurgeryType = "محاولة تعديل غير مصرح بها"
        });

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(403);
        ExtractMessage(statusResult.Value).Should().Be("غير مصرح لك بعرض بيانات هذا المريض");

        accessMock.Verify(p => p.CanAccessPatientAsync(seeded.PatientId), Times.Once,
            "Update must call CanAccessPatientAsync with the surgery's patientId");

        auditMock.Verify(
            a => a.LogAsync(
                AuditAction.View,
                "Patient",
                seeded.PatientId,
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            Times.Once,
            "a 403 denial on Update must produce an audit log entry");

        _db.ChangeTracker.Clear();
        var stored = await _db.SurgeryCases.FindAsync(seeded.SurgeryId);
        stored!.SurgeryType.Should().Be(originalSurgeryType,
            "a 403 denial must NOT mutate the surgery (no SurgeryType update)");
    }

    // ── UpsertPreop (PUT /api/surgery-cases/{id}/preop) — SEC-ROUTE access ───────

    [Fact]
    public async Task UpsertPreop_ByDoctorWithoutAccess_Returns403()
    {
        // SEC-ROUTE: UpsertPreop now resolves PatientId from the fetched surgery and calls
        // DenyIfDoctorCannotAccess(surgery.PatientId). A cross-patient doctor gets 403 +
        // Arabic message + audit log + NO PreopReport created.
        var seeded = await SeedSurgeryAsync();

        var accessMock = new Mock<IPatientAccessService>();
        SurgeryTestData.SetupDoctorWithAccess(accessMock);
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.OralSurgeon);
        var auditMock = new Mock<IAuditService>();

        var controller = SurgeryTestData.BuildController(_db, accessMock, currentUserMock, auditMock);

        var result = await controller.UpsertPreop(seeded.SurgeryId, new UpsertPreopRequest
        {
            SurgeryLocation = "محاولة غير مصرح بها"
        });

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(403);
        ExtractMessage(statusResult.Value).Should().Be("غير مصرح لك بعرض بيانات هذا المريض");

        accessMock.Verify(p => p.CanAccessPatientAsync(seeded.PatientId), Times.Once,
            "UpsertPreop must call CanAccessPatientAsync with the surgery's patientId");

        auditMock.Verify(
            a => a.LogAsync(
                AuditAction.View,
                "Patient",
                seeded.PatientId,
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            Times.Once,
            "a 403 denial on UpsertPreop must produce an audit log entry");

        (await _db.PreopReports.CountAsync(p => p.SurgeryCaseId == seeded.SurgeryId))
            .Should().Be(0, "a 403 denial must NOT create a PreopReport");
    }

    // ── UpsertPostop (PUT /api/surgery-cases/{id}/postop) — SEC-ROUTE access ─────

    [Fact]
    public async Task UpsertPostop_ByDoctorWithoutAccess_Returns403()
    {
        var seeded = await SeedSurgeryAsync();

        var accessMock = new Mock<IPatientAccessService>();
        SurgeryTestData.SetupDoctorWithAccess(accessMock);
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.OralSurgeon);
        var auditMock = new Mock<IAuditService>();

        var controller = SurgeryTestData.BuildController(_db, accessMock, currentUserMock, auditMock);

        var result = await controller.UpsertPostop(seeded.SurgeryId, new UpsertPostopRequest
        {
            Instructions = "محاولة غير مصرح بها"
        });

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(403);
        ExtractMessage(statusResult.Value).Should().Be("غير مصرح لك بعرض بيانات هذا المريض");

        accessMock.Verify(p => p.CanAccessPatientAsync(seeded.PatientId), Times.Once);

        (await _db.PostopRecords.CountAsync(p => p.SurgeryCaseId == seeded.SurgeryId))
            .Should().Be(0, "a 403 denial must NOT create a PostopRecord");
    }

    // ── CreateReferral (POST /api/surgery-cases/{id}/referrals) — SEC-ROUTE access ─

    [Fact]
    public async Task CreateReferral_ByDoctorWithoutAccess_Returns403()
    {
        var seeded = await SeedSurgeryAsync();

        var accessMock = new Mock<IPatientAccessService>();
        SurgeryTestData.SetupDoctorWithAccess(accessMock);
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.OralSurgeon);
        var auditMock = new Mock<IAuditService>();

        var controller = SurgeryTestData.BuildController(_db, accessMock, currentUserMock, auditMock);

        var result = await controller.CreateReferral(seeded.SurgeryId, new CreateHospitalReferralRequest
        {
            HospitalName = "محاولة غير مصرح بها"
        });

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(403);
        ExtractMessage(statusResult.Value).Should().Be("غير مصرح لك بعرض بيانات هذا المريض");

        accessMock.Verify(p => p.CanAccessPatientAsync(seeded.PatientId), Times.Once);

        (await _db.HospitalReferrals.CountAsync(r => r.SurgeryCaseId == seeded.SurgeryId))
            .Should().Be(0, "a 403 denial must NOT create a HospitalReferral");
    }

    // ── ApproveOperativeReport (PUT /api/surgery-cases/{id}/operative/approve) ──

    [Fact]
    public async Task ApproveOperativeReport_ByDoctorWithoutAccess_Returns403()
    {
        // SEC-ROUTE: ApproveOperativeReport resolves PatientId via the parent surgery case
        // (the operative report itself has no PatientId column). A cross-patient doctor
        // gets 403 + Arabic message + audit log + NO ApprovedAt set.
        var seeded = await SeedSurgeryWithOperativeReportAsync();

        var accessMock = new Mock<IPatientAccessService>();
        SurgeryTestData.SetupDoctorWithAccess(accessMock);
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.OralSurgeon);
        var auditMock = new Mock<IAuditService>();

        var controller = SurgeryTestData.BuildController(_db, accessMock, currentUserMock, auditMock);

        var result = await controller.ApproveOperativeReport(seeded.SurgeryId);

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(403);
        ExtractMessage(statusResult.Value).Should().Be("غير مصرح لك بعرض بيانات هذا المريض");

        accessMock.Verify(p => p.CanAccessPatientAsync(seeded.PatientId), Times.Once,
            "ApproveOperativeReport must resolve the patient via the surgery case and call CanAccessPatientAsync");

        auditMock.Verify(
            a => a.LogAsync(
                AuditAction.View,
                "Patient",
                seeded.PatientId,
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            Times.Once,
            "a 403 denial on ApproveOperativeReport must produce an audit log entry");

        _db.ChangeTracker.Clear();
        var stored = await _db.OperativeReports.FirstAsync(r => r.SurgeryCaseId == seeded.SurgeryId);
        stored.ApprovedAt.Should().BeNull("a 403 denial must NOT set ApprovedAt");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<(Guid SurgeryId, Guid PatientId)> SeedSurgeryAsync()
    {
        var patient = SurgeryTestData.BuildPatient();
        _db.Patients.Add(patient);
        await _db.SaveChangesAsync();

        var surgery = SurgeryTestData.BuildSurgeryCase(patient.Id);
        _db.SurgeryCases.Add(surgery);
        await _db.SaveChangesAsync();

        return (surgery.Id, patient.Id);
    }

    private async Task<(Guid SurgeryId, Guid PatientId)> SeedSurgeryWithOperativeReportAsync()
    {
        var seeded = await SeedSurgeryAsync();
        var report = SurgeryTestData.BuildOperativeReport(seeded.SurgeryId);
        _db.OperativeReports.Add(report);
        await _db.SaveChangesAsync();
        return seeded;
    }

    private static string ExtractMessage(object? value)
    {
        value.Should().NotBeNull("the 4xx response must carry a payload");
        var prop = value!.GetType().GetProperty("message");
        prop.Should().NotBeNull("the 4xx response must carry a 'message' field (Arabic)");
        return (string)prop!.GetValue(value)!;
    }
}
