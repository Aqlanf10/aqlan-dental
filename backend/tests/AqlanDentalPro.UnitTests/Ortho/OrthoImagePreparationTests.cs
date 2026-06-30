using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.API.Services;
using AqlanDentalPro.Application.Interfaces.Services;
using Microsoft.Extensions.Logging.Abstractions;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ortho;

public class OrthoImagePreparationTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IPatientAccessService> _patientAccess = new();
    private readonly OrthoImagePreparationRenderer _renderer =
        new(NullLogger<OrthoImagePreparationRenderer>.Instance);
    private readonly OrthoCasesController _controller;

    public OrthoImagePreparationTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _currentUser.SetupGet(x => x.IsAdmin).Returns(true);
        _patientAccess.Setup(x => x.CanAccessPatientAsync(It.IsAny<Guid>())).ReturnsAsync(true);
        _controller = CreateController(_patientAccess.Object);
    }

    public void Dispose() => _db.Dispose();

    private OrthoCasesController CreateController(IPatientAccessService patientAccess) =>
        new(new OrthoService(_db, _currentUser.Object), _db, _currentUser.Object, patientAccess, new Mock<IAuditService>().Object,
            new OrthoCaseQueryService(_db, NullLogger<OrthoCaseQueryService>.Instance));

    private async Task<(Guid CaseId, Guid PhotoId)> SeedPhotoAsync(string photoUrl = "/uploads/original.jpg")
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            FirstName = "Image",
            LastName = "Preparation",
            IsActive = true,
        };
        var orthoCase = new OrthoCase
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            CaseNumber = $"ORT-{Guid.NewGuid():N}",
            IsActive = true,
        };
        var photo = new OrthoClinicalPhoto
        {
            Id = Guid.NewGuid(),
            OrthoCaseId = orthoCase.Id,
            PhotoUrl = photoUrl,
            PhotoType = "Extraoral",
            Category = "Extraoral",
            Subtype = "Profile",
            TreatmentPhase = "Initial",
            IsActive = true,
        };
        _db.AddRange(patient, orthoCase, photo);
        await _db.SaveChangesAsync();
        return (orthoCase.Id, photo.Id);
    }

    private static object ValueOf(IActionResult result) =>
        result.Should().BeOfType<OkObjectResult>().Subject.Value!;

    private static T? PropertyOf<T>(object value, string name) =>
        (T?)value.GetType().GetProperty(name)?.GetValue(value);

    [Fact]
    public async Task SavePreparation_PersistsNonDestructiveMetadata_AndKeepsOriginalPhoto()
    {
        var (caseId, photoId) = await SeedPhotoAsync();

        var result = await _controller.SaveImagePreparation(caseId, photoId, new SaveOrthoImagePreparationRequest
        {
            CropX = 0.1m,
            CropY = 0.15m,
            CropWidth = 0.75m,
            CropHeight = 0.7m,
            Zoom = 1.4m,
            RotationDegrees = 5,
            Brightness = 12,
            Contrast = 18,
            FlipHorizontal = true,
            AspectRatio = "4:5",
            Preset = "ExtraoralProfile",
            Status = "SelectedForPresentation",
        }, _renderer);

        result.Should().BeOfType<OkObjectResult>();
        var photo = await _db.OrthoClinicalPhotos.SingleAsync(p => p.Id == photoId);
        var preparation = await _db.OrthoImagePreparations.SingleAsync(p => p.OrthoClinicalPhotoId == photoId);
        photo.PhotoUrl.Should().Be("/uploads/original.jpg");
        photo.IsSelectedForReport.Should().BeTrue();
        preparation.CropX.Should().Be(0.1m);
        preparation.AspectRatio.Should().Be("4:5");
        preparation.Status.Should().Be("SelectedForPresentation");
        preparation.PreparedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPreparation_WithoutSavedMetadata_ReturnsOriginalDefaults()
    {
        var (caseId, photoId) = await SeedPhotoAsync();

        var value = ValueOf(await _controller.GetImagePreparation(caseId, photoId));

        // The DTO property names are PascalCase (Status, Zoom, OriginalPhotoUrl) — System.Text.Json's
        // default camelCase policy still serializes them to the same JSON keys as the original
        // anonymous type (status, zoom, originalPhotoUrl), so the HTTP response shape is unchanged.
        PropertyOf<string>(value, "Status").Should().Be("OriginalUploaded");
        PropertyOf<decimal>(value, "Zoom").Should().Be(1m);
        PropertyOf<string>(value, "OriginalPhotoUrl").Should().Be("/uploads/original.jpg");
    }

    [Fact]
    public async Task SavePreparation_InvalidCrop_ReturnsBadRequestWithoutWriting()
    {
        var (caseId, photoId) = await SeedPhotoAsync();

        var result = await _controller.SaveImagePreparation(caseId, photoId, new SaveOrthoImagePreparationRequest
        {
            CropX = 0.7m,
            CropWidth = 0.5m,
            CropHeight = 1m,
        }, _renderer);

        result.Should().BeOfType<BadRequestObjectResult>();
        (await _db.OrthoImagePreparations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SavePreparation_WhenPatientAccessDenied_ReturnsForbidWithoutWriting()
    {
        var (caseId, photoId) = await SeedPhotoAsync();
        var deniedAccess = new Mock<IPatientAccessService>();
        deniedAccess.Setup(x => x.CanAccessPatientAsync(It.IsAny<Guid>())).ReturnsAsync(false);
        var controller = CreateController(deniedAccess.Object);

        var result = await controller.SaveImagePreparation(caseId, photoId, new SaveOrthoImagePreparationRequest(), _renderer);

        result.Should().BeOfType<ForbidResult>();
        (await _db.OrthoImagePreparations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ResetPreparation_RemovesMetadata_ClearsSelection_AndKeepsOriginal()
    {
        var (caseId, photoId) = await SeedPhotoAsync();
        await _controller.SaveImagePreparation(caseId, photoId, new SaveOrthoImagePreparationRequest
        {
            Status = "ApprovedForPresentation",
        }, _renderer);

        var result = await _controller.ResetImagePreparation(caseId, photoId, _renderer);

        result.Should().BeOfType<OkObjectResult>();
        (await _db.OrthoImagePreparations.CountAsync()).Should().Be(0);
        var photo = await _db.OrthoClinicalPhotos.SingleAsync(p => p.Id == photoId);
        photo.PhotoUrl.Should().Be("/uploads/original.jpg");
        photo.IsSelectedForReport.Should().BeFalse();
    }

    [Fact]
    public async Task GetPhotos_ExposesPreparationReadinessForPresentation()
    {
        var (caseId, photoId) = await SeedPhotoAsync();
        await _controller.SaveImagePreparation(caseId, photoId, new SaveOrthoImagePreparationRequest
        {
            Status = "PreparedForReport",
            AspectRatio = "4:5",
        }, _renderer);

        var value = ValueOf(await _controller.GetPhotos(caseId));
        var photo = ((System.Collections.IEnumerable)value).Cast<object>().Single();

        PropertyOf<string>(photo, "PreparationStatus").Should().Be("PreparedForReport");
        PropertyOf<bool>(photo, "IsPreparedForReport").Should().BeTrue();
    }
}
