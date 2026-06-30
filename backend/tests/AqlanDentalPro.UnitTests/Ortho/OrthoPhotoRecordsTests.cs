using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ortho;

/// <summary>
/// Ortho Phase 2 — standardized records and images:
/// - AddPhoto persists Category/Subtype/TreatmentPhase/IsSelectedForReport;
/// - invalid Category/TreatmentPhase values are rejected with Arabic messages;
/// - GetPhotos filters by category/phase/selectedOnly;
/// - PATCH photo updates tagging fields;
/// - checklist auto-derivation matches (Category, Subtype) tags with
///   backward-compatible fallback to the legacy caption heuristic.
/// </summary>
public class OrthoPhotoRecordsTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly OrthoCasesController _controller;

    public OrthoPhotoRecordsTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(x => x.IsAdmin).Returns(true);
        var patientAccess = new Mock<IPatientAccessService>();
        patientAccess.Setup(p => p.CanAccessPatientAsync(It.IsAny<Guid>())).ReturnsAsync(true);
        var audit = new Mock<IAuditService>();
        _controller = new OrthoCasesController(
            new OrthoService(_db, currentUser.Object), _db, currentUser.Object, patientAccess.Object, audit.Object,
            new OrthoCaseQueryService(_db, NullLogger<OrthoCaseQueryService>.Instance));
    }

    public void Dispose() => _db.Dispose();

    private async Task<Guid> SeedOrthoCase()
    {
        var patientId = Guid.NewGuid();
        var orthoCaseId = Guid.NewGuid();
        _db.Patients.Add(new Patient { Id = patientId, FirstName = "Test", LastName = "Patient", IsActive = true });
        _db.OrthoCases.Add(new OrthoCase
        {
            Id = orthoCaseId,
            PatientId = patientId,
            CaseNumber = "ORT-P2-001",
            IsActive = true,
        });
        await _db.SaveChangesAsync();
        return orthoCaseId;
    }

    private static string? GetMessage(IActionResult result)
    {
        var value = (result as ObjectResult)?.Value;
        return value?.GetType().GetProperty("message")?.GetValue(value) as string;
    }

    // ─── AddPhoto: persistence of the new fields ─────────────────────────────

    [Fact]
    public async Task AddPhoto_WithCategoryAndPhase_PersistsAllNewFields()
    {
        var orthoCaseId = await SeedOrthoCase();

        var result = await _controller.AddPhoto(orthoCaseId, new AddOrthoPhotoRequest
        {
            PhotoUrl = "/uploads/p1.jpg",
            PhotoType = "Extraoral",
            Category = "extraoral", // lowercase on purpose — must normalize
            Subtype = "FrontalSmile",
            TreatmentPhase = "INITIAL", // uppercase on purpose — must normalize
            IsSelectedForReport = true,
        });

        result.Should().BeOfType<OkObjectResult>();
        var photo = await _db.OrthoClinicalPhotos.SingleAsync(p => p.OrthoCaseId == orthoCaseId);
        photo.Category.Should().Be("Extraoral"); // canonical enum name
        photo.Subtype.Should().Be("FrontalSmile");
        photo.TreatmentPhase.Should().Be("Initial"); // canonical enum name
        photo.IsSelectedForReport.Should().BeTrue();
    }

    [Fact]
    public async Task AddPhoto_WithoutTags_StillWorks_LegacyQuickUpload()
    {
        var orthoCaseId = await SeedOrthoCase();

        var result = await _controller.AddPhoto(orthoCaseId, new AddOrthoPhotoRequest
        {
            PhotoUrl = "/uploads/legacy.jpg",
            PhotoType = "Intraoral",
            Caption = "صورة قديمة",
        });

        result.Should().BeOfType<OkObjectResult>();
        var photo = await _db.OrthoClinicalPhotos.SingleAsync(p => p.OrthoCaseId == orthoCaseId);
        photo.Category.Should().BeNull();
        photo.Subtype.Should().BeNull();
        photo.TreatmentPhase.Should().BeNull();
        photo.IsSelectedForReport.Should().BeFalse();
    }

    // ─── Validation ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AddPhoto_InvalidCategory_ReturnsArabic400()
    {
        var orthoCaseId = await SeedOrthoCase();

        var result = await _controller.AddPhoto(orthoCaseId, new AddOrthoPhotoRequest
        {
            PhotoUrl = "/uploads/p1.jpg",
            Category = "NotACategory",
        });

        result.Should().BeOfType<BadRequestObjectResult>();
        GetMessage(result).Should().Be("فئة الصورة غير صالحة");
        (await _db.OrthoClinicalPhotos.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AddPhoto_InvalidTreatmentPhase_ReturnsArabic400()
    {
        var orthoCaseId = await SeedOrthoCase();

        var result = await _controller.AddPhoto(orthoCaseId, new AddOrthoPhotoRequest
        {
            PhotoUrl = "/uploads/p1.jpg",
            TreatmentPhase = "Sometime",
        });

        result.Should().BeOfType<BadRequestObjectResult>();
        GetMessage(result).Should().Be("مرحلة العلاج غير صالحة");
    }

    [Theory]
    [InlineData("Extraoral", true)]
    [InlineData("intraoral", true)]
    [InlineData("RADIOGRAPH", true)]
    [InlineData("Document", true)]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("7", false)] // numeric values are not valid category names
    [InlineData("Panorama", false)]
    public void TryNormalizeCategory_ValidatesAgainstEnum(string? input, bool expectedValid)
    {
        OrthoPhotoRecords.TryNormalizeCategory(input, out _).Should().Be(expectedValid);
    }

    [Theory]
    [InlineData("Initial", true)]
    [InlineData("progress", true)]
    [InlineData("FINAL", true)]
    [InlineData(null, true)]
    [InlineData("Before", false)]
    [InlineData("5", false)]
    public void TryNormalizePhase_ValidatesAgainstEnum(string? input, bool expectedValid)
    {
        OrthoPhotoRecords.TryNormalizePhase(input, out _).Should().Be(expectedValid);
    }

    // ─── GetPhotos filters ──────────────────────────────────────────────────

    private async Task<Guid> SeedTaggedPhotos()
    {
        var orthoCaseId = await SeedOrthoCase();
        _db.OrthoClinicalPhotos.AddRange(
            new OrthoClinicalPhoto
            {
                OrthoCaseId = orthoCaseId, PhotoUrl = "/u/before.jpg", PhotoType = "Extraoral",
                Category = "Extraoral", Subtype = "FrontalRest", TreatmentPhase = "Initial",
                IsSelectedForReport = true, SortOrder = 1,
            },
            new OrthoClinicalPhoto
            {
                OrthoCaseId = orthoCaseId, PhotoUrl = "/u/during.jpg", PhotoType = "Intraoral",
                Category = "Intraoral", Subtype = "Frontal", TreatmentPhase = "Progress",
                IsSelectedForReport = false, SortOrder = 2,
            },
            new OrthoClinicalPhoto
            {
                OrthoCaseId = orthoCaseId, PhotoUrl = "/u/after.jpg", PhotoType = "Extraoral",
                Category = "Extraoral", Subtype = "FrontalSmile", TreatmentPhase = "Final",
                IsSelectedForReport = true, SortOrder = 3,
            },
            new OrthoClinicalPhoto
            {
                OrthoCaseId = orthoCaseId, PhotoUrl = "/u/untagged.jpg", PhotoType = "Intraoral",
                SortOrder = 4,
            });
        await _db.SaveChangesAsync();
        return orthoCaseId;
    }

    private static int CountOf(IActionResult result)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        return ((System.Collections.IEnumerable)ok.Value!).Cast<object>().Count();
    }

    [Fact]
    public async Task GetPhotos_NoFilters_ReturnsAllIncludingUntagged()
    {
        var orthoCaseId = await SeedTaggedPhotos();
        var result = await _controller.GetPhotos(orthoCaseId);
        CountOf(result).Should().Be(4);
    }

    [Fact]
    public async Task GetPhotos_FilterByPhase_IsCaseInsensitive()
    {
        var orthoCaseId = await SeedTaggedPhotos();
        var result = await _controller.GetPhotos(orthoCaseId, phase: "initial");
        CountOf(result).Should().Be(1);
    }

    [Fact]
    public async Task GetPhotos_FilterBySelectedOnly_ReturnsOnlyReportPhotos()
    {
        var orthoCaseId = await SeedTaggedPhotos();
        var result = await _controller.GetPhotos(orthoCaseId, selectedOnly: true);
        CountOf(result).Should().Be(2);
    }

    [Fact]
    public async Task GetPhotos_FilterByCategory_CombinesWithPhase()
    {
        var orthoCaseId = await SeedTaggedPhotos();
        var result = await _controller.GetPhotos(orthoCaseId, category: "Extraoral", phase: "Final");
        CountOf(result).Should().Be(1);
    }

    [Fact]
    public async Task GetPhotos_InvalidPhaseFilter_ReturnsArabic400()
    {
        var orthoCaseId = await SeedTaggedPhotos();
        var result = await _controller.GetPhotos(orthoCaseId, phase: "garbage");
        result.Should().BeOfType<BadRequestObjectResult>();
        GetMessage(result).Should().Be("مرحلة العلاج غير صالحة");
    }

    // ─── PATCH photo ─────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdatePhoto_TogglesIsSelectedForReport_AndUpdatesTags()
    {
        var orthoCaseId = await SeedTaggedPhotos();
        var photo = await _db.OrthoClinicalPhotos.FirstAsync(p => p.PhotoUrl == "/u/during.jpg");

        var result = await _controller.UpdatePhoto(orthoCaseId, photo.Id, new UpdateOrthoPhotoRequest
        {
            IsSelectedForReport = true,
            TreatmentPhase = "final",
            Subtype = "Right",
            Caption = "بعد إغلاق المسافات",
        });

        result.Should().BeOfType<OkObjectResult>();
        var updated = await _db.OrthoClinicalPhotos.SingleAsync(p => p.Id == photo.Id);
        updated.IsSelectedForReport.Should().BeTrue();
        updated.TreatmentPhase.Should().Be("Final");
        updated.Subtype.Should().Be("Right");
        updated.Caption.Should().Be("بعد إغلاق المسافات");
        updated.Category.Should().Be("Intraoral"); // untouched field stays
    }

    [Fact]
    public async Task UpdatePhoto_UnknownPhoto_ReturnsArabic404()
    {
        var orthoCaseId = await SeedOrthoCase();
        var result = await _controller.UpdatePhoto(orthoCaseId, Guid.NewGuid(), new UpdateOrthoPhotoRequest
        {
            IsSelectedForReport = true,
        });
        result.Should().BeOfType<NotFoundObjectResult>();
        GetMessage(result).Should().Be("الصورة غير موجودة");
    }

    [Fact]
    public async Task UpdatePhoto_InvalidCategory_ReturnsArabic400()
    {
        var orthoCaseId = await SeedTaggedPhotos();
        var photo = await _db.OrthoClinicalPhotos.FirstAsync(p => p.OrthoCaseId == orthoCaseId);
        var result = await _controller.UpdatePhoto(orthoCaseId, photo.Id, new UpdateOrthoPhotoRequest
        {
            Category = "Bogus",
        });
        result.Should().BeOfType<BadRequestObjectResult>();
        GetMessage(result).Should().Be("فئة الصورة غير صالحة");
    }

    // ─── Checklist auto-derivation from (Category, Subtype) ─────────────────

    private static OrthoClinicalPhoto Tagged(string category, string subtype) => new()
    {
        OrthoCaseId = Guid.NewGuid(),
        PhotoUrl = "/u/x.jpg",
        PhotoType = "Intraoral", // intentionally generic — tags must win, not PhotoType
        Category = category,
        Subtype = subtype,
    };

    [Fact]
    public void ChecklistDerive_FromCategoryAndSubtype_MatchesAllStandardItems()
    {
        var photos = new[]
        {
            Tagged("Extraoral", "FrontalRest"),
            Tagged("Extraoral", "FrontalSmile"),
            Tagged("Extraoral", "Profile"),
            Tagged("Intraoral", "Frontal"),
            Tagged("Intraoral", "Right"),
            Tagged("Intraoral", "Left"),
            Tagged("Intraoral", "UpperOcclusal"),
            Tagged("Intraoral", "LowerOcclusal"),
            Tagged("Radiograph", "OPG"),
            Tagged("Radiograph", "LateralCeph"),
            Tagged("Radiograph", "CBCT"),
        };

        OrthoPhotoRecords.DeriveExtraoralFrontal(photos).Should().BeTrue();
        OrthoPhotoRecords.DeriveExtraoralProfile(photos).Should().BeTrue();
        OrthoPhotoRecords.DeriveExtraoralSmile(photos).Should().BeTrue();
        OrthoPhotoRecords.DeriveIntraoralFrontal(photos).Should().BeTrue();
        OrthoPhotoRecords.DeriveIntraoralRight(photos).Should().BeTrue();
        OrthoPhotoRecords.DeriveIntraoralLeft(photos).Should().BeTrue();
        OrthoPhotoRecords.DeriveUpperOcclusal(photos).Should().BeTrue();
        OrthoPhotoRecords.DeriveLowerOcclusal(photos).Should().BeTrue();
        OrthoPhotoRecords.DeriveOpg(photos).Should().BeTrue();
        OrthoPhotoRecords.DeriveLateralCeph(photos).Should().BeTrue();
        OrthoPhotoRecords.DeriveCbct(photos).Should().BeTrue();
    }

    [Fact]
    public void ChecklistDerive_FrontalSmile_CountsAsExtraoralFrontalAndSmile()
    {
        var photos = new[] { Tagged("Extraoral", "FrontalSmile") };
        OrthoPhotoRecords.DeriveExtraoralFrontal(photos).Should().BeTrue();
        OrthoPhotoRecords.DeriveExtraoralSmile(photos).Should().BeTrue();
        OrthoPhotoRecords.DeriveExtraoralProfile(photos).Should().BeFalse();
    }

    [Fact]
    public void ChecklistDerive_NoMatchingTags_ReturnsFalse()
    {
        var photos = new[] { Tagged("Intraoral", "Frontal") };
        OrthoPhotoRecords.DeriveExtraoralFrontal(photos).Should().BeFalse();
        OrthoPhotoRecords.DeriveOpg(photos).Should().BeFalse();
        OrthoPhotoRecords.DeriveUpperOcclusal(photos).Should().BeFalse();
    }

    [Fact]
    public void ChecklistDerive_LegacyCaptionFallback_StillWorksForUntaggedPhotos()
    {
        // Old photos: no Category/Subtype, only PhotoType + caption keywords
        var photos = new[]
        {
            new OrthoClinicalPhoto { PhotoUrl = "/u/1.jpg", PhotoType = "Extraoral", Caption = "frontal at rest" },
            new OrthoClinicalPhoto { PhotoUrl = "/u/2.jpg", PhotoType = "Intraoral", Caption = "upper arch" },
            new OrthoClinicalPhoto { PhotoUrl = "/u/3.jpg", PhotoType = "Radiograph", Caption = "OPG 2025" },
        };

        OrthoPhotoRecords.DeriveExtraoralFrontal(photos).Should().BeTrue();
        OrthoPhotoRecords.DeriveUpperOcclusal(photos).Should().BeTrue();
        OrthoPhotoRecords.DeriveOpg(photos).Should().BeTrue();
        // And items with no matching caption stay false
        OrthoPhotoRecords.DeriveIntraoralLeft(photos).Should().BeFalse();
        OrthoPhotoRecords.DeriveCbct(photos).Should().BeFalse();
    }

    [Fact]
    public async Task GetChecklist_AutoDerives_FromTaggedPhotos_WhenNoChecklistSaved()
    {
        var orthoCaseId = await SeedOrthoCase();
        _db.OrthoClinicalPhotos.AddRange(
            new OrthoClinicalPhoto
            {
                OrthoCaseId = orthoCaseId, PhotoUrl = "/u/a.jpg", PhotoType = "Progress",
                Category = "Extraoral", Subtype = "Profile",
            },
            new OrthoClinicalPhoto
            {
                OrthoCaseId = orthoCaseId, PhotoUrl = "/u/b.jpg", PhotoType = "Progress",
                Category = "Radiograph", Subtype = "CBCT",
            },
            // Legacy untagged photo — caption fallback must still derive IntraoralRight
            new OrthoClinicalPhoto
            {
                OrthoCaseId = orthoCaseId, PhotoUrl = "/u/c.jpg", PhotoType = "Intraoral",
                Caption = "right buccal",
            });
        await _db.SaveChangesAsync();

        var result = await _controller.GetChecklist(orthoCaseId);
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = ok.Value!;
        bool Flag(string name) => (bool)value.GetType().GetProperty(name)!.GetValue(value)!;

        Flag("ExtraoralProfile").Should().BeTrue();
        Flag("Cbct").Should().BeTrue();
        Flag("IntraoralRight").Should().BeTrue();
        Flag("ExtraoralFrontal").Should().BeFalse();
        Flag("Opg").Should().BeFalse();
    }
}
