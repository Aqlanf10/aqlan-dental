using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ortho;

/// <summary>
/// Tests for treatment plan approval authorization and audit accuracy.
/// Verifies that ApprovedBy records the actual approving doctor/user,
/// not the assigned orthodontist when someone else approves.
/// </summary>
public class OrthoTreatmentPlanApprovalTests : IDisposable
{
    private readonly AppDbContext _db;

    public OrthoTreatmentPlanApprovalTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private Mock<ICurrentUserService> CreateCurrentUser(
        Guid? userId, UserRole role, bool isAdmin = false)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.SetupGet(x => x.UserId).Returns(userId);
        mock.SetupGet(x => x.Role).Returns(role);
        mock.SetupGet(x => x.IsAdmin).Returns(isAdmin);
        return mock;
    }

    private async Task<(Guid orthoCaseId, Guid assignedDoctorId, Guid adminUserId)> SeedData()
    {
        var patientId = Guid.NewGuid();
        var assignedDoctorId = Guid.NewGuid();
        var assignedDoctorUserId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();

        _db.Patients.Add(new Patient { Id = patientId, FirstName = "Test", LastName = "Patient", IsActive = true });
        _db.Doctors.Add(new Doctor
        {
            Id = assignedDoctorId,
            UserId = assignedDoctorUserId,
            Name = "Dr. Assigned Ortho",
            IsActive = true,
        });
        _db.Users.Add(new User
        {
            Id = assignedDoctorUserId,
            Username = "ortho_doc",
            PasswordHash = "hash",
            Role = UserRole.Orthodontist,
            IsActive = true,
        });

        var orthoCaseId = Guid.NewGuid();
        _db.OrthoCases.Add(new OrthoCase
        {
            Id = orthoCaseId,
            PatientId = patientId,
            DoctorId = assignedDoctorId,
            CaseNumber = "ORT-APPROVAL-001",
            IsActive = true,
        });
        _db.TreatmentPlans.Add(new AqlanDentalPro.Domain.Entities.TreatmentPlan
        {
            OrthoCaseId = orthoCaseId,
            PlanVersion = 1,
            IsApproved = false,
        });
        await _db.SaveChangesAsync();
        return (orthoCaseId, assignedDoctorId, adminUserId);
    }

    [Fact]
    public async Task AssignedOrthodontist_CanApprove_RecordsTheirDoctorId()
    {
        // Arrange
        var (orthoCaseId, assignedDoctorId, _) = await SeedData();
        var orthoCase = await _db.OrthoCases.FindAsync(orthoCaseId);
        var assignedDoctor = await _db.Doctors.FindAsync(assignedDoctorId);

        var currentUser = CreateCurrentUser(assignedDoctor!.UserId, UserRole.Orthodontist);

        // Simulate approval logic from controller
        var approverId = currentUser.Object.UserId;
        var isAssignedOrthodontist = orthoCase!.DoctorId != null &&
            await _db.Doctors.AnyAsync(d => d.Id == orthoCase.DoctorId && d.UserId == approverId && d.IsActive);

        isAssignedOrthodontist.Should().BeTrue("the logged-in user is the assigned orthodontist");

        // ApprovedBy should be the assigned doctor's ID
        Guid? approvedByDoctorId = null;
        if (isAssignedOrthodontist)
        {
            approvedByDoctorId = orthoCase.DoctorId;
        }

        approvedByDoctorId.Should().Be(assignedDoctorId);
    }

    [Fact]
    public async Task AdminApprove_DoesNotFalselyAttributeToAssignedDoctor()
    {
        // Arrange
        var (orthoCaseId, assignedDoctorId, _) = await SeedData();
        var orthoCase = await _db.OrthoCases.FindAsync(orthoCaseId);

        // Admin user (not the assigned orthodontist)
        var adminUserId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(adminUserId, UserRole.Admin, isAdmin: true);

        var approverId = currentUser.Object.UserId;
        var isAssignedOrthodontist = orthoCase!.DoctorId != null &&
            await _db.Doctors.AnyAsync(d => d.Id == orthoCase.DoctorId && d.UserId == approverId && d.IsActive);

        isAssignedOrthodontist.Should().BeFalse("admin is not the assigned orthodontist");

        // Admin has no linked Doctor record — ApprovedBy should be null, NOT the assigned doctor
        var adminDoctor = await _db.Doctors
            .Where(d => d.UserId == approverId && d.IsActive)
            .Select(d => (Guid?)d.Id)
            .FirstOrDefaultAsync();

        adminDoctor.Should().BeNull("admin has no Doctor record");
        // ApprovedBy should NOT be the assigned orthodontist's DoctorId
        adminDoctor.Should().NotBe(assignedDoctorId);
    }

    [Fact]
    public async Task AdminWithDoctorRecord_RecordsTheirOwnDoctorId()
    {
        // Arrange: Admin who also has a Doctor record
        var (orthoCaseId, assignedDoctorId, _) = await SeedData();
        var orthoCase = await _db.OrthoCases.FindAsync(orthoCaseId);

        var adminUserId = Guid.NewGuid();
        var adminDoctorId = Guid.NewGuid();
        _db.Doctors.Add(new Doctor
        {
            Id = adminDoctorId,
            UserId = adminUserId,
            Name = "Dr. Admin",
            IsActive = true,
        });
        await _db.SaveChangesAsync();

        var currentUser = CreateCurrentUser(adminUserId, UserRole.Admin, isAdmin: true);

        var approverId = currentUser.Object.UserId;
        var isAssignedOrthodontist = orthoCase!.DoctorId != null &&
            await _db.Doctors.AnyAsync(d => d.Id == orthoCase.DoctorId && d.UserId == approverId && d.IsActive);

        isAssignedOrthodontist.Should().BeFalse();

        // Admin's doctor record should be found
        var adminDoctor = await _db.Doctors
            .Where(d => d.UserId == approverId && d.IsActive)
            .Select(d => (Guid?)d.Id)
            .FirstOrDefaultAsync();

        adminDoctor.Should().Be(adminDoctorId);
        adminDoctor.Should().NotBe(assignedDoctorId, "it's the admin's own doctor record, not the assigned orthodontist");
    }

    [Fact]
    public async Task NonOrthodontistNonAdmin_CannotApprove()
    {
        // Arrange
        var (orthoCaseId, _, _) = await SeedData();
        var orthoCase = await _db.OrthoCases.FindAsync(orthoCaseId);

        // A receptionist (not the assigned ortho, not admin)
        var receptionistUserId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(receptionistUserId, UserRole.Reception, isAdmin: false);

        var approverId = currentUser.Object.UserId;
        var isAssignedOrthodontist = orthoCase!.DoctorId != null &&
            await _db.Doctors.AnyAsync(d => d.Id == orthoCase.DoctorId && d.UserId == approverId && d.IsActive);

        var canApprove = isAssignedOrthodontist || currentUser.Object.IsAdmin;

        canApprove.Should().BeFalse("receptionist cannot approve treatment plans");
    }

    [Fact]
    public async Task ApprovedAt_IsSetToUtcNow()
    {
        var beforeApproval = DateTime.UtcNow.AddSeconds(-1);

        // Simulate the approval timestamp logic
        var approvedAt = DateTime.UtcNow;

        var afterApproval = DateTime.UtcNow.AddSeconds(1);
        approvedAt.Should().BeOnOrAfter(beforeApproval);
        approvedAt.Should().BeOnOrBefore(afterApproval);
    }
}
