using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ceph;

/// <summary>
/// SEQ-15 — the detail API must expose only the doctor's clinical note, never
/// the internal calibration JSON stored in CephAnalysis.Notes.
/// </summary>
public sealed class CephNotesDtoTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly CephService _service;

    public CephNotesDtoTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _service = new CephService(
            _db,
            new Mock<ICurrentUserService>().Object,
            new Mock<ILogger<CephService>>().Object);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetById_ReturnsUserNotesOnly_FromCalibrationEnvelope()
    {
        const string clinicalNote = "مراجعة النقطة A قبل الاعتماد";
        var stored = "{\"PixelsPerMm\":11.5,\"ImageWidth\":1920,\"ImageHeight\":1080," +
                     "\"UserNotes\":\"" + clinicalNote + "\"}";
        var analysisId = await SeedAsync(stored);

        var detail = await _service.GetByIdAsync(analysisId);

        detail.Should().NotBeNull();
        detail!.Notes.Should().Be(clinicalNote);
        detail.Notes.Should().NotContain("PixelsPerMm");
        (await _db.CephAnalyses.FindAsync(analysisId))!.Notes.Should().Be(stored,
            "sanitizing the DTO must not rewrite persisted calibration data");
    }

    [Fact]
    public async Task GetById_PreservesLegacyPlainTextNote()
    {
        var analysisId = await SeedAsync("  ملاحظة قديمة نصية  ");

        var detail = await _service.GetByIdAsync(analysisId);

        detail!.Notes.Should().Be("ملاحظة قديمة نصية");
    }

    [Theory]
    [InlineData("{\"PixelsPerMm\":10,\"ImageWidth\":800,\"ImageHeight\":600}")]
    [InlineData("{\"pixelsPerMm\":10,\"userNotes\":null}")]
    [InlineData("{\"PixelsPerMm\":10,\"UserNotes\":\"   \"}")]
    public async Task GetById_ReturnsNull_WhenCalibrationEnvelopeHasNoClinicalNote(string stored)
    {
        var analysisId = await SeedAsync(stored);

        var detail = await _service.GetByIdAsync(analysisId);

        detail!.Notes.Should().BeNull();
    }

    [Fact]
    public void DetailDto_PreservesMalformedJsonLookingClinicalText()
    {
        var dto = new CephAnalysisDetailDto
        {
            Notes = "{ملاحظة سريرية غير مكتملة"
        };

        dto.Notes.Should().Be("{ملاحظة سريرية غير مكتملة");
    }

    private async Task<Guid> SeedAsync(string? notes)
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            FirstName = "مريض",
            LastName = "اختبار",
            IsActive = true,
        };
        var orthoCase = new OrthoCase
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            CaseNumber = $"ORT-{Guid.NewGuid():N}"[..12],
            IsActive = true,
        };
        var analysis = new CephAnalysis
        {
            Id = Guid.NewGuid(),
            OrthoCaseId = orthoCase.Id,
            AnalysisType = "steiner",
            XrayFileUrl = "/uploads/ceph.jpg",
            Notes = notes,
            IsActive = true,
        };

        _db.AddRange(patient, orthoCase, analysis);
        await _db.SaveChangesAsync();
        return analysis.Id;
    }
}
