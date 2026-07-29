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

namespace AqlanDentalPro.UnitTests.Visits;

/// <summary>
/// Unit tests for <see cref="VisitsController.CreateVisit"/> (POST /api/visits).
///
/// Unlike <c>SurgeryController.Create</c> (TEST-02), <c>VisitsController.CreateVisit</c> does
/// NOT call <c>pg_advisory_xact_lock</c> nor open an explicit transaction — the whole action
/// runs against the standard EF change tracker. Therefore the full happy-path CAN be exercised
/// under EF InMemory: we assert persistence + the CLIN-04 queue-linking side-effect, which is
/// the central piece of this controller's daily-workflow contract.
///
/// Coverage map:
///   - Happy path (admin + doctor with access) → 200 + persisted visit
///   - Per-patient access (CLIN-01): 403 for cross-patient doctor, short-circuit before persist
///   - Non-doctor role: access check bypassed (CanAccessPatientAsync never called)
///   - Required-field + FK validation: empty PatientId / unknown Patient / unknown Doctor /
///     unknown Service — all return 400 with the exact Arabic message the controller emits
///   - Doctor resolution: persisted Visit.DoctorId == req.DoctorId (no User→Doctor substitution;
///     callers must pass Doctor.Id, not User.Id — same finding as the prescriptions audit)
///   - CLIN-04 queue linking: sets queueItem.VisitId + transitions Waiting/Called/InRoom → InProgress
///   - CLIN-04 edge cases: already-linked queue item is NOT overwritten; Completed/Cancelled/NoShow
///     queue items are NOT linked
///
/// FINDING #1 (documented in PR description, NOT fixed per TEST-12 test-only scope):
///   The audit's task description suggested "valid payload → 201 + persists". The controller
///   actually returns <c>200 OK</c> (not <c>201 Created</c>) with body <c>{ visit.Id, message }</c>.
///   There is no <c>CreatedAtAction</c> / <c>CreatedAtRoute</c>. This is a minor REST-conformance
///   inconsistency but is intentional (the frontend reads <c>visit.Id</c> from the 200 body).
///   Tests assert 200 — flagged in PR description as a finding, not a fix.
/// </summary>
public class VisitsControllerCreateTests : IDisposable
{
    private readonly AppDbContext _db;

    public VisitsControllerCreateTests()
    {
        _db = VisitsTestData.CreateDb();
    }

    public void Dispose() => _db.Dispose();

    // ── Happy path ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateVisit_ValidPayload_AsAdmin_Returns200AndPersistsVisit()
    {
        var seeded = await SeedPatientDoctorAsync();

        var controller = BuildControllerAsAdmin();

        var req = new CreateVisitRequest
        {
            PatientId = seeded.PatientId,
            DoctorId = seeded.DoctorId,
            VisitDate = "2026-06-15",
            VisitType = "معاينة",
            Specialty = "General",
            ChiefComplaint = "ألم في الضرس",
            ClinicalNotes = "ملاحظات سريرية",
            Cost = 7500m
        };

        var result = await controller.CreateVisit(req);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200, "the controller returns 200 (not 201) on success — see FINDING #1");

        // Response body carries the new visit id + Arabic success message
        var idProp = ok.Value!.GetType().GetProperty("Id");
        idProp.Should().NotBeNull("the response must echo back the new visit Id");
        var newVisitId = (Guid)idProp!.GetValue(ok.Value)!;
        newVisitId.Should().NotBeEmpty();

        var messageProp = ok.Value.GetType().GetProperty("message");
        messageProp!.GetValue(ok.Value).Should().Be("تم إضافة الزيارة بنجاح");

        // Persistence check
        _db.ChangeTracker.Clear();
        var stored = await _db.Visits.SingleAsync(v => v.Id == newVisitId);
        stored.PatientId.Should().Be(seeded.PatientId);
        stored.DoctorId.Should().Be(seeded.DoctorId);
        stored.VisitType.Should().Be("معاينة");
        stored.Specialty.Should().Be(Specialty.General);
        stored.ChiefComplaint.Should().Be("ألم في الضرس");
        stored.Cost.Should().Be(7500m);
        stored.VisitDate.Should().Be(new DateOnly(2026, 6, 15));
        stored.IsActive.Should().BeTrue("a newly-created visit must be active");
    }

    [Fact]
    public async Task CreateVisit_ByDoctorWithPatientAccess_Returns200AndPersists()
    {
        var seeded = await SeedPatientDoctorAsync();

        var accessMock = new Mock<IPatientAccessService>();
        VisitsTestData.SetupDoctorWithAccess(accessMock, seeded.PatientId);
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(seeded.UserId);
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.GeneralDentist);

        var controller = VisitsTestData.BuildController(_db, accessMock, currentUserMock);

        var req = new CreateVisitRequest
        {
            PatientId = seeded.PatientId,
            DoctorId = seeded.DoctorId,
            VisitType = "متابعة"
        };

        var result = await controller.CreateVisit(req);

        result.Should().BeOfType<OkObjectResult>();
        (await _db.Visits.CountAsync()).Should().Be(1,
            "an authorized doctor's Create must persist exactly one visit");
        accessMock.Verify(p => p.CanAccessPatientAsync(seeded.PatientId), Times.Once,
            "the access check must run before persistence for a doctor role");
    }

    // ── Per-patient access (CLIN-01) ─────────────────────────────────────────────

    [Fact]
    public async Task CreateVisit_ByDoctorWithoutPatientAccess_Returns403_ArabicMessage_DoesNotPersist()
    {
        var seeded = await SeedPatientDoctorAsync();

        var accessMock = new Mock<IPatientAccessService>();
        VisitsTestData.SetupDoctorWithAccess(accessMock); // no accessible patients
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(seeded.UserId);
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.GeneralDentist);

        var controller = VisitsTestData.BuildController(_db, accessMock, currentUserMock);

        var req = new CreateVisitRequest
        {
            PatientId = seeded.PatientId,
            DoctorId = seeded.DoctorId,
            VisitType = "معاينة"
        };

        var result = await controller.CreateVisit(req);

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(403);
        ExtractMessage(statusResult.Value).Should().Be("غير مصرح لك بعرض بيانات هذا المريض");

        (await _db.Visits.CountAsync()).Should().Be(0,
            "a denied Create must short-circuit BEFORE any persistence attempt");
    }

    [Fact]
    public async Task CreateVisit_ByNonDoctorRole_AccessCheckBypassed_CanAccessPatientAsyncNeverCalled()
    {
        var patient = VisitsTestData.BuildPatient();
        _db.Patients.Add(patient);
        await _db.SaveChangesAsync();

        var accessMock = new Mock<IPatientAccessService>();
        VisitsTestData.SetupNonDoctor(accessMock); // IsDoctor = false
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.Admin);
        currentUserMock.SetupGet(c => c.IsAdmin).Returns(true);

        var controller = VisitsTestData.BuildController(_db, accessMock, currentUserMock);

        var req = new CreateVisitRequest
        {
            PatientId = patient.Id,
            VisitType = "معاينة"
        };

        await controller.CreateVisit(req);

        accessMock.Verify(p => p.CanAccessPatientAsync(It.IsAny<Guid>()), Times.Never,
            "non-doctor roles must short-circuit the access check (per DenyIfDoctorCannotAccess)");
    }

    // ── Required-field + FK validation (Arabic 400 messages) ─────────────────────

    [Fact]
    public async Task CreateVisit_EmptyPatientId_Returns400_ArabicMessage()
    {
        var controller = BuildControllerAsAdmin();

        var req = new CreateVisitRequest
        {
            PatientId = Guid.Empty,
            VisitType = "معاينة"
        };

        var result = await controller.CreateVisit(req);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.StatusCode.Should().Be(400);
        ExtractMessage(bad.Value).Should().Be("معرّف المريض مطلوب");
    }

    [Fact]
    public async Task CreateVisit_UnknownPatient_Returns400_ArabicMessage()
    {
        var controller = BuildControllerAsAdmin();

        var req = new CreateVisitRequest
        {
            PatientId = Guid.NewGuid(), // not seeded
            VisitType = "معاينة"
        };

        var result = await controller.CreateVisit(req);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.StatusCode.Should().Be(400);
        ExtractMessage(bad.Value).Should().Be("المريض غير موجود");
    }

    [Fact]
    public async Task CreateVisit_UnknownDoctor_Returns400_ArabicMessage()
    {
        var patient = VisitsTestData.BuildPatient();
        _db.Patients.Add(patient);
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var req = new CreateVisitRequest
        {
            PatientId = patient.Id,
            DoctorId = Guid.NewGuid(), // not seeded
            VisitType = "معاينة"
        };

        var result = await controller.CreateVisit(req);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.StatusCode.Should().Be(400);
        ExtractMessage(bad.Value).Should().Be("الطبيب غير موجود");
    }

    [Fact]
    public async Task CreateVisit_UnknownService_Returns400_ArabicMessage()
    {
        var patient = VisitsTestData.BuildPatient();
        _db.Patients.Add(patient);
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var req = new CreateVisitRequest
        {
            PatientId = patient.Id,
            ServiceId = Guid.NewGuid(), // not seeded
            VisitType = "معاينة"
        };

        var result = await controller.CreateVisit(req);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.StatusCode.Should().Be(400);
        ExtractMessage(bad.Value).Should().Be("الخدمة غير موجودة");
    }

    // ── Doctor resolution (TEST-12: DoctorId = Doctor.Id, not User.Id) ───────────

    [Fact]
    public async Task CreateVisit_StoresDoctorId_AsProvided_DoctorIdNotUserId()
    {
        // The audit's recurring concern: persisted DoctorId must be Doctor.Id,
        // never the User.Id. The controller stores req.DoctorId verbatim (no
        // User→Doctor substitution), so the caller is responsible for passing
        // doctor.Id. This test asserts that contract: pass doctor.Id, get doctor.Id back.
        var seeded = await SeedPatientDoctorAsync();

        var controller = BuildControllerAsAdmin();

        var req = new CreateVisitRequest
        {
            PatientId = seeded.PatientId,
            DoctorId = seeded.DoctorId, // <- Doctor.Id, NOT User.Id
            VisitType = "معاينة"
        };

        var result = await controller.CreateVisit(req);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var newVisitId = (Guid)ok.Value!.GetType().GetProperty("Id")!.GetValue(ok.Value)!;

        _db.ChangeTracker.Clear();
        var stored = await _db.Visits.SingleAsync(v => v.Id == newVisitId);
        stored.DoctorId.Should().Be(seeded.DoctorId,
            "persisted Visit.DoctorId must equal Doctor.Id (the value passed in req.DoctorId)");
        stored.DoctorId.Should().NotBe(seeded.UserId,
            "persisted Visit.DoctorId must NEVER be User.Id — there is no User→Doctor resolution in this controller");
    }

    // ── CLIN-04: queue linking ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateVisit_WithAppointmentId_LinksClinicQueueItem_SetsVisitId_AndTransitionsToInProgress()
    {
        // CLIN-04 fix: CreateVisit must set queueItem.VisitId so the daily-ops "Now Serving"
        // panel and patient-journey VisitStatus can see the new visit. It must also transition
        // a Waiting/Called/InRoom queue item to InProgress.
        var seeded = await SeedPatientDoctorAsync();
        var appointment = VisitsTestData.BuildAppointment(seeded.PatientId, seeded.DoctorId);
        var queueItem = VisitsTestData.BuildClinicQueueItem(seeded.PatientId, appointment.Id, ClinicQueueStatus.Waiting);
        _db.Appointments.Add(appointment);
        _db.ClinicQueueItems.Add(queueItem);
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var req = new CreateVisitRequest
        {
            PatientId = seeded.PatientId,
            DoctorId = seeded.DoctorId,
            AppointmentId = appointment.Id,
            VisitType = "معاينة"
        };

        var result = await controller.CreateVisit(req);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var newVisitId = (Guid)ok.Value!.GetType().GetProperty("Id")!.GetValue(ok.Value)!;

        _db.ChangeTracker.Clear();
        var linkedQueue = await _db.ClinicQueueItems.SingleAsync(q => q.Id == queueItem.Id);
        linkedQueue.VisitId.Should().Be(newVisitId,
            "CLIN-04: the new visit's id must be linked back to the active queue item");
        linkedQueue.Status.Should().Be(ClinicQueueStatus.InProgress,
            "CLIN-04: a Waiting/Called/InRoom queue item must transition to InProgress when a visit is created");
        linkedQueue.StartedAt.Should().NotBeNull(
            "CLIN-04: StartedAt must be set when transitioning to InProgress");
    }

    [Fact]
    public async Task CreateVisit_WithAppointmentId_WhenQueueItemAlreadyHasVisitId_DoesNotOverwrite()
    {
        // Guard against a regression: if the queue item is already linked to an earlier visit
        // (e.g. a re-opened visit), CreateVisit must NOT overwrite the existing VisitId.
        var seeded = await SeedPatientDoctorAsync();
        var appointment = VisitsTestData.BuildAppointment(seeded.PatientId, seeded.DoctorId);
        var earlierVisitId = Guid.NewGuid();
        var queueItem = VisitsTestData.BuildClinicQueueItem(seeded.PatientId, appointment.Id, ClinicQueueStatus.InProgress);
        queueItem.VisitId = earlierVisitId;
        _db.Appointments.Add(appointment);
        _db.ClinicQueueItems.Add(queueItem);
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var req = new CreateVisitRequest
        {
            PatientId = seeded.PatientId,
            DoctorId = seeded.DoctorId,
            AppointmentId = appointment.Id,
            VisitType = "متابعة"
        };

        await controller.CreateVisit(req);

        _db.ChangeTracker.Clear();
        var linkedQueue = await _db.ClinicQueueItems.SingleAsync(q => q.Id == queueItem.Id);
        linkedQueue.VisitId.Should().Be(earlierVisitId,
            "an already-linked queue item must NOT be re-linked to the newer visit");
    }

    [Theory]
    [InlineData(ClinicQueueStatus.Completed)]
    [InlineData(ClinicQueueStatus.Cancelled)]
    [InlineData(ClinicQueueStatus.NoShow)]
    public async Task CreateVisit_WithAppointmentId_WhenQueueItemIsTerminal_DoesNotLink(ClinicQueueStatus terminalStatus)
    {
        // CLIN-04 filter: only Waiting/Called/InRoom/InProgress queue items are candidates.
        // Completed/Cancelled/NoShow items are explicitly excluded — creating a visit against
        // an already-finished appointment must NOT retroactively link a dead queue item.
        var seeded = await SeedPatientDoctorAsync();
        var appointment = VisitsTestData.BuildAppointment(seeded.PatientId, seeded.DoctorId);
        var queueItem = VisitsTestData.BuildClinicQueueItem(seeded.PatientId, appointment.Id, terminalStatus);
        _db.Appointments.Add(appointment);
        _db.ClinicQueueItems.Add(queueItem);
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var req = new CreateVisitRequest
        {
            PatientId = seeded.PatientId,
            DoctorId = seeded.DoctorId,
            AppointmentId = appointment.Id,
            VisitType = "معاينة"
        };

        var result = await controller.CreateVisit(req);

        result.Should().BeOfType<OkObjectResult>("the visit itself must still be created");
        _db.ChangeTracker.Clear();
        var linkedQueue = await _db.ClinicQueueItems.SingleAsync(q => q.Id == queueItem.Id);
        linkedQueue.VisitId.Should().BeNull(
            "a {0} queue item must NOT be linked to the new visit", terminalStatus);
        linkedQueue.Status.Should().Be(terminalStatus,
            "the terminal queue status must be preserved (no transition to InProgress)");
    }

    [Fact]
    public async Task CreateVisit_NoAppointmentId_LeavesQueueItemsUnchanged()
    {
        // Walk-in visit (no appointment): there is no appointmentId to match against,
        // so no queue item should be touched.
        var seeded = await SeedPatientDoctorAsync();
        var queueItem = VisitsTestData.BuildClinicQueueItem(seeded.PatientId, appointmentId: null);
        _db.ClinicQueueItems.Add(queueItem);
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var req = new CreateVisitRequest
        {
            PatientId = seeded.PatientId,
            DoctorId = seeded.DoctorId,
            // AppointmentId deliberately null
            VisitType = "معاينة عادية"
        };

        await controller.CreateVisit(req);

        _db.ChangeTracker.Clear();
        var untouched = await _db.ClinicQueueItems.SingleAsync(q => q.Id == queueItem.Id);
        untouched.VisitId.Should().BeNull(
            "a walk-in visit must not touch any pre-existing queue item");
        untouched.Status.Should().Be(ClinicQueueStatus.Waiting,
            "queue item status must be unchanged when no appointmentId is provided");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<(Guid PatientId, Guid DoctorId, Guid UserId)> SeedPatientDoctorAsync()
    {
        var patient = VisitsTestData.BuildPatient();
        var (user, doctor) = VisitsTestData.BuildDoctor();
        _db.Patients.Add(patient);
        _db.Users.Add(user);
        _db.Doctors.Add(doctor);
        await _db.SaveChangesAsync();
        return (patient.Id, doctor.Id, user.Id);
    }

    private VisitsController BuildControllerAsAdmin()
    {
        var accessMock = new Mock<IPatientAccessService>();
        VisitsTestData.SetupNonDoctor(accessMock);
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.Admin);
        currentUserMock.SetupGet(c => c.IsAdmin).Returns(true);
        currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        return VisitsTestData.BuildController(_db, accessMock, currentUserMock);
    }

    private static string ExtractMessage(object? value)
    {
        value.Should().NotBeNull("the 4xx response must carry a payload");
        var prop = value!.GetType().GetProperty("message");
        prop.Should().NotBeNull("the 4xx response must carry a 'message' field (Arabic)");
        return (string)prop!.GetValue(value)!;
    }
}
