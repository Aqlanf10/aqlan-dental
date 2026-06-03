using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
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
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
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
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
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
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
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
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
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

    private static async Task<object> GetSingleJourneyItem(AppDbContext db, DateOnly date)
    {
        var logger = new CapturingLogger<PatientJourneyController>();
        var controller = BuildController(db, logger);
        var result = await controller.GetToday(date.ToString("yyyy-MM-dd"), status: null, doctorId: null, serviceId: null, roomId: null);
        if (result is not OkObjectResult ok)
        {
            throw new InvalidOperationException(logger.LastException?.ToString() ?? $"Unexpected result: {result.GetType().Name}");
        }
        var list = ok.Value.Should().BeAssignableTo<IEnumerable<object>>().Subject.ToList();
        return list.Should().ContainSingle().Subject;
    }

    private static PatientJourneyController BuildController(AppDbContext db, ILogger<PatientJourneyController>? logger = null)
    {
        var access = new Mock<IPatientAccessService>();
        access.SetupGet(x => x.IsDoctor).Returns(false);
        access.SetupGet(x => x.HasFullAccess).Returns(true);
        access.Setup(x => x.GetAccessiblePatientIdsAsync()).ReturnsAsync((HashSet<Guid>?)null);

        return new PatientJourneyController(
            db,
            logger ?? NullLogger<PatientJourneyController>.Instance,
            new Mock<ICommissionService>().Object,
            new Mock<IFinanceService>().Object,
            access.Object);
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
