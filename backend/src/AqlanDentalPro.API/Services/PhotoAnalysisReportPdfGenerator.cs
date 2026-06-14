using System.Globalization;
using System.Text;
using System.Text.Json;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AqlanDentalPro.API.Services;

/// <summary>
/// Arabic PDF report for a saved facial photo analysis (profile/frontal).
/// Mirrors CephReportPdfGenerator (A4, RTL, Noto Naskh Arabic, clinic identity
/// from Settings, logo with graceful fallback). The photo is shown with the
/// placed soft-tissue landmarks overlaid (SVG layer) when the image dimensions
/// can be read; otherwise the photo is shown alone — never a crash.
/// </summary>
public class PhotoAnalysisReportPdfGenerator(AppDbContext db)
{
    private const string FontName = AqlanDentalPro.Infrastructure.Services.PdfService.ArabicFontName;

    private static readonly string[] ArabicMonths =
        ["يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
         "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر"];

    private sealed class Landmark { public double x { get; set; } public double y { get; set; } }

    private sealed class MeasurementDto
    {
        public string? NameAr { get; set; }
        public double? Value { get; set; }
        public double? Normal { get; set; }
        public double? Sd { get; set; }
        public string? Severity { get; set; }
        public string? InterpretationAr { get; set; }
    }

    /// <summary>Throws <see cref="ArgumentException"/> (mapped to Arabic 404) when missing.</summary>
    public async Task<byte[]> GenerateAsync(Guid id)
    {
        var analysis = await db.PhotoAnalyses
            .Include(p => p.OrthoCase).ThenInclude(o => o.Patient)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

        if (analysis is null)
            throw new ArgumentException($"Photo analysis {id} not found.", nameof(id));

        var identity = await CephReportPdfGenerator.ResolveClinicIdentityAsync(db);
        return Generate(analysis, identity);
    }

    private static byte[] Generate(PhotoAnalysis analysis, CephReportClinicIdentity identity)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        AqlanDentalPro.Infrastructure.Services.PdfService.EnsureFontsRegistered();

        var isProfile = string.Equals(analysis.ViewType, "profile", StringComparison.OrdinalIgnoreCase);
        var measurements = DeserializeMeasurements(analysis.MeasurementsJson);
        var landmarks = DeserializeLandmarks(analysis.LandmarksJson);

        Image? photo = null;
        (int W, int H)? dims = null;
        var imagePath = CephReportPdfGenerator.ResolveUploadFilePath(analysis.ImageFileUrl);
        if (imagePath is not null)
        {
            try
            {
                var bytes = File.ReadAllBytes(imagePath);
                photo = Image.FromBinaryData(bytes);
                dims = ReadImageDimensions(bytes);
            }
            catch { photo = null; }
        }

        var overlay = (dims is not null && landmarks.Count > 0)
            ? BuildOverlaySvg(landmarks, dims.Value.W, dims.Value.H)
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

                page.Header().Element(c => ComposeHeader(c, analysis, identity, isProfile));
                page.Content().Element(c => ComposeContent(c, analysis, identity, isProfile, photo, overlay, dims, measurements));
                page.Footer().Element(c => ComposeFooter(c, identity));
            });
        });

        return doc.GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, PhotoAnalysis analysis, CephReportClinicIdentity identity, bool isProfile)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Row(idRow =>
                {
                    var logoPath = ResolveLogoPath();
                    if (logoPath is not null)
                        idRow.ConstantItem(48).PaddingLeft(8).AlignTop().Image(logoPath);

                    idRow.RelativeItem().Column(col =>
                    {
                        col.Item().Text(identity.ClinicName).Bold().FontSize(15).FontFamily(FontName);
                        col.Item().Text(identity.LeadDoctor).Bold().FontSize(11).FontFamily(FontName);
                        col.Item().Text(identity.LeadDoctorTitle).FontSize(9).FontFamily(FontName);
                        col.Item().Text(identity.LeadDoctorCredentials)
                            .FontSize(8).FontColor(Colors.Grey.Darken1).FontFamily(FontName);
                    });
                });

                row.RelativeItem().AlignLeft().Column(col =>
                {
                    col.Item().AlignLeft().Text(isProfile ? "تقرير تحليل صورة البروفايل" : "تقرير تحليل الصورة الأمامية")
                        .Bold().FontSize(14).FontFamily(FontName);
                    col.Item().AlignLeft().Text($"المريض: {PatientName(analysis)}").FontSize(10).FontFamily(FontName);
                    col.Item().AlignLeft().Text($"رقم الحالة: {analysis.OrthoCase?.CaseNumber ?? "غير محدد"}").FontSize(9).FontFamily(FontName);
                    col.Item().AlignLeft().Text($"التاريخ: {FormatArabicDate(analysis.CreatedAt)}").FontSize(9).FontFamily(FontName);
                });
            });

            column.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    private static void ComposeContent(
        IContainer container, PhotoAnalysis analysis, CephReportClinicIdentity identity,
        bool isProfile, Image? photo, string? overlay, (int W, int H)? dims, List<MeasurementDto> measurements)
    {
        container.Column(column =>
        {
            column.Spacing(10);

            column.Item().Text("الصورة والقياسات").Bold().FontSize(11).FontFamily(FontName);

            if (photo is not null)
            {
                var aspect = dims is not null && dims.Value.H > 0 ? (float)dims.Value.W / dims.Value.H : 3f / 4f;
                column.Item().AlignCenter().MaxHeight(330)
                    .AspectRatio(aspect, AspectRatioOption.FitArea).Layers(layers =>
                    {
                        layers.PrimaryLayer().Image(photo).FitUnproportionally()
                            .WithRasterDpi(150).WithCompressionQuality(ImageCompressionQuality.High);
                        if (overlay is not null) layers.Layer().Svg(overlay);
                    });
            }
            else
            {
                column.Item().Border(1).BorderColor(Colors.Grey.Lighten2)
                    .Background(Colors.Grey.Lighten4).Padding(12).AlignCenter()
                    .Text("الصورة غير متاحة").FontSize(10).FontColor(Colors.Grey.Darken1).FontFamily(FontName);
            }

            ComposeMeasurementsTable(column, measurements, isProfile);

            // Signature block
            column.Item().PaddingTop(15).Row(row =>
            {
                row.RelativeItem();
                row.ConstantItem(230).Column(col =>
                {
                    col.Item().PaddingBottom(25).LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                    col.Item().AlignCenter().Text($"{identity.LeadDoctor} — {identity.LeadDoctorTitle}")
                        .Bold().FontSize(10).FontFamily(FontName);
                    col.Item().AlignCenter().Text(identity.LeadDoctorCredentials)
                        .FontSize(8).FontColor(Colors.Grey.Darken1).FontFamily(FontName);
                    col.Item().PaddingTop(8).AlignCenter().Text("التاريخ: ‏________________").FontSize(9).FontFamily(FontName);
                });
            });

            column.Item().Text("قياسات هندسية مستقلة عن المقياس — أداة مساعدة سريرية تتطلب مراجعة الأخصائي.")
                .FontSize(8).FontColor(Colors.Grey.Darken1).FontFamily(FontName);
        });
    }

    private static void ComposeMeasurementsTable(ColumnDescriptor column, List<MeasurementDto> measurements, bool isProfile)
    {
        if (measurements.Count == 0)
        {
            column.Item().Text("لا توجد قياسات محسوبة — ضع المعالم على الصورة أولًا.")
                .FontSize(9).FontColor(Colors.Grey.Darken1).FontFamily(FontName);
            return;
        }

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3);    // القياس
                columns.RelativeColumn(1.4f);  // القيمة
                columns.RelativeColumn(1.6f);  // المعيار ±SD
                columns.RelativeColumn(1.4f);  // الحالة
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCellStyle).Text("القياس").Bold().FontFamily(FontName);
                header.Cell().Element(HeaderCellStyle).Text("القيمة").Bold().FontFamily(FontName);
                header.Cell().Element(HeaderCellStyle).Text("المعيار ±SD").Bold().FontFamily(FontName);
                header.Cell().Element(HeaderCellStyle).Text("الحالة").Bold().FontFamily(FontName);
            });

            var unit = isProfile ? "°" : "";
            foreach (var m in measurements)
            {
                table.Cell().Element(CellStyle).Column(c =>
                {
                    c.Item().Text(m.NameAr ?? "—").FontFamily(FontName);
                    if (!string.IsNullOrWhiteSpace(m.InterpretationAr) && !IsNormal(m.Severity))
                        c.Item().Text(m.InterpretationAr!).FontSize(7.5f).FontColor(Colors.Grey.Darken1).FontFamily(FontName);
                });
                table.Cell().Element(CellStyle)
                    .Text(m.Value.HasValue ? $"{m.Value.Value:0.##}{unit}" : "—").FontFamily(FontName);
                table.Cell().Element(CellStyle)
                    .Text(m.Normal.HasValue ? $"{m.Normal.Value:0.##}{unit} ± {(m.Sd ?? 0):0.##}" : "—").FontFamily(FontName);

                var (label, bg) = SeverityDisplay(m.Severity);
                table.Cell().Element(c => CellStyle(c).Background(bg)).Text(label).FontSize(9).FontFamily(FontName);
            }
        });
    }

    private static void ComposeFooter(IContainer container, CephReportClinicIdentity identity)
    {
        container.Column(column =>
        {
            var contact = string.Join(" — ",
                new[] { identity.Phones, identity.Location }.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (contact.Length > 0)
                column.Item().AlignCenter().Text(contact).FontSize(8).FontColor(Colors.Grey.Darken1).FontFamily(FontName);

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

    // ── Landmark overlay (SVG in image coordinate space) ──
    private static string BuildOverlaySvg(Dictionary<string, Landmark> landmarks, int w, int h)
    {
        var inv = CultureInfo.InvariantCulture;
        double r = Math.Max(4.0, w / 160.0);
        double fontSize = r * 2.2;
        var sb = new StringBuilder();
        sb.Append(string.Create(inv,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {w} {h}\" preserveAspectRatio=\"none\">"));
        foreach (var (key, p) in landmarks)
        {
            sb.Append(string.Create(inv,
                $"<circle cx=\"{p.x:0.##}\" cy=\"{p.y:0.##}\" r=\"{r:0.##}\" fill=\"#22D3EE\" stroke=\"#FFFFFF\" stroke-width=\"{r * 0.25:0.##}\"/>"));
            sb.Append(string.Create(inv,
                $"<text x=\"{p.x + r * 1.4:0.##}\" y=\"{p.y - r * 0.6:0.##}\" font-family=\"Arial, sans-serif\" font-size=\"{fontSize:0.##}\" font-weight=\"bold\" fill=\"#0891B2\">{System.Security.SecurityElement.Escape(key)}</text>"));
        }
        sb.Append("</svg>");
        return sb.ToString();
    }

    // ── Helpers ──
    private static List<MeasurementDto> DeserializeMeasurements(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<MeasurementDto>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        catch { return []; }
    }

    private static Dictionary<string, Landmark> DeserializeLandmarks(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, Landmark>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        catch { return []; }
    }

    /// <summary>Reads pixel dimensions from PNG/JPEG/WEBP headers; null when unknown.</summary>
    public static (int W, int H)? ReadImageDimensions(byte[] b)
    {
        try
        {
            // PNG
            if (b.Length > 24 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47)
            {
                int w = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
                int h = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
                if (w > 0 && h > 0) return (w, h);
            }
            // JPEG: scan SOF0..SOF3 markers
            if (b.Length > 4 && b[0] == 0xFF && b[1] == 0xD8)
            {
                int i = 2;
                while (i + 9 < b.Length)
                {
                    if (b[i] != 0xFF) { i++; continue; }
                    byte marker = b[i + 1];
                    if (marker is 0xD8 or 0xD9 or >= 0xD0 and <= 0xD7) { i += 2; continue; }
                    int len = (b[i + 2] << 8) | b[i + 3];
                    if (len < 2) break;
                    if (marker is >= 0xC0 and <= 0xC3 or >= 0xC5 and <= 0xC7 or >= 0xC9 and <= 0xCB or >= 0xCD and <= 0xCF)
                    {
                        int h = (b[i + 5] << 8) | b[i + 6];
                        int w = (b[i + 7] << 8) | b[i + 8];
                        if (w > 0 && h > 0) return (w, h);
                    }
                    i += 2 + len;
                }
            }
            // WEBP
            if (b.Length > 30 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F'
                && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P')
            {
                var fmt = Encoding.ASCII.GetString(b, 12, 4);
                if (fmt == "VP8X")
                {
                    int w = 1 + (b[24] | (b[25] << 8) | (b[26] << 16));
                    int h = 1 + (b[27] | (b[28] << 8) | (b[29] << 16));
                    return (w, h);
                }
                if (fmt == "VP8 ")
                {
                    int w = (b[26] | (b[27] << 8)) & 0x3FFF;
                    int h = (b[28] | (b[29] << 8)) & 0x3FFF;
                    if (w > 0 && h > 0) return (w, h);
                }
            }
        }
        catch { /* fall through */ }
        return null;
    }

    private static bool IsNormal(string? severity) =>
        string.IsNullOrWhiteSpace(severity) || severity.Equals("normal", StringComparison.OrdinalIgnoreCase);

    private static (string Label, string Background) SeverityDisplay(string? severity) =>
        severity?.ToLowerInvariant() switch
        {
            "mild" => ("انحراف بسيط", "#FEF9C3"),
            "severe" => ("انحراف شديد", "#FEE2E2"),
            _ => ("طبيعي", "#DCFCE7"),
        };

    private static string PatientName(PhotoAnalysis analysis)
    {
        var p = analysis.OrthoCase?.Patient;
        if (p is null) return "غير محدد";
        var name = $"{p.FirstName} {p.LastName}".Trim();
        return name.Length > 0 ? name : "غير محدد";
    }

    private static string FormatArabicDate(DateTime dt) =>
        $"{dt.Day} {ArabicMonths[dt.Month - 1]} {dt.Year}";

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

    private static IContainer CellStyle(IContainer container) =>
        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).PaddingHorizontal(5);

    private static IContainer HeaderCellStyle(IContainer container) =>
        container.Background(Colors.Grey.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
            .PaddingVertical(3).PaddingHorizontal(5);
}
