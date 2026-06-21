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

namespace AqlanDentalPro.UnitTests.Referrals;

/// <summary>
/// CLIN-24 regression tests:
/// 1. Accept — only the ToDoctor (or an Admin) can accept. Non-ToDoctor → 403.
/// 2. Accept — the ToDoctor succeeds (200 + status=accepted).
/// 3. Complete — only the ToDoctor (or an Admin) can complete. Non-ToDoctor → 403.
/// 4. GetById — unknown id returns 404 with the Arabic existence message.
/// </summary>
public class ReferralOwnershipTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    /// <summary>
    /// Builds an ICurrentUserService mock. Tests set IsAdmin=false so the ToDoctor
    /// ownership check is exercised (admins bypass it).
    /// </summary>
    private static ICurrentUserService CreateCurrentUser(
        Guid? userId,
        UserRole role = UserRole.GeneralDentist,
        bool isAdmin = false)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(u => u.UserId).Returns(userId);
        mock.Setup(u => u.Role).Returns(role);
        mock.Setup(u => u.IsAdmin).Returns(isAdmin);
        mock.Setup(u => u.IsAuthenticated).Returns(true);
        return mock.Object;
    }

    /// <summary>
    /// Non-doctor PatientAccessService (full access) so the per-patient
    /// DenyIfDoctorCannotAccess short-circuit doesn't mask the ToDoctor check.
    /// </summary>
    private static IPatientAccessService CreatePatientAccess()
    {
        var mock = new Mock<IPatientAccessService>();
        mock.Setup(p => p.IsDoctor).Returns(false);
        mock.Setup(p => p.HasFullAccess).Returns(true);
        mock.Setup(p => p.IsReception).Returns(false);
        mock.Setup(p => p.CanAccessPatientAsync(It.IsAny<Guid>())).ReturnsAsync(true);
        mock.Setup(p => p.GetCurrentDoctorIdAsync()).ReturnsAsync((Guid?)null);
        mock.Setup(p => p.GetAccessiblePatientIdsAsync()).ReturnsAsync((HashSet<Guid>?)null);
        return mock.Object;
    }

    private static ReferralsController BuildController(
        AppDbContext db,
        ICurrentUserService currentUser)
    {
        var audit = new Mock<IAuditService>().Object;
        var patientAccess = CreatePatientAccess();
        return new ReferralsController(db, currentUser, patientAccess, audit);
    }

    /// <summary>Seeds Patient + 2 Doctors + a pending referral from D1 → D2.</summary>
    private static async Task<(Patient patient, Doctor fromDoctor, Doctor toDoctor, InternalReferral referral)>
        SeedPendingReferralAsync(AppDbContext db, string status = "pending")
    {
        var fromUser = Guid.NewGuid();
        var toUser = Guid.NewGuid();

        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            PatientNumber = "P-R",
            FirstName = "نورا",
            LastName = "عبدالله",
            IsActive = true
        };

        var fromDoctor = new Doctor
        {
            Id = Guid.NewGuid(),
            UserId = fromUser,
            Name = "د. المُحيل",
            IsActive = true
        };

        var toDoctor = new Doctor
        {
            Id = Guid.NewGuid(),
            UserId = toUser,
            Name = "د. المستقبِل",
            IsActive = true
        };

        var referral = new InternalReferral
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            FromDoctorId = fromDoctor.Id,
            ToDoctorId = toDoctor.Id,
            Reason = "consultation",
            Priority = "normal",
            Status = status,
            IsActive = true
        };

        db.Patients.Add(patient);
        db.Doctors.AddRange(fromDoctor, toDoctor);
        db.InternalReferrals.Add(referral);
        await db.SaveChangesAsync();

        return (patient, fromDoctor, toDoctor, referral);
    }

    // ── Test 1: Accept by a non-ToDoctor → 403 ───────────────────────────────

    [Fact]
    public async Task Accept_ByNonToDoctor_Returns403()
    {
        // Arrange
        await using var db = CreateDb();
        var (_, fromDoctor, toDoctor, referral) = await SeedPendingReferralAsync(db);

        // currentUser is the FromDoctor (NOT the ToDoctor) — must be forbidden.
        var controller = BuildController(db, CreateCurrentUser(userId: fromDoctor.UserId));

        // Act
        var result = await controller.Accept(referral.Id);

        // Assert
        var forbid = result.Should().BeOfType<ObjectResult>().Subject;
        forbid.StatusCode.Should().Be(403);
        var msg = forbid.Value!.GetType().GetProperty("message")!.GetValue(forbid.Value) as string;
        msg.Should().NotBeNullOrEmpty("an Arabic 403 message is required");
        // The Arabic message mentions "المستقبِل" (the recipient doctor) — assert on
        // that word rather than "الطبيب المستقبِل" because Arabic prefix rules drop the
        // alef of the article when "لـ" (for) is attached, producing "للطبيب المستقبِل",
        // so the literal substring "الطبيب المستقبِل" is not present.
        msg.Should().Contain("المستقبِل", "message must explain only the ToDoctor can accept");

        // State must be unchanged
        var unchanged = await db.InternalReferrals.SingleAsync(r => r.Id == referral.Id);
        unchanged.Status.Should().Be("pending", "a forbidden Accept must not mutate state");
        unchanged.AcceptedAt.Should().BeNull();
    }

    // ── Test 2: Accept by the ToDoctor → 200 + status=accepted ───────────────

    [Fact]
    public async Task Accept_ByToDoctor_Returns200()
    {
        // Arrange
        await using var db = CreateDb();
        var (_, fromDoctor, toDoctor, referral) = await SeedPendingReferralAsync(db);

        // currentUser is the ToDoctor — allowed.
        var controller = BuildController(db, CreateCurrentUser(userId: toDoctor.UserId));

        // Act
        var result = await controller.Accept(referral.Id);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);

        var saved = await db.InternalReferrals.SingleAsync(r => r.Id == referral.Id);
        saved.Status.Should().Be("accepted");
        saved.AcceptedAt.Should().NotBeNull();
    }

    // ── Test 3: Complete by a non-ToDoctor → 403 ─────────────────────────────

    [Fact]
    public async Task Complete_ByNonToDoctor_Returns403()
    {
        // Arrange
        await using var db = CreateDb();
        var (_, fromDoctor, toDoctor, referral) =
            await SeedPendingReferralAsync(db, status: "accepted");

        // currentUser is the FromDoctor (NOT the ToDoctor) — must be forbidden.
        var controller = BuildController(db, CreateCurrentUser(userId: fromDoctor.UserId));

        // Act
        var result = await controller.Complete(referral.Id);

        // Assert
        var forbid = result.Should().BeOfType<ObjectResult>().Subject;
        forbid.StatusCode.Should().Be(403);
        var msg = forbid.Value!.GetType().GetProperty("message")!.GetValue(forbid.Value) as string;
        msg.Should().NotBeNullOrEmpty("an Arabic 403 message is required");
        // See Accept_ByNonToDoctor_Returns403 for why we assert on "المستقبِل"
        // rather than "الطبيب المستقبِل".
        msg.Should().Contain("المستقبِل", "message must explain only the ToDoctor can complete");

        // State must be unchanged
        var unchanged = await db.InternalReferrals.SingleAsync(r => r.Id == referral.Id);
        unchanged.Status.Should().Be("accepted", "a forbidden Complete must not mutate state");
    }

    // ── Test 4: GetById with unknown id → 404 + Arabic existence message ─────

    [Fact]
    public async Task GetById_UnknownId_Returns404_ArabicMessage()
    {
        // Arrange
        await using var db = CreateDb();
        var controller = BuildController(db, CreateCurrentUser(userId: Guid.NewGuid()));

        // Act
        var result = await controller.GetById(Guid.NewGuid());

        // Assert
        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(404);
        var msg = notFound.Value!.GetType().GetProperty("message")!.GetValue(notFound.Value) as string;
        msg.Should().Be("الإحالة غير موجودة",
            "the existence-check 404 must use the canonical Arabic message");
    }
}
