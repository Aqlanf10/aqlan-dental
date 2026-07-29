using AqlanDentalPro.Application.DTOs.WhatsApp;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Appointments;

/// <summary>
/// YOLO-S1: verifies that WhatsAppService.SendAppointmentReminderAsync sends
/// a reminder to BOTH the patient's phone AND the companion's phone when
/// CompanionPhone is present on the appointment (and NOT just the patient's).
///
/// Uses the real WhatsAppService against an InMemory AppDbContext. The
/// IHttpClientFactory is mocked but never invoked because the service is
/// forced into "demo mode" (no WhatsApp:ApiUrl / WhatsApp:ApiToken configured),
/// which is the documented behavior for tests and unconfigured deployments.
///
/// Companion send is best-effort: a companion-send failure must NOT fail the
/// patient send or the SendAppointmentReminderAsync return value.
/// </summary>
public class WhatsAppReminderCompanionTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static WhatsAppService CreateService(AppDbContext db)
    {
        // IConfiguration returns null/empty for WhatsApp config → demo mode →
        // no real HTTP call. The IHttpClientFactory mock is never invoked.
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["WhatsApp:ApiUrl"]).Returns((string?)null);
        configMock.Setup(c => c["WhatsApp:ApiToken"]).Returns((string?)null);

        var httpFactoryMock = new Mock<IHttpClientFactory>();
        httpFactoryMock.Setup(f => f.CreateClient("WhatsApp")).Returns(new HttpClient());

        var loggerMock = new Mock<ILogger<WhatsAppService>>();
        return new WhatsAppService(db, configMock.Object, httpFactoryMock.Object, loggerMock.Object);
    }

    private static async Task<(Guid appointmentId, Guid patientId)> SeedAsync(AppDbContext db,
        string? companionPhone)
    {
        // WhatsAppService.SendMessageAsync looks up the "appointment_reminder" template
        // in the DB. Seed one with active placeholders so the send path doesn't throw.
        if (!await db.WhatsAppTemplates.AnyAsync(t => t.TemplateKey == "appointment_reminder"))
        {
            db.WhatsAppTemplates.Add(new WhatsAppTemplate
            {
                TemplateKey = "appointment_reminder",
                NameAr = "تذكير الموعد",
                ContentTemplate = "مرحباً {patient_name}، نذكرك بموعدك مع {doctor_name} في {date} الساعة {time}.",
                IsTemplateActive = true,
                Category = "appointments",
            });
        }

        var patient = new Patient
        {
            FirstName = "سارة",
            LastName = "أحمد",
            PatientNumber = "P-COMP-1",
            Phone = "+967771111111",
        };
        var doctor = new Doctor { Name = "د. عقلان" };
        db.Patients.Add(patient);
        db.Doctors.Add(doctor);
        await db.SaveChangesAsync();

        var appt = new Appointment
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            AppointmentDate = new DateOnly(2026, 7, 22),
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(10, 30),
            DurationMinutes = 30,
            AppointmentType = "تقويم",
            Status = AppointmentStatus.Scheduled,
            CompanionName = companionPhone != null ? "الأم" : null,
            CompanionPhone = companionPhone,
            CompanionRelationship = companionPhone != null ? "الأم" : null,
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        return (appt.Id, patient.Id);
    }

    [Fact]
    public async Task SendAppointmentReminder_WithCompanionPhone_CreatesTwoMessages_PatientAndCompanion()
    {
        await using var db = CreateContext();
        var (apptId, _) = await SeedAsync(db, companionPhone: "+967772222222");
        var service = CreateService(db);

        var result = await service.SendAppointmentReminderAsync(
            new SendAppointmentReminderRequest { AppointmentId = apptId, HoursBefore = 24 });

        // Patient send must succeed (returns a DTO, not null).
        result.Should().NotBeNull();

        // TWO messages: patient + companion. Both linked to the same appointment.
        var messages = await db.WhatsAppMessages
            .Where(m => m.RelatedEntityId == apptId)
            .ToListAsync();
        messages.Should().HaveCount(2);

        // One must be addressed to the companion's (normalized) phone.
        // NormalizePhone("+967772222222") → "+967772222222" (kept as-is).
        messages.Should().Contain(m => m.PhoneNumber == "+967772222222",
            "companion message must be addressed to the companion phone, not the patient phone");

        // The other must be addressed to the patient's normalized phone.
        // NormalizePhone("+967771111111") → "+967771111111".
        messages.Should().Contain(m => m.PhoneNumber == "+967771111111",
            "patient message must still be sent — companion is IN ADDITION, not instead of");

        // Companion send is best-effort — both should land in "sent" status (demo mode).
        messages.All(m => m.Status == "sent").Should().BeTrue();
    }

    [Fact]
    public async Task SendAppointmentReminder_WithoutCompanionPhone_CreatesOnlyOneMessage()
    {
        await using var db = CreateContext();
        var (apptId, _) = await SeedAsync(db, companionPhone: null);
        var service = CreateService(db);

        var result = await service.SendAppointmentReminderAsync(
            new SendAppointmentReminderRequest { AppointmentId = apptId, HoursBefore = 24 });

        result.Should().NotBeNull();

        var messages = await db.WhatsAppMessages
            .Where(m => m.RelatedEntityId == apptId)
            .ToListAsync();
        messages.Should().HaveCount(1, "no companion phone means no companion send — behavior unchanged for legacy appointments");
        messages[0].PhoneNumber.Should().Be("+967771111111");
    }

    [Fact]
    public async Task SendAppointmentReminder_WithBlankCompanionPhone_DoesNotSendCompanionMessage()
    {
        // Whitespace-only CompanionPhone must be treated as "no companion" —
        // the Trim/check in SendAppointmentReminderAsync guards against this.
        await using var db = CreateContext();
        var (apptId, _) = await SeedAsync(db, companionPhone: "   ");
        var service = CreateService(db);

        var result = await service.SendAppointmentReminderAsync(
            new SendAppointmentReminderRequest { AppointmentId = apptId, HoursBefore = 24 });

        result.Should().NotBeNull();

        var messages = await db.WhatsAppMessages
            .Where(m => m.RelatedEntityId == apptId)
            .ToListAsync();
        messages.Should().HaveCount(1, "blank companion phone must not trigger a companion send");
    }

    [Fact]
    public async Task SendAppointmentReminder_SetsWhatsAppReminderSentAt_OnAppointment()
    {
        await using var db = CreateContext();
        var (apptId, _) = await SeedAsync(db, companionPhone: "+967773333333");
        var service = CreateService(db);

        await service.SendAppointmentReminderAsync(
            new SendAppointmentReminderRequest { AppointmentId = apptId, HoursBefore = 0 });

        var appt = await db.Appointments.FirstAsync(a => a.Id == apptId);
        appt.WhatsAppReminderSentAt.Should().NotBeNull();
    }

    // CORE-PAT-046: a phone typed WITH the country code but no "+" (staff
    // entered "967770245745" directly) used to fall through NormalizePhone
    // unchanged, dialing Meta's Cloud API without the "+" every other stored
    // format above resolves to.
    [Fact]
    public async Task SendAppointmentReminder_CompanionPhoneWithCountryCodeButNoPlus_GetsPlusPrefixed()
    {
        await using var db = CreateContext();
        var (apptId, _) = await SeedAsync(db, companionPhone: "967772222222");
        var service = CreateService(db);

        await service.SendAppointmentReminderAsync(
            new SendAppointmentReminderRequest { AppointmentId = apptId, HoursBefore = 24 });

        var messages = await db.WhatsAppMessages
            .Where(m => m.RelatedEntityId == apptId)
            .ToListAsync();
        messages.Should().Contain(m => m.PhoneNumber == "+967772222222");
    }
}
