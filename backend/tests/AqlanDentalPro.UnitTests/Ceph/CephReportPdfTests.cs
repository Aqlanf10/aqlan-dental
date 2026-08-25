using AqlanDentalPro.API.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ceph;

/// <summary>
/// Ceph batch C-C tests — Arabic cephalometric PDF report.
/// Follows PdfEndpointErrorHandlingTests / LabOrderPdfStabilizationTests patterns:
/// generator resilience (valid %PDF bytes, graceful no-image path), Arabic 404,
/// Settings-driven clinic identity resolution, and the no-exception-leak source check.
/// </summary>
public class CephReportPdfTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static async Task<Guid> SeedFullAnalysisAsync(AppDbContext db, string? xrayFileUrl = "/uploads/does-not-exist-ceph.png")
    {
        var patient = new Patient { Id = Guid.NewGuid(), FirstName = "سالم", LastName = "المخلافي", PatientNumber = "P-100", IsActive = true };
        var orthoCase = new OrthoCase { Id = Guid.NewGuid(), PatientId = patient.Id, CaseNumber = "OC-77", IsActive = true };
        var analysis = new CephAnalysis
        {
            Id = Guid.NewGuid(),
            OrthoCaseId = orthoCase.Id,
            AnalysisDate = new DateOnly(2026, 6, 1),
            AnalysisType = "full",
            XrayFileUrl = xrayFileUrl,
            Notes = "{\"PixelsPerMm\":7.5,\"ImageWidth\":1000,\"ImageHeight\":800,\"UserNotes\":null}",
            IsActive = true,
        };
        db.AddRange(patient, orthoCase, analysis);

        db.CephLandmarks.AddRange(
            new CephLandmark { AnalysisId = analysis.Id, LandmarkKey = "S",  LandmarkName = "السرج",     XCoord = 450, YCoord = 320, IsActive = true },
            new CephLandmark { AnalysisId = analysis.Id, LandmarkKey = "N",  LandmarkName = "الناسيون",  XCoord = 650, YCoord = 240, IsActive = true },
            new CephLandmark { AnalysisId = analysis.Id, LandmarkKey = "A",  LandmarkName = "النقطة A", XCoord = 730, YCoord = 520, IsActive = true },
            new CephLandmark { AnalysisId = analysis.Id, LandmarkKey = "B",  LandmarkName = "النقطة B", XCoord = 680, YCoord = 630, IsActive = true },
            new CephLandmark { AnalysisId = analysis.Id, LandmarkKey = "Go", LandmarkName = "زاوية الفك", XCoord = 370, YCoord = 690, IsActive = true },
            new CephLandmark { AnalysisId = analysis.Id, LandmarkKey = "Gn", LandmarkName = "الذقن",     XCoord = 640, YCoord = 730, IsActive = true });

        db.CephMeasurements.AddRange(
            new CephMeasurement { AnalysisId = analysis.Id, MeasurementName = "SNA", MeasurementValue = 86, NormalValue = 82, StdDeviation = 2, Unit = "°", Deviation = 4, Classification = "mild", IsActive = true },
            new CephMeasurement { AnalysisId = analysis.Id, MeasurementName = "SNB", MeasurementValue = 80, NormalValue = 80, StdDeviation = 2, Unit = "°", Deviation = 0, Classification = "normal", IsActive = true },
            new CephMeasurement { AnalysisId = analysis.Id, MeasurementName = "ANB", MeasurementValue = 6,  NormalValue = 2,  StdDeviation = 1, Unit = "°", Deviation = 4, Classification = "severe", IsActive = true },
            new CephMeasurement { AnalysisId = analysis.Id, MeasurementName = "FMA", MeasurementValue = 25, NormalValue = 25, StdDeviation = 4, Unit = "°", Deviation = 0, Classification = "normal", IsActive = true });

        db.CephDiagnoses.Add(new CephDiagnosis
        {
            AnalysisId = analysis.Id,
            SkeletalClass = "Class II",
            VerticalPattern = "Normodivergent",
            IncisorInclination = "بروز في القاطعة العلوية",
            AiRecommendation = "التشخيص الهيكلي: الصنف الثاني الهيكلي",
            FinalDiagnosis = "صنف ثاني هيكلي مع بروز سني علوي",
            DoctorApproved = true,
            IsActive = true,
        });

        db.CephNorms.Add(new CephNorm
        {
            MeasurementName = "SNA",
            NameAr = "زاوية SNA",
            AnalysisGroup = "steiner",
            NormalValue = 82,
            StdDeviation = 2,
            Unit = "°",
            InterpretationBelow = "الفك العلوي متراجع للخلف",
            InterpretationAbove = "الفك العلوي بارز للأمام",
            IsActive = true,
        });

        await db.SaveChangesAsync();
        return analysis.Id;
    }

    // ─── Generator: valid PDF bytes, graceful no-image path ──────────────

    [Fact]
    public async Task GenerateAsync_FullAnalysisWithoutImageFile_ReturnsValidPdfBytes()
    {
        await using var db = CreateDb();
        var analysisId = await SeedFullAnalysisAsync(db); // XrayFileUrl points at a missing file

        var pdf = await new CephReportPdfGenerator(db).GenerateAsync(analysisId);

        pdf.Should().NotBeNullOrEmpty();
        pdf.Length.Should().BeGreaterThan(500, "a real report PDF must have meaningful size");
        // %PDF header
        System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF",
            "generated bytes must be a valid PDF even when the radiograph file is unavailable");
    }

    [Fact]
    public async Task GenerateAsync_NullXrayUrl_StillGeneratesPdf()
    {
        await using var db = CreateDb();
        var analysisId = await SeedFullAnalysisAsync(db, xrayFileUrl: null);

        var pdf = await new CephReportPdfGenerator(db).GenerateAsync(analysisId);

        System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task GenerateAsync_WithVtoScenario_IncludesScenarioSectionWithoutFailing()
    {
        await using var db = CreateDb();
        var analysisId = await SeedFullAnalysisAsync(db, xrayFileUrl: null);
        db.CephVtoScenarios.Add(new CephVtoScenario
        {
            CephAnalysisId = analysisId,
            ScenarioGroupId = Guid.NewGuid(),
            VersionNumber = 1,
            Name = "خطة إرجاع القواطع",
            UpperIncisorMoveMm = -2m,
            LowerIncisorMoveMm = 0.5m,
            OverjetBeforeMm = 5m,
            OverjetAfterMm = 2.5m,
            Notes = "مراجعة الطبيب قبل الاعتماد",
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var pdf = await new CephReportPdfGenerator(db).GenerateAsync(analysisId);

        pdf.Length.Should().BeGreaterThan(500);
        System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task GenerateAsync_NoLandmarksNoMeasurementsNoDiagnosis_StillGeneratesPdf()
    {
        await using var db = CreateDb();
        var patient = new Patient { Id = Guid.NewGuid(), FirstName = "مريم", LastName = "قائد", IsActive = true };
        var orthoCase = new OrthoCase { Id = Guid.NewGuid(), PatientId = patient.Id, CaseNumber = "OC-1", IsActive = true };
        var analysis = new CephAnalysis { Id = Guid.NewGuid(), OrthoCaseId = orthoCase.Id, AnalysisType = "steiner", IsActive = true };
        db.AddRange(patient, orthoCase, analysis);
        await db.SaveChangesAsync();

        var pdf = await new CephReportPdfGenerator(db).GenerateAsync(analysis.Id);

        System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF",
            "an empty analysis must still produce a valid PDF (no silent failures, no crash)");
    }

    [Fact]
    public async Task GenerateAsync_WithRealImageFile_RendersImageAndOverlayLayer()
    {
        // Place a real (1x1) PNG in the temp uploads fallback directory so the
        // full Layers(image + SVG overlay) render path is exercised.
        var dir = Path.Combine(Path.GetTempPath(), "aqlan-uploads");
        Directory.CreateDirectory(dir);
        var fileName = $"{Guid.NewGuid():N}.png";
        var fullPath = Path.Combine(dir, fileName);
        File.WriteAllBytes(fullPath, Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="));

        try
        {
            await using var db = CreateDb();
            var analysisId = await SeedFullAnalysisAsync(db, xrayFileUrl: $"/uploads/{fileName}");

            var pdf = await new CephReportPdfGenerator(db).GenerateAsync(analysisId);

            System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF",
                "the radiograph + SVG landmark overlay must render into a valid PDF");
        }
        finally
        {
            File.Delete(fullPath);
        }
    }

    // ─── Arabic 404 ───────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateAsync_MissingAnalysis_ThrowsArgumentException()
    {
        await using var db = CreateDb();

        var act = () => new CephReportPdfGenerator(db).GenerateAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<ArgumentException>(
            "the controller maps ArgumentException to the Arabic 404 «تحليل السيفالومتري غير موجود»");
    }

    [Fact]
    public void ReportPdfEndpoint_Returns404WithArabicMessage_SourceCheck()
    {
        var controllerPath = ControllerSourcePath("CephController.cs");
        if (!File.Exists(controllerPath)) return;

        var content = File.ReadAllText(controllerPath);
        content.Should().Contain("تحليل السيفالومتري غير موجود",
            "missing analyses must return the standard Arabic 404 message");
    }

    // ─── Settings-driven clinic identity (no hardcoding) ──────────────────

    [Fact]
    public async Task ResolveClinicIdentityAsync_AllKeysPresent_ReturnsConfiguredValues()
    {
        await using var db = CreateDb();
        db.Settings.AddRange(
            new Setting { Key = "clinic.name", Value = "مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان" },
            new Setting { Key = "clinic.lead_doctor", Value = "د. عقلان الكامل" },
            new Setting { Key = "clinic.lead_doctor_title", Value = "أخصائي تقويم الأسنان" },
            new Setting { Key = "clinic.lead_doctor_credentials", Value = "جامعة مانيلا المركزية — الفلبين" },
            new Setting { Key = "clinic.phones", Value = "777000000" },
            new Setting { Key = "clinic.location", Value = "تعز — اليمن" });
        await db.SaveChangesAsync();

        var identity = await FinanceClinicIdentity.ResolveAsync(db);

        identity.Name.Should().Be("مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان");
        identity.LeadDoctor.Should().Be("د. عقلان الكامل");
        identity.LeadDoctorTitle.Should().Be("أخصائي تقويم الأسنان");
        identity.LeadDoctorCredentials.Should().Be("جامعة مانيلا المركزية — الفلبين");
        identity.Phones.Should().Be("777000000");
        identity.Location.Should().Be("تعز — اليمن");
    }

    /// <summary>
    /// CORE-REQ-006 (print slice) — this report now resolves identity through
    /// <see cref="FinanceClinicIdentity"/>, the same reader every other document uses, so an
    /// unconfigured clinic falls back to the real centre name/location/phones (not blank text)
    /// exactly like a receipt or statement would.
    /// </summary>
    [Fact]
    public async Task ResolveClinicIdentityAsync_MissingKeys_FallsBackToRealClinicDefaults()
    {
        await using var db = CreateDb(); // no Settings rows at all

        var identity = await FinanceClinicIdentity.ResolveAsync(db);

        identity.Name.Should().Be(FinanceClinicIdentity.DefaultName);
        identity.Location.Should().Be(FinanceClinicIdentity.DefaultLocation);
        identity.Phones.Should().Be(FinanceClinicIdentity.DefaultPhones);
        identity.LeadDoctor.Should().BeEmpty();
        identity.LeadDoctorTitle.Should().BeEmpty();
        identity.LeadDoctorCredentials.Should().BeEmpty();
    }

    [Fact]
    public void Generator_DoesNotHardcodeOwnerIdentity_SourceCheck()
    {
        var generatorPath = GeneratorSourcePath();
        if (!File.Exists(generatorPath)) return;

        var content = File.ReadAllText(generatorPath);
        content.Should().NotContain("د. عقلان",
            "the lead doctor name must come from Settings (clinic.lead_doctor) — no hardcoding");
        content.Should().NotContain("جامعة مانيلا",
            "credentials must come from Settings (clinic.lead_doctor_credentials) — no hardcoding");
        content.Should().Contain("FinanceClinicIdentity",
            "the generator must resolve identity through the single shared reader");
    }

    // ─── Uploads path resolution ──────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-uploads-url.png")]
    [InlineData("/uploads/../secrets.txt")]
    [InlineData("/uploads/sub/dir.png")]
    [InlineData("/uploads/")]
    public void ResolveUploadFilePath_InvalidOrUnsafeUrls_ReturnNull(string? url)
    {
        CephReportPdfGenerator.ResolveUploadFilePath(url).Should().BeNull();
    }

    [Fact]
    public void ResolveUploadFilePath_MissingFile_ReturnsNull()
    {
        CephReportPdfGenerator.ResolveUploadFilePath($"/uploads/{Guid.NewGuid():N}.png").Should().BeNull();
    }

    [Fact]
    public void ResolveUploadFilePath_FileInTempFallbackDirectory_IsResolved()
    {
        // The temp fallback (aqlan-uploads) mirrors UploadsController/Program.cs.
        var dir = Path.Combine(Path.GetTempPath(), "aqlan-uploads");
        Directory.CreateDirectory(dir);
        var fileName = $"{Guid.NewGuid():N}.png";
        var fullPath = Path.Combine(dir, fileName);
        File.WriteAllBytes(fullPath, [1, 2, 3]);
        try
        {
            var resolved = CephReportPdfGenerator.ResolveUploadFilePath($"/uploads/{fileName}");
            resolved.Should().Be(fullPath);
        }
        finally
        {
            File.Delete(fullPath);
        }
    }

    // ─── Overlay SVG ──────────────────────────────────────────────────────

    [Fact]
    public void BuildOverlaySvg_DrawsLandmarksAndReferenceLines()
    {
        var landmarks = new List<CephLandmark>
        {
            new() { LandmarkKey = "S",  XCoord = 450, YCoord = 320, IsActive = true },
            new() { LandmarkKey = "N",  XCoord = 650, YCoord = 240, IsActive = true },
            new() { LandmarkKey = "Go", XCoord = 370, YCoord = 690, IsActive = true },
            new() { LandmarkKey = "Gn", XCoord = 640, YCoord = 730, IsActive = true },
            new() { LandmarkKey = "LS", XCoord = 820, YCoord = 560, IsActive = true },
        };

        var svg = CephReportPdfGenerator.BuildOverlaySvg(landmarks, 1000, 800);

        svg.Should().StartWith("<svg").And.EndWith("</svg>");
        svg.Should().Contain("viewBox=\"0 0 1000 800\"");
        svg.Should().Contain("<line", "S-N and Go-Gn reference lines must be drawn when endpoints exist");
        // Group palette (same as the frontend canvas grouping)
        svg.Should().Contain("#3B82F6", "cranial landmarks use the cranial color");
        svg.Should().Contain("#EF4444", "mandible landmarks use the mandible color");
        svg.Should().Contain("#EC4899", "soft-tissue landmarks use the soft color");
        // Key labels
        svg.Should().Contain(">S</text>").And.Contain(">Gn</text>");
        // SVG numbers must be culture-invariant (no decimal commas)
        svg.Should().NotContain(",\"", "SVG coordinates must use invariant decimal points");
    }

    [Fact]
    public void BuildOverlaySvg_MissingReferenceEndpoints_OmitsLines()
    {
        var landmarks = new List<CephLandmark>
        {
            new() { LandmarkKey = "A", XCoord = 730, YCoord = 520, IsActive = true },
        };

        var svg = CephReportPdfGenerator.BuildOverlaySvg(landmarks, 1000, 800);

        svg.Should().NotContain("<line", "no reference line has both endpoints placed");
        svg.Should().Contain("<circle", "the single landmark must still be drawn");
    }

    // ─── Out-of-range interpretations ─────────────────────────────────────

    [Fact]
    public void GetOutOfRangeInterpretation_AboveRange_ReturnsInterpretationAbove()
    {
        var norm = new CephNorm
        {
            MeasurementName = "SNA", NormalValue = 82, StdDeviation = 2,
            InterpretationBelow = "متراجع", InterpretationAbove = "بارز",
        };
        var m = new CephMeasurement { MeasurementName = "SNA", MeasurementValue = 87 };

        CephReportPdfGenerator.GetOutOfRangeInterpretation(m, norm).Should().Be("بارز");
    }

    [Fact]
    public void GetOutOfRangeInterpretation_WithinRange_ReturnsNull()
    {
        var norm = new CephNorm
        {
            MeasurementName = "SNA", NormalValue = 82, StdDeviation = 2,
            InterpretationBelow = "متراجع", InterpretationAbove = "بارز",
        };
        var m = new CephMeasurement { MeasurementName = "SNA", MeasurementValue = 83 };

        CephReportPdfGenerator.GetOutOfRangeInterpretation(m, norm).Should().BeNull();
    }

    [Fact]
    public void GetOutOfRangeInterpretation_ExplicitMinMax_TakesPrecedenceOverSd()
    {
        var norm = new CephNorm
        {
            MeasurementName = "ANB", NormalValue = 2, StdDeviation = 1,
            MinNormal = 0, MaxNormal = 4,
            InterpretationBelow = "صنف ثالث", InterpretationAbove = "صنف ثاني",
        };
        // 3.5 is outside ±1SD but inside the explicit 0..4 range → no interpretation
        var m = new CephMeasurement { MeasurementName = "ANB", MeasurementValue = 3.5m };

        CephReportPdfGenerator.GetOutOfRangeInterpretation(m, norm).Should().BeNull();
    }

    // ─── Endpoint contract: route, policy, no exception-detail leak ───────

    [Fact]
    public void ReportPdfEndpoint_HasCorrectRoute()
    {
        var method = typeof(AqlanDentalPro.API.Controllers.CephController).GetMethod("GetReportPdf");
        method.Should().NotBeNull("GET /api/ceph/{id}/report/pdf endpoint must exist");

        var httpGetAttr = method!
            .GetCustomAttributes(typeof(HttpGetAttribute), false)
            .Cast<HttpGetAttribute>()
            .FirstOrDefault();

        httpGetAttr.Should().NotBeNull();
        httpGetAttr!.Template.Should().Be("{id:guid}/report/pdf");
    }

    [Fact]
    public void ReportPdfEndpoint_RequiresOrthoAccess()
    {
        var controllerType = typeof(AqlanDentalPro.API.Controllers.CephController);
        var authAttr = controllerType
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .FirstOrDefault();

        authAttr.Should().NotBeNull("CephController must have [Authorize] attribute");
        authAttr!.Policy.Should().Be("OrthoAccess");
    }

    [Fact]
    public void ReportPdfEndpoint_ReturnsArabic500WithoutExceptionDetails()
    {
        var controllerPath = ControllerSourcePath("CephController.cs");
        if (!File.Exists(controllerPath)) return;

        var content = File.ReadAllText(controllerPath);
        content.Should().Contain("حدث خطأ غير متوقع أثناء إنشاء تقرير التحليل السيفالومتري",
            "CephController.GetReportPdf must return an Arabic 500 message");
        // Security: exception internals must never be sent to clients.
        content.Should().NotContain("detail = ex.Message",
            "500 responses must not leak exception messages to clients");
        content.Should().NotContain("type = ex.GetType().Name",
            "500 responses must not leak exception type names to clients");
        content.Should().NotContain("ex.StackTrace",
            "500 responses must not leak stack traces to clients");
    }

    [Fact]
    public void Generator_UsesArabicFontConstant()
    {
        var fontNameField = typeof(CephReportPdfGenerator).GetField("FontName",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        fontNameField.Should().NotBeNull();
        (fontNameField!.GetRawConstantValue() as string).Should().Be(
            AqlanDentalPro.Infrastructure.Services.PdfService.ArabicFontName,
            "the ceph report must use the same Arabic font as other PDF documents");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private static string ControllerSourcePath(string fileName) => Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..",
        "backend", "src", "AqlanDentalPro.API", "Controllers", fileName);

    private static string GeneratorSourcePath() => Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..",
        "backend", "src", "AqlanDentalPro.API", "Services", "CephReportPdfGenerator.cs");
}
