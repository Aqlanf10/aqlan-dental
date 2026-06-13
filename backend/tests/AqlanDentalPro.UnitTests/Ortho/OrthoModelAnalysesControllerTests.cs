using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ortho;

public class OrthoModelAnalysesControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IPatientAccessService> _patientAccess = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    public OrthoModelAnalysesControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _patientAccess
            .Setup(x => x.CanAccessPatientAsync(It.IsAny<Guid>()))
            .ReturnsAsync(true);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task List_UnassignedDoctorPatient_ReturnsForbid()
    {
        var seeded = await SeedCaseAsync();
        _patientAccess
            .Setup(x => x.CanAccessPatientAsync(seeded.PatientId))
            .ReturnsAsync(false);

        var result = await CreateController().List(seeded.CaseId);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Approve_AssignedOrthodontist_FreezesAnalysis()
    {
        var userId = Guid.NewGuid();
        var seeded = await SeedCaseAsync(userId);
        var analysis = await SeedAnalysisAsync(seeded.CaseId);
        _currentUser.SetupGet(x => x.UserId).Returns(userId);
        _currentUser.SetupGet(x => x.IsAdmin).Returns(false);

        var approveResult = await CreateController().Approve(seeded.CaseId, analysis.Id);
        _db.ChangeTracker.Clear();
        var stored = await _db.ModelAnalyses.SingleAsync(x => x.Id == analysis.Id);

        approveResult.Should().BeOfType<OkObjectResult>();
        stored.ApprovedAt.Should().NotBeNull();
        stored.ApprovedBy.Should().Be(seeded.DoctorId);

        var updateResult = await CreateController().Update(
            seeded.CaseId,
            analysis.Id,
            new SaveDentalModelAnalysisRequest(
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Permanent",
                ValidInput(),
                "changed"));

        updateResult.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task Approve_DifferentOrthodontist_ReturnsForbidWithoutMutation()
    {
        var assignedUserId = Guid.NewGuid();
        var seeded = await SeedCaseAsync(assignedUserId);
        var analysis = await SeedAnalysisAsync(seeded.CaseId);
        _currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _currentUser.SetupGet(x => x.IsAdmin).Returns(false);

        var result = await CreateController().Approve(seeded.CaseId, analysis.Id);

        result.Should().BeOfType<ForbidResult>();
        analysis.ApprovedAt.Should().BeNull();
        analysis.ApprovedBy.Should().BeNull();
    }

    [Fact]
    public async Task Approve_AdminWithoutDoctorRecord_IsValidAndAuditableByTimestamp()
    {
        var seeded = await SeedCaseAsync(Guid.NewGuid());
        var analysis = await SeedAnalysisAsync(seeded.CaseId);
        _currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _currentUser.SetupGet(x => x.IsAdmin).Returns(true);

        var result = await CreateController().Approve(seeded.CaseId, analysis.Id);

        result.Should().BeOfType<OkObjectResult>();
        analysis.ApprovedAt.Should().NotBeNull();
        analysis.ApprovedBy.Should().BeNull();
    }

    private OrthoModelAnalysesController CreateController()
        => new(_db, _patientAccess.Object, _currentUser.Object);

    private async Task<(Guid PatientId, Guid CaseId, Guid? DoctorId)> SeedCaseAsync(Guid? doctorUserId = null)
    {
        var patient = new Patient
        {
            FirstName = "Model",
            LastName = "Patient",
            IsActive = true,
        };
        _db.Patients.Add(patient);

        Doctor? doctor = null;
        if (doctorUserId.HasValue)
        {
            var user = new User
            {
                Id = doctorUserId.Value,
                Username = $"ortho-{Guid.NewGuid():N}",
                PasswordHash = "test",
                IsActive = true,
            };
            doctor = new Doctor
            {
                UserId = user.Id,
                Name = "Orthodontist",
                IsActive = true,
            };
            _db.Users.Add(user);
            _db.Doctors.Add(doctor);
        }

        var orthoCase = new OrthoCase
        {
            Patient = patient,
            Doctor = doctor,
            CaseNumber = $"ORT-{Guid.NewGuid():N}",
            IsActive = true,
        };
        _db.OrthoCases.Add(orthoCase);
        await _db.SaveChangesAsync();

        return (patient.Id, orthoCase.Id, doctor?.Id);
    }

    private async Task<ModelAnalysis> SeedAnalysisAsync(Guid caseId)
    {
        var result = DentalModelAnalysisCalculator.Calculate(ValidInput());
        var row = new ModelAnalysis
        {
            OrthoCaseId = caseId,
            AnalysisDate = DateOnly.FromDateTime(DateTime.UtcNow),
            InputDataJson = System.Text.Json.JsonSerializer.Serialize(ValidInput()),
            ResultDataJson = System.Text.Json.JsonSerializer.Serialize(result),
            IsActive = true,
        };
        _db.ModelAnalyses.Add(row);
        await _db.SaveChangesAsync();
        return row;
    }

    private static DentalModelAnalysisInput ValidInput()
        => new(
            new Dictionary<string, decimal?>
            {
                ["16"] = 10, ["15"] = 7, ["14"] = 7, ["13"] = 8, ["12"] = 7, ["11"] = 9,
                ["21"] = 9, ["22"] = 7, ["23"] = 8, ["24"] = 7, ["25"] = 7, ["26"] = 10,
                ["36"] = 10, ["35"] = 7, ["34"] = 7, ["33"] = 7, ["32"] = 6, ["31"] = 5,
                ["41"] = 5, ["42"] = 6, ["43"] = 7, ["44"] = 7, ["45"] = 7, ["46"] = 10,
            },
            96,
            88,
            37,
            47,
            44,
            40,
            34,
            22,
            21,
            75,
            []);
}
