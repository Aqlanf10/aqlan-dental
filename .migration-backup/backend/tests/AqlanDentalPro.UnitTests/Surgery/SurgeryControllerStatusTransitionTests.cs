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
/// SURG-FIX (resolved): <see cref="UpdateSurgeryStatusRequestValidator"/> accepts snake_case
///   strings ("scheduled", "in_progress", "completed", "cancelled", "postponed") in its
///   ValidStatuses HashSet, and the frontend sends exactly those (e.g. surgery/page.tsx calls
///   handleStatus(c.id, "in_progress")). The controller now normalizes the string
///   (removes underscores) before <c>Enum.TryParse&lt;SurgeryCaseStatus&gt;</c>, so "in_progress"
///   → "inprogress" → parses to InProgress (case-insensitive). Before SURG-FIX, the parse
///   failed (Enum.TryParse doesn't handle underscores) and the Scheduled→InProgress transition
///   was UNREACHABLE — every surgery-start attempt returned 400 "حالة الجراحة غير صالحة".
///   See <see cref="UpdateStatus_SnakeCase_InProgress_Returns200"/> +
///   <see cref="UpdateStatus_SnakeCase_AllStatuses_ParseCorrectly"/> for the regression coverage.
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

    // ── SURG-FIX: snake_case status now parses (was 400, now 200) ────────────────

    [Fact]
    public async Task UpdateStatus_SnakeCase_InProgress_Returns200()
    {
        // SURG-FIX regression: the FluentValidation rule accepts snake_case ("in_progress")
        // and the controller now normalizes it (removes the underscore) before
        // Enum.TryParse<SurgeryCaseStatus>(..., true, ...). Previously Enum.TryParse rejected
        // "in_progress" (no underscore handling) and the controller returned 400
        // "حالة الجراحة غير صالحة" instead of performing the Scheduled→InProgress transition.
        // This test pins the FIXED behavior: "in_progress" now returns 200 OK.
        var seeded = await SeedSurgeryAsync(SurgeryCaseStatus.Scheduled);
        var controller = BuildControllerAsAdmin();

        var result = await controller.UpdateStatus(
            seeded.SurgeryId,
            new UpdateSurgeryStatusRequest { Status = "in_progress" });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200,
            "SURG-FIX: snake_case 'in_progress' must parse to InProgress and perform the transition");
    }

    [Fact]
    public async Task UpdateStatus_SnakeCase_InProgress_FromScheduled_Returns200_AndPersists()
    {
        // SURG-FIX full happy path: the production-critical transition (Scheduled → InProgress)
        // was unreachable before the fix because the frontend only sends "in_progress" (snake_case)
        // and the old Enum.TryParse rejected it. Verify the full round-trip:
        //   1. 200 OK response
        //   2. Persisted enum is InProgress (not Scheduled)
        //   3. Response body echoes the original snake_case status string (frontend contract)
        var seeded = await SeedSurgeryAsync(SurgeryCaseStatus.Scheduled);
        var controller = BuildControllerAsAdmin();

        var result = await controller.UpdateStatus(
            seeded.SurgeryId,
            new UpdateSurgeryStatusRequest { Status = "in_progress" });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);

        // The controller echoes req.Status verbatim — frontend depends on receiving snake_case back.
        var statusProp = ok.Value!.GetType().GetProperty("status");
        statusProp.Should().NotBeNull("UpdateStatus response must carry a 'status' field");
        ((string?)statusProp!.GetValue(ok.Value)).Should().Be("in_progress",
            "the response echoes the original snake_case string (frontend contract)");

        // Persisted enum must be InProgress (proves the snake_case string actually parsed).
        _db.ChangeTracker.Clear();
        var stored = await _db.SurgeryCases.SingleAsync(s => s.Id == seeded.SurgeryId);
        stored.Status.Should().Be(SurgeryCaseStatus.InProgress,
            "'in_progress' must persist as the InProgress enum value, not stay Scheduled");
    }

    [Theory]
    // Each row pairs a snake_case input (what the frontend sends) with a valid prior status
    // (so the CLIN-03 transition validator allows the move) and the expected enum.
    [InlineData("scheduled",  SurgeryCaseStatus.Postponed, SurgeryCaseStatus.Scheduled)]   // Postponed → Scheduled
    [InlineData("in_progress", SurgeryCaseStatus.Scheduled,  SurgeryCaseStatus.InProgress)]  // Scheduled → InProgress (the production-breaking one)
    [InlineData("completed",  SurgeryCaseStatus.InProgress, SurgeryCaseStatus.Completed)]  // InProgress → Completed
    [InlineData("cancelled",  SurgeryCaseStatus.Scheduled,  SurgeryCaseStatus.Cancelled)]  // Scheduled → Cancelled
    [InlineData("postponed",  SurgeryCaseStatus.Scheduled,  SurgeryCaseStatus.Postponed)]  // Scheduled → Postponed
    public async Task UpdateStatus_SnakeCase_AllStatuses_ParseCorrectly(
        string snakeCaseStatus, SurgeryCaseStatus priorStatus, SurgeryCaseStatus expectedEnum)
    {
        // SURG-FIX: parameterized coverage that ALL 5 snake_case statuses parse to the correct
        // enum. Before the fix only 4 of 5 worked (the underscore-free ones); "in_progress" was
        // the sole breakage. This guards against a future regression on any of the 5.
        var seeded = await SeedSurgeryAsync(priorStatus);
        var controller = BuildControllerAsAdmin();

        var result = await controller.UpdateStatus(
            seeded.SurgeryId,
            new UpdateSurgeryStatusRequest { Status = snakeCaseStatus });

        result.Should().BeOfType<OkObjectResult>(
            "snake_case '{0}' must parse to {1} (prior={2}) and the transition must be allowed",
            snakeCaseStatus, expectedEnum, priorStatus);

        _db.ChangeTracker.Clear();
        var stored = await _db.SurgeryCases.SingleAsync(s => s.Id == seeded.SurgeryId);
        stored.Status.Should().Be(expectedEnum,
            "snake_case '{0}' must persist as {1}", snakeCaseStatus, expectedEnum);
    }

    [Fact]
    public async Task UpdateStatus_SnakeCase_TrulyInvalidString_StillReturns400()
    {
        // SURG-FIX safety net: normalization must NOT loosen the validation for truly-invalid
        // strings. "nonsense" has no underscores and doesn't match any enum name, so it must
        // still return 400 "حالة الجراحة غير صالحة". (The companion 'frozen' test below covers
        // the same path; this one uses a different invalid value to be explicit that the fix
        // didn't accidentally accept garbage.)
        var seeded = await SeedSurgeryAsync(SurgeryCaseStatus.Scheduled);
        var controller = BuildControllerAsAdmin();

        var result = await controller.UpdateStatus(
            seeded.SurgeryId,
            new UpdateSurgeryStatusRequest { Status = "nonsense" });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.StatusCode.Should().Be(400);
        var message = ExtractMessage(bad.Value);
        message.Should().Be("حالة الجراحة غير صالحة",
            "truly-invalid strings (no underscore, not an enum name) must still be rejected");

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
