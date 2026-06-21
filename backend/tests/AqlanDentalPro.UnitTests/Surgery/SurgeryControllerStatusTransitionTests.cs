using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
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
/// Unit tests for <see cref="SurgeryController.UpdateStatus"/> (PUT /api/surgery-cases/{id}/status).
///
/// Validates the CLIN-03 fix: status transitions are validated against
/// <see cref="SurgeryCaseStatusTransitions"/> before being applied. Terminal states
/// (Completed, Cancelled) cannot be moved out of. The Arabic error message returned
/// by the transition validator is propagated verbatim to the HTTP 400 response.
///
/// Transition map (from <see cref="SurgeryCaseStatusTransitions"/>):
///   Scheduled   → InProgress, Cancelled, Postponed
///   InProgress  → Completed, Cancelled
///   Completed   → (terminal)
///   Cancelled   → (terminal)
///   Postponed   → Scheduled, Cancelled
///
/// FINDING #1 (documented in PR description — NOT fixed here, test-only PR):
///   <see cref="UpdateSurgeryStatusRequestValidator"/> accepts snake_case strings
///   ("scheduled", "in_progress", "completed", "cancelled", "postponed") in its
///   ValidStatuses HashSet. But the controller's <c>Enum.TryParse&lt;SurgeryCaseStatus&gt;(req.Status, true, ...)</c>
///   only matches PascalCase enum names (Enum.TryParse is case-insensitive but does NOT
///   handle underscores). The result: of the 5 statuses, only 4 actually round-trip —
///   "in_progress" passes FluentValidation but then fails enum parse in the controller,
///   returning <c>BadRequest("حالة الجراحة غير صالحة")</c> instead of performing the
///   Scheduled→InProgress transition. Production users CANNOT move a surgery case into
///   InProgress via this endpoint. The tests below use the PascalCase form ("InProgress")
///   to exercise the transition logic; see <see cref="UpdateStatus_SnakeCase_InProgress_Returns400_EnumParseFails"/>
///   for the explicit regression test on this bug.
/// </summary>
public class SurgeryControllerStatusTransitionTests : IDisposable
{
    private readonly AppDbContext _db;

    public SurgeryControllerStatusTransitionTests()
    {
        _db = SurgeryTestData.CreateDb();
    }

    public void Dispose() => _db.Dispose();

    // ── Happy-path forward transitions ───────────────────────────────────────────

    [Fact]
    public async Task UpdateStatus_Scheduled_To_InProgress_Returns200_AndPersists()
    {
        var seeded = await SeedSurgeryAsync(SurgeryCaseStatus.Scheduled);
        var controller = BuildControllerAsAdmin();

        var result = await controller.UpdateStatus(
            seeded.SurgeryId,
            new UpdateSurgeryStatusRequest { Status = "InProgress" });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);

        // Verify the status was actually persisted
        _db.ChangeTracker.Clear();
        var stored = await _db.SurgeryCases.SingleAsync(s => s.Id == seeded.SurgeryId);
        stored.Status.Should().Be(SurgeryCaseStatus.InProgress,
            "UpdateStatus must persist the new status on a valid forward transition");
    }

    [Fact]
    public async Task UpdateStatus_InProgress_To_Completed_Returns200_AndPersists()
    {
        var seeded = await SeedSurgeryAsync(SurgeryCaseStatus.InProgress);
        var controller = BuildControllerAsAdmin();

        var result = await controller.UpdateStatus(
            seeded.SurgeryId,
            new UpdateSurgeryStatusRequest { Status = "completed" });

        result.Should().BeOfType<OkObjectResult>();
        _db.ChangeTracker.Clear();
        var stored = await _db.SurgeryCases.SingleAsync(s => s.Id == seeded.SurgeryId);
        stored.Status.Should().Be(SurgeryCaseStatus.Completed);
    }

    [Fact]
    public async Task UpdateStatus_Scheduled_To_Cancelled_Returns200_AndPersists()
    {
        var seeded = await SeedSurgeryAsync(SurgeryCaseStatus.Scheduled);
        var controller = BuildControllerAsAdmin();

        var result = await controller.UpdateStatus(
            seeded.SurgeryId,
            new UpdateSurgeryStatusRequest { Status = "cancelled" });

        result.Should().BeOfType<OkObjectResult>();
        _db.ChangeTracker.Clear();
        var stored = await _db.SurgeryCases.SingleAsync(s => s.Id == seeded.SurgeryId);
        stored.Status.Should().Be(SurgeryCaseStatus.Cancelled);
    }

    [Fact]
    public async Task UpdateStatus_Scheduled_To_Postponed_Returns200_AndPersists()
    {
        var seeded = await SeedSurgeryAsync(SurgeryCaseStatus.Scheduled);
        var controller = BuildControllerAsAdmin();

        var result = await controller.UpdateStatus(
            seeded.SurgeryId,
            new UpdateSurgeryStatusRequest { Status = "postponed" });

        result.Should().BeOfType<OkObjectResult>();
        _db.ChangeTracker.Clear();
        var stored = await _db.SurgeryCases.SingleAsync(s => s.Id == seeded.SurgeryId);
        stored.Status.Should().Be(SurgeryCaseStatus.Postponed);
    }

    [Fact]
    public async Task UpdateStatus_Postponed_To_Scheduled_Returns200_AndPersists()
    {
        var seeded = await SeedSurgeryAsync(SurgeryCaseStatus.Postponed);
        var controller = BuildControllerAsAdmin();

        var result = await controller.UpdateStatus(
            seeded.SurgeryId,
            new UpdateSurgeryStatusRequest { Status = "scheduled" });

        result.Should().BeOfType<OkObjectResult>();
        _db.ChangeTracker.Clear();
        var stored = await _db.SurgeryCases.SingleAsync(s => s.Id == seeded.SurgeryId);
        stored.Status.Should().Be(SurgeryCaseStatus.Scheduled);
    }

    [Fact]
    public async Task UpdateStatus_InProgress_To_Cancelled_Returns200_AndPersists()
    {
        var seeded = await SeedSurgeryAsync(SurgeryCaseStatus.InProgress);
        var controller = BuildControllerAsAdmin();

        var result = await controller.UpdateStatus(
            seeded.SurgeryId,
            new UpdateSurgeryStatusRequest { Status = "cancelled" });

        result.Should().BeOfType<OkObjectResult>();
        _db.ChangeTracker.Clear();
        var stored = await _db.SurgeryCases.SingleAsync(s => s.Id == seeded.SurgeryId);
        stored.Status.Should().Be(SurgeryCaseStatus.Cancelled);
    }

    // ── Idempotent same-status transition (always allowed) ───────────────────────

    [Fact]
    public async Task UpdateStatus_SameStatus_Returns200_Idempotent()
    {
        var seeded = await SeedSurgeryAsync(SurgeryCaseStatus.Scheduled);
        var controller = BuildControllerAsAdmin();

        var result = await controller.UpdateStatus(
            seeded.SurgeryId,
            new UpdateSurgeryStatusRequest { Status = "scheduled" });

        result.Should().BeOfType<OkObjectResult>();
    }

    // ── Terminal state — no transitions out ──────────────────────────────────────

    [Fact]
    public async Task UpdateStatus_Completed_To_InProgress_Returns400_ArabicMessage()
    {
        var seeded = await SeedSurgeryAsync(SurgeryCaseStatus.Completed);
        var controller = BuildControllerAsAdmin();

        var result = await controller.UpdateStatus(
            seeded.SurgeryId,
            new UpdateSurgeryStatusRequest { Status = "InProgress" });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.StatusCode.Should().Be(400);

        var message = ExtractMessage(bad.Value);
        message.Should().Contain("لا يمكن تغيير حالة الجراحة",
            "the transition validator's Arabic error must be propagated to the 400 response");
        message.Should().Contain("مكتملة", "the current-status label must be in the message");
        message.Should().Contain("قيد التنفيذ", "the target-status label must be in the message");

        // Status must remain unchanged
        _db.ChangeTracker.Clear();
        var stored = await _db.SurgeryCases.SingleAsync(s => s.Id == seeded.SurgeryId);
        stored.Status.Should().Be(SurgeryCaseStatus.Completed,
            "a rejected transition must NOT mutate the persisted status");
    }

    [Fact]
    public async Task UpdateStatus_Completed_To_Scheduled_Returns400_ArabicMessage()
    {
        var seeded = await SeedSurgeryAsync(SurgeryCaseStatus.Completed);
        var controller = BuildControllerAsAdmin();

        var result = await controller.UpdateStatus(
            seeded.SurgeryId,
            new UpdateSurgeryStatusRequest { Status = "scheduled" });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var message = ExtractMessage(bad.Value);
        message.Should().Contain("لا يمكن تغيير حالة الجراحة");
        message.Should().Contain("مكتملة");
        message.Should().Contain("مقررة");
    }

    [Theory]
    [InlineData("Scheduled")]   // Cancelled→Scheduled: invalid transition
    [InlineData("InProgress")]  // Cancelled→InProgress: invalid transition
    [InlineData("Completed")]   // Cancelled→Completed: invalid transition
    [InlineData("Postponed")]   // Cancelled→Postponed: invalid transition
    public async Task UpdateStatus_Cancelled_To_Anything_Returns400_ArabicMessage(string targetStatus)
    {
        var seeded = await SeedSurgeryAsync(SurgeryCaseStatus.Cancelled);
        var controller = BuildControllerAsAdmin();

        var result = await controller.UpdateStatus(
            seeded.SurgeryId,
            new UpdateSurgeryStatusRequest { Status = targetStatus });

        // Same-status (Cancelled→Cancelled) is idempotent and allowed — not part of this Theory.
        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.StatusCode.Should().Be(400);
        var message = ExtractMessage(bad.Value);
        message.Should().Contain("لا يمكن تغيير حالة الجراحة");
        message.Should().Contain("ملغاة", "current-status label must be 'ملغاة'");
    }

    [Fact]
    public async Task UpdateStatus_InProgress_To_Postponed_Returns400_ArabicMessage()
    {
        // CLIN-03 explicitly disallows InProgress → Postponed (clinically meaningless).
        var seeded = await SeedSurgeryAsync(SurgeryCaseStatus.InProgress);
        var controller = BuildControllerAsAdmin();

        var result = await controller.UpdateStatus(
            seeded.SurgeryId,
            new UpdateSurgeryStatusRequest { Status = "postponed" });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var message = ExtractMessage(bad.Value);
        message.Should().Contain("لا يمكن تغيير حالة الجراحة");
        message.Should().Contain("قيد التنفيذ");
        message.Should().Contain("مؤجلة");
    }

    // ── Invalid status enum ──────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStatus_SnakeCase_InProgress_Returns400_EnumParseFails()
    {
        // FINDING #1 regression test: the FluentValidation rule accepts snake_case
        // ("in_progress") but the controller's Enum.TryParse<SurgeryCaseStatus>(..., true, ...)
        // does NOT handle underscores. So "in_progress" reaches the controller, fails enum
        // parse, and returns 400 "حالة الجراحة غير صالحة" instead of performing the
        // Scheduled→InProgress transition. See the PR description for the full bug analysis.
        //
        // This test pins the current (buggy) behavior so a future fix to either layer —
        // aligning the validator's HashSet with the enum's PascalCase names, OR adding a
        // snake_case enum converter — must explicitly update this test, preventing silent
        // regressions in either direction.
        var seeded = await SeedSurgeryAsync(SurgeryCaseStatus.Scheduled);
        var controller = BuildControllerAsAdmin();

        var result = await controller.UpdateStatus(
            seeded.SurgeryId,
            new UpdateSurgeryStatusRequest { Status = "in_progress" });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.StatusCode.Should().Be(400);
        var message = ExtractMessage(bad.Value);
        message.Should().Be("حالة الجراحة غير صالحة",
            "Enum.TryParse rejects 'in_progress' (underscore), so the controller returns " +
            "the enum-parse error rather than the transition error or a 200 OK");

        // Status must remain unchanged
        _db.ChangeTracker.Clear();
        var stored = await _db.SurgeryCases.SingleAsync(s => s.Id == seeded.SurgeryId);
        stored.Status.Should().Be(SurgeryCaseStatus.Scheduled,
            "a rejected UpdateStatus must NOT mutate the persisted status");
    }

    [Fact]
    public async Task UpdateStatus_InvalidStatusEnum_Returns400_ArabicMessage()
    {
        var seeded = await SeedSurgeryAsync(SurgeryCaseStatus.Scheduled);
        var controller = BuildControllerAsAdmin();

        var result = await controller.UpdateStatus(
            seeded.SurgeryId,
            new UpdateSurgeryStatusRequest { Status = "frozen" });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var message = ExtractMessage(bad.Value);
        message.Should().Be("حالة الجراحة غير صالحة");
    }

    // ── Unknown surgery case id → 404 ────────────────────────────────────────────

    [Fact]
    public async Task UpdateStatus_UnknownCaseId_Returns404_Arabic()
    {
        var controller = BuildControllerAsAdmin();
        var unknownId = Guid.NewGuid();

        var result = await controller.UpdateStatus(
            unknownId,
            new UpdateSurgeryStatusRequest { Status = "InProgress" });

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(404);
        var message = ExtractMessage(notFound.Value);
        message.Should().Be("الحالة الجراحية غير موجودة");
    }

    // ── Per-patient access check (CLIN-01) ──────────────────────────────────────

    [Fact]
    public async Task UpdateStatus_CrossPatientDoctor_Returns403_ArabicMessage()
    {
        var seeded = await SeedSurgeryAsync(SurgeryCaseStatus.Scheduled);

        var accessMock = new Mock<IPatientAccessService>();
        SurgeryTestData.SetupDoctorWithAccess(accessMock); // NO accessible patients → cross-patient
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.OralSurgeon);

        var controller = SurgeryTestData.BuildController(_db, accessMock, currentUserMock);

        var result = await controller.UpdateStatus(
            seeded.SurgeryId,
            new UpdateSurgeryStatusRequest { Status = "InProgress" });

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(403);
        var message = ExtractMessage(statusResult.Value);
        message.Should().Be("غير مصرح لك بعرض بيانات هذا المريض");

        // Status must remain unchanged
        _db.ChangeTracker.Clear();
        var stored = await _db.SurgeryCases.SingleAsync(s => s.Id == seeded.SurgeryId);
        stored.Status.Should().Be(SurgeryCaseStatus.Scheduled,
            "an access-denied UpdateStatus must NOT mutate the persisted status");
    }

    [Fact]
    public async Task UpdateStatus_DoctorWithPatientAccess_Returns200_AndPersists()
    {
        var seeded = await SeedSurgeryAsync(SurgeryCaseStatus.Scheduled);

        var accessMock = new Mock<IPatientAccessService>();
        SurgeryTestData.SetupDoctorWithAccess(accessMock, seeded.PatientId);
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.OralSurgeon);

        var controller = SurgeryTestData.BuildController(_db, accessMock, currentUserMock);

        var result = await controller.UpdateStatus(
            seeded.SurgeryId,
            new UpdateSurgeryStatusRequest { Status = "InProgress" });

        result.Should().BeOfType<OkObjectResult>();
        _db.ChangeTracker.Clear();
        var stored = await _db.SurgeryCases.SingleAsync(s => s.Id == seeded.SurgeryId);
        stored.Status.Should().Be(SurgeryCaseStatus.InProgress);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<(Guid SurgeryId, Guid PatientId)> SeedSurgeryAsync(SurgeryCaseStatus initialStatus)
    {
        var patient = SurgeryTestData.BuildPatient();
        _db.Patients.Add(patient);
        await _db.SaveChangesAsync();

        var surgery = SurgeryTestData.BuildSurgeryCase(patient.Id, status: initialStatus);
        _db.SurgeryCases.Add(surgery);
        await _db.SaveChangesAsync();

        return (surgery.Id, patient.Id);
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
