using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace AqlanDentalPro.IntegrationTests;

/// <summary>
/// Verifies that the appointment double-booking race is correctly serialized by
/// <see cref="AqlanDentalPro.Infrastructure.Repositories.AppointmentRepository.TryCreateWithConflictGuardAsync"/>,
/// which acquires a per-doctor PostgreSQL advisory lock inside a transaction before
/// the conflict check + insert. Without that lock, two concurrent POST
/// /api/appointments for the same doctor+slot would both pass the conflict check
/// and both insert — producing two 200s and a corrupted schedule.
///
/// These tests hit a real PostgreSQL instance (via Testcontainers) because the
/// advisory lock + transaction behavior is PostgreSQL-specific and cannot be
/// reproduced by EF Core's InMemory provider.
/// </summary>
public class AppointmentDoubleBookingTests : IClassFixture<TestWebAppFactory>, IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;
    private HttpClient _client = null!;
    private AdminSeed _seed = null!;

    public AppointmentDoubleBookingTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        // Reset the schema so no rows leak between test classes.
        await _factory.ResetDatabaseAsync();

        // Seed a branch, doctor, patient, admin user, and clinic service
        // directly via EF Core — bypasses the API so the test is isolated to
        // the double-booking behavior under test.
        _seed = await SeedAdminAndReferenceDataAsync();

        // Authenticated client as the admin user (StaffOnly policy is required
        // by AppointmentsController). The JWT carries the admin's UserId.
        _client = _factory.CreateAuthenticatedClient(
            _seed.AdminUserId, "test.admin", UserRole.Admin, _seed.BranchId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task TwoConcurrentAppointmentsForSameDoctorAndSlot_ExactlyOneCreated_AndExactlyOneConflict()
    {
        // Arrange — identical appointment payload for both requests.
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd");
        var startTime = "09:30";
        var payload = new
        {
            PatientId = _seed.PatientId,
            DoctorId = _seed.DoctorId,
            AppointmentDate = date,
            StartTime = startTime,
            DurationMinutes = 30,
            AppointmentType = "Consultation",
            ServiceId = _seed.ServiceId,
        };
        var json = JsonContent.Create(payload);

        // Act — fire both POSTs simultaneously. The advisory lock inside
        // TryCreateWithConflictGuardAsync serializes them; the loser sees the
        // winner's row when it re-checks HasConflictAsync inside its own
        // (now-blocked) transaction and returns false → 409 Conflict.
        var response1Task = _client.PostAsync("/api/appointments", json);
        var response2Task = _client.PostAsync("/api/appointments", json);
        var responses = await Task.WhenAll(response1Task, response2Task);

        // Assert — exactly one Created (201) and exactly one Conflict (409).
        // The controller returns CreatedAtAction (201) on success and
        // Conflict(new { message = error }) when TryCreateWithConflictGuardAsync
        // returns false. Note: .NET's HttpClient follows redirects by default
        // but CreatedAtAction returns Location header with 201 — no redirect.
        var statusCodes = responses.Select(r => r.StatusCode).ToList();

        statusCodes.Should().HaveCount(2);
        statusCodes.Count(c => c == HttpStatusCode.Created).Should().Be(1,
            "exactly one request must succeed");
        statusCodes.Count(c => c == HttpStatusCode.Conflict).Should().Be(1,
            "exactly one request must be rejected as a double booking");

        // Belt-and-braces: confirm only one appointment row exists for that
        // doctor+date+start in the database.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dateOnly = DateOnly.Parse(date);
            var startOnly = TimeOnly.Parse(startTime);
            var appts = await db.Appointments
                .Where(a => a.DoctorId == _seed.DoctorId
                            && a.AppointmentDate == dateOnly
                            && a.StartTime == startOnly
                            && a.IsActive)
                .ToListAsync();
            appts.Should().HaveCount(1, "the advisory lock must prevent two inserts for the same slot");
        }
    }

    [Fact]
    public async Task SequentialAppointmentsForSameDoctorAndSlot_SecondReturnsConflict()
    {
        // Sanity check — the non-concurrent path also rejects the second booking.
        // This guards against a regression where the conflict check is correct but
        // the advisory lock is broken (so the concurrent test above would also pass
        // by luck rather than by serialization).
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)).ToString("yyyy-MM-dd");
        var startTime = "10:00";
        var payload = new
        {
            PatientId = _seed.PatientId,
            DoctorId = _seed.DoctorId,
            AppointmentDate = date,
            StartTime = startTime,
            DurationMinutes = 30,
            AppointmentType = "Consultation",
            ServiceId = _seed.ServiceId,
        };

        var first = await _client.PostAsync("/api/appointments", JsonContent.Create(payload));
        var second = await _client.PostAsync("/api/appointments", JsonContent.Create(payload));

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── Seeding helpers ───────────────────────────────────────────────────────

    private async Task<AdminSeed> SeedAdminAndReferenceDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var branch = new Branch
        {
            Id = Guid.NewGuid(),
            Name = "Test Branch",
            IsMain = true,
            IsActive = true,
        };
        db.Branches.Add(branch);

        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "test.admin",
            Role = UserRole.Admin,
            BranchId = branch.Id,
            PasswordHash = "not-used-in-tests",
            PasswordSalt = "not-used-in-tests",
            IsActive = true,
            MustChangePassword = false,
        };
        db.Users.Add(adminUser);

        var doctorUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "test.doctor",
            Role = UserRole.GeneralDentist,
            BranchId = branch.Id,
            PasswordHash = "not-used-in-tests",
            PasswordSalt = "not-used-in-tests",
            IsActive = true,
            MustChangePassword = false,
        };
        db.Users.Add(doctorUser);

        var doctor = new Doctor
        {
            Id = Guid.NewGuid(),
            UserId = doctorUser.Id,
            Name = "Dr. Test",
            Specialty = "General Dentistry",
            BranchId = branch.Id,
            Color = "#FF0000",
            IsActive = true,
        };
        db.Doctors.Add(doctor);

        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            PatientNumber = "GM-TEST-001",
            FirstName = "Test",
            LastName = "Patient",
            PrimaryDoctorId = doctor.Id,
            BranchId = branch.Id,
            IsActive = true,
        };
        db.Patients.Add(patient);

        var service = new ClinicService
        {
            Id = Guid.NewGuid(),
            ArabicName = "استشارة",
            EnglishName = "Consultation",
            Code = "CONS",
            DefaultDurationMinutes = 30,
            DefaultPrice = 100,
            IsActive = true,
        };
        db.ClinicServices.Add(service);

        await db.SaveChangesAsync();

        return new AdminSeed
        {
            AdminUserId = adminUser.Id,
            BranchId = branch.Id,
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            ServiceId = service.Id,
        };
    }

    private sealed record AdminSeed
    {
        public required Guid AdminUserId { get; init; }
        public required Guid BranchId { get; init; }
        public required Guid DoctorId { get; init; }
        public required Guid PatientId { get; init; }
        public required Guid ServiceId { get; init; }
    }
}
