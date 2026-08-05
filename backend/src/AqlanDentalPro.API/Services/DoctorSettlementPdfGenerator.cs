using AqlanDentalPro.Application.DTOs.Commission;
using AqlanDentalPro.Infrastructure.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AqlanDentalPro.API.Services;

/// <summary>
/// CORE-FIN-LAB-ADJ (follow-up) — the doctor's settlement statement.
/// <para>
/// The corrections panel could show a doctor was overpaid, but there was nothing to hand the
/// doctor. That matters here more than in most reports: a negative correction means the clinic
/// is about to pay them less than the invoice lines say, and asking someone to accept a smaller
/// envelope on the strength of a screen they cannot take away is how disputes start. This is
/// the paper that explains the number — accrued commission, every correction with the lab order
/// and invoice that caused it, what has been paid, and what remains.
/// </para>
/// </summary>
public sealed record DoctorSettlementStatement(
    DoctorSettlementSummaryDto Summary,
    IReadOnlyList<CommissionAdjustmentDto> Adjustments,
    DateOnly IssuedOn);

public class DoctorSettlementPdfGenerator(FinanceClinicIdentity identity, DoctorSettlementStatement model) : IDocument
{
    private const string FontName = PdfService.ArabicFontName;
    private const string Accent = "#0e7490";
    private const string Owed = "#15803d";     // clinic owes the doctor
    private const string Overpaid = "#b91c1c"; // doctor owes the clinic

    /// <summary>
    /// QuestPDF validates its licence BEFORE calling <see cref="Compose"/>, so setting it
    /// inside Compose is too late — the generator compiles cleanly and then throws the first
    /// time a user asks for the document. A static constructor runs before any instance
    /// exists, which is the only placement that is guaranteed to be early enough.
    /// </summary>
    static DoctorSettlementPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"كشف تسوية عمولة — {model.Summary.DoctorName}",
        Author = identity.Name,
        Subject = "Doctor Commission Settlement",
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1.4f, Unit.Centimetre);
            page.PageColor(Colors.White);
            page.ContentFromRightToLeft();
            page.DefaultTextStyle(x => x.FontSize(9).FontFamily(FontName));

            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });
    }

    private void ComposeHeader(IContainer container) => container.Column(column =>
    {
        column.Item().Text(identity.Name).FontSize(13).Bold().FontColor(Accent);

        // Owner's decision, recorded in CLAUDE.md: every report carries the lead doctor and
        // their credentials, read from Settings — never hardcoded.
        if (identity.ShouldRenderLeadDoctor)
        {
            var title = identity.HasLeadDoctorTitle
                ? $"{identity.LeadDoctor} — {identity.LeadDoctorTitle}"
                : identity.LeadDoctor;
            column.Item().Text(title).FontSize(9);

            if (identity.HasLeadDoctorCredentials)
                column.Item().Text(identity.LeadDoctorCredentials).FontSize(8).FontColor(Colors.Grey.Darken1);
        }

        column.Item().PaddingTop(6).Text("كشف تسوية عمولة الطبيب").FontSize(12).Bold();
        column.Item().PaddingBottom(4).Text($"الطبيب: {model.Summary.DoctorName ?? "—"}").FontSize(10);
        column.Item().PaddingBottom(6).LineHorizontal(1).LineColor(Accent);
    });

    private void ComposeContent(IContainer container) => container.PaddingVertical(8).Column(column =>
    {
        column.Spacing(12);

        column.Item().Element(ComposeSummary);

        column.Item().Text("بنود التسوية").FontSize(10).Bold();

        if (model.Adjustments.Count == 0)
        {
            column.Item().Text("لا توجد بنود تسوية على هذا الطبيب.")
                .FontSize(9).FontColor(Colors.Grey.Darken1);
        }
        else
        {
            column.Item().Element(ComposeAdjustmentsTable);

            // The rule the owner set, printed where it is acted on rather than left implicit.
            column.Item().PaddingTop(4).Text(
                "بند التسوية يُنشأ عندما تتغيّر التكلفة الفعلية بعد صرف العمولة. "
                + "العمولة المصروفة لا تُعدَّل، ويُسوّى الفرق في هذا الكشف.")
                .FontSize(7.5f).FontColor(Colors.Grey.Darken1);
        }
    });

    private void ComposeSummary(IContainer container) => container.Background("#f0f9ff").Padding(10).Column(column =>
    {
        column.Spacing(4);

        Row(column, "العمولة المستحقة من الفواتير", model.Summary.EarnedFromLineItems, null);
        Row(column, "إجمالي بنود التسوية", model.Summary.AdjustmentsTotal, Signed(model.Summary.AdjustmentsTotal));
        Row(column, "الإجمالي المستحق", model.Summary.TotalDue, null, bold: true);
        Row(column, "المصروف سابقًا", model.Summary.AlreadyPaid, null);

        column.Item().PaddingTop(2).LineHorizontal(0.8f).LineColor(Accent);

        // A negative remaining is the doctor owing the clinic. Labelling it plainly is the
        // whole point of handing over a statement instead of a number.
        var remaining = model.Summary.Remaining;
        var label = remaining < 0 ? "مبلغ مستردّ من الطبيب" : "المتبقي للصرف";
        Row(column, label, Math.Abs(remaining), remaining < 0 ? Overpaid : Owed, bold: true, size: 11);
    });

    private static string? Signed(decimal value) => value == 0 ? null : value < 0 ? Overpaid : Owed;

    private void Row(
        ColumnDescriptor column, string label, decimal value,
        string? colour, bool bold = false, float size = 9)
        => column.Item().Row(row =>
        {
            var name = row.RelativeItem().Text(label).FontSize(size);
            if (bold) name.Bold();

            var amount = row.AutoItem().Text($"{value:N2} {model.Summary.Currency}").FontSize(size);
            if (bold) amount.Bold();
            if (colour is not null) amount.FontColor(colour);
        });

    private void ComposeAdjustmentsTable(IContainer container) => container.Table(table =>
    {
        table.ColumnsDefinition(columns =>
        {
            columns.RelativeColumn(1.4f); // التاريخ
            columns.RelativeColumn(2.0f); // المريض
            columns.RelativeColumn(2.0f); // الخدمة
            columns.RelativeColumn(1.6f); // طلب المعمل
            columns.RelativeColumn(1.4f); // مصروفة
            columns.RelativeColumn(1.4f); // الصحيحة
            columns.RelativeColumn(1.4f); // الفرق
        });

        table.Header(header =>
        {
            foreach (var title in new[]
                     { "التاريخ", "المريض", "الخدمة", "طلب المعمل", "عمولة مصروفة", "العمولة الصحيحة", "الفرق" })
            {
                header.Cell().Background(Accent).Padding(4)
                    .Text(title).FontSize(8).Bold().FontColor(Colors.White);
            }
        });

        foreach (var a in model.Adjustments)
        {
            var cancelled = string.Equals(a.Status, "Cancelled", StringComparison.OrdinalIgnoreCase);

            Cell(table, a.CreatedAt.ToString("yyyy-MM-dd"), cancelled);
            Cell(table, a.PatientName, cancelled);
            Cell(table, a.ServiceName, cancelled);
            Cell(table, a.LabOrderNumber ?? "—", cancelled);
            Cell(table, $"{a.PaidCommissionAmount:N2} {a.Currency}", cancelled);
            Cell(table, $"{a.RecalculatedCommissionAmount:N2} {a.Currency}", cancelled);

            // A cancelled line is printed struck-through rather than dropped: the doctor may
            // have seen it on a previous statement, and silently removing it invites the
            // question this document exists to answer.
            var diff = table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4)
                .Text($"{(a.AdjustmentAmount > 0 ? "+" : "")}{a.AdjustmentAmount:N2} {a.Currency}")
                .FontSize(8).Bold();
            if (cancelled) diff.Strikethrough().FontColor(Colors.Grey.Medium);
            else diff.FontColor(a.AdjustmentAmount < 0 ? Overpaid : Owed);
        }
    });

    private static void Cell(TableDescriptor table, string text, bool cancelled)
    {
        var cell = table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4)
            .Text(text).FontSize(8);
        if (cancelled) cell.Strikethrough().FontColor(Colors.Grey.Medium);
    }

    private void ComposeFooter(IContainer container) => container.Column(column =>
    {
        column.Item().PaddingTop(6).LineHorizontal(0.8f).LineColor(Colors.Grey.Lighten1);
        column.Item().PaddingTop(3).Row(row =>
        {
            row.RelativeItem().Text(identity.ContactLine).FontSize(7).FontColor(Colors.Grey.Darken1);
            row.AutoItem().Text($"تاريخ الإصدار: {model.IssuedOn:yyyy-MM-dd}")
                .FontSize(7).FontColor(Colors.Grey.Darken1);
        });

        if (!string.IsNullOrWhiteSpace(identity.ReceiptFooterText))
            column.Item().PaddingTop(2).Text(identity.ReceiptFooterText).FontSize(7).FontColor(Colors.Grey.Darken1);

        column.Item().PaddingTop(10).Row(row =>
        {
            row.RelativeItem().Text("توقيع الطبيب: ____________________").FontSize(8);
            row.RelativeItem().AlignLeft().Text("توقيع المحاسب: ____________________").FontSize(8);
        });
    });
}
