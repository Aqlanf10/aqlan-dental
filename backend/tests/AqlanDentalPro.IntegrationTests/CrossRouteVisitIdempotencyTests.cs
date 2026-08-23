using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Xunit;

namespace AqlanDentalPro.IntegrationTests;

/// <summary>
/// CORE-F-001 — verifies that starting a visit for the same appointment through two
/// different routes at the same time cannot create two Visit rows.
///
/// Three routes can create a Visit for an appointment:
///   * POST /api/appointments/{id}/start-visit           (AppointmentsController)
///   * POST /api/clinic-queue/{queueItemId}/start          (ClinicQueueController)
///   * PatientJourneyController.StartVisit -> CheckoutService.StartVisitAsync
///
/// The first and third lock on the Appointment's own Id (a PostgreSQL advisory
/// lock, `pg_advisory_xact_lock`). Before this fix, ClinicQueueController.StartVisit
/// only locked on the ClinicQueueItem's own Id — a different GUID from the
/// Appointment's Id for the same logical appointment. Two concurrent requests
/// hitting the appointment route and the queue route for the same appointment
/// could therefore each acquire a different advisory lock, both pass the
/// "does a Visit already exist for this appointment" check, and both insert —
/// producing two Visit rows for one AppointmentId (duplicate clinical/financial
/// records). This test hits a real PostgreSQL instance via Testcontainers because
/// advisory locks are PostgreSQL-specific and cannot be reproduced by EF Core's
/// InMemory provider.
/// </summary>
public class CrossRouteVisitIdempotencyTests : IClassFixture<TestWebAppFactory>, IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;
    private HttpClient _client = null!;
    private Guid _appointmentId;
    private Guid _queueItemId;

    public CrossRouteVisitIdempotencyTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
        var seed = await SeedAppointmentAndQueueItemAsync();
        _appointmentId = seed.AppointmentId;
        _queueItemId = seed.QueueItemId;

        _client = _factory.CreateAuthenticatedClient(
            seed.AdminUserId, "test.admin", UserRole.Admin, seed.BranchId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ConcurrentStartVisit_ViaAppointmentRouteAndQueueRoute_ExactlyOneVisitCreated()
    {
        // Act — fire both routes at the same appointment simultaneously.
        var appointmentRouteTask = _client.PostAsync($"/api/appointments/{_appointmentId}/start-visit", null);
        var queueRouteTask = _client.PostAsync($"/api/clinic-queue/{_queueItemId}/start", null);
        var responses = await Task.WhenAll(appointmentRouteTask, queueRouteTask);

        // At least one request must succeed; the DB invariant below is the real
        // assertion (a 200/200 with a shared VisitId is fine, but two distinct
        // Visit rows is the bug this test guards against).
        responses.Should().Contain(r => r.StatusCode == HttpStatusCode.OK,
            "at least one of the two routes must succeed in starting the visit");

        // Assert — exactly one active Visit row exists for this appointment,
        // no matter how the two routes' responses came back.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var visits = await db.Visits
            .Where(v => v.AppointmentId == _appointmentId && v.IsActive)
            .ToListAsync();

        visits.Should().HaveCount(1,
            "the appointment-scoped advisory lock must serialize both routes so only one Visit is ever created");
    }

    // ── Seeding helpers ───────────────────────────────────────────────────────

    private async Task<Seed> SeedAppointmentAndQueueItemAsync()
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
            PatientNumber = "GM-TEST-002",
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
            Code = "CONS2",
            DefaultDurationMinutes = 30,
            DefaultPrice = 100,
            IsActive = true,
        };
        db.ClinicServices.Add(service);

        // Both StartVisit routes reject an appointment that is not today, and they ask
        // ClinicTimeProvider what today is. Seeding the server's date puts the fixture a
        // day behind the clinic between 21:00 and 24:00 UTC — after midnight in Aden —
        // and the routes correctly refuse a yesterday appointment.
        var today = ClinicTimeProvider.ClinicToday();

        // AppointmentStatus.InRoom -> InProgress is a valid transition
        // (AppointmentStatusTransitions), which is what AppointmentsController.StartVisit
        // requires.
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            BranchId = branch.Id,
            AppointmentDate = today,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(9, 30),
            DurationMinutes = 30,
            AppointmentType = "Consultation",
            Status = AppointmentStatus.InRoom,
            ServiceId = service.Id,
            IsActive = true,
        };
        db.Appointments.Add(appointment);

        // ClinicQueueStatus.InRoom -> InProgress is a valid transition
        // (ClinicQueueStatusTransitions), which is what ClinicQueueController.StartVisit
        // requires. Linked to the same appointment via AppointmentId, which is the
        // whole point of this test: two different Ids (queue item vs appointment) for
        // the same logical patient encounter.
        var queueItem = new ClinicQueueItem
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            AppointmentId = appointment.Id,
            DoctorId = doctor.Id,
            ServiceId = service.Id,
            Status = ClinicQueueStatus.InRoom,
            QueueDate = today,
            IsActive = true,
        };
        db.ClinicQueueItems.Add(queueItem);

        await db.SaveChangesAsync();

        return new Seed
        {
            AdminUserId = adminUser.Id,
            BranchId = branch.Id,
            AppointmentId = appointment.Id,
            QueueItemId = queueItem.Id,
        };
    }

    private sealed record Seed
    {
        public required Guid AdminUserId { get; init; }
        public required Guid BranchId { get; init; }
        public required Guid AppointmentId { get; init; }
        public required Guid QueueItemId { get; init; }
    }
}
