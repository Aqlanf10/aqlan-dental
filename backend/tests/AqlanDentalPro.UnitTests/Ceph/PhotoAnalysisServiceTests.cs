using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ceph;

public class PhotoAnalysisServiceTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static PhotoAnalysisService CreateService(AppDbContext db, Guid? userId = null)
    {
        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.UserId).Returns(userId ?? Guid.NewGuid());
        return new PhotoAnalysisService(db, user.Object);
    }

    private static async Task<Guid> SeedCaseAsync(AppDbContext db)
    {
        var orthoCase = new OrthoCase { Id = Guid.NewGuid(), PatientId = Guid.NewGuid(), CaseNumber = "OC-P", IsActive = true };
        db.OrthoCases.Add(orthoCase);
        await db.SaveChangesAsync();
        return orthoCase.Id;
    }

    [Fact]
    public async Task Create_SavesAnalysis_AndResolvesDoctorIdFromUser()
    {
        await using var db = CreateDb();
        var caseId = await SeedCaseAsync(db);
        var userId = Guid.NewGuid();
        var doctor = new Doctor { Id = Guid.NewGuid(), UserId = userId, IsActive = true, Name = "د. عقلان" };
        db.Doctors.Add(doctor);
        await db.SaveChangesAsync();

        var (result, error) = await CreateService(db, userId).CreateAsync(new SavePhotoAnalysisRequest
        {
            OrthoCaseId = caseId,
            ViewType = "profile",
            ImageFileUrl = "/uploads/face.jpg",
            LandmarksJson = "{\"Sn\":{\"x\":1,\"y\":2}}",
            MeasurementsJson = "[]",
        });

        error.Should().BeNull();
        result.Should().NotBeNull();
        var saved = await db.PhotoAnalyses.SingleAsync();
        saved.DoctorId.Should().Be(doctor.Id, "DoctorId references Doctors.Id resolved via Doctors.UserId");
        saved.ViewType.Should().Be("profile");
    }

    [Fact]
    public async Task Create_RejectsUnknownCase_WithArabicError()
    {
        await using var db = CreateDb();
        var (result, error) = await CreateService(db).CreateAsync(new SavePhotoAnalysisRequest
        {
            OrthoCaseId = Guid.NewGuid(), ViewType = "frontal", ImageFileUrl = "/uploads/x.jpg",
        });
        result.Should().BeNull();
        error.Should().Be("حالة التقويم غير موجودة");
    }

    [Fact]
    public async Task Create_RejectsInvalidViewType()
    {
        await using var db = CreateDb();
        var caseId = await SeedCaseAsync(db);
        var (_, error) = await CreateService(db).CreateAsync(new SavePhotoAnalysisRequest
        {
            OrthoCaseId = caseId, ViewType = "bogus", ImageFileUrl = "/uploads/x.jpg",
        });
        error.Should().Be("نوع الصورة غير صالح");
    }

    [Fact]
    public async Task List_ReturnsCaseAnalyses_AndGetReturnsDetail()
    {
        await using var db = CreateDb();
        var caseId = await SeedCaseAsync(db);
        var svc = CreateService(db);
        var (created, _) = await svc.CreateAsync(new SavePhotoAnalysisRequest
        {
            OrthoCaseId = caseId, ViewType = "frontal", ImageFileUrl = "/uploads/a.jpg", MeasurementsJson = "[1]",
        });

        var list = await svc.ListAsync(caseId);
        list.Should().HaveCount(1);
        list[0].ViewType.Should().Be("frontal");

        var detail = await svc.GetByIdAsync(created!.Id);
        detail.Should().NotBeNull();
        detail!.MeasurementsJson.Should().Be("[1]");
    }

    [Fact]
    public async Task SoftDelete_RemovesFromActiveQueries()
    {
        await using var db = CreateDb();
        var caseId = await SeedCaseAsync(db);
        var svc = CreateService(db);
        var (created, _) = await svc.CreateAsync(new SavePhotoAnalysisRequest
        {
            OrthoCaseId = caseId, ViewType = "profile", ImageFileUrl = "/uploads/a.jpg",
        });

        (await svc.SoftDeleteAsync(created!.Id)).Should().BeTrue();
        (await svc.ListAsync(caseId)).Should().BeEmpty("soft-deleted rows are filtered by the global query filter");
    }
}
