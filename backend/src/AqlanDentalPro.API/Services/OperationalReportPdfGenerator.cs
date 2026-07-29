using System.Globalization;
using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Infrastructure.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AqlanDentalPro.API.Services;

/// <summary>
/// Generates printable Arabic PDF documents for all detailed operational reports.
/// The PDF consumes the same report model as the JSON and CSV endpoints so filters,
/// branch scoping, totals, and currencies cannot drift between export formats.
/// </summary>
public static class OperationalReportPdfGenerator
{
    private const string FontName = PdfService.ArabicFontName;
    private const string PrimaryColor = "#174A73";
    private const string AccentColor = "#E8F2F8";
    private static readonly CultureInfo NumberCulture = CultureInfo.InvariantCulture;

    public static byte[] Generate(OperationalReportData report, FinanceClinicIdentity identity)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        PdfService.EnsureFontsRegistered();

        var generatedAt = ClinicTimeProvider.ClinicNow();
        var tableFontSize = report.Columns.Count switch
        {
            <= 8 => 8.5f,
            <= 12 => 7.25f,
            _ => 6.25f
        };

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.MarginHorizontal(0.7f, Unit.Centimetre);
                page.MarginVertical(0.6f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.ContentFromRightToLeft();
                page.DefaultTextStyle(style => style.FontFamily(FontName).FontSize(9));

                page.Header().Element(header => ComposeHeader(header, report, identity));
                page.Content().Element(content => ComposeContent(content, report, tableFontSize));
                page.Footer().Element(footer => ComposeFooter(footer, identity, generatedAt));
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeHeader(
        IContainer container,
        OperationalReportData report,
        FinanceClinicIdentity identity)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem(4).Row(identityRow =>
                {
                    if (PdfLogoCache.TryGetLogo(out var logoBytes))
                    {
                        identityRow.ConstantItem(52)
                            .PaddingLeft(7)
                            .AlignTop()
                            .Image(logoBytes);
                    }

                    identityRow.RelativeItem().Column(details =>
                    {
                        details.Item().Text(identity.Name)
                            .Bold()
                            .FontSize(13)
                            .FontColor(PrimaryColor);
                        details.Item().Text(identity.ContactLine)
                            .FontSize(7.5f)
                            .FontColor(Colors.Grey.Darken1);
                    });
                });

                row.RelativeItem(3).AlignLeft().Column(details =>
                {
                    details.Item().AlignLeft().Text(report.Title)
                        .Bold()
                        .FontSize(15)
                        .FontColor(PrimaryColor);
                    details.Item().AlignLeft()
                        .Text($"الفترة: {report.FromDate:yyyy-MM-dd} إلى {report.ToDate:yyyy-MM-dd}")
                        .FontSize(9);
                    details.Item().AlignLeft()
                        .Text($"عدد السجلات: {report.Rows.Count.ToString("#,##0", NumberCulture)}")
                        .FontSize(8)
                        .FontColor(Colors.Grey.Darken1);
                });
            });

            column.Item().PaddingTop(5).LineHorizontal(1.2f).LineColor(PrimaryColor);
        });
    }

    private static void ComposeContent(
        IContainer container,
        OperationalReportData report,
        float tableFontSize)
    {
        container.PaddingTop(7).Column(column =>
        {
            column.Spacing(7);

            if (report.Summary.Count > 0)
                column.Item().Element(summary => ComposeSummary(summary, report.Summary));

            if (report.Rows.Count == 0)
            {
                column.Item()
                    .PaddingTop(40)
                    .AlignCenter()
                    .Border(1)
                    .BorderColor(Colors.Grey.Lighten2)
                    .Background(Colors.Grey.Lighten4)
                    .PaddingVertical(18)
                    .PaddingHorizontal(35)
                    .Text("لا توجد نتائج مطابقة للفترة المحددة.")
                    .Bold()
                    .FontSize(12)
                    .FontColor(Colors.Grey.Darken1);
                return;
            }

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    foreach (var reportColumn in report.Columns)
                        columns.RelativeColumn(ColumnWeight(reportColumn));
                });

                table.Header(header =>
                {
                    foreach (var reportColumn in report.Columns)
                    {
                        header.Cell()
                            .Element(HeaderCell)
                            .AlignMiddle()
                            .Text(reportColumn.Label)
                            .Bold()
                            .FontSize(tableFontSize)
                            .FontColor(Colors.White);
                    }
                });

                for (var rowIndex = 0; rowIndex < report.Rows.Count; rowIndex++)
                {
                    var row = report.Rows[rowIndex];
                    var shaded = rowIndex % 2 == 1;
                    foreach (var reportColumn in report.Columns)
                    {
                        var value = FormatValue(row.GetValueOrDefault(reportColumn.Key), reportColumn);
                        table.Cell()
                            .Element(cell => BodyCell(cell, shaded))
                            .AlignMiddle()
                            .Text(value)
                            .FontSize(tableFontSize);
                    }
                }
            });
        });
    }

    private static void ComposeSummary(
        IContainer container,
        IReadOnlyList<ReportSummary> summaries)
    {
        container.Row(row =>
        {
            foreach (var summary in summaries)
            {
                row.RelativeItem()
                    .PaddingHorizontal(3)
                    .Border(1)
                    .BorderColor("#C9DCE8")
                    .Background(AccentColor)
                    .PaddingVertical(5)
                    .PaddingHorizontal(8)
                    .Column(card =>
                    {
                        card.Item().Text(summary.Label)
                            .FontSize(7.5f)
                            .FontColor(Colors.Grey.Darken1);
                        card.Item().Text(FormatSummaryValue(summary))
                            .Bold()
                            .FontSize(11)
                            .FontColor(PrimaryColor);
                    });
            }
        });
    }

    private static void ComposeFooter(
        IContainer container,
        FinanceClinicIdentity identity,
        DateTime generatedAt)
    {
        container.PaddingTop(4).BorderTop(1).BorderColor(Colors.Grey.Lighten2).Row(row =>
        {
            row.RelativeItem().Text(identity.ContactLine)
                .FontSize(7)
                .FontColor(Colors.Grey.Darken1);

            row.RelativeItem().AlignCenter().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(7).FontColor(Colors.Grey.Medium));
                text.Span("صفحة ");
                text.CurrentPageNumber();
                text.Span(" من ");
                text.TotalPages();
            });

            row.RelativeItem().AlignLeft()
                .Text($"تاريخ الإنشاء: {generatedAt:yyyy-MM-dd HH:mm}")
                .FontSize(7)
                .FontColor(Colors.Grey.Darken1);
        });
    }

    private static IContainer HeaderCell(IContainer container) =>
        container
            .Background(PrimaryColor)
            .Border(0.5f)
            .BorderColor(Colors.White)
            .PaddingVertical(4)
            .PaddingHorizontal(3)
            .AlignCenter();

    private static IContainer BodyCell(IContainer container, bool shaded)
    {
        var cell = container
            .BorderBottom(0.5f)
            .BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(3)
            .PaddingHorizontal(2)
            .AlignCenter();
        return shaded ? cell.Background("#F7FAFC") : cell;
    }

    private static float ColumnWeight(ReportColumn column)
    {
        if (column.Key is "patientName" or "doctorName") return 1.55f;
        if (column.Key is "diagnosis" or "treatmentDone" or "serviceDescription"
            or "previousTreatment" or "currentTreatment" or "nextStep" or "currentStage")
            return 1.9f;
        if (column.Key is "phone") return 1.15f;

        return column.Kind switch
        {
            "currency" => 0.75f,
            "number" => 0.8f,
            "percent" => 0.85f,
            "date" => 1.05f,
            "money" => 1.05f,
            _ => 1.15f
        };
    }

    private static string FormatSummaryValue(ReportSummary summary)
    {
        var value = FormatNumberLike(summary.Value);
        return string.IsNullOrWhiteSpace(summary.Currency)
            ? value
            : $"{value} {summary.Currency}";
    }

    private static string FormatValue(object? value, ReportColumn column)
    {
        if (value is null || string.IsNullOrWhiteSpace(value.ToString()))
            return "—";

        if (column.Kind == "boolean" && value is bool boolean)
            return boolean ? "نعم" : "لا";

        if (value is DateOnly dateOnly)
            return dateOnly.ToString("yyyy-MM-dd", NumberCulture);
        if (value is DateTime dateTime)
            return dateTime.ToString("yyyy-MM-dd HH:mm", NumberCulture);

        var formatted = column.Kind is "money" or "number" or "percent"
            ? FormatNumberLike(value)
            : value.ToString() ?? "—";

        return column.Kind == "percent" ? $"{formatted}%" : formatted;
    }

    private static string FormatNumberLike(object value) => value switch
    {
        decimal number => number.ToString("#,##0.##", NumberCulture),
        double number => number.ToString("#,##0.##", NumberCulture),
        float number => number.ToString("#,##0.##", NumberCulture),
        long number => number.ToString("#,##0", NumberCulture),
        int number => number.ToString("#,##0", NumberCulture),
        short number => number.ToString("#,##0", NumberCulture),
        _ => value.ToString() ?? "—"
    };
}
