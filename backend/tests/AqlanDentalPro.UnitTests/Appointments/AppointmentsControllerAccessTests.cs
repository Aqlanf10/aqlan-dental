using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.DTOs.Appointments;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Repositories;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Appointments;

/// <summary>
/// CORE-APPT-001: [ServiceFilter(typeof(PatientAccessFilter))] on AppointmentsController
/// only enforces on endpoints carrying a "patientId" route/query value — in practice just
/// GetByPatient. Every other action (by appointment id, or by request-body PatientId, or a
/// list with no patientId at all) previously let a restricted doctor read or write any
/// patient's appointment. These tests exercise the explicit DenyIfDoctorCannotAccess /
/// GetAccessiblePatientIdsIfDoctorAsync checks added to close that gap, mirroring the
/// established VisitsControllerAccessTests shape.
/// </summary>
public class AppointmentsControllerAccessTests : IDisposable
{
    private static readonly Guid TestBranchId = Guid.NewGuid();
    private readonly AppDbContext _db;

    public AppointmentsControllerAccessTests()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"appt-access-{Guid.NewGuid()}")
            .Options);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static Patient BuildPatient(string first = "سعيد", string last = "المريض") => new()
    {
        PatientNumber = $"P-{Guid.NewGuid():N}"[..12],
        FirstName = first,
        LastName = last,
        BranchId = TestBranchId,
        IsActive = true,
    };

    // Appointment.DoctorId is a required (non-nullable) FK, and AppointmentRepository's
    // queries all .Include(a => a.Doctor) — EF Core translates a required navigation
    // Include to an INNER JOIN, so an appointment with no matching Doctor row is silently
    // dropped from every result set. Every seeded appointment needs a real Doctor row.
    private static Doctor BuildDoctor(string name = "د. تجريبي") => new()
    {
        UserId = Guid.NewGuid(),
        Name = name,
        IsActive = true,
    };

    private static Appointment BuildAppointment(Guid patientId, Guid doctorId, DateOnly date, AppointmentStatus status = AppointmentStatus.Scheduled) => new()
    {
        PatientId = patientId,
        DoctorId = doctorId,
        BranchId = TestBranchId,
        AppointmentDate = date,
        StartTime = new TimeOnly(10, 0),
        EndTime = new TimeOnly(10, 30),
        DurationMinutes = 30,
        AppointmentType = "معاينة",
        Status = status,
        IsActive = true,
    };

    private async Task<(Patient Patient, Appointment Appointment)> SeedAsync(DateOnly? date = null)
    {
        var patient = BuildPatient();
        var doctor = BuildDoctor();
        _db.Patients.Add(patient);
        _db.Doctors.Add(doctor);

        // GOLIVE-PERM-001: the write endpoints now read the appointments switch, and
        // PermissionGuard fails closed when no row exists for (role, resource). These tests
        // are about patient access and clinic-day rules, not permissions, so grant the role
        // its ordinary appointment rights — the same rows the seeder creates in a real database.
        _db.RolePermissions.Add(new RolePermission
        {
            Id = Guid.NewGuid(),
            Role = nameof(UserRole.GeneralDentist),
            Resource = "appointments",
            CanView = true,
            CanCreate = true,
            CanEdit = true,
        });
        await _db.SaveChangesAsync();

        var appointment = BuildAppointment(patient.Id, doctor.Id, date ?? DateOnly.FromDateTime(DateTime.UtcNow));
        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync();

        return (patient, appointment);
    }

    private static void SetupNonDoctor(Mock<IPatientAccessService> mock)
    {
        mock.SetupGet(p => p.IsDoctor).Returns(false);
        mock.Setup(p => p.CanAccessPatientAsync(It.IsAny<Guid>())).ReturnsAsync(true);
        mock.Setup(p => p.GetAccessiblePatientIdsAsync()).ReturnsAsync((HashSet<Guid>?)null);
    }

    private static void SetupDoctorWithAccess(Mock<IPatientAccessService> mock, params Guid[] accessiblePatientIds)
    {
        var set = new HashSet<Guid>(accessiblePatientIds);
        mock.SetupGet(p => p.IsDoctor).Returns(true);
        mock.Setup(p => p.CanAccessPatientAsync(It.IsAny<Guid>())).ReturnsAsync((Guid pid) => set.Contains(pid));
        mock.Setup(p => p.GetAccessiblePatientIdsAsync()).ReturnsAsync(set);
    }

    private AppointmentsController BuildController(Mock<IPatientAccessService> accessMock, bool isAdmin = false)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(c => c.IsAuthenticated).Returns(true);
        currentUser.SetupGet(c => c.IsAdmin).Returns(isAdmin);
        currentUser.SetupGet(c => c.Role).Returns(isAdmin ? UserRole.Admin : UserRole.GeneralDentist);
        currentUser.SetupGet(c => c.BranchId).Returns(TestBranchId);

        var repo = new AppointmentRepository(_db);
        var scopeFactory = new Mock<IServiceScopeFactory>();
        var service = new AppointmentService(repo, currentUser.Object, scopeFactory.Object, NullLogger<AppointmentService>.Instance);

        var whatsapp = new Mock<IWhatsAppService>();
        var email = new Mock<IEmailService>();
        var push = new Mock<IRealTimePushService>();
        var audit = new Mock<IAuditService>();
        var clock = new ClinicClock();

        return new AppointmentsController(
            service, _db, currentUser.Object, whatsapp.Object, email.Object, push.Object,
            accessMock.Object, audit.Object, clock, new JourneyBusinessDatePolicy(clock),
            NullLogger<AppointmentsController>.Instance);
    }

    private static string ExtractMessage(object? value)
    {
        value.Should().NotBeNull("the 4xx/403 response must carry a payload");
        var prop = value!.GetType().GetProperty("message");
        prop.Should().NotBeNull("the response must carry a 'message' field (Arabic)");
        return (string)prop!.GetValue(value)!;
    }

    [Fact]
    public async Task UpdateStatus_FutureArrivalWithoutExplicitOverride_IsRejectedWithoutQueueMutation()
    {
        var (_, appointment) = await SeedAsync(new ClinicClock().Today().AddDays(1));
        var access = new Mock<IPatientAccessService>();
        SetupNonDoctor(access);
        var controller = BuildController(access, isAdmin: false);

        var result = await controller.UpdateStatus(
            appointment.Id,
            new UpdateAppointmentStatusRequest { Status = "Arrived" });

        result.Should().BeOfType<BadRequestObjectResult>();
        appointment.Status.Should().Be(AppointmentStatus.Scheduled);
        (await _db.ClinicQueueItems.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UpdateStatus_AdminFutureArrivalWithReason_IsAuditedAndQueuedOnBusinessDate()
    {
        var clock = new ClinicClock();
        var (_, appointment) = await SeedAsync(clock.Today().AddDays(1));
        var access = new Mock<IPatientAccessService>();
        SetupNonDoctor(access);
        var controller = BuildController(access, isAdmin: true);

        var result = await controller.UpdateStatus(
            appointment.Id,
            new UpdateAppointmentStatusRequest
            {
                Status = "Arrived",
                OverrideFutureAppointment = true,
                OverrideReason = "حضور مبكر بموافقة المدير"
            });

        result.Should().BeOfType<OkObjectResult>();
        appointment.Status.Should().Be(AppointmentStatus.Arrived);
        appointment.ArrivedAt.Should().NotBeNull();
        var queue = await _db.ClinicQueueItems.SingleAsync();
        queue.QueueDate.Should().Be(clock.Today());
        var audit = await _db.AuditLogs.SingleAsync();
        audit.Resource.Should().Be("FutureAppointmentJourneyOverride");
    }

    [Fact]
    public async Task StartVisit_FutureAppointmentWithoutOverride_IsRejectedWithoutVisit()
    {
        var (_, appointment) = await SeedAsync(new ClinicClock().Today().AddDays(1));
        appointment.Status = AppointmentStatus.InRoom;
        await _db.SaveChangesAsync();
        var access = new Mock<IPatientAccessService>();
        SetupNonDoctor(access);
        var controller = BuildController(access, isAdmin: false);

        var result = await controller.StartVisit(appointment.Id, new());

        result.Should().BeOfType<BadRequestObjectResult>();
        (await _db.Visits.CountAsync()).Should().Be(0);
        appointment.Status.Should().Be(AppointmentStatus.InRoom);
    }

    [Fact]
    public async Task StartVisit_AdminFutureOverride_CreatesVisitOnBusinessDateAndAudit()
    {
        var clock = new ClinicClock();
        var (_, appointment) = await SeedAsync(clock.Today().AddDays(1));
        appointment.Status = AppointmentStatus.InRoom;
        await _db.SaveChangesAsync();
        var access = new Mock<IPatientAccessService>();
        SetupNonDoctor(access);
        var controller = BuildController(access, isAdmin: true);

        var result = await controller.StartVisit(appointment.Id, new()
        {
            OverrideFutureAppointment = true,
            OverrideReason = "بدء استثنائي بموافقة المدير"
        });

        result.Should().BeOfType<OkObjectResult>();
        appointment.Status.Should().Be(AppointmentStatus.InProgress);
        (await _db.Visits.SingleAsync()).VisitDate.Should().Be(clock.Today());
        var audit = await _db.AuditLogs.SingleAsync();
        audit.Resource.Should().Be("FutureAppointmentJourneyOverride");
        audit.NewData!.RootElement.GetProperty("operation").GetString()
            .Should().Be("LegacyAppointmentStartVisit");
    }

    // ── GetById ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_CrossPatientDoctor_Returns403_Arabic()
    {
        var (_, appointment) = await SeedAsync();
        var accessMock = new Mock<IPatientAccessService>();
        SetupDoctorWithAccess(accessMock); // no accessible patients

        var controller = BuildController(accessMock);
        var result = await controller.GetById(appointment.Id);

        var status = result.Result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(403);
        ExtractMessage(status.Value).Should().Be("غير مصرح لك بعرض بيانات هذا المريض");
    }

    [Fact]
    public async Task GetById_SamePatientDoctor_Returns200()
    {
        var (patient, appointment) = await SeedAsync();
        var accessMock = new Mock<IPatientAccessService>();
        SetupDoctorWithAccess(accessMock, patient.Id);

        var controller = BuildController(accessMock);
        var result = await controller.GetById(appointment.Id);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_Admin_BypassesAccessCheck_Returns200()
    {
        var (_, appointment) = await SeedAsync();
        var accessMock = new Mock<IPatientAccessService>();
        SetupNonDoctor(accessMock);

        var controller = BuildController(accessMock, isAdmin: true);
        var result = await controller.GetById(appointment.Id);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    // ── GetToday — list filtering (no single patientId to gate on) ──────────────

    [Fact]
    public async Task GetToday_RestrictedDoctor_OnlySeesAccessiblePatients()
    {
        // GetTodayAsync filters by ClinicTimeProvider.ClinicToday(), not DateTime.UtcNow
        // (Yemen is UTC+3) — seed on the clinic day so the test isn't flaky in that window.
        var today = ClinicTimeProvider.ClinicToday();
        var (ownPatient, _) = await SeedAsync(today);
        var (otherPatient, _) = await SeedAsync(today);

        var accessMock = new Mock<IPatientAccessService>();
        SetupDoctorWithAccess(accessMock, ownPatient.Id);

        var controller = BuildController(accessMock);
        var result = await controller.GetToday(doctorId: null);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ((IEnumerable<AppointmentDto>)ok.Value!).ToList();

        list.Should().ContainSingle(a => a.PatientId == ownPatient.Id);
        list.Should().NotContain(a => a.PatientId == otherPatient.Id,
            "a restricted doctor must not see another patient's appointment just by calling /today without a patientId");
    }

    [Fact]
    public async Task GetToday_Admin_SeesAllPatients()
    {
        var today = ClinicTimeProvider.ClinicToday();
        await SeedAsync(today);
        await SeedAsync(today);

        var accessMock = new Mock<IPatientAccessService>();
        SetupNonDoctor(accessMock);

        var controller = BuildController(accessMock, isAdmin: true);
        var result = await controller.GetToday(doctorId: null);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ((IEnumerable<AppointmentDto>)ok.Value!).Should().HaveCount(2);
    }

    // ── GetByRange — list filtering when patientId is omitted ────────────────────

    [Fact]
    public async Task GetByRange_NoPatientIdFilter_RestrictedDoctor_OnlySeesAccessiblePatients()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (ownPatient, _) = await SeedAsync(today);
        var (otherPatient, _) = await SeedAsync(today);

        var accessMock = new Mock<IPatientAccessService>();
        SetupDoctorWithAccess(accessMock, ownPatient.Id);

        var controller = BuildController(accessMock);
        var result = await controller.GetByRange(
            from: today.ToString("yyyy-MM-dd"), to: today.ToString("yyyy-MM-dd"),
            startDate: null, endDate: null, doctorId: null, patientId: null, status: null, branchId: null);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ((IEnumerable<AppointmentDto>)ok.Value!).ToList();

        list.Should().ContainSingle(a => a.PatientId == ownPatient.Id);
        list.Should().NotContain(a => a.PatientId == otherPatient.Id);
    }

    // ── Create ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_CrossPatientDoctor_Returns403_AndDoesNotPersist()
    {
        var otherPatient = BuildPatient();
        _db.Patients.Add(otherPatient);
        await _db.SaveChangesAsync();

        var accessMock = new Mock<IPatientAccessService>();
        SetupDoctorWithAccess(accessMock); // no accessible patients

        var controller = BuildController(accessMock);
        var req = new CreateAppointmentRequest
        {
            PatientId = otherPatient.Id,
            DoctorId = Guid.NewGuid(),
            AppointmentDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            StartTime = "10:00",
            DurationMinutes = 30,
            AppointmentType = "معاينة",
        };

        var result = await controller.Create(req);

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(403);
        ExtractMessage(status.Value).Should().Be("غير مصرح لك بعرض بيانات هذا المريض");

        (await _db.Appointments.CountAsync()).Should().Be(0,
            "a denied Create must not create an appointment for an inaccessible patient");
    }

    // ── Update ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_CrossPatientDoctor_Returns403_AndDoesNotMutate()
    {
        var (patient, appointment) = await SeedAsync();

        var accessMock = new Mock<IPatientAccessService>();
        SetupDoctorWithAccess(accessMock); // no accessible patients

        var controller = BuildController(accessMock);
        var req = new CreateAppointmentRequest
        {
            PatientId = patient.Id,
            DoctorId = Guid.NewGuid(),
            AppointmentDate = appointment.AppointmentDate.ToString("yyyy-MM-dd"),
            StartTime = "11:00",
            DurationMinutes = 30,
            AppointmentType = "معاينة",
        };

        var result = await controller.Update(appointment.Id, req);

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(403);

        _db.ChangeTracker.Clear();
        var stored = await _db.Appointments.SingleAsync(a => a.Id == appointment.Id);
        stored.StartTime.Should().Be(appointment.StartTime, "a denied Update must not mutate the persisted appointment");
    }

    [Fact]
    public async Task Update_SpoofedAccessiblePatientIdInBody_StillDenied_AndDoesNotMutateInaccessibleAppointment()
    {
        // Codex review finding on PR #760: AppointmentService.UpdateAsync loads the
        // appointment by route `id` and never reassigns PatientId from req.PatientId —
        // it always stays whichever patient the appointment already belongs to. A guard
        // that only checks req.PatientId can be defeated by naming an OWN accessible
        // patient in the body while the route id targets a completely different,
        // inaccessible patient's appointment. The authorization must be against the
        // appointment's *stored* owner, not the request body.
        var (ownPatient, _) = await SeedAsync();
        var (otherPatient, otherAppointment) = await SeedAsync();

        var accessMock = new Mock<IPatientAccessService>();
        SetupDoctorWithAccess(accessMock, ownPatient.Id); // only ownPatient is accessible

        var controller = BuildController(accessMock);
        var req = new CreateAppointmentRequest
        {
            PatientId = ownPatient.Id, // spoofed: names an accessible patient in the body
            DoctorId = Guid.NewGuid(),
            AppointmentDate = otherAppointment.AppointmentDate.ToString("yyyy-MM-dd"),
            StartTime = "11:00",
            DurationMinutes = 30,
            AppointmentType = "معاينة",
        };

        var result = await controller.Update(otherAppointment.Id, req); // route id targets the OTHER patient's appointment

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(403,
            "authorization must be checked against the appointment's actual stored patient, not the spoofable request body");

        _db.ChangeTracker.Clear();
        var stored = await _db.Appointments.SingleAsync(a => a.Id == otherAppointment.Id);
        stored.StartTime.Should().Be(otherAppointment.StartTime,
            "the inaccessible patient's appointment must not be mutated just because the body named a different, accessible patient");
        stored.PatientId.Should().Be(otherPatient.Id, "PatientId must never change on Update");
    }

    [Fact]
    public async Task Update_OwnPatientAppointment_Succeeds()
    {
        var (patient, appointment) = await SeedAsync();

        var accessMock = new Mock<IPatientAccessService>();
        SetupDoctorWithAccess(accessMock, patient.Id);

        var controller = BuildController(accessMock);
        var req = new CreateAppointmentRequest
        {
            PatientId = patient.Id,
            DoctorId = Guid.NewGuid(),
            AppointmentDate = appointment.AppointmentDate.ToString("yyyy-MM-dd"),
            StartTime = "11:00",
            DurationMinutes = 30,
            AppointmentType = "معاينة",
        };

        var result = await controller.Update(appointment.Id, req);

        result.Should().BeOfType<OkObjectResult>();
    }

    // ── UpdateStatus ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStatus_CrossPatientDoctor_Returns403_AndDoesNotMutate()
    {
        var (_, appointment) = await SeedAsync();

        var accessMock = new Mock<IPatientAccessService>();
        SetupDoctorWithAccess(accessMock); // no accessible patients

        var controller = BuildController(accessMock);
        var result = await controller.UpdateStatus(appointment.Id, new UpdateAppointmentStatusRequest { Status = "Confirmed" });

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(403);

        _db.ChangeTracker.Clear();
        var stored = await _db.Appointments.SingleAsync(a => a.Id == appointment.Id);
        stored.Status.Should().Be(AppointmentStatus.Scheduled, "a denied status update must not mutate the appointment");
    }

    // ── BatchUpdateStatus — inaccessible appointments silently excluded ─────────

    [Fact]
    public async Task BatchUpdateStatus_RestrictedDoctor_OnlyUpdatesAccessiblePatientAppointments()
    {
        var (ownPatient, ownAppointment) = await SeedAsync();
        var (_, otherAppointment) = await SeedAsync();

        var accessMock = new Mock<IPatientAccessService>();
        SetupDoctorWithAccess(accessMock, ownPatient.Id);

        var controller = BuildController(accessMock);
        var result = await controller.BatchUpdateStatus(new BatchUpdateStatusRequest
        {
            AppointmentIds = [ownAppointment.Id, otherAppointment.Id],
            Status = "Confirmed",
        });

        result.Should().BeOfType<OkObjectResult>();

        _db.ChangeTracker.Clear();
        (await _db.Appointments.SingleAsync(a => a.Id == ownAppointment.Id)).Status.Should().Be(AppointmentStatus.Confirmed);
        (await _db.Appointments.SingleAsync(a => a.Id == otherAppointment.Id)).Status.Should().Be(AppointmentStatus.Scheduled,
            "a restricted doctor's batch request must not touch another patient's appointment");
    }

    // ── Delete ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_CrossPatientDoctor_Returns403_AndDoesNotMutate()
    {
        var (_, appointment) = await SeedAsync();

        var accessMock = new Mock<IPatientAccessService>();
        SetupDoctorWithAccess(accessMock); // no accessible patients

        var controller = BuildController(accessMock);
        var result = await controller.Delete(appointment.Id);

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(403);

        _db.ChangeTracker.Clear();
        var stored = await _db.Appointments.SingleAsync(a => a.Id == appointment.Id);
        stored.IsActive.Should().BeTrue("a denied Delete must not soft-delete the appointment");
    }

    // ── GetUpcoming — list filtering + PatientId now present in the response ────

    [Fact]
    public async Task GetUpcoming_RestrictedDoctor_OnlySeesAccessiblePatients_AndResponseCarriesPatientId()
    {
        // CORE-APPT-018: this used to do TimeOnly.FromDateTime(now).AddMinutes(30)
        // with the date fixed to today. TimeOnly wraps at midnight, so a run after
        // 23:30 produced an appointment dated TODAY at ~00:15 — already in the past —
        // and the test failed for reasons unrelated to access filtering. Doing the
        // arithmetic on DateTime cannot wrap and rolls the date correctly, so this
        // test is now deterministic at every hour of the day.
        var now = ClinicTimeProvider.ClinicNow();
        var target = now.AddMinutes(30);
        var soon = TimeOnly.FromDateTime(target);
        // Named `date`, not `today` — near midnight the target legitimately falls on
        // the next clinic day, and GetUpcoming is expected to span that boundary.
        var date = DateOnly.FromDateTime(target);

        var ownPatient = BuildPatient();
        var otherPatient = BuildPatient("خالد", "العولقي");
        var doctor = BuildDoctor();
        _db.Patients.AddRange(ownPatient, otherPatient);
        _db.Doctors.Add(doctor);
        await _db.SaveChangesAsync();

        _db.Appointments.AddRange(
            new Appointment
            {
                PatientId = ownPatient.Id, DoctorId = doctor.Id, AppointmentDate = date, StartTime = soon, EndTime = soon.AddMinutes(30),
                DurationMinutes = 30, AppointmentType = "معاينة", Status = AppointmentStatus.Scheduled, IsActive = true,
            },
            new Appointment
            {
                PatientId = otherPatient.Id, DoctorId = doctor.Id, AppointmentDate = date, StartTime = soon, EndTime = soon.AddMinutes(30),
                DurationMinutes = 30, AppointmentType = "معاينة", Status = AppointmentStatus.Scheduled, IsActive = true,
            });
        await _db.SaveChangesAsync();

        var accessMock = new Mock<IPatientAccessService>();
        SetupDoctorWithAccess(accessMock, ownPatient.Id);

        var controller = BuildController(accessMock);
        var result = await controller.GetUpcoming(hours: 2);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var items = ((System.Collections.IEnumerable)ok.Value!).Cast<object>().ToList();

        items.Should().ContainSingle("only the accessible patient's appointment should be returned");
        var patientIdProp = items[0].GetType().GetProperty("PatientId");
        patientIdProp.Should().NotBeNull("CORE-APPT-013: the upcoming-appointments response must carry PatientId so the frontend widget can link to the patient");
        ((Guid)patientIdProp!.GetValue(items[0])!).Should().Be(ownPatient.Id);
    }

    // ── SendReminder / StartVisit / GetEmailAvailable — spot-check the remaining
    //    single-appointment endpoints all gate on the owning patient ──────────────

    [Fact]
    public async Task SendReminder_CrossPatientDoctor_Returns403()
    {
        var (_, appointment) = await SeedAsync();
        var accessMock = new Mock<IPatientAccessService>();
        SetupDoctorWithAccess(accessMock);

        var controller = BuildController(accessMock);
        var result = await controller.SendReminder(appointment.Id);

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task StartVisit_CrossPatientDoctor_Returns403_AndDoesNotCreateVisit()
    {
        var (_, appointment) = await SeedAsync();
        var accessMock = new Mock<IPatientAccessService>();
        SetupDoctorWithAccess(accessMock);

        var controller = BuildController(accessMock);
        var result = await controller.StartVisit(appointment.Id);

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(403);
        (await _db.Visits.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task GetEmailAvailable_CrossPatientDoctor_Returns403()
    {
        var (_, appointment) = await SeedAsync();
        var accessMock = new Mock<IPatientAccessService>();
        SetupDoctorWithAccess(accessMock);

        var controller = BuildController(accessMock);
        var result = await controller.GetEmailAvailable(appointment.Id);

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(403);
    }
}
