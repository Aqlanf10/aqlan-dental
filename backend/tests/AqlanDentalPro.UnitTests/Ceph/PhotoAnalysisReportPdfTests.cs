using AqlanDentalPro.API.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ceph;

/// <summary>
/// Arabic PDF report for saved facial photo analyses: valid %PDF bytes, graceful
/// no-image path, Arabic 404 for a missing analysis, and the header-only image
/// dimension reader.
/// </summary>
public class PhotoAnalysisReportPdfTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static async Task<Guid> SeedAsync(AppDbContext db, string viewType, string imageUrl = "/uploads/missing-photo.jpg")
    {
        var patient = new Patient { Id = Guid.NewGuid(), FirstName = "سالم", LastName = "المخلافي", PatientNumber = "P-9", IsActive = true };
        var orthoCase = new OrthoCase { Id = Guid.NewGuid(), PatientId = patient.Id, CaseNumber = "OC-9", IsActive = true };
        var analysis = new PhotoAnalysis
        {
            Id = Guid.NewGuid(), OrthoCaseId = orthoCase.Id, ViewType = viewType,
            ImageFileUrl = imageUrl, IsActive = true,
            LandmarksJson = "{\"Sn\":{\"x\":120,\"y\":200},\"Pog\":{\"x\":130,\"y\":300}}",
            MeasurementsJson = "[" +
                "{\"key\":\"FacialConvexity\",\"nameAr\":\"تحدّب الوجه\",\"value\":14,\"normal\":12,\"sd\":4,\"severity\":\"normal\",\"interpretationAr\":\"ضمن الحدود الطبيعية\"}," +
                "{\"key\":\"Nasolabial\",\"nameAr\":\"الزاوية الأنفية-الشفوية\",\"value\":90,\"normal\":102,\"sd\":8,\"severity\":\"mild\",\"interpretationAr\":\"بروز القاطع العلوي\"}]",
        };
        db.AddRange(patient, orthoCase, analysis);
        await db.SaveChangesAsync();
        return analysis.Id;
    }

    private static void AssertPdf(byte[] bytes)
    {
        bytes.Should().NotBeNullOrEmpty();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task Generate_Profile_ProducesValidPdf_GracefulWhenImageMissing()
    {
        await using var db = CreateDb();
        var id = await SeedAsync(db, "profile");
        AssertPdf(await new PhotoAnalysisReportPdfGenerator(db).GenerateAsync(id));
    }

    [Fact]
    public async Task Generate_Frontal_AlsoProducesValidPdf()
    {
        await using var db = CreateDb();
        var id = await SeedAsync(db, "frontal");
        AssertPdf(await new PhotoAnalysisReportPdfGenerator(db).GenerateAsync(id));
    }

    [Fact]
    public async Task Generate_MissingAnalysis_ThrowsArgumentException()
    {
        await using var db = CreateDb();
        var act = async () => await new PhotoAnalysisReportPdfGenerator(db).GenerateAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void ReadImageDimensions_ParsesPngHeader()
    {
        var b = new byte[26];
        b[0] = 0x89; b[1] = 0x50; b[2] = 0x4E; b[3] = 0x47;
        b[16] = 0; b[17] = 0; b[18] = 0x01; b[19] = 0x2C; // width 300
        b[20] = 0; b[21] = 0; b[22] = 0; b[23] = 0xC8;     // height 200

        var dims = PhotoAnalysisReportPdfGenerator.ReadImageDimensions(b);
        dims.Should().NotBeNull();
        dims!.Value.W.Should().Be(300);
        dims.Value.H.Should().Be(200);
    }

    [Fact]
    public void ReadImageDimensions_ParsesLosslessWebPHeader()
    {
        var b = new byte[31];
        b[0] = (byte)'R'; b[1] = (byte)'I'; b[2] = (byte)'F'; b[3] = (byte)'F';
        b[8] = (byte)'W'; b[9] = (byte)'E'; b[10] = (byte)'B'; b[11] = (byte)'P';
        b[12] = (byte)'V'; b[13] = (byte)'P'; b[14] = (byte)'8'; b[15] = (byte)'L';
        b[20] = 0x2F;

        // VP8L packs width-1 and height-1 across four bytes.
        const int widthMinusOne = 299;
        const int heightMinusOne = 199;
        b[21] = (byte)(widthMinusOne & 0xFF);
        b[22] = (byte)(((widthMinusOne >> 8) & 0x3F) | ((heightMinusOne & 0x03) << 6));
        b[23] = (byte)((heightMinusOne >> 2) & 0xFF);
        b[24] = (byte)((heightMinusOne >> 10) & 0x0F);

        var dims = PhotoAnalysisReportPdfGenerator.ReadImageDimensions(b);
        dims.Should().NotBeNull();
        dims!.Value.W.Should().Be(300);
        dims.Value.H.Should().Be(200);
    }

    [Fact]
    public async Task Generate_FrontalWithStoredImageAndFullMeasurements_ProducesValidPdf()
    {
        var uploadsDir = Path.Combine(Path.GetTempPath(), "aqlan-uploads");
        Directory.CreateDirectory(uploadsDir);
        var fileName = $"photo-analysis-{Guid.NewGuid():N}.png";
        var filePath = Path.Combine(uploadsDir, fileName);

        // Valid 1x1 PNG. The test exercises image loading, overlay composition,
        // and a full frontal measurement table in the same report.
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        await File.WriteAllBytesAsync(filePath, png);

        try
        {
            await using var db = CreateDb();
            var patient = new Patient
            {
                Id = Guid.NewGuid(),
                FirstName = "سالم",
                LastName = "المخلافي",
                PatientNumber = "P-10",
                IsActive = true,
            };
            var orthoCase = new OrthoCase
            {
                Id = Guid.NewGuid(),
                PatientId = patient.Id,
                CaseNumber = "OC-10",
                IsActive = true,
            };
            var measurements = Enumerable.Range(1, 8)
                .Select(i => new
                {
                    key = $"M{i}",
                    nameAr = $"قياس {i}",
                    value = 1.0 + i / 100.0,
                    normal = 1.0,
                    sd = 0.1,
                    severity = i % 3 == 0 ? "mild" : "normal",
                    interpretationAr = "مراجعة سريرية",
                });
            var analysis = new PhotoAnalysis
            {
                Id = Guid.NewGuid(),
                OrthoCaseId = orthoCase.Id,
                ViewType = "frontal",
                ImageFileUrl = $"/uploads/{fileName}",
                IsActive = true,
                LandmarksJson = "{\"Sn\":{\"x\":0.5,\"y\":0.5}}",
                MeasurementsJson = System.Text.Json.JsonSerializer.Serialize(measurements),
            };
            db.AddRange(patient, orthoCase, analysis);
            await db.SaveChangesAsync();

            AssertPdf(await new PhotoAnalysisReportPdfGenerator(db).GenerateAsync(analysis.Id));
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}
