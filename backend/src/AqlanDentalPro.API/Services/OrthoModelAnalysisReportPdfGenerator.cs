using System.Globalization;
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
/// Arabic dental model / cast analysis PDF report (QuestPDF).
/// Mirrors <see cref="CephReportPdfGenerator"/> (A4, RTL, Noto Naskh Arabic,
/// clinic identity from Settings — no hardcoding). Reads the existing
/// <see cref="ModelAnalysis"/> entity: top-level summary columns for Bolton/ALD/Pont
/// plus the full <see cref="DentalModelAnalysisResult"/> deserialized from
/// ResultDataJson for interpretations and mixed-dentition predictions.
/// No recalculation — this only renders what was already saved (PR #364).
/// </summary>
public class OrthoModelAnalysisReportPdfGenerator(AppDbContext db)
{
    private const string FontName = AqlanDentalPro.Infrastructure.Services.PdfService.ArabicFontName;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Builds the report for the latest model analysis of a case.</summary>
    public Task<byte[]> GenerateLatestAsync(Guid orthoCaseId) => GenerateInternalAsync(orthoCaseId, null);

    /// <summary>Builds the report for a specific model analysis.</summary>
    public Task<byte[]> GenerateAsync(Guid analysisId) => GenerateInternalAsync(null, analysisId);

    private async Task<byte[]> GenerateInternalAsync(Guid? orthoCaseId, Guid? analysisId)
    {
        var query = db.ModelAnalyses
            .AsNoTracking()
            .Include(m => m.OrthoCase).ThenInclude(o => o.Patient)
            .AsQueryable();

        ModelAnalysis? analysis;
        if (analysisId.HasValue)
        {
            analysis = await query.FirstOrDefaultAsync(m => m.Id == analysisId.Value);
        }
        else
        {
            analysis = await query
                .Where(m => m.OrthoCaseId == orthoCaseId!.Value)
                .OrderByDescending(m => m.AnalysisDate)
                .ThenByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync();
        }

        if (analysis is null)
            throw new ArgumentException("Model analysis not found.", nameof(analysisId));

        var identity = await CephReportPdfGenerator.ResolveClinicIdentityAsync(db);
        DentalModelAnalysisResult? result = null;
        try { result = JsonSerializer.Deserialize<DentalModelAnalysisResult>(analysis.ResultDataJson, JsonOptions); }
        catch (JsonException) { /* fall back to entity summary columns only */ }

        return Generate(analysis, result, identity);
    }

    private static byte[] Generate(ModelAnalysis analysis, DentalModelAnalysisResult? result, CephReportClinicIdentity identity)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        AqlanDentalPro.Infrastructure.Services.PdfService.EnsureFontsRegistered();

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
                page.Content().Element(c => ComposeContent(c, analysis, result, identity));
                page.Footer().Element(c => ComposeFooter(c, identity));
            });
        });

        return doc.GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, ModelAnalysis analysis, CephReportClinicIdentity identity)
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
                    col.Item().AlignLeft().Text("تقرير تحليل النماذج الدراسية").Bold().FontSize(14).FontFamily(FontName);
                    col.Item().AlignLeft().Text($"المريض: {PatientName(analysis)}").FontSize(10).FontFamily(FontName);
                    col.Item().AlignLeft().Text($"رقم الحالة: {analysis.OrthoCase?.CaseNumber ?? "غير محدد"}").FontSize(9).FontFamily(FontName);
                    col.Item().AlignLeft().Text($"تاريخ التحليل: {AnalysisDateText(analysis)}").FontSize(9).FontFamily(FontName);
                    col.Item().AlignLeft().Text($"نوع الإطباق: {DentitionStageAr(analysis.DentitionStage)}").FontSize(9).FontFamily(FontName);
                });
            });

            column.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    private static void ComposeContent(
        IContainer container, ModelAnalysis analysis, DentalModelAnalysisResult? result, CephReportClinicIdentity identity)
    {
        container.Column(column =>
        {
            column.Spacing(10);

            // ── Approval status banner ──
            if (analysis.ApprovedAt is not null)
                column.Item().Background("#DCFCE7").Border(1).BorderColor(Colors.Green.Lighten1).Padding(6)
                    .Text($"✓ تحليل معتمد — {FormatDateTime(analysis.ApprovedAt.Value)}")
                    .Bold().FontSize(9).FontColor(Colors.Green.Darken2).FontFamily(FontName);
            else
                column.Item().Background("#FEF9C3").Border(1).BorderColor(Colors.Yellow.Darken1).Padding(6)
                    .Text("تحليل غير معتمد بعد").FontSize(9).FontColor(Colors.Grey.Darken2).FontFamily(FontName);

            // ── Bolton ──
            column.Item().Text("تحليل بولتون (Bolton)").Bold().FontSize(11).FontFamily(FontName);
            if (analysis.BoltonOverall.HasValue || analysis.BoltonAnterior.HasValue)
            {
                KeyValueTable(column,
                [
                    ("النسبة الكلية (Overall)", Pct(analysis.BoltonOverall), result?.Bolton?.OverallInterpretation),
                    ("النسبة الأمامية (Anterior)", Pct(analysis.BoltonAnterior), result?.Bolton?.AnteriorInterpretation),
                    ("التفاوت الكلي", Mm(result?.Bolton?.OverallDiscrepancy), null),
                    ("التفاوت الأمامي", Mm(result?.Bolton?.AnteriorDiscrepancy), null),
                ]);
            }
            else NoData(column, "لا يوجد تحليل بولتون محسوب.");

            // ── Arch-length discrepancy (ALD) ──
            column.Item().Text("تفاوت طول القوس (ALD)").Bold().FontSize(11).FontFamily(FontName);
            KeyValueTable(column,
            [
                ("الفك العلوي", Mm(analysis.UpperAld), result?.UpperArch?.Interpretation),
                ("الفك السفلي", Mm(analysis.LowerAld), result?.LowerArch?.Interpretation),
            ]);

            // ── Pont ──
            if (analysis.PontIndex.HasValue || result?.Pont is not null)
            {
                column.Item().Text("مؤشر بونت (Pont)").Bold().FontSize(11).FontFamily(FontName);
                var pont = result?.Pont;
                KeyValueTable(column,
                [
                    ("مجموع القواطع", Mm(analysis.PontIndex ?? pont?.IncisorSum), null),
                    ("العرض الضاحكي المتوقع", Mm(pont?.PredictedInterpremolarWidth), null),
                    ("العرض الرحوي المتوقع", Mm(pont?.PredictedIntermolarWidth), null),
                    ("فرق الضواحك (مقاس − متوقع)", SignedMm(pont?.PremolarDifference), null),
                    ("فرق الأرحاء (مقاس − متوقع)", SignedMm(pont?.MolarDifference), null),
                ]);
                column.Item().Text("مؤشر بونت مرجع وصفي فقط ولا يحدد قرار التوسيع وحده.")
                    .FontSize(7.5f).FontColor(Colors.Grey.Darken1).FontFamily(FontName);
            }

            // ── Mixed dentition prediction (Moyers / Tanaka-Johnston) ──
            if (result?.Moyers is not null || result?.TanakaJohnston is not null)
            {
                column.Item().Text("تنبؤ الإطباق المختلط").Bold().FontSize(11).FontFamily(FontName);
                if (result.Moyers is not null)
                    KeyValueTable(column,
                    [
                        ($"موير (مئين {result.Moyers.Percentile}) — علوي/جهة", Mm(result.Moyers.Prediction.PredictedUpperPerSide), null),
                        ($"موير (مئين {result.Moyers.Percentile}) — سفلي/جهة", Mm(result.Moyers.Prediction.PredictedLowerPerSide), null),
                        ("تفاوت المساحة العلوي/جهة", SignedMm(result.Moyers.Prediction.UpperSpaceDiscrepancyPerSide), null),
                        ("تفاوت المساحة السفلي/جهة", SignedMm(result.Moyers.Prediction.LowerSpaceDiscrepancyPerSide), null),
                    ]);
                if (result.TanakaJohnston is not null)
                    KeyValueTable(column,
                    [
                        ("تاناكا-جونستون — علوي/جهة", Mm(result.TanakaJohnston.Prediction.PredictedUpperPerSide), null),
                        ("تاناكا-جونستون — سفلي/جهة", Mm(result.TanakaJohnston.Prediction.PredictedLowerPerSide), null),
                    ]);
            }

            // ── Warnings from the saved analysis ──
            if (result?.Warnings is { Count: > 0 })
            {
                column.Item().PaddingTop(2).Text("ملاحظات التحليل").Bold().FontSize(10).FontFamily(FontName);
                foreach (var w in result.Warnings)
                    column.Item().Text($"• {w}").FontSize(8.5f).FontColor(Colors.Grey.Darken1).FontFamily(FontName);
            }

            if (!string.IsNullOrWhiteSpace(analysis.Notes))
            {
                column.Item().PaddingTop(2).Text("ملاحظات الطبيب").Bold().FontSize(10).FontFamily(FontName);
                column.Item().Text(analysis.Notes).FontSize(9).FontFamily(FontName);
            }

            // ── Signature ──
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
        });
    }

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

    // ── Reusable 2/3-column key→value→interpretation table ──
    private static void KeyValueTable(ColumnDescriptor column, (string Label, string Value, string? Note)[] rows)
    {
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2.4f); // القياس
                columns.RelativeColumn(1.4f); // القيمة
                columns.RelativeColumn(2.6f); // التفسير
            });
            foreach (var (label, value, note) in rows)
            {
                table.Cell().Element(CellStyle).Text(label).FontFamily(FontName);
                table.Cell().Element(CellStyle).Text(value).FontFamily(FontName);
                table.Cell().Element(CellStyle).Text(string.IsNullOrWhiteSpace(note) ? "—" : note)
                    .FontSize(9).FontColor(Colors.Grey.Darken2).FontFamily(FontName);
            }
        });
    }

    private static void NoData(ColumnDescriptor column, string message) =>
        column.Item().Text(message).FontSize(9).FontColor(Colors.Grey.Darken1).FontFamily(FontName);

    private static IContainer CellStyle(IContainer container) =>
        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).PaddingHorizontal(5);

    // ── Formatting helpers ──
    private static string Pct(decimal? v) => v.HasValue ? string.Create(Inv, $"{v.Value:0.0}%") : "—";
    private static string Mm(decimal? v) => v.HasValue ? string.Create(Inv, $"{v.Value:0.0} مم") : "—";
    private static string SignedMm(decimal? v) => v.HasValue ? string.Create(Inv, $"{v.Value:+0.0;-0.0;0.0} مم") : "—";

    private static string AnalysisDateText(ModelAnalysis analysis) =>
        analysis.AnalysisDate.HasValue
            ? CephReportPdfGenerator.FormatArabicDate(analysis.AnalysisDate.Value)
            : CephReportPdfGenerator.FormatArabicDate(DateOnly.FromDateTime(analysis.CreatedAt));

    private static string FormatDateTime(DateTime dt) =>
        CephReportPdfGenerator.FormatArabicDate(DateOnly.FromDateTime(dt));

    private static string DentitionStageAr(string? stage) => stage switch
    {
        "Mixed" => "إطباق مختلط",
        "Permanent" => "إطباق دائم",
        _ => stage ?? "غير محدد",
    };

    private static string PatientName(ModelAnalysis analysis)
    {
        var p = analysis.OrthoCase?.Patient;
        if (p is null) return "غير محدد";
        var name = $"{p.FirstName} {p.LastName}".Trim();
        return name.Length > 0 ? name : "غير محدد";
    }

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
}
