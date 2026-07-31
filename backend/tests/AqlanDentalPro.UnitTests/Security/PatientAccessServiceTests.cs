using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Security;

/// <summary>
/// Tests for PatientAccessService — covers all 9 scenarios:
/// non-doctor full access, doctor-by-primary, doctor-by-appointment,
/// doctor-by-visit, doctor-by-treatment-step, doctor without doctor record,
/// cross-doctor deny, inactive record exclusion, accessible-ids set.
/// </summary>
public class PatientAccessServiceTests
{
    // ── Factories ─────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ICurrentUserService CreateUser(
        Guid userId,
        UserRole role,
        bool isAdmin = false,
        Guid? branchId = null)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(u => u.UserId).Returns(userId);
        mock.Setup(u => u.Role).Returns(role);
        mock.Setup(u => u.BranchId).Returns(branchId);
        mock.Setup(u => u.IsAdmin).Returns(isAdmin);
        mock.Setup(u => u.IsAuthenticated).Returns(true);
        return mock.Object;
    }

    private static PatientAccessService Build(AppDbContext db, ICurrentUserService user) =>
        new(db, user, NullLogger<PatientAccessService>.Instance);

    // Seed helpers

    private static async Task<(Patient patient, Doctor doctor)> SeedPatientAndDoctor(
        AppDbContext db, Guid userId, UserRole role = UserRole.GeneralDentist, bool isPrimary = false)
    {
        var user = new User { Id = userId, Username = "dr", Role = role, IsActive = true, PasswordHash = "h", PasswordSalt = "s" };
        db.Users.Add(user);

        var doctor = new Doctor { UserId = userId, Name = "د. محمد", IsActive = true };
        db.Doctors.Add(doctor);

        var patient = new Patient { FirstName = "أحمد", LastName = "علي", PatientNumber = "P001", IsActive = true };
        if (isPrimary) patient.PrimaryDoctorId = doctor.Id;
        db.Patients.Add(patient);

        await db.SaveChangesAsync();
        return (patient, doctor);
    }

    // ── IsDoctor ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(UserRole.Admin,          false)]
    [InlineData(UserRole.Reception,      false)]
    [InlineData(UserRole.Accountant,     false)]
    [InlineData(UserRole.Assistant,      false)]
    [InlineData(UserRole.BranchManager,  false)]
    [InlineData(UserRole.Orthodontist,   true)]
    [InlineData(UserRole.GeneralDentist, true)]
    [InlineData(UserRole.OralSurgeon,    true)]
    public void IsDoctor_CorrectForEachRole(UserRole role, bool expected)
    {
        using var db = CreateDb();
        var svc = Build(db, CreateUser(Guid.NewGuid(), role));
        svc.IsDoctor.Should().Be(expected);
    }

    // ── HasFullAccess ─────────────────────────────────────────────────────────

    [Fact]
    public void HasFullAccess_NonDoctorRole_ReturnsTrue()
    {
        using var db = CreateDb();
        var svc = Build(db, CreateUser(Guid.NewGuid(), UserRole.Accountant));
        svc.HasFullAccess.Should().BeTrue();
    }

    [Fact]
    public void HasFullAccess_DoctorRole_ReturnsFalse()
    {
        using var db = CreateDb();
        var svc = Build(db, CreateUser(Guid.NewGuid(), UserRole.Orthodontist));
        svc.HasFullAccess.Should().BeFalse();
    }

    // ── CanAccessPatient: non-doctor always allowed ───────────────────────────

    [Fact]
    public async Task CanAccessPatient_AdminRole_AlwaysTrue()
    {
        using var db = CreateDb();
        var svc = Build(db, CreateUser(Guid.NewGuid(), UserRole.Admin, isAdmin: true));
        var result = await svc.CanAccessPatientAsync(Guid.NewGuid());
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessPatient_ReceptionRole_AlwaysTrue()
    {
        using var db = CreateDb();
        var svc = Build(db, CreateUser(Guid.NewGuid(), UserRole.Reception));
        var result = await svc.CanAccessPatientAsync(Guid.NewGuid());
        result.Should().BeTrue();
    }

    // ── CanAccessPatient: doctor access paths ─────────────────────────────────

    [Fact]
    public async Task CanAccessPatient_DoctorIsPrimaryDoctor_ReturnsTrue()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var (patient, _) = await SeedPatientAndDoctor(db, userId, isPrimary: true);

        var svc = Build(db, CreateUser(userId, UserRole.GeneralDentist));
        var result = await svc.CanAccessPatientAsync(patient.Id);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessPatient_DoctorHasAppointment_ReturnsTrue()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var (patient, doctor) = await SeedPatientAndDoctor(db, userId);

        db.Appointments.Add(new Appointment
        {
            PatientId    = patient.Id,
            DoctorId     = doctor.Id,
            AppointmentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            StartTime    = TimeOnly.FromDateTime(DateTime.UtcNow),
            EndTime      = TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(1)),
            Status       = AppointmentStatus.Scheduled,
            AppointmentType = "استشارة",
            IsActive     = true,
        });
        await db.SaveChangesAsync();

        var svc = Build(db, CreateUser(userId, UserRole.Orthodontist));
        (await svc.CanAccessPatientAsync(patient.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessPatient_DoctorHasVisit_ReturnsTrue()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var (patient, doctor) = await SeedPatientAndDoctor(db, userId);

        db.Visits.Add(new Visit
        {
            PatientId = patient.Id,
            DoctorId  = doctor.Id,
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow),
            IsActive  = true,
        });
        await db.SaveChangesAsync();

        var svc = Build(db, CreateUser(userId, UserRole.OralSurgeon));
        (await svc.CanAccessPatientAsync(patient.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessPatient_DoctorHasTreatmentStep_ReturnsTrue()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var (patient, doctor) = await SeedPatientAndDoctor(db, userId);

        db.PatientTreatmentPlanSteps.Add(new PatientTreatmentPlanStep
        {
            PatientId           = patient.Id,
            ResponsibleDoctorId = doctor.Id,
            Title               = "تقويم",
            IsActive            = true,
        });
        await db.SaveChangesAsync();

        var svc = Build(db, CreateUser(userId, UserRole.Orthodontist));
        (await svc.CanAccessPatientAsync(patient.Id)).Should().BeTrue();
    }

    // ── CanAccessPatient: denial cases ────────────────────────────────────────

    [Fact]
    public async Task CanAccessPatient_DoctorNotLinked_ReturnsFalse()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();

        // Create a doctor user but no link to the patient
        var user = new User { Id = userId, Username = "dr2", Role = UserRole.GeneralDentist, IsActive = true, PasswordHash = "h", PasswordSalt = "s" };
        db.Users.Add(user);
        db.Doctors.Add(new Doctor { UserId = userId, Name = "د. سارة", IsActive = true });
        var patient = new Patient { FirstName = "خالد", LastName = "محمد", PatientNumber = "P002", IsActive = true };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var svc = Build(db, CreateUser(userId, UserRole.Orthodontist));
        (await svc.CanAccessPatientAsync(patient.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task CanAccessPatient_DoctorHasNoRecord_ReturnsFalse()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();

        // User with doctor role but NO Doctor entity in DB
        var patient = new Patient { FirstName = "سلمى", LastName = "أحمد", PatientNumber = "P003", IsActive = true };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var svc = Build(db, CreateUser(userId, UserRole.OralSurgeon));
        (await svc.CanAccessPatientAsync(patient.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task CanAccessPatient_CrossDoctor_ReturnsFalse()
    {
        await using var db = CreateDb();

        // Doctor A is linked, Doctor B tries to access
        var userIdA = Guid.NewGuid();
        var userIdB = Guid.NewGuid();

        var userA = new User { Id = userIdA, Username = "drA", Role = UserRole.GeneralDentist, IsActive = true, PasswordHash = "h", PasswordSalt = "s" };
        var userB = new User { Id = userIdB, Username = "drB", Role = UserRole.Orthodontist, IsActive = true, PasswordHash = "h", PasswordSalt = "s" };
        db.Users.AddRange(userA, userB);

        var doctorA = new Doctor { UserId = userIdA, Name = "د. أ", IsActive = true };
        db.Doctors.Add(doctorA);
        db.Doctors.Add(new Doctor { UserId = userIdB, Name = "د. ب", IsActive = true });

        var patient = new Patient { FirstName = "فاطمة", LastName = "علي", PatientNumber = "P004", PrimaryDoctorId = doctorA.Id, IsActive = true };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var svcB = Build(db, CreateUser(userIdB, UserRole.Orthodontist));
        (await svcB.CanAccessPatientAsync(patient.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task CanAccessPatient_DoctorLinkedAcrossBranch_ReturnsFalse()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var allowedBranchId = Guid.NewGuid();
        var otherBranchId = Guid.NewGuid();
        var (patient, _) = await SeedPatientAndDoctor(db, userId, isPrimary: true);
        patient.BranchId = otherBranchId;
        await db.SaveChangesAsync();

        var svc = Build(db, CreateUser(
            userId,
            UserRole.GeneralDentist,
            branchId: allowedBranchId));

        (await svc.CanAccessPatientAsync(patient.Id)).Should().BeFalse(
            "a clinical link must never override the branch carried by the staff JWT");
    }

    // Regression test: the ortho-surgical joint-planning workflow assigns a surgeon (or
    // orthodontist) to a case via SurgeonId/OrthodontistId on OrthoSurgicalCase — often for a
    // patient the surgeon is reviewing for the FIRST time (that is the entire point of a
    // surgical referral/consultation), so none of the 5 pre-existing link types apply yet.
    // Without recognizing this 6th link, DenyIfDoctorCannotAccess denies the very surgeon the
    // case was sent to, breaking the send-to-surgeon workflow at its core.
    [Fact]
    public async Task CanAccessPatient_DoctorIsAssignedSurgeon_OnOrthoSurgicalCase_ReturnsTrue()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var (patient, doctor) = await SeedPatientAndDoctor(db, userId); // no primary/appointment/visit/step/referral link

        db.OrthoSurgicalCases.Add(new OrthoSurgicalCase
        {
            CaseNumber = "OS-PA-001",
            PatientId = patient.Id,
            OrthoCaseId = Guid.NewGuid(),
            SurgeonId = doctor.Id,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var svc = Build(db, CreateUser(userId, UserRole.OralSurgeon));
        (await svc.CanAccessPatientAsync(patient.Id)).Should().BeTrue(
            "an OralSurgeon assigned as SurgeonId on a shared ortho-surgical case must be able " +
            "to review it even with no prior appointment/visit/referral to this patient");
    }

    [Fact]
    public async Task CanAccessPatient_DoctorIsAssignedOrthodontist_OnOrthoSurgicalCase_ReturnsTrue()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var (patient, doctor) = await SeedPatientAndDoctor(db, userId);

        db.OrthoSurgicalCases.Add(new OrthoSurgicalCase
        {
            CaseNumber = "OS-PA-002",
            PatientId = patient.Id,
            OrthoCaseId = Guid.NewGuid(),
            OrthodontistId = doctor.Id,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var svc = Build(db, CreateUser(userId, UserRole.Orthodontist));
        (await svc.CanAccessPatientAsync(patient.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessPatient_InactiveOrthoSurgicalCase_ReturnsFalse()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var (patient, doctor) = await SeedPatientAndDoctor(db, userId);

        db.OrthoSurgicalCases.Add(new OrthoSurgicalCase
        {
            CaseNumber = "OS-PA-003",
            PatientId = patient.Id,
            OrthoCaseId = Guid.NewGuid(),
            SurgeonId = doctor.Id,
            IsActive = false, // soft-deleted — must not grant access
        });
        await db.SaveChangesAsync();

        var svc = Build(db, CreateUser(userId, UserRole.OralSurgeon));
        (await svc.CanAccessPatientAsync(patient.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task CanAccessPatient_DoctorHasInternalReferral_ReturnsTrue()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var (patient, doctor) = await SeedPatientAndDoctor(db, userId);

        // fromDoctor is a separate doctor who refers the patient to our doctor
        var fromUserId = Guid.NewGuid();
        var fromUser = new User { Id = fromUserId, Username = "drFrom", Role = UserRole.Orthodontist, IsActive = true, PasswordHash = "h", PasswordSalt = "s" };
        db.Users.Add(fromUser);
        var fromDoctor = new Doctor { UserId = fromUserId, Name = "د. المحيل", IsActive = true };
        db.Doctors.Add(fromDoctor);
        await db.SaveChangesAsync();

        db.InternalReferrals.Add(new InternalReferral
        {
            PatientId    = patient.Id,
            FromDoctorId = fromDoctor.Id,
            ToDoctorId   = doctor.Id,
            IsActive     = true,
        });
        await db.SaveChangesAsync();

        var svc = Build(db, CreateUser(userId, UserRole.GeneralDentist));
        (await svc.CanAccessPatientAsync(patient.Id)).Should().BeTrue();
    }

    // ── GetAccessiblePatientIds ───────────────────────────────────────────────

    [Fact]
    public async Task GetAccessiblePatientIds_NonDoctorRole_ReturnsNull()
    {
        await using var db = CreateDb();
        var svc = Build(db, CreateUser(Guid.NewGuid(), UserRole.Admin));
        var result = await svc.GetAccessiblePatientIdsAsync();
        result.Should().BeNull(); // null = no filter, see all patients
    }

    [Fact]
    public async Task GetAccessiblePatientIds_DoctorWithLinkedPatients_ReturnsCorrectSet()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var (patient, doctor) = await SeedPatientAndDoctor(db, userId, isPrimary: true);

        // Add a second patient linked via appointment
        var patient2 = new Patient { FirstName = "منى", LastName = "سالم", PatientNumber = "P005", IsActive = true };
        db.Patients.Add(patient2);
        await db.SaveChangesAsync();

        db.Appointments.Add(new Appointment
        {
            PatientId    = patient2.Id,
            DoctorId     = doctor.Id,
            AppointmentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            StartTime    = TimeOnly.FromDateTime(DateTime.UtcNow),
            EndTime      = TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(1)),
            Status       = AppointmentStatus.Scheduled,
            AppointmentType = "علاج",
            IsActive     = true,
        });
        await db.SaveChangesAsync();

        var svc = Build(db, CreateUser(userId, UserRole.GeneralDentist));
        var ids = await svc.GetAccessiblePatientIdsAsync();

        ids.Should().NotBeNull();
        ids!.Should().Contain(patient.Id);
        ids.Should().Contain(patient2.Id);
        ids.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAccessiblePatientIds_RemovesLinkedPatientsFromOtherBranches()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var allowedBranchId = Guid.NewGuid();
        var otherBranchId = Guid.NewGuid();
        var (patient, _) = await SeedPatientAndDoctor(db, userId, isPrimary: true);
        patient.BranchId = otherBranchId;
        await db.SaveChangesAsync();

        var svc = Build(db, CreateUser(
            userId,
            UserRole.Orthodontist,
            branchId: allowedBranchId));

        var ids = await svc.GetAccessiblePatientIdsAsync();

        ids.Should().NotBeNull();
        ids.Should().BeEmpty("clinical lists must be restricted to the current branch");
    }

    [Fact]
    public async Task GetAccessiblePatientIds_DoctorWithNoPatients_ReturnsEmpty()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Username = "drX", Role = UserRole.OralSurgeon, IsActive = true, PasswordHash = "h", PasswordSalt = "s" };
        db.Users.Add(user);
        db.Doctors.Add(new Doctor { UserId = userId, Name = "د. خ", IsActive = true });
        await db.SaveChangesAsync();

        var svc = Build(db, CreateUser(userId, UserRole.OralSurgeon));
        var ids = await svc.GetAccessiblePatientIdsAsync();

        ids.Should().NotBeNull();
        ids!.Should().BeEmpty();
    }

    // ── GetAccessiblePatientIds: per-link-type (HOTFIX PR165 regression) ─────

    [Fact]
    public async Task GetAccessiblePatientIds_DoctorWithPrimaryLink_ReturnsPatient()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var (patient, _) = await SeedPatientAndDoctor(db, userId, isPrimary: true);

        var svc = Build(db, CreateUser(userId, UserRole.GeneralDentist));
        var ids = await svc.GetAccessiblePatientIdsAsync();

        ids.Should().NotBeNull();
        ids!.Should().Contain(patient.Id);
    }

    [Fact]
    public async Task GetAccessiblePatientIds_DoctorWithAppointmentLink_ReturnsPatient()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var (patient, doctor) = await SeedPatientAndDoctor(db, userId);

        db.Appointments.Add(new Appointment
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            AppointmentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            StartTime = TimeOnly.FromDateTime(DateTime.UtcNow),
            EndTime = TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(1)),
            Status = AppointmentStatus.Scheduled,
            AppointmentType = "استشارة",
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var svc = Build(db, CreateUser(userId, UserRole.GeneralDentist));
        var ids = await svc.GetAccessiblePatientIdsAsync();

        ids.Should().NotBeNull();
        ids!.Should().Contain(patient.Id);
    }

    [Fact]
    public async Task GetAccessiblePatientIds_DoctorWithVisitLink_ReturnsPatient()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var (patient, doctor) = await SeedPatientAndDoctor(db, userId);

        db.Visits.Add(new Visit
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow),
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var svc = Build(db, CreateUser(userId, UserRole.GeneralDentist));
        var ids = await svc.GetAccessiblePatientIdsAsync();

        ids.Should().NotBeNull();
        ids!.Should().Contain(patient.Id);
    }

    [Fact]
    public async Task GetAccessiblePatientIds_DoctorWithTreatmentStepLink_ReturnsPatient()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var (patient, doctor) = await SeedPatientAndDoctor(db, userId);

        db.PatientTreatmentPlanSteps.Add(new PatientTreatmentPlanStep
        {
            PatientId = patient.Id,
            ResponsibleDoctorId = doctor.Id,
            Title = "تقويم",
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var svc = Build(db, CreateUser(userId, UserRole.Orthodontist));
        var ids = await svc.GetAccessiblePatientIdsAsync();

        ids.Should().NotBeNull();
        ids!.Should().Contain(patient.Id);
    }

    [Fact]
    public async Task GetAccessiblePatientIds_DoctorWithReferralLink_ReturnsPatient()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var (patient, doctor) = await SeedPatientAndDoctor(db, userId);

        // Create a separate from-doctor
        var fromUserId = Guid.NewGuid();
        var fromUser = new User { Id = fromUserId, Username = "drFrom", Role = UserRole.Orthodontist, IsActive = true, PasswordHash = "h", PasswordSalt = "s" };
        db.Users.Add(fromUser);
        var fromDoctor = new Doctor { UserId = fromUserId, Name = "د. المحيل", IsActive = true };
        db.Doctors.Add(fromDoctor);
        await db.SaveChangesAsync();

        db.InternalReferrals.Add(new InternalReferral
        {
            PatientId = patient.Id,
            FromDoctorId = fromDoctor.Id,
            ToDoctorId = doctor.Id,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var svc = Build(db, CreateUser(userId, UserRole.GeneralDentist));
        var ids = await svc.GetAccessiblePatientIdsAsync();

        ids.Should().NotBeNull();
        ids!.Should().Contain(patient.Id);
    }

    [Fact]
    public async Task GetAccessiblePatientIds_DoctorWithOrthoSurgicalSurgeonLink_ReturnsPatient()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var (patient, doctor) = await SeedPatientAndDoctor(db, userId);

        db.OrthoSurgicalCases.Add(new OrthoSurgicalCase
        {
            CaseNumber = "OS-PA-004",
            PatientId = patient.Id,
            OrthoCaseId = Guid.NewGuid(),
            SurgeonId = doctor.Id,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var svc = Build(db, CreateUser(userId, UserRole.OralSurgeon));
        var ids = await svc.GetAccessiblePatientIdsAsync();

        ids.Should().NotBeNull();
        ids!.Should().Contain(patient.Id);
    }

    [Fact]
    public async Task GetAccessiblePatientIds_NoDuplicateIds_WhenMultipleLinks()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var (patient, doctor) = await SeedPatientAndDoctor(db, userId, isPrimary: true);

        // Add appointment link to the same patient — should not duplicate
        db.Appointments.Add(new Appointment
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            AppointmentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            StartTime = TimeOnly.FromDateTime(DateTime.UtcNow),
            EndTime = TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(1)),
            Status = AppointmentStatus.Scheduled,
            AppointmentType = "علاج",
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var svc = Build(db, CreateUser(userId, UserRole.GeneralDentist));
        var ids = await svc.GetAccessiblePatientIdsAsync();

        ids.Should().NotBeNull();
        ids!.Should().ContainSingle(p => p == patient.Id);
    }
}
