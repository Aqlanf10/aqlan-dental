using System.Globalization;
using System.Text;
using System.Text.Json;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AqlanDentalPro.API.Services;

/// <summary>
/// Clinic identity strings used in the ceph report header/footer/signature.
/// Every value falls back to an empty string when its Settings key is missing —
/// report generation must never crash because of missing configuration.
/// </summary>
public sealed record CephReportClinicIdentity(
    string ClinicName,
    string LeadDoctor,
    string LeadDoctorTitle,
    string LeadDoctorCredentials,
    string Phones,
    string Location);

/// <summary>
/// Ceph batch C-C — Arabic cephalometric analysis PDF report (QuestPDF).
/// Follows the LabOrderPdfGenerator document patterns (A4, RTL, Noto Naskh Arabic).
/// The tracing overlay (landmarks + reference lines) is drawn as an SVG layer on
/// top of the radiograph via QuestPDF Layers — QuestPDF 2026.5.0 no longer ships
/// SkiaSharp as a referencable dependency and the legacy Canvas element was
/// removed, so the vector overlay uses the supported .Svg() element instead.
/// </summary>
public class CephReportPdfGenerator(AppDbContext db)
{
    private const string FontName = AqlanDentalPro.Infrastructure.Services.PdfService.ArabicFontName;

    // ── Landmark group palette (same grouping as frontend CephCanvas LANDMARK_DEFS) ──
    private static readonly Dictionary<string, string> LandmarkGroupByKey = new(StringComparer.Ordinal)
    {
        // cranial
        ["S"] = "cranial", ["N"] = "cranial", ["Or"] = "cranial", ["Po"] = "cranial",
        // maxilla
        ["ANS"] = "maxilla", ["PNS"] = "maxilla", ["A"] = "maxilla",
        // mandible
        ["B"] = "mandible", ["Pog"] = "mandible", ["Gn"] = "mandible", ["Me"] = "mandible",
        ["Go"] = "mandible", ["Co"] = "mandible", ["Ar"] = "mandible", ["D"] = "mandible", ["Pm"] = "mandible",
        // dental
        ["U1T"] = "dental", ["U1A"] = "dental", ["L1T"] = "dental", ["L1A"] = "dental",
        // soft tissue
        ["LS"] = "soft", ["LI"] = "soft", ["Pn"] = "soft", ["Cm"] = "soft",
    };

    private static readonly Dictionary<string, string> GroupColor = new(StringComparer.Ordinal)
    {
        ["cranial"]  = "#3B82F6",
        ["maxilla"]  = "#F97316",
        ["mandible"] = "#EF4444",
        ["dental"]   = "#22C55E",
        ["soft"]     = "#EC4899",
    };

    private const string DefaultLandmarkColor = "#3B82F6";

    // Reference lines drawn when both endpoints exist (colors follow the canvas planes).
    private static readonly (string From, string To, string Color)[] ReferenceLines =
    [
        ("S",  "N",  "#60A5FA"),
        ("Go", "Gn", "#F87171"),
        ("N",  "A",  "#FB923C"),
        ("N",  "B",  "#FCD34D"),
        // Jarabak (Björk) polygon legs — S-N is drawn above; add S-Ar / Ar-Go / Go-Me.
        ("S",  "Ar", "#C084FC"),
        ("Ar", "Go", "#C084FC"),
        ("Go", "Me", "#F87171"),
    ];

    // Anatomical tracing contours — must mirror frontend lib/cephTracing.ts so the
    // PDF tracing matches the on-screen tracing (cranial base, maxilla, mandible,
    // facial + soft-tissue profile, incisors).
    private static readonly (string[] Keys, string Color)[] TracingContours =
    [
        (["S", "N"], "#60A5FA"),
        (["PNS", "ANS", "A"], "#FB923C"),
        (["Co", "Go", "Me", "Pog", "B"], "#F87171"),
        (["Ar", "Go"], "#F87171"),
        (["N", "A", "Pog"], "#FBBF24"),
        (["Pn", "Cm", "LS", "LI", "Pog"], "#F472B6"),
        (["U1A", "U1T"], "#34D399"),
        (["L1A", "L1T"], "#10B981"),
    ];

    private static readonly string[] GroupOrder = ["steiner", "tweed", "mcnamara", "ricketts", "downs", "jarabak", "wits"];

    private static readonly Dictionary<string, string> AnalysisGroupTitleAr = new(StringComparer.OrdinalIgnoreCase)
    {
        ["steiner"]  = "تحليل ستاينر",
        ["tweed"]    = "تحليل تويد",
        ["mcnamara"] = "تحليل ماكنامارا",
        ["ricketts"] = "تحليل ريكتس",
        ["downs"]    = "تحليل داونز",
        ["jarabak"]  = "تحليل جاراباك (اتجاه النمو)",
        ["wits"]     = "مؤشر وتس (Wits)",
    };

    private static readonly Dictionary<string, string> AnalysisTypeAr = new(StringComparer.OrdinalIgnoreCase)
    {
        ["steiner"]  = "ستاينر",
        ["tweed"]    = "تويد",
        ["mcnamara"] = "ماكنامارا",
        ["ricketts"] = "ريكتس",
        ["downs"]    = "داونز",
        ["jarabak"]  = "جاراباك",
        ["wits"]     = "وتس",
        ["full"]     = "شامل",
    };

    private static readonly string[] ArabicMonths =
        ["يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
         "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر"];

    // ──────────────────────────────────────────────────────────────────────
    //  PUBLIC API
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the Arabic cephalometric report PDF for the given analysis.
    /// Throws <see cref="ArgumentException"/> when the analysis does not exist
    /// (mapped to an Arabic 404 by the controller).
    /// </summary>
    public async Task<byte[]> GenerateAsync(Guid analysisId)
    {
        var analysis = await db.CephAnalyses
            .Include(a => a.OrthoCase).ThenInclude(o => o.Patient)
            .Include(a => a.Landmarks)
            .Include(a => a.Measurements)
            .Include(a => a.Diagnosis)
            .FirstOrDefaultAsync(a => a.Id == analysisId && a.IsActive);

        if (analysis is null)
            throw new ArgumentException($"Ceph analysis {analysisId} not found.", nameof(analysisId));

        var identity = await ResolveClinicIdentityAsync(db);

        var norms = await db.CephNorms.Where(n => n.IsActive).ToListAsync();
        var normByName = norms
            .GroupBy(n => n.MeasurementName)
            .ToDictionary(g => g.Key, g => g.First());

        return Generate(analysis, identity, normByName);
    }

    /// <summary>
    /// Reads the clinic identity Settings keys used by the report.
    /// Missing keys resolve to empty strings — never throws for missing config.
    /// Static + public so tests can verify settings resolution directly.
    /// </summary>
    public static async Task<CephReportClinicIdentity> ResolveClinicIdentityAsync(AppDbContext db)
    {
        string[] keys =
        [
            "clinic.name", "clinic.lead_doctor", "clinic.lead_doctor_title",
            "clinic.lead_doctor_credentials", "clinic.phones", "clinic.location",
        ];

        var settings = await db.Settings
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        string Get(string key) => settings.TryGetValue(key, out var v) ? v ?? string.Empty : string.Empty;

        return new CephReportClinicIdentity(
            ClinicName:            Get("clinic.name"),
            LeadDoctor:            Get("clinic.lead_doctor"),
            LeadDoctorTitle:       Get("clinic.lead_doctor_title"),
            LeadDoctorCredentials: Get("clinic.lead_doctor_credentials"),
            Phones:                Get("clinic.phones"),
            Location:              Get("clinic.location"));
    }

    /// <summary>
    /// Resolves a relative upload URL (e.g. "/uploads/{file}") to a local file
    /// path using the same directory priority as Program.cs static files and
    /// UploadsController: UPLOADS_PATH env var → wwwroot/uploads → temp fallback.
    /// Returns null when the URL is invalid or the file does not exist —
    /// the PDF then renders without the tracing image («الصورة غير متاحة»).
    /// </summary>
    public static string? ResolveUploadFilePath(string? xrayFileUrl)
    {
        if (string.IsNullOrWhiteSpace(xrayFileUrl)) return null;

        var url = xrayFileUrl.Trim();
        var marker = url.IndexOf("/uploads/", StringComparison.OrdinalIgnoreCase);
        if (marker < 0) return null;

        var fileName = Uri.UnescapeDataString(url[(marker + "/uploads/".Length)..]);
        // Path-traversal guard — uploads are stored flat with GUID file names.
        if (fileName.Length == 0 || fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains(".."))
            return null;

        var candidateDirs = new List<string>();
        var envPath = Environment.GetEnvironmentVariable("UPLOADS_PATH");
        if (!string.IsNullOrWhiteSpace(envPath)) candidateDirs.Add(envPath);
        candidateDirs.Add(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads"));
        candidateDirs.Add(Path.Combine(Path.GetTempPath(), "aqlan-uploads"));

        foreach (var dir in candidateDirs)
        {
            try
            {
                var path = Path.Combine(dir, fileName);
                if (File.Exists(path)) return path;
            }
            catch
            {
                // Invalid path characters etc. — try next candidate.
            }
        }

        return null;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  DOCUMENT
    // ──────────────────────────────────────────────────────────────────────

    private static byte[] Generate(
        CephAnalysis analysis,
        CephReportClinicIdentity identity,
        IReadOnlyDictionary<string, CephNorm> normByName)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        AqlanDentalPro.Infrastructure.Services.PdfService.EnsureFontsRegistered();

        var landmarks = analysis.Landmarks.Where(l => l.IsActive).ToList();
        var measurements = analysis.Measurements.Where(m => m.IsActive).ToList();
        var (imageWidth, imageHeight) = ReadImageDimensions(analysis.Notes);

        // Load the radiograph; any failure (missing file, unreadable, non-image)
        // degrades to the «الصورة غير متاحة» note — never a crash.
        Image? xrayImage = null;
        var imagePath = ResolveUploadFilePath(analysis.XrayFileUrl);
        if (imagePath is not null)
        {
            try
            {
                xrayImage = Image.FromBinaryData(await File.ReadAllBytesAsync(imagePath));
            }
            catch
            {
                xrayImage = null;
            }
        }

        var overlaySvg = landmarks.Count > 0
            ? BuildOverlaySvg(landmarks, imageWidth, imageHeight)
            : null;

        var doc = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.ContentFromRightToLeft();
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(FontName));

                page.Header().Element(c => ComposeHeader(c, analysis, identity));
                page.Content().Element(c => ComposeContent(
                    c, analysis, identity, landmarks, measurements, normByName, xrayImage, overlaySvg, imageWidth, imageHeight));
                page.Footer().Element(c => ComposeFooter(c, identity));
            });
        });

        return doc.GeneratePdf();
    }

    // ── Header: clinic identity (from Settings — no hardcoding) + report meta ──
    private static void ComposeHeader(IContainer container, CephAnalysis analysis, CephReportClinicIdentity identity)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Row(idRow =>
                {
                    // Clinic logo (same mechanism as receipts/invoices: Fonts/logo.png).
                    // Fallback is graceful — when no logo file exists the clinic name
                    // text alone heads the report; no broken image is ever shown.
                    var logoPath = ResolveLogoPath();
                    if (logoPath is not null)
                        idRow.ConstantItem(48).PaddingLeft(8).AlignTop().Image(logoPath);

                    idRow.RelativeItem().Column(col =>
                    {
                        col.Item().Text(identity.ClinicName).Bold().FontSize(15).FontFamily(FontName);
                        // Lead doctor block: name (bold) / title / credentials (smaller, muted)
                        col.Item().Text(identity.LeadDoctor).Bold().FontSize(11).FontFamily(FontName);
                        col.Item().Text(identity.LeadDoctorTitle).FontSize(9).FontFamily(FontName);
                        col.Item().Text(identity.LeadDoctorCredentials)
                            .FontSize(8).FontColor(Colors.Grey.Darken1).FontFamily(FontName);
                    });
                });

                row.RelativeItem().AlignLeft().Column(col =>
                {
                    col.Item().AlignLeft().Text("تقرير التحليل السيفالومتري").Bold().FontSize(14).FontFamily(FontName);
                    col.Item().AlignLeft().Text($"المريض: {PatientName(analysis)}").FontSize(10).FontFamily(FontName);
                    col.Item().AlignLeft().Text($"رقم الحالة: {analysis.OrthoCase?.CaseNumber ?? "غير محدد"}").FontSize(9).FontFamily(FontName);
                    col.Item().AlignLeft().Text($"تاريخ التحليل: {FormatArabicDate(analysis.AnalysisDate)}").FontSize(9).FontFamily(FontName);
                    col.Item().AlignLeft().Text($"نوع التحليل: {AnalysisTypeArOf(analysis.AnalysisType)}").FontSize(9).FontFamily(FontName);
                });
            });

            column.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    // ── Content ──
    private static void ComposeContent(
        IContainer container,
        CephAnalysis analysis,
        CephReportClinicIdentity identity,
        List<CephLandmark> landmarks,
        List<CephMeasurement> measurements,
        IReadOnlyDictionary<string, CephNorm> normByName,
        Image? xrayImage,
        string? overlaySvg,
        int imageWidth,
        int imageHeight)
    {
        container.Column(column =>
        {
            column.Spacing(10);

            // ── Tracing image section ──
            column.Item().Text("الصورة الشعاعية والتتبع").Bold().FontSize(11).FontFamily(FontName);

            if (xrayImage is not null)
            {
                var aspect = imageWidth > 0 && imageHeight > 0 ? (float)imageWidth / imageHeight : 4f / 3f;
                column.Item().AlignCenter().MaxHeight(330)
                    .AspectRatio(aspect, AspectRatioOption.FitArea).Layers(layers =>
                {
                    // The frontend canvas stretches the radiograph to the calibrated
                    // ImageWidth×ImageHeight box that landmark coordinates live in,
                    // so the PDF does the same (FitUnproportionally fills the box).
                    layers.PrimaryLayer()
                        .Image(xrayImage)
                        .FitUnproportionally()
                        .WithRasterDpi(150)
                        .WithCompressionQuality(ImageCompressionQuality.High);

                    if (overlaySvg is not null)
                        layers.Layer().Svg(overlaySvg);
                });

                if (landmarks.Count > 0)
                    column.Item().AlignCenter()
                        .Text($"عدد المعالم الموضوعة: {landmarks.Count}")
                        .FontSize(8).FontColor(Colors.Grey.Darken1).FontFamily(FontName);
            }
            else
            {
                column.Item().Border(1).BorderColor(Colors.Grey.Lighten2)
                    .Background(Colors.Grey.Lighten4).Padding(12).AlignCenter()
                    .Text("الصورة غير متاحة").FontSize(10).FontColor(Colors.Grey.Darken1).FontFamily(FontName);
            }

            // ── Measurements grouped by analysis ──
            ComposeMeasurementGroups(column, measurements, normByName);

            // ── Diagnosis ──
            ComposeDiagnosis(column, analysis.Diagnosis);

            // ── Signature block ──
            column.Item().PaddingTop(15).Row(row =>
            {
                row.RelativeItem();
                row.ConstantItem(230).Column(col =>
                {
                    col.Item().PaddingBottom(25).LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                    col.Item().AlignCenter()
                        .Text($"{identity.LeadDoctor} — {identity.LeadDoctorTitle}")
                        .Bold().FontSize(10).FontFamily(FontName);
                    col.Item().AlignCenter()
                        .Text(identity.LeadDoctorCredentials)
                        .FontSize(8).FontColor(Colors.Grey.Darken1).FontFamily(FontName);
                    col.Item().PaddingTop(8).AlignCenter()
                        .Text("التاريخ: ‏________________").FontSize(9).FontFamily(FontName);
                });
            });
        });
    }

    private static void ComposeMeasurementGroups(
        ColumnDescriptor column,
        List<CephMeasurement> measurements,
        IReadOnlyDictionary<string, CephNorm> normByName)
    {
        if (measurements.Count == 0)
        {
            column.Item().Text("لا توجد قياسات محسوبة بعد — ضع المعالم ثم اضغط «احسب».")
                .FontSize(9).FontColor(Colors.Grey.Darken1).FontFamily(FontName);
            return;
        }

        var grouped = measurements
            .GroupBy(m => normByName.TryGetValue(m.MeasurementName, out var n) && !string.IsNullOrWhiteSpace(n.AnalysisGroup)
                ? n.AnalysisGroup.ToLowerInvariant()
                : CephService.GetMeasurementGroup(m.MeasurementName))
            .ToDictionary(g => g.Key, g => g.ToList());

        var orderedGroups = GroupOrder.Where(grouped.ContainsKey)
            .Concat(grouped.Keys.Where(k => !GroupOrder.Contains(k)).OrderBy(k => k));

        foreach (var group in orderedGroups)
        {
            var rows = grouped[group];
            if (rows.Count == 0) continue; // skip groups with no rows

            column.Item().PaddingTop(4)
                .Text(AnalysisGroupTitleAr.TryGetValue(group, out var title) ? title : group)
                .Bold().FontSize(11).FontFamily(FontName);

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);   // القياس
                    columns.RelativeColumn(1.2f); // القيمة
                    columns.RelativeColumn(1.6f); // المعيار ±SD
                    columns.RelativeColumn(1.2f); // الانحراف
                    columns.RelativeColumn(1.4f); // الحالة
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCellStyle).Text("القياس").Bold().FontFamily(FontName);
                    header.Cell().Element(HeaderCellStyle).Text("القيمة").Bold().FontFamily(FontName);
                    header.Cell().Element(HeaderCellStyle).Text("المعيار ±SD").Bold().FontFamily(FontName);
                    header.Cell().Element(HeaderCellStyle).Text("الانحراف").Bold().FontFamily(FontName);
                    header.Cell().Element(HeaderCellStyle).Text("الحالة").Bold().FontFamily(FontName);
                });

                foreach (var m in rows)
                {
                    normByName.TryGetValue(m.MeasurementName, out var norm);
                    var nameAr = !string.IsNullOrWhiteSpace(norm?.NameAr)
                        ? norm!.NameAr!
                        : CephService.GetMeasurementNameAr(m.MeasurementName);
                    var interpretation = GetOutOfRangeInterpretation(m, norm);

                    table.Cell().Element(CellStyle).Column(c =>
                    {
                        c.Item().Text(nameAr).FontFamily(FontName);
                        if (!string.IsNullOrWhiteSpace(interpretation))
                            c.Item().Text(interpretation)
                                .FontSize(7.5f).FontColor(Colors.Grey.Darken1).FontFamily(FontName);
                    });

                    table.Cell().Element(CellStyle)
                        .Text(m.MeasurementValue.HasValue ? $"{m.MeasurementValue.Value:0.0} {m.Unit}" : "—")
                        .FontFamily(FontName);

                    table.Cell().Element(CellStyle)
                        .Text(m.NormalValue.HasValue
                            ? $"{m.NormalValue.Value:0.0} ± {m.StdDeviation ?? 0:0.0}"
                            : "—")
                        .FontFamily(FontName);

                    var deviation = m.MeasurementValue.HasValue && m.NormalValue.HasValue
                        ? m.MeasurementValue.Value - m.NormalValue.Value
                        : m.Deviation;
                    table.Cell().Element(CellStyle)
                        .Text(deviation.HasValue ? $"{deviation.Value:+0.0;-0.0;0.0}" : "—")
                        .FontFamily(FontName);

                    var (statusAr, statusBg) = ClassificationDisplay(m.Classification);
                    table.Cell().Element(c => CellStyle(c).Background(statusBg))
                        .Text(statusAr).FontSize(9).FontFamily(FontName);
                }
            });
        }
    }

    private static void ComposeDiagnosis(ColumnDescriptor column, CephDiagnosis? diagnosis)
    {
        column.Item().PaddingTop(4).Text("التشخيص").Bold().FontSize(11).FontFamily(FontName);

        if (diagnosis is null)
        {
            column.Item().Text("لا يوجد تشخيص مسجل بعد.")
                .FontSize(9).FontColor(Colors.Grey.Darken1).FontFamily(FontName);
            return;
        }

        column.Item().Column(col =>
        {
            col.Spacing(2);
            if (!string.IsNullOrWhiteSpace(diagnosis.SkeletalClass))
                col.Item().Text($"العلاقة الهيكلية: {SkeletalClassAr(diagnosis.SkeletalClass)}").FontFamily(FontName);
            if (!string.IsNullOrWhiteSpace(diagnosis.VerticalPattern))
                col.Item().Text($"النمط الرأسي: {VerticalPatternAr(diagnosis.VerticalPattern)}").FontFamily(FontName);
            if (!string.IsNullOrWhiteSpace(diagnosis.IncisorInclination))
                col.Item().Text($"ميلان القواطع: {diagnosis.IncisorInclination}").FontFamily(FontName);
        });

        // Rule-engine summary — honest labeling: automated, needs specialist review.
        if (!string.IsNullOrWhiteSpace(diagnosis.AiRecommendation))
        {
            column.Item().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(8).Column(box =>
            {
                box.Item().Text("الملخص القاعدي الآلي — يتطلب مراجعة الأخصائي")
                    .Bold().FontSize(9).FontColor(Colors.Grey.Darken2).FontFamily(FontName);
                box.Item().PaddingTop(3).Text(diagnosis.AiRecommendation).FontSize(9).FontFamily(FontName);
            });
        }

        if (!string.IsNullOrWhiteSpace(diagnosis.FinalDiagnosis))
        {
            column.Item().Border(1).BorderColor(Colors.Green.Lighten1).Padding(8).Column(box =>
            {
                box.Item().Row(r =>
                {
                    r.AutoItem().Text("تشخيص الطبيب المعتمد")
                        .Bold().FontSize(9).FontColor(Colors.Green.Darken2).FontFamily(FontName);
                    if (diagnosis.DoctorApproved)
                        r.AutoItem().PaddingHorizontal(4).Text("✓ معتمد")
                            .Bold().FontSize(9).FontColor(Colors.Green.Darken2).FontFamily(FontName);
                });
                box.Item().PaddingTop(3).Text(diagnosis.FinalDiagnosis).FontSize(9).FontFamily(FontName);
            });
        }
    }

    // ── Footer: phones/location (Settings) + page numbers ──
    private static void ComposeFooter(IContainer container, CephReportClinicIdentity identity)
    {
        container.Column(column =>
        {
            var contact = string.Join(" — ",
                new[] { identity.Phones, identity.Location }.Where(s => !string.IsNullOrWhiteSpace(s)));

            if (contact.Length > 0)
                column.Item().AlignCenter().Text(contact)
                    .FontSize(8).FontColor(Colors.Grey.Darken1).FontFamily(FontName);

            column.Item().AlignCenter().Text(text =>
            {
                text.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Medium).FontFamily(FontName));
                text.Span("صفحة ");
                text.CurrentPageNumber();
                text.Span(" من ");
                text.TotalPages();
            });
        });
    }

    // ──────────────────────────────────────────────────────────────────────
    //  TRACING OVERLAY (SVG layer in image coordinate space)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the landmark/reference-line overlay as an SVG document whose
    /// viewBox matches the calibrated image space the landmark coordinates
    /// live in. Internal for unit testing.
    /// </summary>
    public static string BuildOverlaySvg(List<CephLandmark> landmarks, int imageWidth, int imageHeight)
    {
        var w = imageWidth > 0 ? imageWidth : 800;
        var h = imageHeight > 0 ? imageHeight : 600;

        var points = landmarks
            .Where(l => !string.IsNullOrWhiteSpace(l.LandmarkKey) && l.XCoord.HasValue && l.YCoord.HasValue)
            .GroupBy(l => l.LandmarkKey)
            .ToDictionary(g => g.Key, g => ((double)g.First().XCoord!.Value, (double)g.First().YCoord!.Value));

        var inv = CultureInfo.InvariantCulture;
        double r = Math.Max(4.0, w / 200.0);          // landmark radius
        double lineW = Math.Max(1.5, w / 500.0);      // reference line width
        double fontSize = r * 2.6;                    // key label size

        var sb = new StringBuilder();
        sb.Append(string.Create(inv,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {w} {h}\" preserveAspectRatio=\"none\">"));

        // Reference lines first so circles render on top.
        foreach (var (from, to, color) in ReferenceLines)
        {
            if (!points.TryGetValue(from, out var p1) || !points.TryGetValue(to, out var p2)) continue;
            sb.Append(string.Create(inv,
                $"<line x1=\"{p1.Item1:0.##}\" y1=\"{p1.Item2:0.##}\" x2=\"{p2.Item1:0.##}\" y2=\"{p2.Item2:0.##}\" " +
                $"stroke=\"{color}\" stroke-width=\"{lineW:0.##}\" stroke-opacity=\"0.85\"/>"));
        }

        // Anatomical tracing contours — polylines through present landmarks.
        var tracingW = lineW * 1.4;
        foreach (var (keys, color) in TracingContours)
        {
            var present = keys.Where(points.ContainsKey).ToList();
            if (present.Count < 2) continue;
            var coords = string.Join(" ", present.Select(k =>
                string.Create(inv, $"{points[k].Item1:0.##},{points[k].Item2:0.##}")));
            sb.Append(string.Create(inv,
                $"<polyline points=\"{coords}\" fill=\"none\" stroke=\"{color}\" " +
                $"stroke-width=\"{tracingW:0.##}\" stroke-opacity=\"0.9\" stroke-linejoin=\"round\" stroke-linecap=\"round\"/>"));
        }

        foreach (var (key, (x, y)) in points)
        {
            var color = LandmarkGroupByKey.TryGetValue(key, out var group) && GroupColor.TryGetValue(group, out var c)
                ? c
                : DefaultLandmarkColor;

            sb.Append(string.Create(inv,
                $"<circle cx=\"{x:0.##}\" cy=\"{y:0.##}\" r=\"{r:0.##}\" fill=\"{color}\" " +
                $"stroke=\"#FFFFFF\" stroke-width=\"{lineW * 0.6:0.##}\"/>"));
            sb.Append(string.Create(inv,
                $"<text x=\"{x + r * 1.5:0.##}\" y=\"{y - r * 0.7:0.##}\" font-family=\"Arial, sans-serif\" " +
                $"font-size=\"{fontSize:0.##}\" font-weight=\"bold\" fill=\"{color}\">{key}</text>"));
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    // ──────────────────────────────────────────────────────────────────────
    //  HELPERS
    // ──────────────────────────────────────────────────────────────────────

    private static (int Width, int Height) ReadImageDimensions(string? notesJson)
    {
        if (!string.IsNullOrWhiteSpace(notesJson))
        {
            try
            {
                var notes = JsonSerializer.Deserialize<CephNotesData>(notesJson);
                if (notes is not null && notes.ImageWidth > 0 && notes.ImageHeight > 0)
                    return (notes.ImageWidth, notes.ImageHeight);
            }
            catch
            {
                // Plain-text notes from records created before the JSON format.
            }
        }

        // Same default box the frontend canvas uses when dimensions are unknown.
        return (800, 600);
    }

    private static string PatientName(CephAnalysis analysis)
    {
        var p = analysis.OrthoCase?.Patient;
        if (p is null) return "غير محدد";
        var name = $"{p.FirstName} {p.LastName}".Trim();
        return name.Length > 0 ? name : "غير محدد";
    }

    /// <summary>
    /// Resolves the clinic logo file — same convention as the receipt/invoice
    /// PDFs (Fonts/logo.png next to the app). Returns null when absent so the
    /// report falls back to the clinic name text with no broken image.
    /// </summary>
    private static string? ResolveLogoPath()
    {
        var paths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Fonts", "logo.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "Fonts", "logo.png"),
        };
        foreach (var path in paths)
            if (File.Exists(path)) return path;
        return null;
    }

    private static string AnalysisTypeArOf(string? analysisType) =>
        !string.IsNullOrWhiteSpace(analysisType) && AnalysisTypeAr.TryGetValue(analysisType, out var ar)
            ? ar
            : analysisType ?? "غير محدد";

    public static string FormatArabicDate(DateOnly date) =>
        $"{date.Day} {ArabicMonths[date.Month - 1]} {date.Year}";

    private static string SkeletalClassAr(string skeletalClass) => skeletalClass switch
    {
        "Class I"   => "الصنف الأول الهيكلي (Class I)",
        "Class II"  => "الصنف الثاني الهيكلي (Class II)",
        "Class III" => "الصنف الثالث الهيكلي (Class III)",
        _           => skeletalClass,
    };

    private static string VerticalPatternAr(string verticalPattern) => verticalPattern switch
    {
        "Normodivergent" => "نمط رأسي طبيعي",
        "Hyperdivergent" => "نمط رأسي مرتفع (وجه طويل)",
        "Hypodivergent"  => "نمط رأسي منخفض (وجه قصير)",
        _                => verticalPattern,
    };

    private static (string Label, string Background) ClassificationDisplay(string? classification) =>
        classification?.ToLowerInvariant() switch
        {
            "normal" => ("طبيعي",        "#DCFCE7"), // green tint
            "mild"   => ("انحراف بسيط", "#FEF9C3"), // yellow tint
            "severe" => ("انحراف شديد", "#FEE2E2"), // red tint
            _        => ("—",            "#FFFFFF"),
        };

    /// <summary>
    /// Returns the configured Arabic interpretation (InterpretationBelow/Above)
    /// when the measured value falls outside the norm's normal range, else null.
    /// </summary>
    public static string? GetOutOfRangeInterpretation(CephMeasurement m, CephNorm? norm)
    {
        if (norm is null || !m.MeasurementValue.HasValue) return null;

        var value = m.MeasurementValue.Value;
        var min = norm.MinNormal ?? norm.NormalValue - norm.StdDeviation;
        var max = norm.MaxNormal ?? norm.NormalValue + norm.StdDeviation;

        if (value < min && !string.IsNullOrWhiteSpace(norm.InterpretationBelow))
            return norm.InterpretationBelow;
        if (value > max && !string.IsNullOrWhiteSpace(norm.InterpretationAbove))
            return norm.InterpretationAbove;
        return null;
    }

    private static IContainer CellStyle(IContainer container) =>
        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).PaddingHorizontal(5);

    private static IContainer HeaderCellStyle(IContainer container) =>
        container.Background(Colors.Grey.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
            .PaddingVertical(3).PaddingHorizontal(5);

    // Mirrors CephService.CephNotesData (private there) — same JSON shape.
    private sealed class CephNotesData
    {
        public double PixelsPerMm { get; set; } = 1.0;
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public string? UserNotes { get; set; }
    }
}
