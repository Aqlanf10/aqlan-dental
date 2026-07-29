using System.Text.Json;
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
/// Unit tests for the sub-resource endpoints of <see cref="SurgeryController"/>:
///   - UpsertPreop / GetPreop           (PUT/GET /api/surgery-cases/{id}/preop)
///   - UpsertPostop / GetPostop         (PUT/GET /api/surgery-cases/{id}/postop)
///   - UpsertOperativeReport / GetOperativeReport
///                                        (PUT/GET /api/surgery-cases/{id}/operative)
///   - ApproveOperativeReport           (PUT /api/surgery-cases/{id}/operative/approve)
///   - Hospital referrals CRUD          (/api/surgery-cases/{id}/referrals)
///
/// FINDING #2 (documented in PR description):
///   The audit hinted "ApproveOperativeReport_ByNonAuthorizedRole_Returns403 (if restricted)"
///   — ApproveOperativeReport is NOT role-restricted (any SurgeryAccess role can approve).
///   Additionally, <see cref="OperativeReport"/> has an <c>ApprovedAt</c> field but NO
///   <c>ApprovedBy</c> field — there is no audit trail of WHO approved the report. The
///   audit-trail gap means a malicious doctor could approve their own report and the audit
///   log would not record who did it. SEE FINDING #2 in PR description (follow-up ticket).
/// </summary>
public class SurgeryControllerOperativeReportTests : IDisposable
{
    private readonly AppDbContext _db;

    public SurgeryControllerOperativeReportTests()
    {
        _db = SurgeryTestData.CreateDb();
    }

    public void Dispose() => _db.Dispose();

    // ── ApproveOperativeReport ───────────────────────────────────────────────────

    [Fact]
    public async Task ApproveOperativeReport_UnknownSurgeryCaseId_Returns404_Arabic()
    {
        var controller = BuildControllerAsAdmin();

        var result = await controller.ApproveOperativeReport(Guid.NewGuid());

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(404);
        ExtractMessage(notFound.Value).Should().Be("التقرير الجراحي غير موجود");
    }

    [Fact]
    public async Task ApproveOperativeReport_WhenReportExists_SetsApprovedAt_AndReturns200()
    {
        var seeded = await SeedSurgeryWithOperativeReportAsync();

        var controller = BuildControllerAsAdmin();

        // ApprovedAt must be null before approval
        (await _db.OperativeReports.FirstAsync(r => r.SurgeryCaseId == seeded.SurgeryId))
            .ApprovedAt.Should().BeNull();

        var result = await controller.ApproveOperativeReport(seeded.SurgeryId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ExtractMessage(ok.Value).Should().Be("تم اعتماد التقرير");

        // Verify the response payload includes the ApprovedAt ISO timestamp
        var approvedAtProp = ok.Value!.GetType().GetProperty("ApprovedAt");
        approvedAtProp.Should().NotBeNull("the response must echo back the ApprovedAt timestamp");
        var approvedAtStr = (string?)approvedAtProp!.GetValue(ok.Value);
        approvedAtStr.Should().NotBeNullOrEmpty();

        // Verify the report was persisted with ApprovedAt set
        _db.ChangeTracker.Clear();
        var stored = await _db.OperativeReports.FirstAsync(r => r.SurgeryCaseId == seeded.SurgeryId);
        stored.ApprovedAt.Should().NotBeNull("ApproveOperativeReport must persist ApprovedAt");
        stored.ApprovedAt!.Value.Kind.Should().Be(DateTimeKind.Utc,
            "ApprovedAt must be stored as UTC (controller uses DateTime.UtcNow)");
    }

    [Fact]
    public async Task ApproveOperativeReport_IsIdempotent_CallingTwiceDoesNotThrow()
    {
        // Calling approve twice should not error — the second call just overwrites ApprovedAt.
        var seeded = await SeedSurgeryWithOperativeReportAsync();

        var controller = BuildControllerAsAdmin();

        var first = await controller.ApproveOperativeReport(seeded.SurgeryId);
        first.Should().BeOfType<OkObjectResult>();

        var second = await controller.ApproveOperativeReport(seeded.SurgeryId);
        second.Should().BeOfType<OkObjectResult>("re-approving an already-approved report must be idempotent");
    }

    [Fact]
    public async Task ApproveOperativeReport_NoReportForSurgery_Returns404_Arabic()
    {
        // Surgery case exists, but no operative report has been created yet.
        var seeded = await SeedSurgeryAsync();
        var controller = BuildControllerAsAdmin();

        var result = await controller.ApproveOperativeReport(seeded.SurgeryId);

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        ExtractMessage(notFound.Value).Should().Be("التقرير الجراحي غير موجود");
    }

    // ── UpsertPreop ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertPreop_UnknownSurgeryCaseId_Returns404_Arabic()
    {
        var controller = BuildControllerAsAdmin();

        var result = await controller.UpsertPreop(Guid.NewGuid(), new UpsertPreopRequest());

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        ExtractMessage(notFound.Value).Should().Be("الحالة غير موجودة");
    }

    [Fact]
    public async Task UpsertPreop_WithValidPayload_CreatesReport_AndPersists()
    {
        var seeded = await SeedSurgeryAsync();
        var (doctor, _) = await SeedDoctorAsync();

        var controller = BuildControllerAsAdmin();

        var req = new UpsertPreopRequest
        {
            SurgeryDate = "2026-06-20",
            SurgeryLocation = "غرفة العمليات 1",
            AnesthesiaType = "موضعي",
            ConsentSigned = true,
            DoctorId = doctor.Id,
            Checklist = new Dictionary<string, bool>
            {
                ["fasting"] = true,
                ["consent_signed"] = true,
                ["blood_pressure"] = true
            },
            RequiredTests = new List<string> { "CBC", "PT", "INR" }
        };

        var result = await controller.UpsertPreop(seeded.SurgeryId, req);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ExtractMessage(ok.Value).Should().Be("تم الحفظ");

        // NOTE: EF InMemory does NOT round-trip JsonDocument columns reliably after a
        // ChangeTracker.Clear() + reload (the JsonDocument reference is lost when the
        // InMemory store clones the entity row). For the JSON-typed fields (Checklist,
        // RequiredTests), we query the tracked entity BEFORE clearing — the controller's
        // SaveChangesAsync has already applied the values to the tracked instance, so we
        // can assert on the in-memory JSON content. The scalar fields are asserted after
        // Clear+reload to prove actual persistence to the InMemory store.
        var tracked = await _db.PreopReports.SingleAsync(p => p.SurgeryCaseId == seeded.SurgeryId);
        tracked.Checklist.Should().NotBeNull("the checklist dictionary must be serialized to JSON");
        tracked.RequiredTests.Should().NotBeNull("the required-tests list must be serialized to JSON");

        var checklistDict = JsonSerializer.Deserialize<Dictionary<string, bool>>(tracked.Checklist!.RootElement.GetRawText());
        checklistDict!["fasting"].Should().BeTrue();
        checklistDict!["blood_pressure"].Should().BeTrue();
        var testsList = JsonSerializer.Deserialize<List<string>>(tracked.RequiredTests!.RootElement.GetRawText());
        testsList.Should().BeEquivalentTo(new[] { "CBC", "PT", "INR" });

        // Scalar fields survive the InMemory reload — verify persistence separately.
        _db.ChangeTracker.Clear();
        var stored = await _db.PreopReports.SingleAsync(p => p.SurgeryCaseId == seeded.SurgeryId);
        stored.SurgeryDate.Should().Be(DateOnly.Parse("2026-06-20"));
        stored.SurgeryLocation.Should().Be("غرفة العمليات 1");
        stored.AnesthesiaType.Should().Be("موضعي");
        stored.ConsentSigned.Should().BeTrue();
        stored.DoctorId.Should().Be(doctor.Id);
    }

    [Fact]
    public async Task UpsertPreop_UpdatesExisting_WhenReportAlreadyExists()
    {
        var seeded = await SeedSurgeryAsync();
        var existing = SurgeryTestData.BuildPreopReport(seeded.SurgeryId);
        _db.PreopReports.Add(existing);
        await _db.SaveChangesAsync();
        var existingId = existing.Id;

        var controller = BuildControllerAsAdmin();

        var req = new UpsertPreopRequest
        {
            SurgeryDate = "2026-07-01",
            SurgeryLocation = "غرفة 2",
            AnesthesiaType = "عام",
            ConsentSigned = false
        };

        var result = await controller.UpsertPreop(seeded.SurgeryId, req);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var idProp = ok.Value!.GetType().GetProperty("Id");
        ((Guid)idProp!.GetValue(ok.Value)!).Should().Be(existingId,
            "UpsertPreop must reuse the existing PreopReport row, not create a new one");

        _db.ChangeTracker.Clear();
        var stored = await _db.PreopReports.SingleAsync(p => p.SurgeryCaseId == seeded.SurgeryId);
        stored.SurgeryLocation.Should().Be("غرفة 2");
        stored.AnesthesiaType.Should().Be("عام");
        stored.SurgeryDate.Should().Be(DateOnly.Parse("2026-07-01"));
    }

    [Fact]
    public async Task GetPreop_ReturnsNull_WhenNoReportExists()
    {
        var seeded = await SeedSurgeryAsync();
        var controller = BuildControllerAsAdmin();

        var result = await controller.GetPreop(seeded.SurgeryId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeNull("GetPreop returns Ok(null) when no preop report exists");
    }

    [Fact]
    public async Task GetPreop_ReturnsReport_WhenExists()
    {
        var seeded = await SeedSurgeryAsync();
        var (doctor, _) = await SeedDoctorAsync();
        var report = SurgeryTestData.BuildPreopReport(seeded.SurgeryId, doctor.Id);
        _db.PreopReports.Add(report);
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var result = await controller.GetPreop(seeded.SurgeryId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic payload = ok.Value!;
        ((string)payload.SurgeryLocation).Should().Be("غرفة العمليات 1");
        ((string)payload.DoctorName).Should().Be(doctor.Name);
        ((bool)payload.ConsentSigned).Should().BeTrue();
    }

    // ── UpsertOperativeReport ────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertOperativeReport_UnknownSurgeryCaseId_Returns404_Arabic()
    {
        var controller = BuildControllerAsAdmin();

        var result = await controller.UpsertOperativeReport(Guid.NewGuid(), new UpsertOperativeReportRequest());

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        ExtractMessage(notFound.Value).Should().Be("الحالة غير موجودة");
    }

    [Fact]
    public async Task UpsertOperativeReport_WithValidPayload_CreatesReport_AndPersists()
    {
        var seeded = await SeedSurgeryAsync();
        var (doctor, _) = await SeedDoctorAsync();

        var controller = BuildControllerAsAdmin();

        var req = new UpsertOperativeReportRequest
        {
            SurgeryDateTime = "2026-06-15T10:30",
            DurationMinutes = 60,
            AnesthesiaUsed = "موضعي مع седATION",
            Technique = "خلع جراحي مع تقطيب",
            DetailedDescription = "تفاصيل كاملة للجراحة",
            Outcome = "ناجحة",
            Complications = "لا يوجد",
            SuturesCount = 4,
            SpecimenSent = true,
            DoctorId = doctor.Id
        };

        var result = await controller.UpsertOperativeReport(seeded.SurgeryId, req);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ExtractMessage(ok.Value).Should().Be("تم الحفظ");

        _db.ChangeTracker.Clear();
        var stored = await _db.OperativeReports.SingleAsync(r => r.SurgeryCaseId == seeded.SurgeryId);
        stored.SurgeryDateTime.Should().NotBeNull();
        stored.DurationMinutes.Should().Be(60);
        stored.AnesthesiaUsed.Should().Be("موضعي مع седATION");
        stored.Technique.Should().Be("خلع جراحي مع تقطيب");
        stored.DetailedDescription.Should().Be("تفاصيل كاملة للجراحة");
        stored.Outcome.Should().Be("ناجحة");
        stored.Complications.Should().Be("لا يوجد");
        stored.SuturesCount.Should().Be(4);
        stored.SpecimenSent.Should().BeTrue();
        stored.DoctorId.Should().Be(doctor.Id);
        stored.ApprovedAt.Should().BeNull("a freshly-created report must not be pre-approved");
    }

    [Fact]
    public async Task GetOperativeReport_ReturnsApprovedAt_WhenApproved()
    {
        var seeded = await SeedSurgeryAsync();
        var (doctor, _) = await SeedDoctorAsync();
        var report = SurgeryTestData.BuildOperativeReport(seeded.SurgeryId, doctor.Id);
        report.ApprovedAt = SurgeryTestData.FixedNowUtc;
        _db.OperativeReports.Add(report);
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var result = await controller.GetOperativeReport(seeded.SurgeryId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic payload = ok.Value!;
        ((string?)payload.ApprovedAt).Should().NotBeNull("ApprovedAt must be returned when set");
    }

    [Fact]
    public async Task GetOperativeReport_ReturnsNull_WhenNoReportExists()
    {
        var seeded = await SeedSurgeryAsync();
        var controller = BuildControllerAsAdmin();

        var result = await controller.GetOperativeReport(seeded.SurgeryId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeNull();
    }

    // ── UpsertPostop ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertPostop_WithValidPayload_PersistsPrescriptionAndFollowup()
    {
        var seeded = await SeedSurgeryAsync();

        var controller = BuildControllerAsAdmin();

        var req = new UpsertPostopRequest
        {
            Instructions = "تناول المسكنات كل 8 ساعات وتجنب المضغ على الجانب الأيمن",
            Prescription = new List<PrescriptionItemDto>
            {
                new() { Medicine = "أموكسيسيلين", Dosage = "500mg", Frequency = "كل 8 ساعات", Duration = "7 أيام" },
                new() { Medicine = "بروفين", Dosage = "400mg", Frequency = "كل 8 ساعات", Duration = "5 أيام" }
            },
            FollowupSchedule = new List<FollowupItemDto>
            {
                new() { Date = "2026-06-22", Notes = "إزالة الغرز" },
                new() { Date = "2026-07-15", Notes = "متابعة" }
            }
        };

        var result = await controller.UpsertPostop(seeded.SurgeryId, req);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ExtractMessage(ok.Value).Should().Be("تم الحفظ");

        // NOTE: EF InMemory does NOT round-trip JsonDocument columns reliably after a
        // ChangeTracker.Clear() + reload (see UpsertPreop_WithValidPayload_CreatesReport_AndPersists
        // for the full explanation). For the JSON-typed fields (Prescription, FollowupSchedule),
        // we query the tracked entity BEFORE clearing — the controller's SaveChangesAsync has
        // already applied the values to the tracked instance, so we can assert on the JSON content.
        var tracked = await _db.PostopRecords.SingleAsync(p => p.SurgeryCaseId == seeded.SurgeryId);
        tracked.Instructions.Should().Contain("المسكنات");
        tracked.Prescription.Should().NotBeNull();
        tracked.FollowupSchedule.Should().NotBeNull();

        var rx = JsonSerializer.Deserialize<List<PrescriptionItemDto>>(tracked.Prescription!.RootElement.GetRawText());
        rx.Should().HaveCount(2);
        rx![0].Medicine.Should().Be("أموكسيسيلين");

        var followups = JsonSerializer.Deserialize<List<FollowupItemDto>>(tracked.FollowupSchedule!.RootElement.GetRawText());
        followups.Should().HaveCount(2);
        followups![0].Date.Should().Be("2026-06-22");

        // Scalar fields survive the InMemory reload — verify persistence separately.
        _db.ChangeTracker.Clear();
        var stored = await _db.PostopRecords.SingleAsync(p => p.SurgeryCaseId == seeded.SurgeryId);
        stored.Instructions.Should().Contain("المسكنات");
    }

    [Fact]
    public async Task GetPostop_ReturnsNull_WhenNoRecordExists()
    {
        var seeded = await SeedSurgeryAsync();
        var controller = BuildControllerAsAdmin();

        var result = await controller.GetPostop(seeded.SurgeryId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeNull();
    }

    // ── Hospital Referrals ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateReferral_UnknownSurgeryCaseId_Returns404_Arabic()
    {
        var controller = BuildControllerAsAdmin();

        var result = await controller.CreateReferral(Guid.NewGuid(), new CreateHospitalReferralRequest());

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        ExtractMessage(notFound.Value).Should().Be("الحالة غير موجودة");
    }

    [Fact]
    public async Task CreateReferral_WithValidPayload_Persists_WithPendingStatus()
    {
        var seeded = await SeedSurgeryAsync();
        var controller = BuildControllerAsAdmin();

        var req = new CreateHospitalReferralRequest
        {
            HospitalName = "مستشفى الثورة",
            Reason = "حالة معقدة تتطلب تخديرًا عامًا",
            ReferralDate = "2026-06-18",
            Notes = "تحويل عاجل"
        };

        var result = await controller.CreateReferral(seeded.SurgeryId, req);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ExtractMessage(ok.Value).Should().Be("تم إنشاء الإحالة");

        _db.ChangeTracker.Clear();
        var stored = await _db.HospitalReferrals.SingleAsync(r => r.SurgeryCaseId == seeded.SurgeryId);
        stored.HospitalName.Should().Be("مستشفى الثورة");
        stored.Reason.Should().Be("حالة معقدة تتطلب تخديرًا عامًا");
        stored.ReferralDate.Should().Be(DateOnly.Parse("2026-06-18"));
        stored.Status.Should().Be("pending", "new referrals must start in the pending state");
        stored.Notes.Should().Be("تحويل عاجل");
        stored.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetReferrals_ReturnsEmpty_WhenNoneExist()
    {
        var seeded = await SeedSurgeryAsync();
        var controller = BuildControllerAsAdmin();

        var result = await controller.GetReferrals(seeded.SurgeryId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ((IEnumerable<object>)ok.Value!).Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateReferral_UnknownReferralId_Returns404_Arabic()
    {
        var seeded = await SeedSurgeryAsync();
        var controller = BuildControllerAsAdmin();

        var result = await controller.UpdateReferral(seeded.SurgeryId, Guid.NewGuid(), new UpdateHospitalReferralRequest());

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        ExtractMessage(notFound.Value).Should().Be("الإحالة غير موجودة");
    }

    [Fact]
    public async Task UpdateReferral_UpdatesStatus_AndPersists()
    {
        var seeded = await SeedSurgeryAsync();
        var referral = new HospitalReferral
        {
            SurgeryCaseId = seeded.SurgeryId,
            HospitalName = "مستشفى الثورة",
            Status = "pending",
            IsActive = true
        };
        _db.HospitalReferrals.Add(referral);
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var result = await controller.UpdateReferral(seeded.SurgeryId, referral.Id, new UpdateHospitalReferralRequest
        {
            Status = "completed",
            Notes = "تم الاستلام"
        });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ExtractMessage(ok.Value).Should().Be("تم تحديث الإحالة");

        _db.ChangeTracker.Clear();
        var stored = await _db.HospitalReferrals.FirstAsync(r => r.Id == referral.Id);
        stored.Status.Should().Be("completed");
        stored.Notes.Should().Be("تم الاستلام");
    }

    [Fact]
    public async Task DeleteReferral_SoftDeletesReferral()
    {
        var seeded = await SeedSurgeryAsync();
        var referral = new HospitalReferral
        {
            SurgeryCaseId = seeded.SurgeryId,
            HospitalName = "مستشفى الثورة",
            Status = "pending",
            IsActive = true
        };
        _db.HospitalReferrals.Add(referral);
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var result = await controller.DeleteReferral(seeded.SurgeryId, referral.Id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ExtractMessage(ok.Value).Should().Be("تم حذف الإحالة");

        _db.ChangeTracker.Clear();
        var stored = await _db.HospitalReferrals.IgnoreQueryFilters().FirstAsync(r => r.Id == referral.Id);
        stored.IsActive.Should().BeFalse("DeleteReferral must soft-delete the referral");
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

    private async Task<(Doctor Doctor, User User)> SeedDoctorAsync(string name = "د. جراح")
    {
        var (user, doctor) = SurgeryTestData.BuildDoctor(name: name);
        _db.Users.Add(user);
        _db.Doctors.Add(doctor);
        await _db.SaveChangesAsync();
        return (doctor, user);
    }

    private SurgeryController BuildControllerAsAdmin()
    {
        var accessMock = new Mock<IPatientAccessService>();
        SurgeryTestData.SetupNonDoctor(accessMock);
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.Admin);
        currentUserMock.SetupGet(c => c.IsAdmin).Returns(true);
        return SurgeryTestData.BuildController(_db, accessMock, currentUserMock);
    }

    private static string ExtractMessage(object? value)
    {
        value.Should().NotBeNull("the 4xx response must carry a payload");
        var prop = value!.GetType().GetProperty("message");
        prop.Should().NotBeNull("the 4xx response must carry a 'message' field (Arabic)");
        return (string)prop!.GetValue(value)!;
    }
}
