using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AqlanDentalPro.UnitTests.Visits;

/// <summary>
/// Shared seed + builder helpers for <see cref="VisitsController"/> unit tests.
/// Mirrors the TEST-02 pattern in <c>AqlanDentalPro.UnitTests.Surgery.SurgeryTestData</c>
/// (EF InMemory + Mock&lt;IPatientAccessService&gt; + Mock&lt;ICurrentUserService&gt; +
/// Mock&lt;IAuditService&gt;). All dates are fixed (no DateTime.Now) so tests stay
/// deterministic.
/// </summary>
internal static class VisitsTestData
{
    /// <summary>
    /// Fixed UTC timestamp used everywhere a "now" value is needed in tests.
    /// 2026-06-15T10:30:00Z — chosen to avoid month/year boundaries that would
    /// make date formatting non-deterministic.
    /// </summary>
    public static readonly DateTime FixedNowUtc = new(2026, 6, 15, 10, 30, 0, DateTimeKind.Utc);

    /// <summary>
    /// Fixed clinic-local date corresponding to <see cref="FixedNowUtc"/>.
    /// Used for VisitDate / AppointmentDate so date assertions don't depend on
    /// wall-clock time.
    /// </summary>
    public static readonly DateOnly FixedVisitDate = new(2026, 6, 15);

    public static AppDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"visits-tests-{Guid.NewGuid()}")
            .Options);

    /// <summary>
    /// Builds a fully-valid <see cref="Visit"/> (active) linked to a Patient + Doctor.
    /// Caller controls patient/doctor IDs by passing pre-seeded entities.
    /// </summary>
    public static Visit BuildVisit(
        Guid patientId,
        Guid? doctorId = null,
        Guid? appointmentId = null,
        string visitType = "معاينة",
        Specialty? specialty = Specialty.General,
        decimal? cost = 5000m,
        string? chiefComplaint = "ألم في الضرس")
        => new()
        {
            PatientId = patientId,
            DoctorId = doctorId,
            AppointmentId = appointmentId,
            VisitDate = FixedVisitDate,
            VisitType = visitType,
            Specialty = specialty,
            ChiefComplaint = chiefComplaint,
            ClinicalNotes = "ملاحظات سريرية",
            TreatmentDone = "حشو بسيط",
            Diagnosis = "تسوس",
            Instructions = "مسكنات",
            Cost = cost,
            IsActive = true,
            CreatedAt = FixedNowUtc,
            UpdatedAt = FixedNowUtc
        };

    /// <summary>
    /// Builds a User + Doctor pair (linked via Doctor.UserId) for the given role.
    /// Returns both so tests can assert on either id — the audit's "DoctorId = Doctor.Id,
    /// not User.Id" concern (TEST-12) is enforced by tests that assert the persisted
    /// <see cref="Visit.DoctorId"/> equals <see cref="Doctor.Id"/> when the controller
    /// is given a req.DoctorId. The controller stores req.DoctorId as-is (no
    /// User→Doctor resolution), so callers must pass doctor.Id, not user.Id.
    /// </summary>
    public static (User User, Doctor Doctor) BuildDoctor(
        UserRole role = UserRole.GeneralDentist,
        string name = "د. أحمد")
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

    public static Patient BuildPatient(string firstName = "سعيد", string lastName = "المريض")
        => new()
        {
            PatientNumber = $"P-{Guid.NewGuid():N}".Substring(0, 12),
            FirstName = firstName,
            LastName = lastName,
            IsActive = true
        };

    /// <summary>
    /// Builds a fully-valid <see cref="Appointment"/> (Scheduled) linked to a Patient + Doctor.
    /// Used for tests that exercise the CLIN-04 queue-linking path in CreateVisit.
    /// </summary>
    public static Appointment BuildAppointment(
        Guid patientId,
        Guid doctorId,
        DateOnly? date = null,
        AppointmentStatus status = AppointmentStatus.Scheduled)
        => new()
        {
            PatientId = patientId,
            DoctorId = doctorId,
            AppointmentDate = date ?? FixedVisitDate,
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(10, 30),
            DurationMinutes = 30,
            AppointmentType = "معاينة",
            Status = status,
            IsActive = true,
            CreatedAt = FixedNowUtc,
            UpdatedAt = FixedNowUtc
        };

    /// <summary>
    /// Builds a fully-valid <see cref="ClinicQueueItem"/> in the Waiting state, linked to a
    /// Patient + Appointment. The CreateVisit action looks for exactly such an item to link
    /// back to the new visit (CLIN-04 fix: sets queueItem.VisitId + transitions status to InProgress).
    /// </summary>
    public static ClinicQueueItem BuildClinicQueueItem(
        Guid patientId,
        Guid? appointmentId = null,
        ClinicQueueStatus status = ClinicQueueStatus.Waiting)
        => new()
        {
            PatientId = patientId,
            AppointmentId = appointmentId,
            Status = status,
            Priority = ClinicQueuePriority.Normal,
            QueueDate = FixedVisitDate,
            IsActive = true,
            CreatedAt = FixedNowUtc,
            UpdatedAt = FixedNowUtc
        };

    public static ClinicService BuildClinicService(string arabicName = "معاينة عامة")
        => new()
        {
            ArabicName = arabicName,
            EnglishName = "General Consultation",
            Code = $"SVC-{Guid.NewGuid():N}".Substring(0, 10),
            Category = ServiceCategory.Consultation,
            DefaultPrice = 5000m,
            DefaultDurationMinutes = 30,
            IsActive = true,
            CreatedAt = FixedNowUtc,
            UpdatedAt = FixedNowUtc
        };

    /// <summary>
    /// Constructs the <see cref="VisitsController"/> with the standard mocked dependencies.
    /// PatientAccess mock is configured by the caller — the controller's
    /// <c>DenyIfDoctorCannotAccess</c> depends on <c>IPatientAccessService.IsDoctor</c> +
    /// <c>CanAccessPatientAsync</c>.
    /// </summary>
    public static VisitsController BuildController(
        AppDbContext db,
        Mock<IPatientAccessService>? patientAccessMock = null,
        Mock<ICurrentUserService>? currentUserMock = null,
        Mock<IAuditService>? auditMock = null)
    {
        patientAccessMock ??= new Mock<IPatientAccessService>();
        currentUserMock ??= new Mock<ICurrentUserService>();
        auditMock ??= new Mock<IAuditService>();

        return new VisitsController(
            db,
            currentUserMock.Object,
            patientAccessMock.Object,
            auditMock.Object);
    }

    /// <summary>
    /// Configures the patient-access mock as a non-doctor role (Admin/Reception) so
    /// <c>DenyIfDoctorCannotAccess</c> short-circuits and lets the action proceed.
    /// Use this when testing the happy path on actions that don't hit raw SQL.
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
