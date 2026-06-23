using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.API.Services;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using DocumentFormat.OpenXml.Packaging;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using A = DocumentFormat.OpenXml.Drawing;

namespace AqlanDentalPro.UnitTests.Ortho;

/// <summary>
/// Sprint-polish regressions: the prepared crop region + flips actually reach the
/// PowerPoint, age is computed correctly, and photo deletion enforces patient access.
/// </summary>
[Collection("ai-env")]
public class OrthoPresentationPolishTests
{
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9WlK7xkAAAAASUVORK5CYII=");

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    // ── Crop composition (pure) ──────────────────────────────────────────────

    [Fact]
    public void CalculateCrop_AppliesPreparedRegionThenAspectFill()
    {
        // 100×100 image, 2:1 box, keep the middle 50% horizontally.
        var crop = PowerPointPresentationBuilder.CalculateCrop(
            100, 100, 200, 100, cropX: 0.25, cropY: 0, cropWidth: 0.5, cropHeight: 1);

        crop.Left.Should().Be(25_000);   // prepared left edge
        crop.Right.Should().Be(25_000);  // prepared right edge
        crop.Top.Should().Be(37_500);    // aspect-fill of the kept sub-image
        crop.Bottom.Should().Be(37_500);
    }

    [Fact]
    public void CalculateCrop_FullRegion_PreservesAspectFillOnlyBehavior()
    {
        var crop = PowerPointPresentationBuilder.CalculateCrop(100, 100, 200, 100);

        crop.Left.Should().Be(0);
        crop.Right.Should().Be(0);
        crop.Top.Should().Be(25_000);
        crop.Bottom.Should().Be(25_000);
    }

    [Fact]
    public void CalculateCrop_InvalidRegion_FallsBackToFullImage()
    {
        var crop = PowerPointPresentationBuilder.CalculateCrop(
            100, 100, 100, 100, cropX: 0.9, cropY: 0, cropWidth: 0.5, cropHeight: 1);

        crop.Should().Be(new PowerPointPresentationBuilder.Crop(0, 0, 0, 0));
    }

    // ── Age ──────────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateAge_NotYetBirthday_DecrementsByOne()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dob = new DateOnly(today.Year - 20, 12, 31); // Dec 31 → birthday almost never passed
        var expected = today.Month == 12 && today.Day == 31 ? 20 : 19;

        OrthoCasePresentationService.CalculateAge(dob).Should().Be(expected);
    }

    // ── Prepared crop + flip reach the deck ──────────────────────────────────

    [Fact]
    public async Task Generate_AppliesPreparedCropAndFlipToSlideImage()
    {
        await using var db = CreateDb();
        var uploads = Path.Combine(Path.GetTempPath(), $"ortho-polish-{Guid.NewGuid():N}");
        Directory.CreateDirectory(uploads);
        var previous = Environment.GetEnvironmentVariable("UPLOADS_PATH");
        try
        {
            var fileName = $"{Guid.NewGuid():N}.png";
            await File.WriteAllBytesAsync(Path.Combine(uploads, fileName), OnePixelPng);
            Environment.SetEnvironmentVariable("UPLOADS_PATH", uploads);

            var patient = new Patient { Id = Guid.NewGuid(), FirstName = "ر", LastName = "ح", PatientNumber = "P", IsActive = true };
            var orthoCase = new OrthoCase { Id = Guid.NewGuid(), PatientId = patient.Id, CaseNumber = "OC", Status = OrthoCaseStatus.Active, IsActive = true };
            db.AddRange(patient, orthoCase);
            db.OrthoClinicalPhotos.Add(new OrthoClinicalPhoto
            {
                Id = Guid.NewGuid(),
                OrthoCaseId = orthoCase.Id,
                PhotoUrl = $"/uploads/{fileName}",
                PhotoType = "Extraoral",
                Category = "Extraoral",
                Subtype = "Frontal",
                IsSelectedForReport = true,
                IsActive = true,
                ImagePreparation = new OrthoImagePreparation
                {
                    Id = Guid.NewGuid(),
                    // Keep the middle 60% horizontally and mirror.
                    CropX = 0.2m, CropY = 0m, CropWidth = 0.6m, CropHeight = 1m,
                    FlipHorizontal = true,
                    Status = "ApprovedForPresentation",
                    IsActive = true,
                },
            });
            await db.SaveChangesAsync();

            var bytes = await new OrthoCasePresentationService(db).GenerateAsync(
                orthoCase.Id, new GenerateOrthoCasePresentationRequest());

            using var stream = new MemoryStream(bytes);
            using var document = PresentationDocument.Open(stream, false);

            var sourceRects = document.PresentationPart!.SlideParts
                .SelectMany(part => part.Slide.Descendants<A.SourceRectangle>())
                .ToList();
            // The prepared horizontal region (0.2 .. 0.8) → 20000 cropped off each side.
            sourceRects.Any(r => r.Left is not null && r.Left.Value == 20_000
                              && r.Right is not null && r.Right.Value == 20_000)
                .Should().BeTrue();

            document.PresentationPart.SlideParts
                .SelectMany(part => part.Slide.Descendants<A.Transform2D>())
                .Any(t => t.HorizontalFlip is not null && t.HorizontalFlip.Value)
                .Should().BeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable("UPLOADS_PATH", previous);
            if (Directory.Exists(uploads)) Directory.Delete(uploads, recursive: true);
        }
    }

    // ── DeletePhoto enforces patient access ──────────────────────────────────

    private static OrthoCasesController BuildController(AppDbContext db, bool canAccess)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(x => x.IsAdmin).Returns(true);
        var access = new Mock<IPatientAccessService>();
        access.Setup(x => x.CanAccessPatientAsync(It.IsAny<Guid>())).ReturnsAsync(canAccess);
        var audit = new Mock<IAuditService>();
        return new OrthoCasesController(
            new OrthoService(db, currentUser.Object), db, currentUser.Object, access.Object, audit.Object);
    }

    [Fact]
    public async Task DeletePhoto_DeniedPatientAccess_ReturnsForbid()
    {
        await using var db = CreateDb();
        var caseId = Guid.NewGuid();
        var photoId = Guid.NewGuid();
        db.OrthoCases.Add(new OrthoCase { Id = caseId, PatientId = Guid.NewGuid(), CaseNumber = "OC", IsActive = true });
        db.OrthoClinicalPhotos.Add(new OrthoClinicalPhoto { Id = photoId, OrthoCaseId = caseId, PhotoUrl = "/uploads/x.png", IsActive = true });
        await db.SaveChangesAsync();

        var result = await BuildController(db, canAccess: false).DeletePhoto(caseId, photoId);

        result.Should().BeOfType<ForbidResult>();
        (await db.OrthoClinicalPhotos.CountAsync()).Should().Be(1); // not deleted
    }

    [Fact]
    public async Task DeletePhoto_AllowedAccess_DeletesPhoto()
    {
        await using var db = CreateDb();
        var caseId = Guid.NewGuid();
        var photoId = Guid.NewGuid();
        db.OrthoCases.Add(new OrthoCase { Id = caseId, PatientId = Guid.NewGuid(), CaseNumber = "OC", IsActive = true });
        db.OrthoClinicalPhotos.Add(new OrthoClinicalPhoto { Id = photoId, OrthoCaseId = caseId, PhotoUrl = "/uploads/x.png", IsActive = true });
        await db.SaveChangesAsync();

        var result = await BuildController(db, canAccess: true).DeletePhoto(caseId, photoId);

        result.Should().BeOfType<OkObjectResult>();
        (await db.OrthoClinicalPhotos.CountAsync()).Should().Be(0);
    }
}
