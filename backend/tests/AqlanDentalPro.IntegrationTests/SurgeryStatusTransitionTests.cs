using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace AqlanDentalPro.IntegrationTests;

/// <summary>
/// Verifies that <see cref="SurgeryController.UpdateStatus"/> enforces the
/// surgery-case state machine defined in
/// <see cref="AqlanDentalPro.Application.Services.SurgeryCaseStatusTransitions"/>.
/// The controller must reject terminal-state transitions (Completed → Scheduled,
/// Cancelled → InProgress) with HTTP 400 and accept legal transitions
/// (Scheduled → InProgress) with HTTP 200.
///
/// These are integration tests (not unit tests) because the validation runs
/// end-to-end through the ASP.NET pipeline: model binding, FluentValidation
/// (<see cref="UpdateSurgeryStatusRequestValidator"/>), the controller action,
/// the transition validator, and <c>SaveChangesAsync</c> against PostgreSQL.
/// A unit test would only exercise the static
/// <c>SurgeryCaseStatusTransitions.GetValidationError</c> call in isolation
/// and miss a regression where the controller forgets to call it (CLIN-03).
/// </summary>
public class SurgeryStatusTransitionTests : IClassFixture<TestWebAppFactory>, IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;
    private HttpClient _client = null!;
    private SurgerySeed _seed = null!;

    public SurgeryStatusTransitionTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
        _seed = await SeedSurgeryDataAsync();

        // SurgeryAccess policy requires Admin or OralSurgeon. Admin is the simpler
        // choice — it also bypasses the per-patient access check (DenyIfDoctorCannotAccess
        // short-circuits for non-doctor roles), so the test is isolated to the
        // transition-validation behavior under test.
        _client = _factory.CreateAuthenticatedClient(
            _seed.AdminUserId, "test.admin", UserRole.Admin, _seed.BranchId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Completed_To_Scheduled_Returns_BadRequest()
    {
        // Completed is a terminal state — SurgeryCaseStatusTransitions
        // allows no transitions out of it. The controller must call
        // GetValidationError and return 400 with the Arabic message.
        var surgeryId = await CreateSurgeryCaseAsync(initialStatus: SurgeryCaseStatus.Completed);

        var response = await _client.PutAsJsonAsync(
            $"/api/surgery-cases/{surgeryId}/status",
            new { Status = "scheduled" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "Completed is terminal — reverting to Scheduled would corrupt the audit trail");

        // Verify the status was NOT persisted (the controller must short-circuit
        // before SaveChangesAsync when GetValidationError returns non-null).
        await AssertSurgeryStatusUnchangedAsync(surgeryId, SurgeryCaseStatus.Completed);
    }

    [Fact]
    public async Task Scheduled_To_InProgress_Returns_Ok()
    {
        // Scheduled → InProgress is a legal transition (doctor starts the surgery).
        var surgeryId = await CreateSurgeryCaseAsync(initialStatus: SurgeryCaseStatus.Scheduled);

        var response = await _client.PutAsJsonAsync(
            $"/api/surgery-cases/{surgeryId}/status",
            new { Status = "in_progress" });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "Scheduled → InProgress is a legal clinical transition");

        await AssertSurgeryStatusUnchangedAsync(surgeryId, SurgeryCaseStatus.InProgress);
    }

    [Fact]
    public async Task Cancelled_To_InProgress_Returns_BadRequest()
    {
        // Cancelled is also terminal — once a surgery is cancelled it must not
        // be reopened; a new surgery case should be created instead.
        var surgeryId = await CreateSurgeryCaseAsync(initialStatus: SurgeryCaseStatus.Cancelled);

        var response = await _client.PutAsJsonAsync(
            $"/api/surgery-cases/{surgeryId}/status",
            new { Status = "in_progress" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "Cancelled is terminal — must not be able to resume a cancelled surgery");

        await AssertSurgeryStatusUnchangedAsync(surgeryId, SurgeryCaseStatus.Cancelled);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a surgery case with the given initial status by seeding it
    /// directly in the database. The API's POST /api/surgery-cases always
    /// creates with <see cref="SurgeryCaseStatus.Scheduled"/>, so for the
    /// terminal-state tests we have to bypass the API and set the status
    /// ourselves before issuing the PUT.
    /// </summary>
    private async Task<Guid> CreateSurgeryCaseAsync(SurgeryCaseStatus initialStatus)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var surgery = new SurgeryCase
        {
            Id = Guid.NewGuid(),
            CaseNumber = $"SU-{DateTime.UtcNow.Year}-{Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant()}",
            PatientId = _seed.PatientId,
            DoctorId = null, // not required for the status-transition path
            SurgeryType = "Wisdom Tooth Extraction",
            Status = initialStatus,
            IsActive = true,
        };
        db.SurgeryCases.Add(surgery);
        await db.SaveChangesAsync();
        return surgery.Id;
    }

    private async Task AssertSurgeryStatusUnchangedAsync(Guid surgeryId, SurgeryCaseStatus expected)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var surgery = await db.SurgeryCases.AsNoTracking().FirstAsync(s => s.Id == surgeryId);
        surgery.Status.Should().Be(expected,
            "an invalid transition must not persist a new status, and a valid transition must persist the new status");
    }

    private async Task<SurgerySeed> SeedSurgeryDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var branch = new Branch
        {
            Id = Guid.NewGuid(),
            Name = "Surgery Test Branch",
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

        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            PatientNumber = "GM-SURG-001",
            FirstName = "Surgery",
            LastName = "Patient",
            BranchId = branch.Id,
            IsActive = true,
        };
        db.Patients.Add(patient);

        await db.SaveChangesAsync();

        return new SurgerySeed
        {
            AdminUserId = adminUser.Id,
            BranchId = branch.Id,
            PatientId = patient.Id,
        };
    }

    private sealed record SurgerySeed
    {
        public required Guid AdminUserId { get; init; }
        public required Guid BranchId { get; init; }
        public required Guid PatientId { get; init; }
    }
}
