using AqlanDentalPro.API.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using A = DocumentFormat.OpenXml.Drawing;

namespace AqlanDentalPro.UnitTests.Ortho;

[Collection("ai-env")]
public class OrthoCasePresentationTests
{
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9WlK7xkAAAAASUVORK5CYII=");

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    [Fact]
    public async Task SparseCase_GeneratesValidNarrativePresentationWithFixedSections()
    {
        await using var db = CreateDb();
        var caseId = await SeedCaseAsync(db, rich: false);

        var bytes = await new OrthoCasePresentationService(db).GenerateAsync(
            caseId,
            new GenerateOrthoCasePresentationRequest());

        using var stream = new MemoryStream(bytes);
        using var document = PresentationDocument.Open(stream, false);
        // 19 fixed narrative sections (no visits/progress photos for a sparse case).
        document.PresentationPart!.SlideParts.Should().HaveCount(19);

        var errors = new OpenXmlValidator().Validate(document).ToList();
        errors.Should().BeEmpty(
            string.Join(Environment.NewLine, errors.Select(error => error.Description)));
    }

    [Fact]
    public async Task RichCase_EmbedsSelectedPreparedImageAndUsesCropRectangle()
    {
        await using var db = CreateDb();
        var caseId = await SeedCaseAsync(db, rich: true);
        var uploads = Path.Combine(Path.GetTempPath(), $"ortho-pptx-{Guid.NewGuid():N}");
        Directory.CreateDirectory(uploads);
        var previous = Environment.GetEnvironmentVariable("UPLOADS_PATH");

        try
        {
            var fileName = $"{Guid.NewGuid():N}.png";
            await File.WriteAllBytesAsync(Path.Combine(uploads, fileName), OnePixelPng);
            Environment.SetEnvironmentVariable("UPLOADS_PATH", uploads);
            db.OrthoClinicalPhotos.Add(new OrthoClinicalPhoto
            {
                Id = Guid.NewGuid(),
                OrthoCaseId = caseId,
                PhotoUrl = $"/uploads/{fileName}",
                PhotoType = "Extraoral",
                Category = "Extraoral",
                Subtype = "Frontal",
                Caption = "الصورة الأمامية",
                IsSelectedForReport = true,
                IsActive = true,
                ImagePreparation = new OrthoImagePreparation
                {
                    Id = Guid.NewGuid(),
                    PreparedImageUrl = $"/uploads/{fileName}",
                    Status = "PreparedForReport",
                    IsActive = true,
                },
            });
            await db.SaveChangesAsync();

            var bytes = await new OrthoCasePresentationService(db).GenerateAsync(
                caseId,
                new GenerateOrthoCasePresentationRequest());

            using var stream = new MemoryStream(bytes);
            using var document = PresentationDocument.Open(stream, false);
            document.PresentationPart!.SlideParts
                .SelectMany(part => part.ImageParts)
                .Should()
                .NotBeEmpty();
            var cropRectangles = document.PresentationPart.SlideParts
                .SelectMany(part => part.Slide.Descendants<A.SourceRectangle>())
                .ToList();
            cropRectangles.Any(rectangle =>
                    (rectangle.Left is not null && rectangle.Left.Value > 0) ||
                    (rectangle.Right is not null && rectangle.Right.Value > 0) ||
                    (rectangle.Top is not null && rectangle.Top.Value > 0) ||
                    (rectangle.Bottom is not null && rectangle.Bottom.Value > 0))
                .Should()
                .BeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable("UPLOADS_PATH", previous);
            if (Directory.Exists(uploads))
                Directory.Delete(uploads, recursive: true);
        }
    }

    [Fact]
    public async Task RequestedSlides_CanSkipEmptyOptionalSlidesButKeepsRequiredSlides()
    {
        await using var db = CreateDb();
        var caseId = await SeedCaseAsync(db, rich: false);

        var bytes = await new OrthoCasePresentationService(db).GenerateAsync(
            caseId,
            new GenerateOrthoCasePresentationRequest
            {
                IncludeEmptyOptionalSlides = false,
                IncludedSlides =
                [
                    OrthoPresentationSlideType.ExtraoralPhotos,
                    OrthoPresentationSlideType.VisitProgress,
                ],
            });

        using var stream = new MemoryStream(bytes);
        using var document = PresentationDocument.Open(stream, false);
        document.PresentationPart!.SlideParts.Should().HaveCount(6);
    }

    [Fact]
    public async Task Definition_ReportsAllFoundationSlidesAndReadiness()
    {
        await using var db = CreateDb();
        var caseId = await SeedCaseAsync(db, rich: true);

        var definition = await new OrthoCasePresentationService(db)
            .GetDefinitionAsync(caseId);

        definition.Template.Should().Be("CasePresentation");
        // 19 fixed sections + one per-visit slide (the rich case seeds a single visit).
        definition.Slides.Should().HaveCount(20);
        definition.Slides.Should().ContainSingle(
            slide => slide.Type == OrthoPresentationSlideType.CephalometricMeasurements &&
                     slide.HasData);
        definition.Slides.Count(slide => slide.Type == OrthoPresentationSlideType.VisitProgress)
            .Should().Be(1);
    }

    [Fact]
    public async Task Narrative_ExpandsOneSlidePerVisit_AndFollowsReferenceOrder()
    {
        await using var db = CreateDb();
        var caseId = await SeedCaseAsync(db, rich: true);
        for (var n = 2; n <= 4; n++) // add visits 2..4 (rich case already seeded visit 1)
            db.OrthoVisits.Add(new OrthoVisit
            {
                Id = Guid.NewGuid(), OrthoCaseId = caseId, VisitNumber = n,
                VisitDate = new DateOnly(2026, 6, n), VisitType = "Adjustment",
                ClinicalNotes = $"ضبط {n}", IsActive = true,
            });
        db.ProblemListItems.Add(new ProblemListItem
        {
            Id = Guid.NewGuid(), OrthoCaseId = caseId, Category = "Dental",
            Description = "ازدحام", Severity = "متوسط", SortOrder = 0, IsActive = true,
        });
        await db.SaveChangesAsync();

        var definition = await new OrthoCasePresentationService(db).GetDefinitionAsync(caseId);
        var types = definition.Slides.Select(s => s.Type).ToList();

        types.First().Should().Be(OrthoPresentationSlideType.Title);
        types.Last().Should().Be(OrthoPresentationSlideType.ThankYou);
        types.Count(t => t == OrthoPresentationSlideType.VisitProgress).Should().Be(4);
        types.Should().Contain(OrthoPresentationSlideType.ProblemList);
        types.Should().Contain(OrthoPresentationSlideType.Bolton);
        types.Should().Contain(OrthoPresentationSlideType.OcclusionAssessment);
        types.Should().Contain(OrthoPresentationSlideType.FacialAnalysis);
        // Diagnostic flow: problem list → diagnosis → treatment plan → the visit story.
        types.IndexOf(OrthoPresentationSlideType.ProblemList)
            .Should().BeLessThan(types.IndexOf(OrthoPresentationSlideType.Diagnosis));
        types.IndexOf(OrthoPresentationSlideType.Diagnosis)
            .Should().BeLessThan(types.IndexOf(OrthoPresentationSlideType.TreatmentObjectives));
        types.IndexOf(OrthoPresentationSlideType.TreatmentObjectives)
            .Should().BeLessThan(types.IndexOf(OrthoPresentationSlideType.TreatmentPlan));
        types.IndexOf(OrthoPresentationSlideType.TreatmentPlan)
            .Should().BeLessThan(types.IndexOf(OrthoPresentationSlideType.VisitProgress));
    }

    [Fact]
    public async Task TitleAndFooter_CarryClinicIdentityFromSettings()
    {
        await using var db = CreateDb();
        var caseId = await SeedCaseAsync(db, rich: false);
        db.Settings.AddRange(
            new Setting { Id = Guid.NewGuid(), Key = "clinic.name", Value = "مركز الدكتور عقلان" },
            new Setting { Id = Guid.NewGuid(), Key = "clinic.lead_doctor", Value = "د. عقلان الكامل" },
            new Setting { Id = Guid.NewGuid(), Key = "clinic.lead_doctor_title", Value = "أخصائي تقويم الأسنان" },
            new Setting { Id = Guid.NewGuid(), Key = "clinic.lead_doctor_credentials", Value = "جامعة مانيلا المركزية — الفلبين" },
            new Setting { Id = Guid.NewGuid(), Key = "clinic.phones", Value = "777000000" },
            new Setting { Id = Guid.NewGuid(), Key = "clinic.location", Value = "تعز" });
        await db.SaveChangesAsync();

        var bytes = await new OrthoCasePresentationService(db).GenerateAsync(
            caseId, new GenerateOrthoCasePresentationRequest());

        using var stream = new MemoryStream(bytes);
        using var document = PresentationDocument.Open(stream, false);
        var allText = string.Join("\n", document.PresentationPart!.SlideParts
            .SelectMany(p => p.Slide.Descendants<A.Text>()).Select(t => t.Text));

        allText.Should().Contain("مركز الدكتور عقلان");
        allText.Should().Contain("أخصائي تقويم الأسنان");
        allText.Should().Contain("جامعة مانيلا المركزية — الفلبين");
        allText.Should().Contain("تعز"); // footer contact
    }

    [Fact]
    public async Task MissingCase_ThrowsArgumentException()
    {
        await using var db = CreateDb();
        var act = () => new OrthoCasePresentationService(db).GenerateAsync(
            Guid.NewGuid(),
            new GenerateOrthoCasePresentationRequest());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static async Task<Guid> SeedCaseAsync(AppDbContext db, bool rich)
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            FirstName = "مريض",
            LastName = "اختبار",
            PatientNumber = "QA-PPTX",
            DateOfBirth = new DateOnly(2000, 1, 1),
            Gender = Gender.Male,
            IsActive = true,
        };
        var doctor = new Doctor
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Name = "طبيب اختبار",
            IsActive = true,
        };
        var orthoCase = new OrthoCase
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            CaseNumber = "OR-QA-PPTX",
            CurrentStage = "Alignment",
            Status = OrthoCaseStatus.Active,
            IsActive = true,
        };

        db.AddRange(patient, doctor, orthoCase);
        if (rich)
        {
            db.OrthoDiagnoses.Add(new OrthoDiagnosis
            {
                Id = Guid.NewGuid(),
                OrthoCaseId = orthoCase.Id,
                SkeletalClassification = "Class II",
                DentalClassification = "Class II div 1",
                Summary = "حالة اختبارية منزوعة الهوية",
                IsActive = true,
            });
            db.TreatmentPlans.Add(new AqlanDentalPro.Domain.Entities.TreatmentPlan
            {
                Id = Guid.NewGuid(),
                OrthoCaseId = orthoCase.Id,
                PlanLabel = "A",
                TreatmentGoals = "تحسين الإطباق والابتسامة",
                ApplianceType = "Fixed",
                BracketSystem = "MBT",
                InitialWire = "0.014 NiTi",
                IsApproved = true,
                IsActive = true,
            });
            var ceph = new CephAnalysis
            {
                Id = Guid.NewGuid(),
                OrthoCaseId = orthoCase.Id,
                AnalysisDate = new DateOnly(2026, 6, 15),
                AnalysisType = "Steiner",
                IsActive = true,
            };
            db.CephAnalyses.Add(ceph);
            db.CephMeasurements.Add(new CephMeasurement
            {
                Id = Guid.NewGuid(),
                AnalysisId = ceph.Id,
                MeasurementName = "SNA",
                MeasurementValue = 82,
                NormalValue = 82,
                Unit = "°",
                IsActive = true,
            });
            db.OrthoVisits.Add(new OrthoVisit
            {
                Id = Guid.NewGuid(),
                OrthoCaseId = orthoCase.Id,
                VisitNumber = 1,
                VisitDate = new DateOnly(2026, 6, 1),
                VisitType = "Adjustment",
                ClinicalNotes = "زيارة اختبارية",
                IsActive = true,
            });
        }

        await db.SaveChangesAsync();
        return orthoCase.Id;
    }
}
