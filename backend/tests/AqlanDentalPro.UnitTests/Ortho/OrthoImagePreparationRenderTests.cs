using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.API.Services;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using DocumentFormat.OpenXml.Packaging;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SkiaSharp;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ortho;

/// <summary>
/// The server-side renderer bakes every prepared adjustment (crop/zoom/rotation/flip/
/// brightness/contrast/aspect) into a new image, and SaveImagePreparation publishes it
/// as PreparedImageUrl so reports use the fully-prepared copy. Failures degrade safely.
/// </summary>
[Collection("ai-env")]
public class OrthoImagePreparationRenderTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static byte[] TestPng(int width, int height, SKColor color)
    {
        using var bmp = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bmp))
            canvas.Clear(color);
        using var image = SKImage.FromBitmap(bmp);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static OrthoImagePreparationRenderer Renderer() =>
        new(NullLogger<OrthoImagePreparationRenderer>.Instance);

    [Fact]
    public void RenderToImage_AppliesRequestedAspectRatio()
    {
        using var src = SKBitmap.Decode(TestPng(400, 200, SKColors.SteelBlue));
        var prep = new OrthoImagePreparation
        {
            CropX = 0m, CropY = 0m, CropWidth = 1m, CropHeight = 1m, Zoom = 1m,
            AspectRatio = "1:1",
        };

        using var image = OrthoImagePreparationRenderer.RenderToImage(src, prep);

        image.Should().NotBeNull();
        image!.Width.Should().Be(image.Height); // square output for 1:1
    }

    [Fact]
    public void RenderToImage_BrightnessIncreasesMeanLuminance()
    {
        using var src = SKBitmap.Decode(TestPng(120, 120, new SKColor(120, 120, 120)));

        using var plain = OrthoImagePreparationRenderer.RenderToImage(src,
            new OrthoImagePreparation { CropWidth = 1m, CropHeight = 1m, Zoom = 1m, AspectRatio = "1:1" });
        using var bright = OrthoImagePreparationRenderer.RenderToImage(src,
            new OrthoImagePreparation { CropWidth = 1m, CropHeight = 1m, Zoom = 1m, AspectRatio = "1:1", Brightness = 60 });

        MeanLuminance(bright!).Should().BeGreaterThan(MeanLuminance(plain!));
    }

    private static double MeanLuminance(SKImage image)
    {
        using var bmp = SKBitmap.FromImage(image);
        double sum = 0;
        var samples = 0;
        for (var x = 0; x < bmp.Width; x += 16)
        for (var y = 0; y < bmp.Height; y += 16)
        {
            var c = bmp.GetPixel(x, y);
            sum += (0.299 * c.Red) + (0.587 * c.Green) + (0.114 * c.Blue);
            samples++;
        }
        return sum / Math.Max(1, samples);
    }

    [Fact]
    public async Task SaveImagePreparation_PublishesRenderedPreparedImageUrl()
    {
        var uploads = Path.Combine(Path.GetTempPath(), $"ortho-render-{Guid.NewGuid():N}");
        Directory.CreateDirectory(uploads);
        var previous = Environment.GetEnvironmentVariable("UPLOADS_PATH");
        try
        {
            Environment.SetEnvironmentVariable("UPLOADS_PATH", uploads);
            var original = $"{Guid.NewGuid():N}.png";
            await File.WriteAllBytesAsync(Path.Combine(uploads, original), TestPng(300, 200, SKColors.Teal));

            await using var db = CreateDb();
            var caseId = Guid.NewGuid();
            var photoId = Guid.NewGuid();
            db.OrthoCases.Add(new OrthoCase { Id = caseId, PatientId = Guid.NewGuid(), CaseNumber = "OC", IsActive = true });
            db.OrthoClinicalPhotos.Add(new OrthoClinicalPhoto
            {
                Id = photoId, OrthoCaseId = caseId, PhotoUrl = $"/uploads/{original}", IsActive = true,
            });
            await db.SaveChangesAsync();

            var controller = BuildController(db);
            var result = await controller.SaveImagePreparation(caseId, photoId, new SaveOrthoImagePreparationRequest
            {
                CropX = 0.1m, CropY = 0.1m, CropWidth = 0.8m, CropHeight = 0.8m,
                Zoom = 1.2m, RotationDegrees = 0, Brightness = 10, Contrast = 5,
                AspectRatio = "4:5", Status = "ApprovedForPresentation",
            }, Renderer());

            result.Should().BeOfType<OkObjectResult>();
            var prep = await db.OrthoImagePreparations.AsNoTracking().FirstAsync();
            prep.PreparedImageUrl.Should().StartWith("/uploads/prepared-");
            File.Exists(Path.Combine(uploads, Path.GetFileName(prep.PreparedImageUrl!))).Should().BeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable("UPLOADS_PATH", previous);
            if (Directory.Exists(uploads)) Directory.Delete(uploads, recursive: true);
        }
    }

    [Fact]
    public async Task Render_MissingOriginal_ReturnsNullWithoutThrowing()
    {
        var photo = new OrthoClinicalPhoto { Id = Guid.NewGuid(), PhotoUrl = "/uploads/does-not-exist.png" };
        var prep = new OrthoImagePreparation { CropWidth = 1m, CropHeight = 1m, Zoom = 1m };

        // CLIN-12: Render is now async (RenderAsync) — file I/O no longer blocks.
        (await Renderer().RenderAsync(photo, prep)).Should().BeNull();
    }

    private static OrthoCasesController BuildController(AppDbContext db)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(x => x.IsAdmin).Returns(true);
        var access = new Mock<IPatientAccessService>();
        access.Setup(x => x.CanAccessPatientAsync(It.IsAny<Guid>())).ReturnsAsync(true);
        return new OrthoCasesController(
            new OrthoService(db, currentUser.Object), db, currentUser.Object, access.Object);
    }
}
