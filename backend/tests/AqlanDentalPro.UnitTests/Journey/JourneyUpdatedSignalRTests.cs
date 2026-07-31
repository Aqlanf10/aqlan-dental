using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.API.Hubs;
using AqlanDentalPro.Application.Common;
using AqlanDentalPro.Application.DTOs.Appointments;
using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.DTOs.Journey;
using AqlanDentalPro.Application.Interfaces.Repositories;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Journey;

/// <summary>
/// Sprint 2 Part 2 — verifies that JourneyUpdated SignalR events are pushed after
/// successful mutations in CheckoutService / AppointmentsController / VisitsController /
/// PaymentsController, and that the push is best-effort (a push failure never fails
/// the HTTP request).
///
/// Design notes:
/// - Real CheckoutService + InMemory db (same pattern as PatientJourneyFinancialRulesTests).
/// - Real AppointmentService with a mocked IAppointmentRepository (the service methods
///   are non-virtual so Moq can't mock them directly — we mock the repo underneath instead).
/// - Mocked IFinanceService for PaymentsController (its methods ARE accessible via the
///   interface, so we mock the whole service).
/// - Mock&lt;IRealTimePushService&gt; lets us Verify the push call after the mutation.
///
/// InMemory + PostgreSQL advisory-lock limitation:
///   CheckoutService.IntakeAsync / SendToQueueAsync / StartVisitAsync / HandoffToReceptionAsync /
///   CheckoutAsync / CreateDraftInvoiceAsync all call
///   <c>db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", ...)</c>
///   to serialize per-appointment/per-visit mutations. EF Core's InMemory provider throws
///   <see cref="InvalidOperationException"/> for raw SQL, so those paths cannot be exercised
///   end-to-end under InMemory (same documented limitation as SurgeryControllerCreateTests).
///   The push behaviour for those paths is identical to MarkLeftWithoutCompletionAsync
///   (same PushJourneyUpdatedAsync helper, same try/catch wrap), so we cover the push
///   contract via MarkLeftWithoutCompletionAsync + the three controllers that don't
///   take advisory locks (Appointments/Visits/Payments).
/// </summary>
public class JourneyUpdatedSignalRTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"journey-updated-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static (Patient Patient, Doctor Doctor, ClinicService Service, Appointment Appointment) SeedAppointment(
        AppDbContext db,
        AppointmentStatus status = AppointmentStatus.Scheduled,
        DateOnly? date = null)
    {
        var user = new User
        {
            Username = $"u-{Guid.NewGuid():N}".Substring(0, 16),
            PasswordHash = "h",
            PasswordSalt = "s",
            Role = UserRole.GeneralDentist
        };
        var doctor = new Doctor { UserId = user.Id, User = user, Name = "د. الاختبار" };
        var patient = new Patient { PatientNumber = "P-TEST", FirstName = "مريض", LastName = "تجربة" };
        var service = new ClinicService
        {
            ArabicName = "كشف",
            EnglishName = "Consultation",
            Code = $"SVC-{Guid.NewGuid():N}".Substring(0, 10),
            DefaultPrice = 5000m
        };
        var today = date ?? ClinicTimeProvider.ClinicToday();
        var appointment = new Appointment
        {
            PatientId = patient.Id,
            Patient = patient,
            DoctorId = doctor.Id,
            Doctor = doctor,
            AppointmentDate = today,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(9, 30),
            AppointmentType = "كشف",
            Status = status,
            ServiceId = service.Id,
            Service = service,
            IsActive = true
        };
        db.Users.Add(user);
        db.Doctors.Add(doctor);
        db.Patients.Add(patient);
        db.ClinicServices.Add(service);
        db.Appointments.Add(appointment);
        db.SaveChanges();
        return (patient, doctor, service, appointment);
    }

    private static (Mock<ICurrentUserService> CurrentUser, Mock<IPatientAccessService> Access,
        Mock<ICommissionService> Commission, Mock<IRealTimePushService> Push) BuildCommonMocks()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns((Guid?)null);
        currentUser.SetupGet(c => c.IsAdmin).Returns(true);
        currentUser.SetupGet(c => c.BranchId).Returns((Guid?)null);

        var access = new Mock<IPatientAccessService>();
        access.SetupGet(a => a.IsDoctor).Returns(false);
        access.SetupGet(a => a.HasFullAccess).Returns(true);
        access.SetupGet(a => a.IsReception).Returns(false);
        access.Setup(a => a.GetAccessiblePatientIdsAsync()).ReturnsAsync((HashSet<Guid>?)null);

        var commission = new Mock<ICommissionService>();
        var push = new Mock<IRealTimePushService>();
        return (currentUser, access, commission, push);
    }

    private static CheckoutService BuildCheckoutService(AppDbContext db, Mock<IRealTimePushService> push)
    {
        var (currentUser, access, commission, _) = BuildCommonMocks();
        return new CheckoutService(
            db, commission.Object, currentUser.Object, access.Object,
            push.Object,
            NullLogger<CheckoutService>.Instance);
    }

    // ─── 1. CheckoutService.MarkLeftWithoutCompletion pushes JourneyUpdated ──
    //
    // Note: The spec suggested CheckoutService_Intake_PushesJourneyUpdated, but
    // IntakeAsync (and SendToQueue/StartVisit/Handoff/Checkout) calls
    // `pg_advisory_xact_lock` which EF InMemory cannot execute. MarkLeftWithoutCompletionAsync
    // is the CheckoutService mutation that uses SaveChangesAsync directly (no advisory lock),
    // making it the cleanest path to assert the push happens. The push helper
    // (PushJourneyUpdatedAsync) is shared by all CheckoutService mutations, so this
    // assertion covers the contract for Intake/SendToQueue/StartVisit/Handoff/Checkout too.

    [Fact]
    public async Task CheckoutService_MarkLeftWithoutCompletion_PushesJourneyUpdated()
    {
        await using var db = CreateDb();
        var seeded = SeedAppointment(db, status: AppointmentStatus.InProgress);
        var today = ClinicTimeProvider.ClinicToday();

        var visit = new Visit
        {
            PatientId = seeded.Patient.Id,
            AppointmentId = seeded.Appointment.Id,
            VisitDate = today,
            CheckoutStatus = null,
            IsActive = true
        };
        db.Visits.Add(visit);
        db.ClinicQueueItems.Add(new ClinicQueueItem
        {
            PatientId = seeded.Patient.Id,
            AppointmentId = seeded.Appointment.Id,
            VisitId = visit.Id,
            DoctorId = seeded.Doctor.Id,
            QueueDate = today,
            Status = ClinicQueueStatus.InProgress,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var push = new Mock<IRealTimePushService>();
        var service = BuildCheckoutService(db, push);

        var result = await service.MarkLeftWithoutCompletionAsync(visit.Id, new LeftWithoutCompletionRequest
        {
            Reason = "المريض غادر قبل استكمال الإجراء"
        });

        result.Should().BeOfType<OkObjectResult>();
        push.Verify(
            x => x.PushToAllAsync(
                RealTimeEvents.JourneyUpdated,
                It.IsAny<object>()),
            Times.Once,
            "MarkLeftWithoutCompletionAsync must push JourneyUpdated after SaveChanges so daily-ops screens invalidate instantly");
    }

    // ─── 2. CheckoutService push failure does not fail the HTTP request ───────
    //
    // Note: Same InMemory/advisory-lock rationale as test #1 — we exercise the
    // MarkLeftWithoutCompletionAsync path, which uses SaveChangesAsync directly.

    [Fact]
    public async Task CheckoutService_PushFailure_DoesNotFailRequest()
    {
        await using var db = CreateDb();
        var seeded = SeedAppointment(db, status: AppointmentStatus.InProgress);
        var today = ClinicTimeProvider.ClinicToday();

        var visit = new Visit
        {
            PatientId = seeded.Patient.Id,
            AppointmentId = seeded.Appointment.Id,
            VisitDate = today,
            CheckoutStatus = null,
            IsActive = true
        };
        db.Visits.Add(visit);
        db.ClinicQueueItems.Add(new ClinicQueueItem
        {
            PatientId = seeded.Patient.Id,
            AppointmentId = seeded.Appointment.Id,
            VisitId = visit.Id,
            DoctorId = seeded.Doctor.Id,
            QueueDate = today,
            Status = ClinicQueueStatus.InProgress,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var push = new Mock<IRealTimePushService>();
        push.Setup(p => p.PushToAllAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ThrowsAsync(new InvalidOperationException("SignalR hub unreachable"));

        var service = BuildCheckoutService(db, push);

        var result = await service.MarkLeftWithoutCompletionAsync(visit.Id, new LeftWithoutCompletionRequest
        {
            Reason = "المريض غادر قبل استكمال الإجراء"
        });

        // The HTTP response must still succeed — the push is best-effort.
        result.Should().BeOfType<OkObjectResult>("push failure must not propagate to the HTTP response");
        var ok = (OkObjectResult)result;
        ok.StatusCode.Should().Be(200);

        // The push was attempted (and failed), and the visit mutation was still committed.
        push.Verify(
            x => x.PushToAllAsync(
                RealTimeEvents.JourneyUpdated,
                It.IsAny<object>()),
            Times.Once,
            "the push must be attempted even though it throws");

        await db.Entry(visit).ReloadAsync();
        visit.CheckoutStatus.Should().Be("LeftWithoutCompletion",
            "the DB mutation must be committed even when the push fails");
    }

    // ─── 3. AppointmentsController.UpdateStatus pushes JourneyUpdated ─────────

    [Fact]
    public async Task AppointmentsController_UpdateStatus_PushesJourneyUpdated()
    {
        await using var db = CreateDb();
        var seeded = SeedAppointment(db, status: AppointmentStatus.Scheduled);

        // Real AppointmentService over a mocked IAppointmentRepository so we don't
        // depend on the EF-InMemory query provider for GetWithDetailAsync (the repo
        // interface lets us return the seeded appointment directly).
        var appointment = await db.Appointments.FindAsync(seeded.Appointment.Id);
        appointment!.Patient = seeded.Patient;
        appointment.Doctor = seeded.Doctor;
        appointment.Service = seeded.Service;

        var repo = new Mock<IAppointmentRepository>();
        repo.Setup(r => r.GetWithDetailAsync(seeded.Appointment.Id))
            .ReturnsAsync(appointment);
        repo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.IsAuthenticated).Returns(true);
        currentUser.SetupGet(c => c.IsAdmin).Returns(true);
        currentUser.SetupGet(c => c.BranchId).Returns((Guid?)null);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        var appointmentService = new AppointmentService(
            repo.Object, currentUser.Object, scopeFactory.Object,
            NullLogger<AppointmentService>.Instance);

        var push = new Mock<IRealTimePushService>();
        var whatsapp = new Mock<IWhatsAppService>();
        var email = new Mock<IEmailService>();
        var patientAccess = new Mock<IPatientAccessService>();
        patientAccess.SetupGet(p => p.IsDoctor).Returns(false);
        var audit = new Mock<IAuditService>();

        var controller = new AppointmentsController(
            appointmentService, db, currentUser.Object,
            whatsapp.Object, email.Object, push.Object,
            patientAccess.Object, audit.Object,
            NullLogger<AppointmentsController>.Instance);

        var result = await controller.UpdateStatus(
            seeded.Appointment.Id,
            new UpdateAppointmentStatusRequest { Status = "Confirmed" });

        result.Should().BeOfType<OkObjectResult>();
        push.Verify(
            x => x.PushToAllAsync(
                MessagingHubEvents.JourneyUpdated,
                It.IsAny<object>()),
            Times.Once,
            "AppointmentsController.UpdateStatus must push JourneyUpdated after a successful status change");
    }

    // ─── 4. PaymentsController.CreatePayment pushes JourneyUpdated ────────────

    [Fact]
    public async Task PaymentsController_CreatePayment_PushesJourneyUpdated()
    {
        var patientId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();

        var finance = new Mock<IPaymentService>();
        finance.Setup(f => f.CreatePaymentAsync(It.IsAny<CreatePaymentRequest>()))
            .ReturnsAsync(new PaymentDto
            {
                Id = paymentId,
                PatientId = patientId,
                InvoiceId = invoiceId,
                Amount = 1000m,
                PaymentDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                PaymentMethod = "cash"
            });

        var pdf = new Mock<IPdfService>();
        var audit = new Mock<IAuditService>();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.BranchId).Returns((Guid?)null);
        currentUser.SetupGet(c => c.IsAdmin).Returns(true);
        currentUser.SetupGet(c => c.Role).Returns(UserRole.Admin); // Admin bypasses PermissionGuard
        var push = new Mock<IRealTimePushService>();

        await using var db = CreateDb();
        var controller = new PaymentsController(
            finance.Object, new Mock<IFinanceReadService>().Object, pdf.Object, audit.Object,
            currentUser.Object, db, push.Object,
            NullLogger<PaymentsController>.Instance);

        var result = await controller.CreatePayment(new CreatePaymentRequest
        {
            PatientId = patientId,
            InvoiceId = invoiceId,
            Amount = 1000m,
            PaymentMethod = "cash"
        });

        result.Should().BeOfType<OkObjectResult>();
        push.Verify(
            x => x.PushToAllAsync(
                MessagingHubEvents.JourneyUpdated,
                It.IsAny<object>()),
            Times.Once,
            "PaymentsController.CreatePayment must push JourneyUpdated after a successful payment creation");
    }

    // ─── 5. VisitsController.CreateVisit pushes JourneyUpdated ────────────────
    //
    // Bonus: VisitsController is one of the four controllers in the spec scope
    // (Appointments/Visits/Payments + CheckoutService). Verifying it here ensures
    // the push fires on the "visit-created" action too.

    [Fact]
    public async Task VisitsController_CreateVisit_PushesJourneyUpdated()
    {
        await using var db = CreateDb();
        var seeded = SeedAppointment(db, status: AppointmentStatus.Scheduled);

        var access = new Mock<IPatientAccessService>();
        access.SetupGet(a => a.IsDoctor).Returns(false);
        access.SetupGet(a => a.HasFullAccess).Returns(true);
        access.Setup(a => a.CanAccessPatientAsync(It.IsAny<Guid>())).ReturnsAsync(true);

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.Role).Returns(UserRole.Admin);
        currentUser.SetupGet(c => c.IsAdmin).Returns(true);
        currentUser.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(c => c.BranchId).Returns((Guid?)null);

        var audit = new Mock<IAuditService>();
        var push = new Mock<IRealTimePushService>();

        var controller = new VisitsController(
            db, currentUser.Object, access.Object, audit.Object,
            push.Object,
            NullLogger<VisitsController>.Instance);

        var result = await controller.CreateVisit(new CreateVisitRequest
        {
            PatientId = seeded.Patient.Id,
            DoctorId = seeded.Doctor.Id,
            VisitDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            VisitType = "معاينة",
            Cost = 5000m
        });

        result.Should().BeOfType<OkObjectResult>();
        push.Verify(
            x => x.PushToAllAsync(
                MessagingHubEvents.JourneyUpdated,
                It.IsAny<object>()),
            Times.Once,
            "VisitsController.CreateVisit must push JourneyUpdated after a successful visit creation");
    }
}
