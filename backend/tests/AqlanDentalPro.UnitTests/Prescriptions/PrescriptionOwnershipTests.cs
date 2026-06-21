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

namespace AqlanDentalPro.UnitTests.Prescriptions;

/// <summary>
/// CLIN-23 regression tests:
/// 1. Visit cross-patient ownership guard — a doctor cannot attach a VisitId belonging
///    to a different patient to a new prescription.
/// 2. Happy path — a valid VisitId on the same patient is preserved.
/// 3. DoctorId resolution — when the client omits DoctorId, the controller must resolve
///    the Doctor row via Doctors.UserId (NOT write currentUser.UserId directly, which
///    would violate the Prescription.DoctorId → Doctors.Id FK).
/// </summary>
public class PrescriptionOwnershipTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    /// <summary>
    /// Builds an ICurrentUserService mock. Defaults: Admin role, IsAdmin=true,
    /// IsAuthenticated=true. Admin role bypasses the per-patient access check so the
    /// tests focus only on the CLIN-23 ownership/FK checks.
    /// </summary>
    private static ICurrentUserService CreateCurrentUser(
        Guid? userId,
        UserRole role = UserRole.Admin,
        bool isAdmin = true)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(u => u.UserId).Returns(userId);
        mock.Setup(u => u.Role).Returns(role);
        mock.Setup(u => u.IsAdmin).Returns(isAdmin);
        mock.Setup(u => u.IsAuthenticated).Returns(true);
        return mock.Object;
    }

    /// <summary>
    /// Non-doctor PatientAccessService (admin) — returns true for any patient.
    /// Avoids the per-patient DenyIfDoctorCannotAccess short-circuit so the test
    /// reaches the VisitId ownership check.
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

    private static PrescriptionsController BuildController(
        AppDbContext db,
        ICurrentUserService currentUser)
    {
        var audit = new Mock<IAuditService>().Object;
        var patientAccess = CreatePatientAccess();
        return new PrescriptionsController(db, currentUser, patientAccess, audit);
    }

    private static CreatePrescriptionRequest MakeRequest(
        Guid patientId,
        Guid? visitId = null,
        Guid? doctorId = null) => new()
        {
            PatientId = patientId,
            VisitId = visitId,
            DoctorId = doctorId,
            Diagnosis = "gingivitis",
            Drugs = new List<DrugItem>
            {
                new()
                {
                    Name = "Amoxicillin",
                    Dose = "500mg",
                    Frequency = "3x/day",
                    Duration = "7 days"
                }
            },
            Notes = "after meals"
        };

    // ── Test 1: Visit belonging to a different patient → 400 ──────────────────

    [Fact]
    public async Task Create_WithVisitBelongingToDifferentPatient_Returns400()
    {
        // Arrange
        await using var db = CreateDb();

        var patientA = new Patient
        {
            Id = Guid.NewGuid(),
            PatientNumber = "P-A",
            FirstName = "أحمد",
            LastName = "سعيد",
            IsActive = true
        };
        var patientB = new Patient
        {
            Id = Guid.NewGuid(),
            PatientNumber = "P-B",
            FirstName = "محمد",
            LastName = "علي",
            IsActive = true
        };
        db.Patients.AddRange(patientA, patientB);

        // Visit belongs to Patient B (NOT Patient A)
        var visitForB = new Visit
        {
            Id = Guid.NewGuid(),
            PatientId = patientB.Id,
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow),
            IsActive = true
        };
        db.Visits.Add(visitForB);
        await db.SaveChangesAsync();

        var controller = BuildController(db, CreateCurrentUser(userId: Guid.NewGuid()));

        // Act — attach Patient B's visit to a prescription for Patient A
        var result = await controller.Create(MakeRequest(patientA.Id, visitId: visitForB.Id));

        // Assert
        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.StatusCode.Should().Be(400);
        var msg = bad.Value!.GetType().GetProperty("message")!.GetValue(bad.Value) as string;
        msg.Should().NotBeNullOrEmpty("an Arabic ownership error message is required");
        msg.Should().Contain("المريض", "error message must mention the patient mismatch");

        // No prescription should have been persisted
        var anyPrescription = await db.Prescriptions.AnyAsync();
        anyPrescription.Should().BeFalse("a rejected Create must not write to the DB");
    }

    // ── Test 2: Valid Visit on the same patient → 201 + VisitId persisted ─────

    [Fact]
    public async Task Create_WithValidVisit_Returns201()
    {
        // Arrange
        await using var db = CreateDb();

        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            PatientNumber = "P-1",
            FirstName = "سالم",
            LastName = "ناصر",
            IsActive = true
        };
        db.Patients.Add(patient);

        var visit = new Visit
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow),
            IsActive = true
        };
        db.Visits.Add(visit);
        await db.SaveChangesAsync();

        var controller = BuildController(db, CreateCurrentUser(userId: Guid.NewGuid()));

        // Act
        var result = await controller.Create(MakeRequest(patient.Id, visitId: visit.Id));

        // Assert
        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.StatusCode.Should().Be(201);
        created.ActionName.Should().Be(nameof(PrescriptionsController.GetById));

        var saved = await db.Prescriptions.SingleAsync();
        saved.PatientId.Should().Be(patient.Id);
        saved.VisitId.Should().Be(visit.Id, "the valid VisitId must be persisted on the prescription");
    }

    // ── Test 3: DoctorId resolved from Doctors.UserId (not Users.Id) ──────────

    [Fact]
    public async Task Create_ResolvesDoctorIdFromDoctorsUserId_NotUserId()
    {
        // Arrange
        await using var db = CreateDb();

        var userId = Guid.NewGuid();   // Users.Id
        var doctorId = Guid.NewGuid(); // Doctors.Id — deliberately different from userId

        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            PatientNumber = "P-2",
            FirstName = "خالد",
            LastName = "حسن",
            IsActive = true
        };
        db.Patients.Add(patient);

        db.Doctors.Add(new Doctor
        {
            Id = doctorId,
            UserId = userId,            // the link that the controller must look up
            Name = "د. عقلان",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db, CreateCurrentUser(userId: userId));

        // Act — request omits DoctorId; controller must resolve it via Doctors.UserId
        var result = await controller.Create(MakeRequest(patient.Id, visitId: null, doctorId: null));

        // Assert
        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.StatusCode.Should().Be(201);

        var saved = await db.Prescriptions.SingleAsync();
        saved.DoctorId.Should().Be(doctorId,
            "DoctorId must be the Doctors.Id resolved via Doctors.UserId lookup — writing the " +
            "Users.Id directly would violate the Prescription.DoctorId → Doctors.Id FK");
        saved.DoctorId.Should().NotBe(userId,
            "the FK violation bug wrote currentUser.UserId (Users.Id) as DoctorId — " +
            "the fix must resolve through Doctors.UserId instead");
    }
}
