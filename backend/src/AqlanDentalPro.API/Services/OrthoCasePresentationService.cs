using System.Buffers.Binary;
using System.Globalization;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Services;

public sealed class OrthoCasePresentationService(AppDbContext db)
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static IReadOnlyList<OrthoPresentationSlideDefinition> FoundationSlides { get; } =
    [
        Define(1, OrthoPresentationSlideType.Title, "عرض الحالة التقويمية", true, ["caseNumber", "patientName", "doctorName"]),
        Define(2, OrthoPresentationSlideType.PatientInformation, "بيانات المريض", true, ["patientNumber", "age", "gender", "startDate"]),
        Define(3, OrthoPresentationSlideType.ChiefComplaint, "المقابلة والشكوى الرئيسية", false, ["chiefComplaint", "history"]),
        Define(4, OrthoPresentationSlideType.ExtraoralPhotos, "الصور خارج الفم", false, images: ["frontal", "profile", "smile"]),
        Define(5, OrthoPresentationSlideType.IntraoralPhotos, "الصور داخل الفم", false, images: ["frontal", "right", "left", "upper", "lower"]),
        Define(6, OrthoPresentationSlideType.PanoramicXray, "الأشعة البانورامية", false, images: ["opg"]),
        Define(7, OrthoPresentationSlideType.CephalometricSummary, "الأشعة السيفالومترية", false, ["analysisType", "diagnosis"], ["lateralCeph"]),
        Define(8, OrthoPresentationSlideType.CephalometricMeasurements, "ملخص قياسات السيفالو", false, tables: ["measurements"]),
        Define(9, OrthoPresentationSlideType.CastAnalysis, "تحليل النماذج وBolton", false, tables: ["castAnalysis"]),
        Define(10, OrthoPresentationSlideType.Diagnosis, "التشخيص", true, ["skeletal", "dental", "softTissue", "functional", "etiology"]),
        Define(11, OrthoPresentationSlideType.TreatmentObjectives, "أهداف العلاج", true, ["treatmentGoals"]),
        Define(12, OrthoPresentationSlideType.TreatmentPlan, "خطة العلاج", true, ["planLabel", "appliance", "duration", "retention"]),
        Define(13, OrthoPresentationSlideType.Mechanotherapy, "ملخص الميكانيكا العلاجية", false, ["brackets", "wire", "anchorage", "elastics", "tads"]),
        Define(14, OrthoPresentationSlideType.VisitProgress, "متابعة الزيارات والتقدم", false, tables: ["visits"]),
        Define(15, OrthoPresentationSlideType.Retention, "مرحلة الاحتفاظ", false, ["debondDate", "retainers", "instructions"]),
        Define(16, OrthoPresentationSlideType.ThankYou, "ختام العرض", true),
    ];

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
            "Foundation",
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

        return PowerPointPresentationBuilder.Build(new OrthoCasePresentationDocument(
            identity.ClinicName,
            string.IsNullOrWhiteSpace(source.Doctor?.Name) ? identity.LeadDoctor : source.Doctor.Name,
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
            .Include(c => c.TreatmentPlans)
            .Include(c => c.Visits)
            .Include(c => c.RetentionRecord)
            .Include(c => c.ModelAnalyses)
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

        var contents = new List<PresentationSlideContent>();
        foreach (var definition in FoundationSlides)
        {
            contents.Add(definition.Type switch
            {
                OrthoPresentationSlideType.Title => Content(definition, true),
                OrthoPresentationSlideType.PatientInformation => Content(
                    definition,
                    true,
                    PatientInformation(source)),
                OrthoPresentationSlideType.ChiefComplaint => Content(
                    definition,
                    HasText(source.Patient.DentalHistory?.ChiefComplaint),
                    ChiefComplaint(source)),
                OrthoPresentationSlideType.ExtraoralPhotos => ImageContent(
                    definition,
                    selectedPhotos,
                    loadedPhotos,
                    photo => EqualsIgnoreCase(photo.Category, "Extraoral") ||
                             EqualsIgnoreCase(photo.PhotoType, "Extraoral")),
                OrthoPresentationSlideType.IntraoralPhotos => ImageContent(
                    definition,
                    selectedPhotos,
                    loadedPhotos,
                    photo => EqualsIgnoreCase(photo.Category, "Intraoral") ||
                             EqualsIgnoreCase(photo.PhotoType, "Intraoral")),
                OrthoPresentationSlideType.PanoramicXray => ImageContent(
                    definition,
                    selectedPhotos,
                    loadedPhotos,
                    photo => EqualsIgnoreCase(photo.Subtype, "OPG") ||
                             EqualsIgnoreCase(photo.Subtype, "Panoramic")),
                OrthoPresentationSlideType.CephalometricSummary => Content(
                    definition,
                    latestCeph is not null,
                    CephSummary(latestCeph),
                    cephImage is null ? [] : [cephImage]),
                OrthoPresentationSlideType.CephalometricMeasurements => Content(
                    definition,
                    latestCeph?.Measurements.Any(item => item.IsActive) == true,
                    table: CephMeasurements(latestCeph)),
                OrthoPresentationSlideType.CastAnalysis => Content(
                    definition,
                    latestModel is not null,
                    table: CastAnalysis(latestModel)),
                OrthoPresentationSlideType.Diagnosis => Content(
                    definition,
                    source.Diagnosis is not null,
                    Diagnosis(source.Diagnosis)),
                OrthoPresentationSlideType.TreatmentObjectives => Content(
                    definition,
                    HasText(approvedPlan?.TreatmentGoals),
                    TextLines(("أهداف العلاج", approvedPlan?.TreatmentGoals))),
                OrthoPresentationSlideType.TreatmentPlan => Content(
                    definition,
                    approvedPlan is not null,
                    TreatmentPlanLines(approvedPlan)),
                OrthoPresentationSlideType.Mechanotherapy => Content(
                    definition,
                    HasMechanotherapy(approvedPlan),
                    Mechanotherapy(approvedPlan)),
                OrthoPresentationSlideType.VisitProgress => Content(
                    definition,
                    source.Visits.Any(item => item.IsActive),
                    table: VisitProgress(source.Visits)),
                OrthoPresentationSlideType.Retention => Content(
                    definition,
                    source.RetentionRecord is not null || HasText(source.RetentionPlan),
                    Retention(source)),
                OrthoPresentationSlideType.ThankYou => Content(definition, true),
                _ => Content(definition, false),
            });
        }

        return contents;
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
            ? Math.Max(0, DateTime.UtcNow.Year - source.Patient.DateOfBirth.Value.Year).ToString(Invariant)
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
        var rows = new List<IReadOnlyList<string>>();
        AddMeasurement(rows, "Bolton الكلي", model.BoltonOverall);
        AddMeasurement(rows, "Bolton الأمامي", model.BoltonAnterior);
        AddMeasurement(rows, "مجموع 12 علوي", model.UpperSum12);
        AddMeasurement(rows, "مجموع 12 سفلي", model.LowerSum12);
        AddMeasurement(rows, "طول القوس العلوي", model.UpperArchLength);
        AddMeasurement(rows, "طول القوس السفلي", model.LowerArchLength);
        AddMeasurement(rows, "ALD علوي", model.UpperAld);
        AddMeasurement(rows, "ALD سفلي", model.LowerAld);
        AddMeasurement(rows, "Pont", model.PontIndex);
        return new PresentationTable(["التحليل", "القيمة"], rows);
    }

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

    private static PresentationTable VisitProgress(IEnumerable<OrthoVisit> visits)
    {
        var rows = visits
            .Where(item => item.IsActive)
            .OrderByDescending(item => item.VisitDate)
            .ThenByDescending(item => item.VisitNumber)
            .Take(10)
            .Select(item => (IReadOnlyList<string>)
            [
                item.VisitNumber.ToString(Invariant),
                item.VisitDate.ToString("yyyy-MM-dd", Invariant),
                item.VisitType ?? string.Empty,
                item.CurrentStage ?? string.Empty,
                item.ClinicalNotes ?? string.Empty,
            ])
            .ToList();
        return new PresentationTable(["#", "التاريخ", "النوع", "المرحلة", "الملاحظات"], rows);
    }

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
        var preferred = photo.ImagePreparation?.PreparedImageUrl;
        var url = HasText(preferred) ? preferred : photo.PhotoUrl;
        var label = photo.Caption ??
                    photo.Subtype ??
                    photo.PhotoType;
        return await LoadImageAsync(url, label, cancellationToken);
    }

    private static async Task<PresentationImage?> LoadImageAsync(
        string? url,
        string label,
        CancellationToken cancellationToken)
    {
        var path = CephReportPdfGenerator.ResolveUploadFilePath(url);
        if (path is null) return null;

        try
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            return TryReadImageSize(bytes, out var width, out var height)
                ? new PresentationImage(label, bytes, width, height)
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

    private static OrthoPresentationSlideDefinition Define(
        int order,
        OrthoPresentationSlideType type,
        string title,
        bool required,
        IReadOnlyList<string>? text = null,
        IReadOnlyList<string>? images = null,
        IReadOnlyList<string>? tables = null) =>
        new(
            order,
            type,
            title,
            required,
            text ?? [],
            images ?? [],
            tables ?? []);

    private static bool EqualsIgnoreCase(string? value, string expected) =>
        string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

    private static bool HasText(string? value) =>
        !string.IsNullOrWhiteSpace(value);

    private static string JoinName(params string?[] values) =>
        string.Join(" ", values.Where(HasText).Select(value => value!.Trim()));

    private static string? FormatDate(DateOnly? value) =>
        value?.ToString("yyyy-MM-dd", Invariant);

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
