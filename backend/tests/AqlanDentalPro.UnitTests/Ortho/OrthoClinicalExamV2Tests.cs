using System.Text.Encodings.Web;
using System.Text.Json;
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

/// <summary>
/// Phase 3 — structured clinical examination tests:
/// - PUT round-trips the new structured fields (occlusal split, habits flags, intraoral health);
/// - invalid enum-like values are rejected with an Arabic 400;
/// - out-of-range measurements are rejected with an Arabic 400;
/// - legacy payloads (without any new field) still save (backward compatibility).
/// </summary>
public class OrthoClinicalExamV2Tests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly OrthoCasesController _controller;

    public OrthoClinicalExamV2Tests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(c => c.IsAdmin).Returns(true);

        var patientAccess = new Mock<IPatientAccessService>();
        patientAccess.Setup(p => p.CanAccessPatientAsync(It.IsAny<Guid>())).ReturnsAsync(true);

        _controller = new OrthoCasesController(
            new OrthoService(_db, currentUser.Object),
            _db,
            currentUser.Object,
            patientAccess.Object);
    }

    public void Dispose() => _db.Dispose();

    /// <summary>Serializes without unicode escaping so Arabic assertions work.</summary>
    private static string ToJson(object? value) =>
        JsonSerializer.Serialize(value, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });

    private async Task<Guid> SeedOrthoCase()
    {
        var patientId = Guid.NewGuid();
        var orthoCaseId = Guid.NewGuid();

        _db.Patients.Add(new Patient { Id = patientId, FirstName = "Test", LastName = "Patient", IsActive = true });
        _db.OrthoCases.Add(new OrthoCase
        {
            Id = orthoCaseId,
            PatientId = patientId,
            CaseNumber = "ORT-EXM-001",
            IsActive = true,
        });
        await _db.SaveChangesAsync();
        return orthoCaseId;
    }

    // ── Round-trip ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Put_RoundTrips_NewStructuredFields()
    {
        var caseId = await SeedOrthoCase();

        var result = await _controller.UpsertClinicalExam(caseId, new UpsertClinicalExamRequest
        {
            ExamDate = "2026-06-12",
            // Occlusal
            MolarRelationRight = "ClassII",
            MolarRelationLeft = "ClassI",
            CanineRelationRight = "ClassIII",
            CanineRelationLeft = "ClassII",
            IncisorRelation = "ClassIIDiv1",
            Overjet = 6.5m,
            Overbite = 4m,
            OverbitePercent = 60m,
            DeepBite = true,
            Crossbite = true,
            CrossbiteType = "PosteriorUnilateralRight",
            ScissorBite = false,
            MidlineUpperShiftMm = 1.5m,
            MidlineLowerShiftMm = -2m,
            UpperCrowdingMm = 5m,
            LowerCrowdingMm = 3.5m,
            LowerSpacingMm = 0m,
            CurveOfSpee = "Deep",
            ArchFormUpper = "Ovoid",
            ArchFormLower = "Tapered",
            BoltonDiscrepancyNote = "Mandibular excess 2mm",
            // Extraoral
            LipCompetenceGrade = "PotentiallyCompetent",
            NasolabialAngle = "Acute",
            ChinPosition = "Retruded",
            FunctionalShift = "انزياح يساري عند الإطباق",
            GummySmile = true,
            // Habits
            ThumbSucking = true,
            MouthBreathing = true,
            TongueThrust = false,
            LipBiting = false,
            NailBiting = true,
            Bruxism = false,
            // Intraoral health
            OralHygiene = "Fair",
            GingivalCondition = "التهاب لثة خفيف",
            PeriodontalConcerns = "لا يوجد",
            MissingTeethFdi = "15,25",
            RetainedDeciduousFdi = "55",
            ImpactedTeethFdi = "13,23",
            SupernumeraryNote = "Mesiodens",
            EctopicEruptionNote = "بزوغ منتبذ 13",
            FrenumNote = "لجام علوي سميك",
            TongueNote = "طبيعي",
            CariesNote = "تسوس 36",
        });

        result.Should().BeOfType<OkObjectResult>();

        var saved = await _db.OrthoClinicalExams.SingleAsync(e => e.OrthoCaseId == caseId);
        saved.MolarRelationRight.Should().Be("ClassII");
        saved.MolarRelationLeft.Should().Be("ClassI");
        saved.CanineRelationRight.Should().Be("ClassIII");
        saved.CanineRelationLeft.Should().Be("ClassII");
        saved.IncisorRelation.Should().Be("ClassIIDiv1");
        saved.OverbitePercent.Should().Be(60m);
        saved.DeepBite.Should().BeTrue();
        saved.Crossbite.Should().BeTrue();
        saved.CrossbiteType.Should().Be("PosteriorUnilateralRight");
        saved.ScissorBite.Should().BeFalse();
        saved.MidlineUpperShiftMm.Should().Be(1.5m);
        saved.MidlineLowerShiftMm.Should().Be(-2m);
        saved.UpperCrowdingMm.Should().Be(5m);
        saved.LowerCrowdingMm.Should().Be(3.5m);
        saved.LowerSpacingMm.Should().Be(0m);
        saved.CurveOfSpee.Should().Be("Deep");
        saved.ArchFormUpper.Should().Be("Ovoid");
        saved.ArchFormLower.Should().Be("Tapered");
        saved.BoltonDiscrepancyNote.Should().Be("Mandibular excess 2mm");
        saved.LipCompetenceGrade.Should().Be("PotentiallyCompetent");
        saved.NasolabialAngle.Should().Be("Acute");
        saved.ChinPosition.Should().Be("Retruded");
        saved.FunctionalShift.Should().Be("انزياح يساري عند الإطباق");
        saved.GummySmile.Should().BeTrue();
        saved.ThumbSucking.Should().BeTrue();
        saved.MouthBreathing.Should().BeTrue();
        saved.TongueThrust.Should().BeFalse();
        saved.LipBiting.Should().BeFalse();
        saved.NailBiting.Should().BeTrue();
        saved.Bruxism.Should().BeFalse();
        saved.OralHygiene.Should().Be("Fair");
        saved.GingivalCondition.Should().Be("التهاب لثة خفيف");
        saved.PeriodontalConcerns.Should().Be("لا يوجد");
        saved.MissingTeethFdi.Should().Be("15,25");
        saved.RetainedDeciduousFdi.Should().Be("55");
        saved.ImpactedTeethFdi.Should().Be("13,23");
        saved.SupernumeraryNote.Should().Be("Mesiodens");
        saved.EctopicEruptionNote.Should().Be("بزوغ منتبذ 13");
        saved.FrenumNote.Should().Be("لجام علوي سميك");
        saved.TongueNote.Should().Be("طبيعي");
        saved.CariesNote.Should().Be("تسوس 36");

        // GET returns the new fields
        var get = await _controller.GetClinicalExam(caseId);
        var ok = get.Should().BeOfType<OkObjectResult>().Subject;
        var json = ToJson(ok.Value);
        json.Should().Contain("\"MolarRelationRight\":\"ClassII\"");
        json.Should().Contain("\"IncisorRelation\":\"ClassIIDiv1\"");
        json.Should().Contain("\"CrossbiteType\":\"PosteriorUnilateralRight\"");
        json.Should().Contain("\"OralHygiene\":\"Fair\"");
        json.Should().Contain("\"MissingTeethFdi\":\"15,25\"");
        json.Should().Contain("\"OverbitePercent\":60");
    }

    [Fact]
    public async Task Put_NormalizesEnumValues_CaseInsensitively()
    {
        var caseId = await SeedOrthoCase();

        var result = await _controller.UpsertClinicalExam(caseId, new UpsertClinicalExamRequest
        {
            MolarRelationRight = "classii",
            IncisorRelation = " classiidiv2 ",
            CurveOfSpee = "DEEP",
            ArchFormUpper = "ovoid",
            OralHygiene = "good",
        });

        result.Should().BeOfType<OkObjectResult>();

        var saved = await _db.OrthoClinicalExams.SingleAsync(e => e.OrthoCaseId == caseId);
        saved.MolarRelationRight.Should().Be("ClassII", "values must be stored in canonical casing");
        saved.IncisorRelation.Should().Be("ClassIIDiv2");
        saved.CurveOfSpee.Should().Be("Deep");
        saved.ArchFormUpper.Should().Be("Ovoid");
        saved.OralHygiene.Should().Be("Good");
    }

    // ── Validation: invalid enum values → Arabic 400 ────────────────────────────

    [Fact]
    public async Task Put_InvalidEnumValue_ReturnsArabic400()
    {
        var caseId = await SeedOrthoCase();

        var result = await _controller.UpsertClinicalExam(caseId, new UpsertClinicalExamRequest
        {
            MolarRelationRight = "ClassIV",
        });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var json = ToJson(bad.Value);
        json.Should().Contain("قيمة غير صالحة للحقل");
        json.Should().Contain("MolarRelationRight");

        (await _db.OrthoClinicalExams.AnyAsync(e => e.OrthoCaseId == caseId))
            .Should().BeFalse("nothing must be persisted when validation fails");
    }

    [Fact]
    public async Task Put_InvalidCrossbiteType_ReturnsArabic400()
    {
        var caseId = await SeedOrthoCase();

        var result = await _controller.UpsertClinicalExam(caseId, new UpsertClinicalExamRequest
        {
            Crossbite = true,
            CrossbiteType = "Sideways",
        });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ToJson(bad.Value).Should().Contain("قيمة غير صالحة للحقل");
    }

    // ── Validation: numeric ranges → Arabic 400 ─────────────────────────────────

    [Fact]
    public async Task Put_OutOfRangeOverjet_ReturnsArabic400()
    {
        var caseId = await SeedOrthoCase();

        var result = await _controller.UpsertClinicalExam(caseId, new UpsertClinicalExamRequest
        {
            Overjet = 50m,
        });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var json = ToJson(bad.Value);
        json.Should().Contain("قيمة خارج النطاق المسموح");
        json.Should().Contain("Overjet");
    }

    [Fact]
    public async Task Put_OutOfRangeOverbitePercent_ReturnsArabic400()
    {
        var caseId = await SeedOrthoCase();

        var result = await _controller.UpsertClinicalExam(caseId, new UpsertClinicalExamRequest
        {
            OverbitePercent = 250m,
        });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ToJson(bad.Value).Should().Contain("قيمة خارج النطاق المسموح");
    }

    [Fact]
    public async Task Put_NegativeMidlineShift_WithinRange_IsAccepted()
    {
        var caseId = await SeedOrthoCase();

        var result = await _controller.UpsertClinicalExam(caseId, new UpsertClinicalExamRequest
        {
            MidlineUpperShiftMm = -3m, // signed: negative = left shift
        });

        result.Should().BeOfType<OkObjectResult>();
        var saved = await _db.OrthoClinicalExams.SingleAsync(e => e.OrthoCaseId == caseId);
        saved.MidlineUpperShiftMm.Should().Be(-3m);
    }

    // ── Backward compatibility ──────────────────────────────────────────────────

    [Fact]
    public async Task Put_LegacyPayload_WithoutNewFields_StillSaves()
    {
        var caseId = await SeedOrthoCase();

        var result = await _controller.UpsertClinicalExam(caseId, new UpsertClinicalExamRequest
        {
            ExamDate = "2026-06-12",
            FacialSymmetry = "متماثل",
            Profile = "Convex",
            LipsCompetence = false,
            MolarRelation = "Class II Div 1",
            CanineRelation = "Class II",
            Overjet = 7m,
            Overbite = 3m,
            Crossbite = false,
            OpenBite = false,
            UpperCrowding = "متوسط",
            LowerCrowding = "خفيف",
            UpperSpacing = 1.5m,
            MidlineUpper = "منحرف يمين",
            TmjFindings = "طبيعي",
            Habits = "تنفس فمي",
            Notes = "ملاحظات",
        });

        result.Should().BeOfType<OkObjectResult>();

        var saved = await _db.OrthoClinicalExams.SingleAsync(e => e.OrthoCaseId == caseId);
        saved.MolarRelation.Should().Be("Class II Div 1", "legacy free-text relations are not validated");
        saved.UpperCrowding.Should().Be("متوسط");
        saved.UpperSpacing.Should().Be(1.5m);
        saved.Habits.Should().Be("تنفس فمي");
        // New structured fields remain null
        saved.MolarRelationRight.Should().BeNull();
        saved.IncisorRelation.Should().BeNull();
        saved.OralHygiene.Should().BeNull();
        saved.ThumbSucking.Should().BeNull();
    }

    [Fact]
    public async Task Put_UpdatesExistingExam_PreservingUpsertBehaviour()
    {
        var caseId = await SeedOrthoCase();

        (await _controller.UpsertClinicalExam(caseId, new UpsertClinicalExamRequest
        {
            Overjet = 5m,
        })).Should().BeOfType<OkObjectResult>();

        (await _controller.UpsertClinicalExam(caseId, new UpsertClinicalExamRequest
        {
            Overjet = 4m,
            MolarRelationRight = "ClassI",
        })).Should().BeOfType<OkObjectResult>();

        var exams = await _db.OrthoClinicalExams.Where(e => e.OrthoCaseId == caseId).ToListAsync();
        exams.Should().HaveCount(1, "PUT is an upsert — it must not create duplicates");
        exams[0].Overjet.Should().Be(4m);
        exams[0].MolarRelationRight.Should().Be("ClassI");
    }
}
