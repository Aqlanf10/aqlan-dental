using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace AqlanDentalPro.UnitTests.Surgery;

/// <summary>
/// Shared seed + builder helpers for SurgeryController unit tests.
/// Mirrors the pattern in <see cref="AqlanDentalPro.UnitTests.Ortho.OrthoModelAnalysesControllerTests"/>
/// (EF InMemory + Mock&lt;IPatientAccessService&gt; + Mock&lt;ICurrentUserService&gt;).
/// All dates are fixed (no DateTime.Now) so tests stay deterministic.
/// </summary>
internal static class SurgeryTestData
{
    /// <summary>
    /// Fixed UTC timestamp used everywhere a "now" value is needed in tests.
    /// 2026-06-15T10:30:00Z — chosen to avoid month/year boundaries that would
    /// make date formatting non-deterministic.
    /// </summary>
    public static readonly DateTime FixedNowUtc = new(2026, 6, 15, 10, 30, 0, DateTimeKind.Utc);

    public static AppDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"surgery-tests-{Guid.NewGuid()}")
            .Options);

    /// <summary>
    /// Builds a fully-valid <see cref="SurgeryCase"/> (Scheduled status) linked to a Patient + Doctor.
    /// Caller controls patient/doctor IDs by passing pre-seeded entities.
    /// </summary>
    public static SurgeryCase BuildSurgeryCase(
        Guid patientId,
        Guid? doctorId = null,
        SurgeryCaseStatus status = SurgeryCaseStatus.Scheduled,
        string surgeryType = "خلع ضرس",
        string? teethInvolved = "36")
        => new()
        {
            CaseNumber = $"SU-2026-{Guid.NewGuid():N}".Substring(0, 16),
            PatientId = patientId,
            DoctorId = doctorId,
            SurgeryType = surgeryType,
            TeethInvolved = teethInvolved,
            Status = status,
            IsActive = true,
            CreatedAt = FixedNowUtc,
            UpdatedAt = FixedNowUtc
        };

    /// <summary>
    /// Builds a User + Doctor pair (linked via Doctor.UserId) for the given role.
    /// Returns both so tests can assert on either id (the audit's "DoctorId = Doctor.Id, not User.Id"
    /// concern is enforced by tests asserting on the persisted DoctorId).
    /// </summary>
    public static (User User, Doctor Doctor) BuildDoctor(
        UserRole role = UserRole.OralSurgeon,
        string name = "د. جراح")
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = $"doc-{Guid.NewGuid():N}".Substring(0, 20),
            PasswordHash = "test-hash",
            PasswordSalt = "test-salt",
            Role = role,
            IsActive = true
        };
        var doctor = new Doctor
        {
            UserId = user.Id,
            Name = name,
            IsActive = true
        };
        return (user, doctor);
    }

    public static Patient BuildPatient(string firstName = "أحمد", string lastName = "المريض")
        => new()
        {
            PatientNumber = $"P-{Guid.NewGuid():N}".Substring(0, 12),
            FirstName = firstName,
            LastName = lastName,
            IsActive = true
        };

    public static PreopReport BuildPreopReport(Guid surgeryCaseId, Guid? doctorId = null)
        => new()
        {
            SurgeryCaseId = surgeryCaseId,
            SurgeryDate = DateOnly.FromDateTime(FixedNowUtc),
            SurgeryLocation = "غرفة العمليات 1",
            AnesthesiaType = "موضعي",
            ConsentSigned = true,
            DoctorId = doctorId,
            IsActive = true
        };

    public static OperativeReport BuildOperativeReport(Guid surgeryCaseId, Guid? doctorId = null)
        => new()
        {
            SurgeryCaseId = surgeryCaseId,
            SurgeryDateTime = FixedNowUtc,
            DurationMinutes = 45,
            AnesthesiaUsed = "موضعي",
            Technique = "خلع جراحي بسيط",
            DetailedDescription = "تفاصيل الجراحة",
            Outcome = "ناجحة",
            SuturesCount = 3,
            SpecimenSent = false,
            DoctorId = doctorId,
            IsActive = true
        };

    public static PostopRecord BuildPostopRecord(Guid surgeryCaseId)
        => new()
        {
            SurgeryCaseId = surgeryCaseId,
            Instructions = "تناول المسكنات وتجنب المضغ",
            IsActive = true
        };

    /// <summary>
    /// Constructs the SurgeryController with the standard mocked dependencies.
    /// PatientAccess mock is configured to either allow or deny access to the given patient id
    /// — the controller's <c>DenyIfDoctorCannotAccess</c> depends on
    /// <c>IPatientAccessService.IsDoctor</c> + <c>CanAccessPatientAsync</c>.
    /// </summary>
    public static SurgeryController BuildController(
        AppDbContext db,
        Mock<IPatientAccessService>? patientAccessMock = null,
        Mock<ICurrentUserService>? currentUserMock = null,
        Mock<IAuditService>? auditMock = null)
    {
        patientAccessMock ??= new Mock<IPatientAccessService>();
        currentUserMock ??= new Mock<ICurrentUserService>();
        auditMock ??= new Mock<IAuditService>();
        var logger = new Mock<ILogger<SurgeryController>>().Object;

        return new SurgeryController(
            db,
            logger,
            patientAccessMock.Object,
            auditMock.Object,
            currentUserMock.Object);
    }

    /// <summary>
    /// Configures the patient-access mock as a non-doctor role (Admin/Reception) so
    /// <c>DenyIfDoctorCannotAccess</c> short-circuits and lets the action proceed.
    /// Use this when testing the happy path on actions that don't hit raw SQL
    /// (GetById, UpdateStatus, UpsertPreop, ApproveOperativeReport, etc.).
    /// </summary>
    public static void SetupNonDoctor(Mock<IPatientAccessService> mock)
    {
        mock.SetupGet(p => p.IsDoctor).Returns(false);
        mock.SetupGet(p => p.HasFullAccess).Returns(true);
        mock.Setup(p => p.CanAccessPatientAsync(It.IsAny<Guid>()))
            .ReturnsAsync(true);
    }

    /// <summary>
    /// Configures the patient-access mock as a doctor role that is allowed to access
    /// only the given patient ids. Any other patient returns 403 from the controller.
    /// </summary>
    public static void SetupDoctorWithAccess(
        Mock<IPatientAccessService> mock,
        params Guid[] accessiblePatientIds)
    {
        var set = new HashSet<Guid>(accessiblePatientIds);
        mock.SetupGet(p => p.IsDoctor).Returns(true);
        mock.SetupGet(p => p.HasFullAccess).Returns(false);
        mock.Setup(p => p.CanAccessPatientAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid pid) => set.Contains(pid));
    }
}
