using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Xunit;

namespace AqlanDentalPro.IntegrationTests;

/// <summary>
/// Verifies the per-patient access control enforced by
/// <see cref="PatientsController.GetById"/> via
/// <see cref="IPatientAccessService.CanAccessPatientAsync"/>.
///
/// Doctors (Orthodontist / GeneralDentist / OralSurgeon) are restricted to
/// patients they are linked to (primary doctor, appointment, visit, treatment
/// plan step, or internal referral). Any other staff role (Admin, Reception,
/// Accountant) has unrestricted access.
///
/// The two tests below pin the contract:
///   * A doctor (non-admin) gets HTTP 403 when requesting a patient whose
///     <c>PrimaryDoctorId</c> points at a different doctor.
///   * An admin gets HTTP 200 for the same patient.
///
/// These are integration tests because the access check flows through the full
/// ASP.NET pipeline: JWT authentication → StaffOnly authorization policy →
/// PatientAccessService (which hits PostgreSQL) → AuditLog write on denial →
/// controller action. Unit tests would mock <c>IPatientAccessService</c> and
/// miss a regression where the controller forgets to call
/// <c>DenyIfDoctorCannotAccess</c>.
/// </summary>
public class PatientAccessControlTests : IClassFixture<TestWebAppFactory>, IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;
    private PatientAccessSeed _seed = null!;

    public PatientAccessControlTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
        _seed = await SeedPatientAccessDataAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Doctor_RequestingAnotherDoctorsPatient_Returns_403()
    {
        // Doctor B is a GeneralDentist (a "doctor" role for PatientAccessService)
        // and has a Doctor record, so GetCurrentDoctorIdAsync returns their DoctorId.
        // The patient's PrimaryDoctorId points at Doctor A — so Doctor B has no
        // primary-doctor link, no appointment, no visit, no treatment-plan step,
        // and no internal-referral link to this patient. DenyIfDoctorCannotAccess
        // must therefore return StatusCode(403).
        var client = _factory.CreateAuthenticatedClient(
            _seed.DoctorBUserId, "test.doctor.b", UserRole.GeneralDentist, _seed.BranchId);

        var response = await client.GetAsync($"/api/patients/{_seed.PatientId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a doctor must not be able to view a patient assigned to another doctor");
    }

    [Fact]
    public async Task Admin_RequestingAnyPatient_Returns_200()
    {
        // Admin is not a "doctor" role, so DenyIfDoctorCannotAccess short-circuits
        // and the controller returns the full patient profile (HTTP 200).
        var client = _factory.CreateAuthenticatedClient(
            _seed.AdminUserId, "test.admin", UserRole.Admin, _seed.BranchId);

        var response = await client.GetAsync($"/api/patients/{_seed.PatientId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "admin must have unrestricted access to any patient");
    }

    [Fact]
    public async Task Doctor_RequestingOwnPatient_Returns_200()
    {
        // Mirror image of the 403 test: Doctor A requesting the patient whose
        // PrimaryDoctorId == Doctor A's DoctorId must succeed. This proves the
        // 403 in the previous test was specifically due to the cross-doctor
        // access (and not because the doctor role is universally denied).
        var client = _factory.CreateAuthenticatedClient(
            _seed.DoctorAUserId, "test.doctor.a", UserRole.GeneralDentist, _seed.BranchId);

        var response = await client.GetAsync($"/api/patients/{_seed.PatientId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "a doctor must be able to view their own primary patient");
    }

    // ── Seeding ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds a branch, an admin user, two doctor users (each with a Doctor
    /// entity so <see cref="PatientAccessService.GetCurrentDoctorIdAsync"/>
    /// returns a non-null id), and a patient whose
    /// <see cref="Patient.PrimaryDoctorId"/> points at Doctor A.
    /// </summary>
    private async Task<PatientAccessSeed> SeedPatientAccessDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var branch = new Branch
        {
            Id = Guid.NewGuid(),
            Name = "Access Control Test Branch",
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

        var doctorAUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "test.doctor.a",
            Role = UserRole.GeneralDentist,
            BranchId = branch.Id,
            PasswordHash = "not-used-in-tests",
            PasswordSalt = "not-used-in-tests",
            IsActive = true,
            MustChangePassword = false,
        };
        db.Users.Add(doctorAUser);

        var doctorBUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "test.doctor.b",
            Role = UserRole.GeneralDentist,
            BranchId = branch.Id,
            PasswordHash = "not-used-in-tests",
            PasswordSalt = "not-used-in-tests",
            IsActive = true,
            MustChangePassword = false,
        };
        db.Users.Add(doctorBUser);

        var doctorA = new Doctor
        {
            Id = Guid.NewGuid(),
            UserId = doctorAUser.Id,
            Name = "Dr. A",
            Specialty = "General Dentistry",
            BranchId = branch.Id,
            IsActive = true,
        };
        db.Doctors.Add(doctorA);

        var doctorB = new Doctor
        {
            Id = Guid.NewGuid(),
            UserId = doctorBUser.Id,
            Name = "Dr. B",
            Specialty = "General Dentistry",
            BranchId = branch.Id,
            IsActive = true,
        };
        db.Doctors.Add(doctorB);

        // The patient is owned by Doctor A. Doctor B has no link to them.
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            PatientNumber = "GM-ACCESS-001",
            FirstName = "Access",
            LastName = "Patient",
            PrimaryDoctorId = doctorA.Id,
            BranchId = branch.Id,
            IsActive = true,
        };
        db.Patients.Add(patient);

        await db.SaveChangesAsync();

        return new PatientAccessSeed
        {
            AdminUserId = adminUser.Id,
            DoctorAUserId = doctorAUser.Id,
            DoctorAId = doctorA.Id,
            DoctorBUserId = doctorBUser.Id,
            DoctorBId = doctorB.Id,
            BranchId = branch.Id,
            PatientId = patient.Id,
        };
    }

    private sealed record PatientAccessSeed
    {
        public required Guid AdminUserId { get; init; }
        public required Guid DoctorAUserId { get; init; }
        public required Guid DoctorAId { get; init; }
        public required Guid DoctorBUserId { get; init; }
        public required Guid DoctorBId { get; init; }
        public required Guid BranchId { get; init; }
        public required Guid PatientId { get; init; }
    }
}
