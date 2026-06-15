using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Services;

public sealed class OrthoCasePresentationService(AppDbContext db)
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public async Task<OrthoPresentationDefinitionResponse> GetDefinitionAsync(
        Guid orthoCaseId,
        CancellationToken cancellationToken = default)
    {
        var source = await LoadSourceAsync(orthoCaseId, cancellationToken);
        var contents = await BuildContentsAsync(source, cancellationToken);
        var slides = contents
            .Select(content => new OrthoPresentationDefinitionItem(
                content.Definition.Order,
                content.Definition.Type,
                content.Definition.Title,
                content.Definition.Required,
                content.HasData,
                content.Definition.TextPlaceholders,
                content.Definition.ImagePlaceholders,
                content.Definition.TablePlaceholders))
            .ToList();

        return new OrthoPresentationDefinitionResponse(
            "CasePresentation",
            slides.Count,
            slides.Count(slide => slide.Required || slide.HasData),
            slides);
    }

    public async Task<byte[]> GenerateAsync(
        Guid orthoCaseId,
        GenerateOrthoCasePresentationRequest request,
        CancellationToken cancellationToken = default)
    {
        var source = await LoadSourceAsync(orthoCaseId, cancellationToken);
        var allContents = await BuildContentsAsync(source, cancellationToken);
        var requested = request.IncludedSlides?.ToHashSet();

        var selected = allContents
            .Where(content =>
                content.Definition.Required ||
                requested is null ||
                requested.Contains(content.Definition.Type))
            .Where(content =>
                content.Definition.Required ||
                content.HasData ||
                request.IncludeEmptyOptionalSlides)
            .OrderBy(content => content.Definition.Order)
            .Select((content, index) => content with
            {
                Definition = content.Definition with { Order = index + 1 },
            })
            .ToList();

        var patientName = JoinName(
            source.Patient.FirstName,
            source.Patient.MiddleName,
            source.Patient.LastName);
        var identity = await CephReportPdfGenerator.ResolveClinicIdentityAsync(db);
        var contact = string.Join("   |   ",
            new[] { identity.Phones, identity.Location }.Where(HasText));

        return PowerPointPresentationBuilder.Build(new OrthoCasePresentationDocument(
            identity.ClinicName,
            string.IsNullOrWhiteSpace(source.Doctor?.Name) ? identity.LeadDoctor : source.Doctor.Name,
            identity.LeadDoctorTitle,
            identity.LeadDoctorCredentials,
            contact,
            source.CaseNumber,
            patientName,
            selected));
    }

    private async Task<OrthoCase> LoadSourceAsync(
        Guid orthoCaseId,
        CancellationToken cancellationToken)
    {
        var source = await db.OrthoCases
            .AsNoTracking()
            .AsSplitQuery()
            .Include(c => c.Patient)
                .ThenInclude(patient => patient.DentalHistory)
            .Include(c => c.Doctor)
            .Include(c => c.ClinicalExam)
            .Include(c => c.Diagnosis)
            .Include(c => c.ProblemList)
            .Include(c => c.TreatmentPlans)
            .Include(c => c.Visits)
            .Include(c => c.RetentionRecord)
            .Include(c => c.ModelAnalyses)
            .Include(c => c.PhotoAnalyses)
            .Include(c => c.CephAnalyses)
                .ThenInclude(analysis => analysis.Measurements)
            .Include(c => c.CephAnalyses)
                .ThenInclude(analysis => analysis.Diagnosis)
            .Include(c => c.OrthoClinicalPhotos)
                .ThenInclude(photo => photo.ImagePreparation)
            .FirstOrDefaultAsync(
                c => c.Id == orthoCaseId && c.IsActive,
                cancellationToken);

        return source ?? throw new ArgumentException(
            "Ortho case not found.",
            nameof(orthoCaseId));
    }

    private async Task<IReadOnlyList<PresentationSlideContent>> BuildContentsAsync(
        OrthoCase source,
        CancellationToken cancellationToken)
    {
        var latestCeph = source.CephAnalyses
            .Where(item => item.IsActive)
            .OrderByDescending(item => item.AnalysisDate)
            .ThenByDescending(item => item.CreatedAt)
            .FirstOrDefault();
        var latestModel = source.ModelAnalyses
            .Where(item => item.IsActive)
            .OrderByDescending(item => item.AnalysisDate)
            .ThenByDescending(item => item.CreatedAt)
            .FirstOrDefault();
        var approvedPlan = source.TreatmentPlans
            .Where(item => item.IsActive)
            .OrderByDescending(item => item.IsApproved)
            .ThenByDescending(item => item.ApprovedAt)
            .ThenByDescending(item => item.CreatedAt)
            .FirstOrDefault();
        var selectedPhotos = source.OrthoClinicalPhotos
            .Where(photo => photo.IsActive && photo.IsSelectedForReport)
            .OrderBy(photo => photo.SortOrder)
            .ThenBy(photo => photo.TakenAt)
            .ToList();

        var loadedPhotos = new Dictionary<Guid, PresentationImage>();
        foreach (var photo in selectedPhotos)
        {
            var loaded = await LoadPhotoAsync(photo, cancellationToken);
            if (loaded is not null)
                loadedPhotos[photo.Id] = loaded;
        }

        var cephImage = latestCeph is null
            ? null
            : await LoadImageAsync(
                latestCeph.XrayFileUrl,
                "الأشعة السيفالومترية",
                cancellationToken);

        // Slide sequence mirrors the reference clinical case-presentation deck: cover →
        // records & analysis → diagnosis & plan → the treatment story (one slide per
        // adjustment visit) → progress photos → retention → thanks.
        var contents = new List<PresentationSlideContent>
        {
            Content(Slide(OrthoPresentationSlideType.Title, "عرض حالة تقويمية", true), true),
            Content(Slide(OrthoPresentationSlideType.PatientInformation, "بيانات المريض", true), true,
                PatientInformation(source)),
            Content(Slide(OrthoPresentationSlideType.ChiefComplaint, "المقابلة والشكوى الرئيسية", false),
                HasText(source.Patient.DentalHistory?.ChiefComplaint) || HasText(source.ClinicalExam?.Notes),
                ChiefComplaint(source)),
            ImageContent(Slide(OrthoPresentationSlideType.ExtraoralPhotos, "الصور خارج الفم", false),
                selectedPhotos, loadedPhotos,
                photo => EqualsIgnoreCase(photo.Category, "Extraoral") || EqualsIgnoreCase(photo.PhotoType, "Extraoral")),
            Content(Slide(OrthoPresentationSlideType.FacialAnalysis, "التحليل الوجهي", false),
                HasFacialData(source), FacialAnalysis(source)),
            ImageContent(Slide(OrthoPresentationSlideType.IntraoralPhotos, "الصور داخل الفم", false),
                selectedPhotos, loadedPhotos,
                photo => EqualsIgnoreCase(photo.Category, "Intraoral") || EqualsIgnoreCase(photo.PhotoType, "Intraoral")),
            Content(Slide(OrthoPresentationSlideType.OcclusionAssessment, "تقييم الإطباق", false),
                source.ClinicalExam is not null, Occlusion(source.ClinicalExam)),
            ImageContent(Slide(OrthoPresentationSlideType.PanoramicXray, "الأشعة البانورامية", false),
                selectedPhotos, loadedPhotos,
                photo => EqualsIgnoreCase(photo.Subtype, "OPG") || EqualsIgnoreCase(photo.Subtype, "Panoramic")),
            Content(Slide(OrthoPresentationSlideType.CephalometricSummary, "الأشعة السيفالومترية", false),
                latestCeph is not null, CephSummary(latestCeph), cephImage is null ? [] : [cephImage]),
            Content(Slide(OrthoPresentationSlideType.CephalometricMeasurements, "جدول قياسات السيفالو", false),
                latestCeph?.Measurements.Any(item => item.IsActive) == true, table: CephMeasurements(latestCeph)),
            Content(Slide(OrthoPresentationSlideType.CastAnalysis, "تحليل النماذج الدراسية", false),
                latestModel is not null, table: CastAnalysis(latestModel)),
            Content(Slide(OrthoPresentationSlideType.Bolton, "تحليل بولتون", false),
                latestModel?.BoltonOverall is not null || latestModel?.BoltonAnterior is not null, Bolton(latestModel)),
            // Diagnostic synthesis follows the records/analyses: the problem list (grouped
            // by category) → the formal diagnosis → treatment objectives → treatment plan.
            Content(Slide(OrthoPresentationSlideType.ProblemList, "قائمة المشاكل التشخيصية", false),
                source.ProblemList.Any(item => item.IsActive), ProblemList(source.ProblemList)),
            Content(Slide(OrthoPresentationSlideType.Diagnosis, "التشخيص", true),
                source.Diagnosis is not null, Diagnosis(source.Diagnosis)),
            Content(Slide(OrthoPresentationSlideType.TreatmentObjectives, "أهداف العلاج", true),
                HasText(approvedPlan?.TreatmentGoals), TextLines(("أهداف العلاج", approvedPlan?.TreatmentGoals))),
            Content(Slide(OrthoPresentationSlideType.TreatmentPlan, "خطة العلاج", true),
                approvedPlan is not null, TreatmentPlanLines(approvedPlan)),
            Content(Slide(OrthoPresentationSlideType.Mechanotherapy, "الميكانيكا العلاجية", false),
                HasMechanotherapy(approvedPlan), Mechanotherapy(approvedPlan)),
        };

        // The treatment story — one slide per adjustment visit, in chronological order.
        var visits = source.Visits
            .Where(item => item.IsActive)
            .OrderBy(item => item.VisitNumber)
            .ThenBy(item => item.VisitDate)
            .ToList();
        foreach (var visit in visits)
        {
            contents.Add(Content(
                Slide(OrthoPresentationSlideType.VisitProgress,
                    $"زيارة {visit.VisitNumber.ToString(Invariant)} — {visit.VisitDate.ToString("yyyy-MM-dd", Invariant)}", false),
                true, VisitLines(visit)));
        }

        // Progress photo galleries (≤ 6 per slide) for any progress-tagged selected photos.
        var progressImages = selectedPhotos
            .Where(IsProgressPhoto)
            .Select(photo => loadedPhotos.GetValueOrDefault(photo.Id))
            .Where(image => image is not null)
            .Cast<PresentationImage>()
            .ToList();
        foreach (var chunk in Chunk(progressImages, 6))
        {
            contents.Add(Content(
                Slide(OrthoPresentationSlideType.ProgressPhotos, "صور التقدّم العلاجي", false),
                chunk.Count > 0, images: chunk));
        }

        // Final / post-treatment records (debond) before the retention summary.
        var finalImages = selectedPhotos
            .Where(photo => HasText(photo.TreatmentPhase) &&
                (EqualsIgnoreCase(photo.TreatmentPhase, "Final") || EqualsIgnoreCase(photo.TreatmentPhase, "Debond") ||
                 EqualsIgnoreCase(photo.TreatmentPhase, "PostTreatment")))
            .Select(photo => loadedPhotos.GetValueOrDefault(photo.Id))
            .Where(image => image is not null)
            .Cast<PresentationImage>()
            .ToList();
        foreach (var chunk in Chunk(finalImages, 6))
            contents.Add(Content(Slide(OrthoPresentationSlideType.FinalRecords, "النتائج بعد العلاج", false),
                chunk.Count > 0, images: chunk));

        // Before/after comparison — pair an initial-phase photo with a final-phase photo
        // of the same view (subtype). Images are interleaved [before, after, …] and the
        // builder lays them as labelled "قبل/بعد" columns.
        var initialBySubtype = selectedPhotos
            .Where(p => IsInitialPhoto(p) && HasText(p.Subtype))
            .GroupBy(p => p.Subtype!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var finalBySubtype = selectedPhotos
            .Where(p => IsFinalPhoto(p) && HasText(p.Subtype))
            .GroupBy(p => p.Subtype!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var beforeAfter = new List<PresentationImage>();
        foreach (var subtype in initialBySubtype.Keys)
        {
            if (beforeAfter.Count >= 6) break; // up to 3 pairs
            if (!finalBySubtype.TryGetValue(subtype, out var finalPhoto)) continue;
            var before = loadedPhotos.GetValueOrDefault(initialBySubtype[subtype].Id);
            var after = loadedPhotos.GetValueOrDefault(finalPhoto.Id);
            if (before is null || after is null) continue;
            beforeAfter.Add(before with { Label = $"قبل — {subtype}" });
            beforeAfter.Add(after with { Label = $"بعد — {subtype}" });
        }
        if (beforeAfter.Count >= 2)
            contents.Add(Content(Slide(OrthoPresentationSlideType.BeforeAfter, "المقارنة: قبل وبعد", false),
                true, images: beforeAfter));

        contents.Add(Content(Slide(OrthoPresentationSlideType.Retention, "مرحلة الاحتفاظ", false),
            source.RetentionRecord is not null || HasText(source.RetentionPlan), Retention(source)));
        contents.Add(Content(Slide(OrthoPresentationSlideType.ThankYou, "ختام العرض", true), true));

        return contents;
    }

    private static OrthoPresentationSlideDefinition Slide(
        OrthoPresentationSlideType type, string title, bool required) =>
        new(0, type, title, required, [], [], []);

    private static bool IsProgressPhoto(OrthoClinicalPhoto photo) =>
        EqualsIgnoreCase(photo.PhotoType, "Progress") ||
        (HasText(photo.TreatmentPhase) &&
         !EqualsIgnoreCase(photo.TreatmentPhase, "Initial") &&
         !EqualsIgnoreCase(photo.TreatmentPhase, "Pretreatment") &&
         !EqualsIgnoreCase(photo.TreatmentPhase, "Final"));

    private static bool IsFinalPhoto(OrthoClinicalPhoto photo) =>
        EqualsIgnoreCase(photo.TreatmentPhase, "Final") ||
        EqualsIgnoreCase(photo.TreatmentPhase, "Debond") ||
        EqualsIgnoreCase(photo.TreatmentPhase, "PostTreatment");

    // Initial/diagnostic records: untagged photos or the pretreatment phases.
    private static bool IsInitialPhoto(OrthoClinicalPhoto photo) =>
        !HasText(photo.TreatmentPhase) ||
        EqualsIgnoreCase(photo.TreatmentPhase, "Initial") ||
        EqualsIgnoreCase(photo.TreatmentPhase, "Pretreatment") ||
        EqualsIgnoreCase(photo.TreatmentPhase, "Baseline");

    private static IEnumerable<List<T>> Chunk<T>(IReadOnlyList<T> items, int size)
    {
        for (var i = 0; i < items.Count; i += size)
            yield return items.Skip(i).Take(size).ToList();
    }

    private static PresentationSlideContent ImageContent(
        OrthoPresentationSlideDefinition definition,
        IReadOnlyList<OrthoClinicalPhoto> photos,
        IReadOnlyDictionary<Guid, PresentationImage> loaded,
        Func<OrthoClinicalPhoto, bool> predicate)
    {
        var images = photos
            .Where(predicate)
            .Select(photo => loaded.GetValueOrDefault(photo.Id))
            .Where(image => image is not null)
            .Cast<PresentationImage>()
            .ToList();
        return Content(definition, images.Count > 0, images: images);
    }

    private static PresentationSlideContent Content(
        OrthoPresentationSlideDefinition definition,
        bool hasData,
        IReadOnlyList<string>? lines = null,
        IReadOnlyList<PresentationImage>? images = null,
        PresentationTable? table = null) =>
        new(
            definition,
            lines ?? [],
            images ?? [],
            table,
            hasData);

    private static IReadOnlyList<string> PatientInformation(OrthoCase source)
    {
        var age = source.Patient.DateOfBirth.HasValue
            ? CalculateAge(source.Patient.DateOfBirth.Value).ToString(Invariant)
            : null;
        return TextLines(
            ("اسم المريض", JoinName(source.Patient.FirstName, source.Patient.MiddleName, source.Patient.LastName)),
            ("رقم الملف", source.Patient.PatientNumber),
            ("العمر", age),
            ("الجنس", source.Patient.Gender?.ToString()),
            ("تاريخ بدء العلاج", FormatDate(source.StartDate)),
            ("الجهاز", source.ApplianceType),
            ("المرحلة الحالية", source.CurrentStage));
    }

    private static IReadOnlyList<string> ChiefComplaint(OrthoCase source) =>
        TextLines(
            ("الشكوى الرئيسية", source.Patient.DentalHistory?.ChiefComplaint),
            ("علاجات سابقة", source.Patient.DentalHistory?.PreviousTreatments),
            ("التاريخ السني", source.Patient.DentalHistory?.Notes),
            ("ملاحظات الفحص", source.ClinicalExam?.Notes));

    private static IReadOnlyList<string> CephSummary(CephAnalysis? analysis) =>
        analysis is null
            ? []
            : TextLines(
                ("نوع التحليل", analysis.AnalysisType),
                ("التاريخ", analysis.AnalysisDate.ToString("yyyy-MM-dd", Invariant)),
                ("التصنيف الهيكلي", analysis.Diagnosis?.SkeletalClass),
                ("النمط العمودي", analysis.Diagnosis?.VerticalPattern),
                ("القواطع", analysis.Diagnosis?.IncisorInclination),
                ("الأنسجة الرخوة", analysis.Diagnosis?.SoftTissueSummary),
                ("التشخيص النهائي", analysis.Diagnosis?.FinalDiagnosis));

    private static PresentationTable? CephMeasurements(CephAnalysis? analysis)
    {
        if (analysis is null) return null;
        var rows = analysis.Measurements
            .Where(item => item.IsActive)
            .OrderBy(item => item.MeasurementName)
            .Take(10)
            .Select(item => (IReadOnlyList<string>)
            [
                item.MeasurementName,
                FormatDecimal(item.MeasurementValue, item.Unit),
                FormatDecimal(item.NormalValue, item.Unit),
                item.Classification ?? string.Empty,
            ])
            .ToList();
        return new PresentationTable(["القياس", "القيمة", "الطبيعي", "التصنيف"], rows);
    }

    private static PresentationTable? CastAnalysis(ModelAnalysis? model)
    {
        if (model is null) return null;
        // Bolton has its own dedicated slide; this table covers arch dimensions/ALD/Pont.
        var rows = new List<IReadOnlyList<string>>();
        AddMeasurement(rows, "مجموع 12 علوي", model.UpperSum12);
        AddMeasurement(rows, "مجموع 12 سفلي", model.LowerSum12);
        AddMeasurement(rows, "طول القوس العلوي", model.UpperArchLength);
        AddMeasurement(rows, "طول القوس السفلي", model.LowerArchLength);
        AddMeasurement(rows, "ALD علوي", model.UpperAld);
        AddMeasurement(rows, "ALD سفلي", model.LowerAld);
        AddMeasurement(rows, "Pont", model.PontIndex);
        return new PresentationTable(["التحليل", "القيمة"], rows);
    }

    private static bool HasFacialData(OrthoCase source) =>
        source.PhotoAnalyses.Any(a => a.IsActive) ||
        HasText(source.Diagnosis?.FacialPattern) ||
        (source.ClinicalExam is { } e && (
            HasText(e.FacialSymmetry) || HasText(e.Profile) || HasText(e.SmileLine) ||
            HasText(e.VerticalProportion) || e.LipsCompetence.HasValue));

    private static IReadOnlyList<string> FacialAnalysis(OrthoCase source)
    {
        var e = source.ClinicalExam;
        var lines = TextLines(
            ("نمط الوجه", source.Diagnosis?.FacialPattern),
            ("تناظر الوجه", e?.FacialSymmetry),
            ("الملف الجانبي", e?.Profile),
            ("كفاءة الشفاه", e?.LipsCompetence is null ? null : (e.LipsCompetence.Value ? "كفؤة" : "غير كفؤة")),
            ("خط الابتسامة", e?.SmileLine),
            ("التناسب الرأسي (الأثلاث)", e?.VerticalProportion)).ToList();

        var profile = source.PhotoAnalyses.Where(a => a.IsActive && EqualsIgnoreCase(a.ViewType, "profile")).Count();
        var frontal = source.PhotoAnalyses.Where(a => a.IsActive && EqualsIgnoreCase(a.ViewType, "frontal")).Count();
        if (profile > 0 || frontal > 0)
            lines.Add($"تحاليل الصور: {(frontal > 0 ? "أمامي ✓ " : string.Empty)}{(profile > 0 ? "جانبي ✓" : string.Empty)}".Trim());
        return lines;
    }

    private static IReadOnlyList<string> Occlusion(OrthoClinicalExam? exam) =>
        exam is null
            ? []
            : TextLines(
                ("علاقة الأرحاء", FirstNonEmpty(exam.MolarRelation, exam.MolarRelationRight, exam.MolarRelationLeft)),
                ("علاقة الأنياب", FirstNonEmpty(exam.CanineRelation, exam.CanineRelationRight, exam.CanineRelationLeft)),
                ("علاقة القواطع", exam.IncisorRelation),
                ("البزوغ الأفقي (Overjet)", exam.Overjet.HasValue ? $"{exam.Overjet.Value:0.#} مم" : null),
                ("التطابق العمودي (Overbite)", exam.Overbite.HasValue ? $"{exam.Overbite.Value:0.#} مم"
                    : exam.OverbitePercent.HasValue ? $"{exam.OverbitePercent.Value:0.#}%" : null),
                ("عضّة معكوسة", exam.Crossbite ? FirstNonEmpty(exam.CrossbiteType, "نعم") : null),
                ("عضّة مفتوحة", exam.OpenBite ? "نعم" : null),
                ("عضّة عميقة", exam.DeepBite == true ? "نعم" : null),
                ("ازدحام علوي", exam.UpperCrowdingMm.HasValue ? $"{exam.UpperCrowdingMm.Value:0.#} مم" : exam.UpperCrowding),
                ("ازدحام سفلي", exam.LowerCrowdingMm.HasValue ? $"{exam.LowerCrowdingMm.Value:0.#} مم" : exam.LowerCrowding),
                ("انزياح الخط المتوسط العلوي", exam.MidlineUpperShiftMm.HasValue ? $"{exam.MidlineUpperShiftMm.Value:0.#} مم" : exam.MidlineUpper),
                ("انزياح الخط المتوسط السفلي", exam.MidlineLowerShiftMm.HasValue ? $"{exam.MidlineLowerShiftMm.Value:0.#} مم" : exam.MidlineLower),
                ("منحنى شبه (Spee)", exam.CurveOfSpee));

    private static IReadOnlyList<string> Bolton(ModelAnalysis? model)
    {
        if (model is null) return [];
        DentalModelAnalysisResult? result = null;
        try { result = JsonSerializer.Deserialize<DentalModelAnalysisResult>(model.ResultDataJson, JsonWeb); }
        catch (JsonException) { /* summary columns only */ }

        return TextLines(
            ("النسبة الكلية (Overall)", model.BoltonOverall.HasValue ? $"{model.BoltonOverall.Value:0.0}%" : null),
            ("النسبة الأمامية (Anterior)", model.BoltonAnterior.HasValue ? $"{model.BoltonAnterior.Value:0.0}%" : null),
            ("التفاوت الكلي", result?.Bolton?.OverallDiscrepancy is { } od ? $"{od:0.0} مم" : null),
            ("التفاوت الأمامي", result?.Bolton?.AnteriorDiscrepancy is { } ad ? $"{ad:0.0} مم" : null),
            ("تفسير العلاقة الكلية", result?.Bolton?.OverallInterpretation),
            ("تفسير العلاقة الأمامية", result?.Bolton?.AnteriorInterpretation));
    }

    // Canonical orthodontic problem-list ordering (Ackerman–Proffit style): skeletal →
    // dental → soft tissue → functional, then any other categories.
    private static readonly (string[] Keys, string Label)[] ProblemCategories =
    [
        (["skeletal", "هيكلي"], "هيكلي"),
        (["dental", "سني", "أسناني"], "سني"),
        (["soft", "soft tissue", "الأنسجة الرخوة", "نسيج رخو", "رخو"], "الأنسجة الرخوة"),
        (["functional", "وظيفي", "وظيفة"], "وظيفي"),
    ];

    private static (int Rank, string Label) ResolveProblemCategory(string? category)
    {
        if (HasText(category))
        {
            for (var i = 0; i < ProblemCategories.Length; i++)
                if (ProblemCategories[i].Keys.Any(k =>
                        category!.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    return (i, ProblemCategories[i].Label);
            return (ProblemCategories.Length, category!.Trim());
        }
        return (ProblemCategories.Length + 1, "أخرى");
    }

    private static IReadOnlyList<string> ProblemList(IEnumerable<ProblemListItem> problems) =>
        // Ordered by canonical category, then SortOrder. The category is a per-item
        // prefix (not a separate header line) so the line count equals the number of
        // problems — grouping never costs a slide-capacity slot, so no item is dropped.
        problems
            .Where(item => item.IsActive)
            .Select(item => (Cat: ResolveProblemCategory(item.Category), Item: item))
            .OrderBy(x => x.Cat.Rank)
            .ThenBy(x => x.Item.SortOrder)
            .Select(x =>
            {
                var severity = HasText(x.Item.Severity) ? $" ({x.Item.Severity!.Trim()})" : string.Empty;
                return $"{x.Cat.Label}: {x.Item.Description.Trim()}{severity}";
            })
            .ToList();

    private static IReadOnlyList<string> VisitLines(OrthoVisit visit) =>
        TextLines(
            ("التاريخ", visit.VisitDate.ToString("yyyy-MM-dd", Invariant)),
            ("النوع", visit.VisitType),
            ("المرحلة", visit.CurrentStage),
            ("السلك العلوي", visit.WireUpper),
            ("السلك السفلي", visit.WireLower),
            ("المطاطات", visit.ElasticsType),
            ("البزوغ الحالي", visit.CurrentOverjet.HasValue ? $"{visit.CurrentOverjet.Value:0.#} مم" : null),
            ("التطابق الحالي", visit.CurrentOverbite.HasValue ? $"{visit.CurrentOverbite.Value:0.#} مم" : null),
            ("ملاحظات", visit.ClinicalNotes),
            ("الموعد القادم", FormatDate(visit.NextAppointmentDate)));

    private static IReadOnlyList<string> Diagnosis(OrthoDiagnosis? diagnosis) =>
        diagnosis is null
            ? []
            : TextLines(
                ("التشخيص الهيكلي", diagnosis.SkeletalClassification),
                ("التشخيص السني", diagnosis.DentalClassification),
                ("نمط الوجه", diagnosis.FacialPattern),
                ("الأنسجة الرخوة", diagnosis.SoftTissueDiagnosis),
                ("التشخيص الوظيفي", diagnosis.FunctionalDiagnosis),
                ("المسببات", diagnosis.Etiology),
                ("الخلاصة", diagnosis.Summary));

    private static IReadOnlyList<string> TreatmentPlanLines(TreatmentPlan? plan) =>
        plan is null
            ? []
            : TextLines(
                ("الخطة", plan.PlanLabel),
                ("الجهاز", plan.ApplianceType),
                ("نظام الحاصرات", plan.BracketSystem),
                ("القلع", plan.ExtractionPlan),
                ("الارتكاز", plan.AnchoragePlan),
                ("المدة المتوقعة", plan.ExpectedDurationMonths.HasValue
                    ? $"{plan.ExpectedDurationMonths} شهر"
                    : null),
                ("الاحتفاظ", plan.RetentionPlan),
                ("المخاطر والحدود", plan.RisksLimitations));

    private static bool HasMechanotherapy(TreatmentPlan? plan) =>
        plan is not null &&
        (HasText(plan.BracketSystem) ||
         HasText(plan.InitialWire) ||
         HasText(plan.AnchoragePlan) ||
         plan.UseElastics ||
         plan.UseTads);

    private static IReadOnlyList<string> Mechanotherapy(TreatmentPlan? plan) =>
        plan is null
            ? []
            : TextLines(
                ("الحاصرات", plan.BracketSystem),
                ("السلك الأولي", plan.InitialWire),
                ("خطة الارتكاز", plan.AnchoragePlan),
                ("المطاطات", plan.UseElastics ? "مخططة" : "غير مخططة"),
                ("المسامير التقويمية TADs", plan.UseTads ? "مخططة" : "غير مخططة"),
                ("خطة القلع", plan.ExtractionPlan));

    private static IReadOnlyList<string> Retention(OrthoCase source) =>
        TextLines(
            ("خطة الاحتفاظ", source.RetentionPlan),
            ("تاريخ فك الجهاز", FormatDate(source.RetentionRecord?.DebondDate)),
            ("المثبت العلوي", source.RetentionRecord?.UpperRetainer),
            ("المثبت السفلي", source.RetentionRecord?.LowerRetainer),
            ("التعليمات", source.RetentionRecord?.Instructions),
            ("الحالة", source.RetentionRecord?.Status));

    private static IReadOnlyList<string> TextLines(
        params (string Label, string? Value)[] values) =>
        values
            .Where(item => HasText(item.Value))
            .Select(item => $"{item.Label}: {item.Value!.Trim()}")
            .ToList();

    private async Task<PresentationImage?> LoadPhotoAsync(
        OrthoClinicalPhoto photo,
        CancellationToken cancellationToken)
    {
        // A baked prepared image (PreparedImageUrl) wins when present; otherwise the
        // original photo is used and the saved crop region + flips are applied at
        // render time so the preparation the doctor set is honored in the deck.
        var preparation = photo.ImagePreparation;
        var preferred = preparation?.PreparedImageUrl;
        var hasBaked = HasText(preferred);
        var url = hasBaked ? preferred : photo.PhotoUrl;
        var label = photo.Caption ??
                    photo.Subtype ??
                    photo.PhotoType;

        // Don't re-apply crop/flip on top of an already-baked prepared image.
        var crop = hasBaked || preparation is null
            ? CropRegion.Full
            : CropRegion.FromPreparation(preparation);

        return await LoadImageAsync(url, label, crop, cancellationToken);
    }

    private static async Task<PresentationImage?> LoadImageAsync(
        string? url,
        string label,
        CancellationToken cancellationToken) =>
        await LoadImageAsync(url, label, CropRegion.Full, cancellationToken);

    private static async Task<PresentationImage?> LoadImageAsync(
        string? url,
        string label,
        CropRegion crop,
        CancellationToken cancellationToken)
    {
        var path = CephReportPdfGenerator.ResolveUploadFilePath(url);
        if (path is null) return null;

        try
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            return TryReadImageSize(bytes, out var width, out var height)
                ? new PresentationImage(
                    label, bytes, width, height,
                    crop.X, crop.Y, crop.Width, crop.Height,
                    crop.FlipHorizontal, crop.FlipVertical)
                : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Normalized crop region (fractions of the original image) plus flips,
    /// derived from a saved <see cref="OrthoImagePreparation"/>.</summary>
    private readonly record struct CropRegion(
        double X, double Y, double Width, double Height,
        bool FlipHorizontal, bool FlipVertical)
    {
        public static CropRegion Full { get; } = new(0, 0, 1, 1, false, false);

        public static CropRegion FromPreparation(OrthoImagePreparation prep)
        {
            // Clamp into [0,1] and guard against zero/negative or out-of-bounds rectangles
            // so a malformed record degrades to the full image rather than an invalid crop.
            var x = Clamp01((double)prep.CropX);
            var y = Clamp01((double)prep.CropY);
            var w = Clamp01((double)prep.CropWidth);
            var h = Clamp01((double)prep.CropHeight);
            if (w <= 0 || h <= 0 || x + w > 1.0001 || y + h > 1.0001)
                return new CropRegion(0, 0, 1, 1, prep.FlipHorizontal, prep.FlipVertical);
            return new CropRegion(x, y, w, h, prep.FlipHorizontal, prep.FlipVertical);
        }

        private static double Clamp01(double value) => Math.Clamp(value, 0, 1);
    }

    internal static bool TryReadImageSize(
        ReadOnlySpan<byte> bytes,
        out int width,
        out int height)
    {
        width = 0;
        height = 0;

        if (bytes.Length >= 24 &&
            bytes[0] == 0x89 &&
            bytes[1] == 0x50 &&
            bytes[2] == 0x4E &&
            bytes[3] == 0x47)
        {
            width = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(16, 4));
            height = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(20, 4));
            return width > 0 && height > 0;
        }

        if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8)
            return false;

        var offset = 2;
        while (offset + 8 < bytes.Length)
        {
            if (bytes[offset] != 0xFF)
            {
                offset++;
                continue;
            }

            var marker = bytes[offset + 1];
            offset += 2;
            if (marker is 0xD8 or 0xD9)
                continue;
            if (offset + 2 > bytes.Length)
                return false;

            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset, 2));
            if (length < 2 || offset + length > bytes.Length)
                return false;

            if (marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or
                0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF)
            {
                height = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset + 3, 2));
                width = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset + 5, 2));
                return width > 0 && height > 0;
            }

            offset += length;
        }

        return false;
    }

    private static bool EqualsIgnoreCase(string? value, string expected) =>
        string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

    private static bool HasText(string? value) =>
        !string.IsNullOrWhiteSpace(value);

    private static readonly JsonSerializerOptions JsonWeb = new(JsonSerializerDefaults.Web);

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(HasText);

    private static string JoinName(params string?[] values) =>
        string.Join(" ", values.Where(HasText).Select(value => value!.Trim()));

    private static string? FormatDate(DateOnly? value) =>
        value?.ToString("yyyy-MM-dd", Invariant);

    // Whole years, decremented when this year's birthday has not yet passed —
    // matches the case-summary PDF so both reports agree on the patient's age.
    internal static int CalculateAge(DateOnly dateOfBirth)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth > today.AddYears(-age)) age--;
        return Math.Max(0, age);
    }

    private static string FormatDecimal(decimal? value, string? unit = null) =>
        value.HasValue
            ? $"{value.Value:0.##}{(HasText(unit) ? $" {unit}" : string.Empty)}"
            : string.Empty;

    private static void AddMeasurement(
        ICollection<IReadOnlyList<string>> rows,
        string label,
        decimal? value)
    {
        if (value.HasValue)
            rows.Add([label, value.Value.ToString("0.##", Invariant)]);
    }
}
