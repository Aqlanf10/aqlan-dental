using AqlanDentalPro.Application.DTOs.WhatsApp;
using AqlanDentalPro.Application.Services;
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
/// CORE-APPT-012: both messaging services hand-rolled their own phone
/// normalization over the RAW Patient.Phone rather than delegating to
/// PhoneNormalizer (which is what Patients.NormalizedPhone is built with).
///
/// Two real defects followed:
///   (a) char.IsDigit ACCEPTS Arabic-Indic digits (٠-٩) but does not convert
///       them to ASCII, so every StartsWith check missed and an unusable
///       non-ASCII number was handed to the gateway — silently.
///   (b) the "00" international prefix was never stripped, so
///       "00967770245745" became "+9670967770245745".
///
/// Driven through the real WhatsAppService (demo mode, no HTTP) so the assertion
/// is on what actually gets persisted to send, not on a reimplementation.
/// </summary>
public class ReminderPhoneNormalizationTests
{
    private const string ExpectedYemenNumber = "+967770245745";

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static WhatsAppService CreateService(AppDbContext db)
    {
        // No WhatsApp:ApiUrl / ApiToken configured → demo mode → no real HTTP call.
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["WhatsApp:ApiUrl"]).Returns((string?)null);
        config.Setup(c => c["WhatsApp:ApiToken"]).Returns((string?)null);

        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(f => f.CreateClient("WhatsApp")).Returns(new HttpClient());

        return new WhatsAppService(db, config.Object, httpFactory.Object,
            new Mock<ILogger<WhatsAppService>>().Object);
    }

    private static async Task<Guid> SeedAppointmentAsync(AppDbContext db, string patientPhone)
    {
        db.WhatsAppTemplates.Add(new WhatsAppTemplate
        {
            TemplateKey = "appointment_reminder",
            NameAr = "تذكير الموعد",
            ContentTemplate = "مرحباً {patient_name}، نذكرك بموعدك مع {doctor_name} في {date} الساعة {time}.",
            IsTemplateActive = true,
            Category = "appointments",
        });

        var patient = new Patient
        {
            FirstName = "سارة",
            LastName = "أحمد",
            PatientNumber = "P-PHONE-1",
            Phone = patientPhone,
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
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        return appt.Id;
    }

    private static async Task<string?> SendAndGetDialledNumberAsync(string storedPhone)
    {
        await using var db = CreateContext();
        var apptId = await SeedAppointmentAsync(db, storedPhone);

        await CreateService(db).SendAppointmentReminderAsync(
            new SendAppointmentReminderRequest { AppointmentId = apptId, HoursBefore = 24 });

        return await db.WhatsAppMessages
            .Where(m => m.RelatedEntityId == apptId)
            .Select(m => m.PhoneNumber)
            .FirstOrDefaultAsync();
    }

    // ─── (a) Arabic-Indic digits ──────────────────────────────────────────

    [Theory]
    [InlineData("٧٧٠٢٤٥٧٤٥")]        // Arabic-Indic, local 9-digit form
    [InlineData("٠٧٧٠٢٤٥٧٤٥")]       // Arabic-Indic with leading zero
    [InlineData("٩٦٧٧٧٠٢٤٥٧٤٥")]     // Arabic-Indic with country code
    public async Task ArabicIndicDigits_AreConvertedToAscii_NotSentAsGarbage(string stored)
    {
        var dialled = await SendAndGetDialledNumberAsync(stored);

        dialled.Should().Be(ExpectedYemenNumber);
        dialled.Should().MatchRegex(@"^\+\d+$",
            "an Arabic-Indic digit reaching the gateway is an unusable number");
    }

    // ─── (b) The "00" international prefix ────────────────────────────────

    [Fact]
    public async Task DoubleZeroInternationalPrefix_IsStripped_NotDoubled()
    {
        var dialled = await SendAndGetDialledNumberAsync("00967770245745");

        dialled.Should().Be(ExpectedYemenNumber);
        dialled.Should().NotBe("+9670967770245745", "the old code produced exactly this");
    }

    // ─── Formats that already worked must keep working ────────────────────

    [Theory]
    [InlineData("+967770245745")]  // already fully qualified
    [InlineData("967770245745")]   // country code, no plus (CORE-PAT-046)
    [InlineData("0770245745")]     // national form
    [InlineData("770245745")]      // bare subscriber number
    [InlineData("+967 77 024 5745")] // separators
    [InlineData("967-770-245-745")]  // dashes
    public async Task AlreadyValidFormats_StillNormalizeToTheSameNumber(string stored)
    {
        (await SendAndGetDialledNumberAsync(stored)).Should().Be(ExpectedYemenNumber);
    }

    // ─── The shared normalizer both services now delegate to ──────────────

    [Fact]
    public void CanonicalNormalizer_AgreesAcrossEveryInputForm()
    {
        // Send-time normalization must land on the same value stored in
        // Patients.NormalizedPhone, otherwise lookups and sends disagree.
        var forms = new[]
        {
            "٧٧٠٢٤٥٧٤٥", "00967770245745", "+967770245745",
            "967770245745", "0770245745", "770245745",
        };

        forms.Select(PhoneNormalizer.Normalize).Distinct()
            .Should().ContainSingle().Which.Should().Be("967770245745");
    }
}
