using System.Security.Claims;
using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.DTOs.Journey;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.DailyOperations;

public class PatientJourneyFinancialRulesTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task GetToday_ConsultationFeeUnpaid_ReturnsWaitingForPaymentGate()
    {
        await using var db = CreateDb();
        var today = ClinicTimeProvider.ClinicToday();
        SeedAppointment(db, today, appointmentType: "NewConsultation", requiresFee: true, fee: 100m);
        await db.SaveChangesAsync();

        var item = await GetSingleJourneyItem(db, today);

        Get<bool>(item, "PaymentBeforeEntryRequired").Should().BeTrue();
        Get<string>(item, "FinancialEntryStatus").Should().Be("WaitingForPayment");
        Get<bool>(item, "CanEnterWithoutPayment").Should().BeFalse();
        Get<bool>(item, "ManagerOverrideAllowed").Should().BeTrue();
    }

    [Fact]
    public async Task GetToday_EmergencyVisit_DoesNotBlockEntryForUnpaidConsultationFee()
    {
        await using var db = CreateDb();
        var today = ClinicTimeProvider.ClinicToday();
        SeedAppointment(db, today, appointmentType: "حالة إسعافية", requiresFee: true, fee: 100m);
        await db.SaveChangesAsync();

        var item = await GetSingleJourneyItem(db, today);

        Get<bool>(item, "PaymentBeforeEntryRequired").Should().BeFalse();
        Get<string>(item, "FinancialEntryStatus").Should().Be("Clear");
        Get<bool>(item, "CanEnterWithoutPayment").Should().BeTrue();
    }

    [Fact]
    public async Task GetToday_PaidConsultationFee_DoesNotBlockEntry()
    {
        await using var db = CreateDb();
        var today = ClinicTimeProvider.ClinicToday();
        var seeded = SeedAppointment(db, today, appointmentType: "NewConsultation", requiresFee: true, fee: 100m);
        db.Payments.Add(new Payment
        {
            PatientId = seeded.Patient.Id,
            Amount = 100m,
            PaymentDate = today,
            PaymentMethod = "cash",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var item = await GetSingleJourneyItem(db, today);

        Get<bool>(item, "PaymentBeforeEntryRequired").Should().BeFalse();
        Get<bool>(item, "ConsultationFeePaid").Should().BeTrue();
    }

    [Fact]
    public async Task GetToday_VisitWithDraftInvoiceAndLabOrder_ReturnsJourneyMarkers()
    {
        await using var db = CreateDb();
        var today = ClinicTimeProvider.ClinicToday();
        var seeded = SeedAppointment(db, today, appointmentType: "LabOrder", requiresFee: false, fee: 0m);
        var visit = new Visit
        {
            PatientId = seeded.Patient.Id,
            AppointmentId = seeded.Appointment.Id,
            VisitDate = today,
            CheckoutStatus = "ReadyForCheckout",
            IsActive = true
        };
        db.Visits.Add(visit);
        db.Invoices.Add(new Invoice
        {
            PatientId = seeded.Patient.Id,
            AppointmentId = seeded.Appointment.Id,
            VisitId = visit.Id,
            InvoiceNumber = "INV-TEST",
            Status = InvoiceStatus.Draft,
            IsActive = true
        });
        db.LabOrders.Add(new LabOrder
        {
            PatientId = seeded.Patient.Id,
            VisitId = visit.Id,
            Status = "Ready",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var item = await GetSingleJourneyItem(db, today);

        Get<bool>(item, "HasDraftInvoice").Should().BeTrue();
        Get<bool>(item, "HasLabOrder").Should().BeTrue();
        Get<string>(item, "LabOrderStatus").Should().Be("Ready");
        Get<string>(item, "NextAction").Should().Be("Checkout");
    }

    [Fact]
    public async Task GetToday_Doctor_DoesNotReceiveReferenceAmount()
    {
        await using var db = CreateDb();
        var today = ClinicTimeProvider.ClinicToday();
        var seeded = SeedAppointment(db, today, appointmentType: "Treatment", requiresFee: false, fee: 0m);
        db.Visits.Add(new Visit
        {
            PatientId = seeded.Patient.Id,
            AppointmentId = seeded.Appointment.Id,
            VisitDate = today,
            CheckoutStatus = "ReadyForCheckout",
            AmountDueReference = 750m,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var item = await GetSingleJourneyItem(db, today, isDoctor: true);

        GetNullable<decimal>(item, "AmountDueReference").Should().BeNull();
    }

    [Fact]
    public async Task GetToday_FinancialUser_ReceivesReferenceAmount()
    {
        await using var db = CreateDb();
        var today = ClinicTimeProvider.ClinicToday();
        var seeded = SeedAppointment(db, today, appointmentType: "Treatment", requiresFee: false, fee: 0m);
        db.Visits.Add(new Visit
        {
            PatientId = seeded.Patient.Id,
            AppointmentId = seeded.Appointment.Id,
            VisitDate = today,
            CheckoutStatus = "ReadyForCheckout",
            AmountDueReference = 750m,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var item = await GetSingleJourneyItem(db, today);

        GetNullable<decimal>(item, "AmountDueReference").Should().Be(750m);
    }

    [Fact]
    public async Task MarkLeftWithoutCompletion_RequiresReason()
    {
        await using var db = CreateDb();
        var controller = BuildController(db);

        var result = await controller.MarkLeftWithoutCompletion(Guid.NewGuid(), new LeftWithoutCompletionRequest
        {
            Reason = " "
        });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task MarkLeftWithoutCompletion_SetsVisitStatusCancelsQueueAndWritesAudit()
    {
        await using var db = CreateDb();
        var today = ClinicTimeProvider.ClinicToday();
        var seeded = SeedAppointment(db, today, appointmentType: "Treatment", requiresFee: false, fee: 0m);
        seeded.Appointment.Status = AppointmentStatus.InProgress;

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
            DoctorId = seeded.Appointment.DoctorId,
            QueueDate = today,
            Status = ClinicQueueStatus.InProgress,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        var result = await controller.MarkLeftWithoutCompletion(visit.Id, new LeftWithoutCompletionRequest
        {
            Reason = "المريض غادر قبل استكمال الإجراء"
        });

        result.Should().BeOfType<OkObjectResult>();

        var savedVisit = await db.Visits.FindAsync(visit.Id);
        savedVisit!.CheckoutStatus.Should().Be("LeftWithoutCompletion");

        var queueItem = await db.ClinicQueueItems.SingleAsync(q => q.VisitId == visit.Id);
        queueItem.Status.Should().Be(ClinicQueueStatus.Cancelled);
        queueItem.CancelledAt.Should().NotBeNull();

        var audit = await db.AuditLogs.SingleAsync(a => a.Resource == "Visit.LeftWithoutCompletion");
        audit.ResourceId.Should().Be(visit.Id);
        audit.NewData.Should().NotBeNull();
        audit.NewData!.RootElement.GetProperty("reason").GetString()
            .Should().Be("المريض غادر قبل استكمال الإجراء");
        audit.NewData.RootElement.GetProperty("checkoutStatus").GetString()
            .Should().Be("LeftWithoutCompletion");
    }

    // ── FIN-PERM: daily-summary checkout balance is permission-driven ──────────

    [Fact]
    public async Task DailySummary_Reception_WithPaymentsPermission_SeesLimitedCheckoutBalanceOnly()
    {
        await using var db = CreateDb();
        var patient = new Patient { PatientNumber = "P-900", FirstName = "A", LastName = "B", IsActive = true };
        db.Patients.Add(patient);
        // Owner-seeded cashier-safe permission (RolePermissions, configurable from Settings).
        db.RolePermissions.Add(new RolePermission { Role = "Reception", Resource = "finance.payments", CanView = true, CanCreate = true });
        await db.SaveChangesAsync();

        var finance = new Mock<IFinanceReadService>();
        finance.Setup(f => f.GetPatientFinanceSummaryAsync(patient.Id))
            .ReturnsAsync(new PatientFinanceSummaryDto
            {
                TotalTreatmentCost = 2000m,
                TotalPaid = 1500m,
                OutstandingBalance = 500m,
                OverdueAmount = 0m,
                FinancialStatus = "on_track",
                ActiveContractsCount = 1,
                TotalPaymentsCount = 3,
            });

        var result = await BuildService(db, finance).GetDailySummaryAsync(PrincipalInRole("Reception"), patient.Id);

        var fin = ExtractFinanceSummary(result);
        fin.Should().NotBeNull("Reception holds finance.payments and may see the checkout balance");
        Get<decimal>(fin!, "OutstandingBalance").Should().Be(500m);
        HasProperty(fin!, "FinancialStatus").Should().BeTrue();
        HasProperty(fin!, "TotalTreatmentCost").Should().BeFalse("the limited tier must not expose total treatment cost");
        HasProperty(fin!, "TotalPaid").Should().BeFalse("the limited tier must not expose totals/history");
        HasProperty(fin!, "TotalPaymentsCount").Should().BeFalse("the limited tier must not expose payment history counts");
    }

    [Fact]
    public async Task DailySummary_Reception_WithoutPaymentsPermission_SeesNoBalance()
    {
        await using var db = CreateDb();
        var patient = new Patient { PatientNumber = "P-901", FirstName = "A", LastName = "B", IsActive = true };
        db.Patients.Add(patient);
        await db.SaveChangesAsync(); // no RolePermissions seeded for Reception

        var finance = new Mock<IFinanceReadService>();
        var result = await BuildService(db, finance).GetDailySummaryAsync(PrincipalInRole("Reception"), patient.Id);

        ExtractFinanceSummary(result).Should().BeNull("without finance.payments the cashier sees no balance");
        finance.Verify(f => f.GetPatientFinanceSummaryAsync(It.IsAny<Guid>()), Times.Never,
            "the balance must not even be computed when no finance permission is held");
    }

    [Fact]
    public async Task DailySummary_Admin_BypassesAndSeesFullSummary()
    {
        await using var db = CreateDb();
        var patient = new Patient { PatientNumber = "P-902", FirstName = "A", LastName = "B", IsActive = true };
        db.Patients.Add(patient);
        await db.SaveChangesAsync(); // no RolePermissions seeded — Admin must still bypass

        var finance = new Mock<IFinanceReadService>();
        finance.Setup(f => f.GetPatientFinanceSummaryAsync(patient.Id))
            .ReturnsAsync(new PatientFinanceSummaryDto { TotalTreatmentCost = 2000m, OutstandingBalance = 500m, FinancialStatus = "on_track" });

        var result = await BuildService(db, finance).GetDailySummaryAsync(PrincipalInRole("Admin"), patient.Id);

        var fin = ExtractFinanceSummary(result);
        fin.Should().NotBeNull();
        HasProperty(fin!, "TotalTreatmentCost").Should().BeTrue("Admin gets the full summary even with no RolePermission rows");
    }

    private static PatientJourneyService BuildService(AppDbContext db, Mock<IFinanceReadService> finance)
    {
        var access = new Mock<IPatientAccessService>();
        access.SetupGet(x => x.IsDoctor).Returns(false);
        access.SetupGet(x => x.HasFullAccess).Returns(true);
        access.SetupGet(x => x.IsReception).Returns(false);
        access.Setup(x => x.GetAccessiblePatientIdsAsync()).ReturnsAsync((HashSet<Guid>?)null);
        return new PatientJourneyService(db, access.Object, finance.Object, NullLogger<PatientJourneyService>.Instance);
    }

    private static ClaimsPrincipal PrincipalInRole(string role) =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role) }, "TestAuth"));

    private static object? ExtractFinanceSummary(IActionResult result)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var prop = ok.Value!.GetType().GetProperty("FinanceSummary");
        prop.Should().NotBeNull("daily-summary response must include FinanceSummary");
        return prop!.GetValue(ok.Value);
    }

    private static bool HasProperty(object obj, string name) => obj.GetType().GetProperty(name) != null;

    private static async Task<object> GetSingleJourneyItem(AppDbContext db, DateOnly date, bool isDoctor = false)
    {
        var logger = new CapturingLogger<PatientJourneyService>();
        var controller = BuildController(db, logger, isDoctor);
        var result = await controller.GetToday(date.ToString("yyyy-MM-dd"), status: null, doctorId: null, serviceId: null, roomId: null);
        if (result is not OkObjectResult ok)
        {
            throw new InvalidOperationException(logger.LastException?.ToString() ?? $"Unexpected result: {result.GetType().Name}");
        }
        var list = ok.Value.Should().BeAssignableTo<IEnumerable<object>>().Subject.ToList();
        return list.Should().ContainSingle().Subject;
    }

    private static PatientJourneyController BuildController(
        AppDbContext db,
        ILogger<PatientJourneyService>? journeyLogger = null,
        bool isDoctor = false)
    {
        var access = new Mock<IPatientAccessService>();
        access.SetupGet(x => x.IsDoctor).Returns(isDoctor);
        access.SetupGet(x => x.HasFullAccess).Returns(!isDoctor);
        access.SetupGet(x => x.IsReception).Returns(false);
        access.Setup(x => x.GetAccessiblePatientIdsAsync()).ReturnsAsync((HashSet<Guid>?)null);

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns((Guid?)null);
        currentUser.SetupGet(x => x.IsAdmin).Returns(true);

        var commission = new Mock<ICommissionService>();
        var finance = new Mock<IFinanceReadService>();
        var audit = new Mock<IAuditService>();

        // CLIN-22: controller is now a thin delegate over PatientJourneyService + CheckoutService.
        // Real service instances + InMemory db preserve the original end-to-end test semantics.
        var push = new Mock<IRealTimePushService>();
        var journeyService = new PatientJourneyService(
            db, access.Object, finance.Object,
            journeyLogger ?? NullLogger<PatientJourneyService>.Instance);
        var checkoutService = new CheckoutService(
            db, commission.Object, currentUser.Object, access.Object,
            push.Object,
            NullLogger<CheckoutService>.Instance);

        return new PatientJourneyController(journeyService, checkoutService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = PrincipalInRole(isDoctor ? "GeneralDentist" : "Admin")
                }
            }
        };
    }

    private static (Patient Patient, Appointment Appointment, ClinicService Service) SeedAppointment(
        AppDbContext db,
        DateOnly date,
        string appointmentType,
        bool requiresFee,
        decimal fee)
    {
        var user = new User
        {
            Username = Guid.NewGuid().ToString("N"),
            PasswordHash = "hash",
            PasswordSalt = "salt",
            Role = UserRole.GeneralDentist
        };
        var doctor = new Doctor
        {
            UserId = user.Id,
            User = user,
            Name = "Test Doctor"
        };
        var patient = new Patient
        {
            PatientNumber = "P-001",
            FirstName = "Test",
            LastName = "Patient"
        };
        var service = new ClinicService
        {
            ArabicName = "كشف",
            EnglishName = "Consultation",
            Code = Guid.NewGuid().ToString("N"),
            RequiresConsultationFee = requiresFee,
            DefaultPrice = fee
        };
        var appointment = new Appointment
        {
            PatientId = patient.Id,
            Patient = patient,
            DoctorId = doctor.Id,
            Doctor = doctor,
            AppointmentDate = date,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(9, 30),
            AppointmentType = appointmentType,
            Status = AppointmentStatus.Scheduled,
            ServiceId = service.Id,
            Service = service,
            IsActive = true
        };

        db.Users.Add(user);
        db.Doctors.Add(doctor);
        db.Patients.Add(patient);
        db.ClinicServices.Add(service);
        db.Appointments.Add(appointment);
        return (patient, appointment, service);
    }

    private static T Get<T>(object item, string propertyName)
    {
        var property = item.GetType().GetProperty(propertyName);
        property.Should().NotBeNull($"journey item should include {propertyName}");
        return (T)property!.GetValue(item)!;
    }

    private static T? GetNullable<T>(object item, string propertyName) where T : struct
    {
        var property = item.GetType().GetProperty(propertyName);
        property.Should().NotBeNull($"journey item should include {propertyName}");
        return (T?)property!.GetValue(item);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public Exception? LastException { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            LastException = exception;
        }
    }
}
