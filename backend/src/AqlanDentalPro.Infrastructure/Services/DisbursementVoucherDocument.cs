using System;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>Data for a disbursement voucher (سند صرف) — money paid OUT.</summary>
public sealed class DisbursementVoucherModel
{
    public string VoucherNumber { get; init; } = string.Empty;
    public DateOnly Date { get; init; }
    public string CategoryLabel { get; init; } = string.Empty;
    public string PayeeName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Description { get; init; } = string.Empty;
    public string PaymentMethod { get; init; } = string.Empty;
    public string? Notes { get; init; }
}

/// <summary>
/// Payment / disbursement voucher (سند صرف) — quarter-A4 (A6, 105×148mm), Arabic
/// RTL, clinic identity from Settings (no hardcoding). The money-out counterpart
/// of the receipt voucher (سند قبض); used for operational expenses, refunds and
/// commission payouts. Distinguished by a red accent and «المبلغ المصروف».
/// </summary>
public class DisbursementVoucherDocument(DisbursementVoucherModel Model, FinanceClinicIdentity Identity) : IDocument
{
    private const string FontName = PdfService.ArabicFontName;
    private const string Accent = "#b91c1c"; // disbursement = red accent

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"سند صرف {Model.VoucherNumber}",
        Author = "Aqlan Dental Pro",
        Subject = "Disbursement Voucher",
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(105, 148, Unit.Millimetre); // ربع A4 (A6)
            page.Margin(0.5f, Unit.Centimetre);
            page.PageColor(Colors.White);
            page.ContentFromRightToLeft();
            page.DefaultTextStyle(x => x.FontSize(8).FontFamily(FontName));

            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });
    }

    void ComposeHeader(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(2);

            column.Item().Row(row =>
            {
                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text(Identity.Name).Bold().FontSize(9).FontColor("#1a3a5c");
                    if (Identity.HasLeadDoctor)
                        col.Item().Text(Identity.HasLeadDoctorTitle
                                ? $"{Identity.LeadDoctor} — {Identity.LeadDoctorTitle}"
                                : Identity.LeadDoctor)
                            .FontSize(7).FontColor(Colors.Grey.Darken1);
                });

                // CLIN-12: logo bytes are cached statically — no per-render file I/O.
                var logoBytes = PdfLogoCache.LogoBytes;
                if (logoBytes is not null)
                    row.ConstantItem(45).AlignLeft().Image(logoBytes);
            });

            column.Item().LineHorizontal(0.5f).LineColor("#1a3a5c");
            column.Item().AlignCenter().Text("سند صرف").Bold().FontSize(12).FontColor(Accent);
            column.Item().AlignCenter().Text($"رقم السند: {(string.IsNullOrWhiteSpace(Model.VoucherNumber) ? "-" : Model.VoucherNumber)}")
                .FontSize(7).FontColor(Colors.Grey.Darken1);
            column.Item().AlignCenter().Text($"التاريخ: {Model.Date:yyyy-MM-dd}")
                .FontSize(7).FontColor(Colors.Grey.Darken1);
            column.Item().LineHorizontal(0.5f).LineColor(Accent);
        });
    }

    void ComposeContent(IContainer container)
    {
        container.PaddingVertical(0.2f, Unit.Centimetre).Column(column =>
        {
            column.Spacing(3);

            Field(column, "المستفيد:", Model.PayeeName);
            if (!string.IsNullOrWhiteSpace(Model.CategoryLabel))
                Field(column, "الفئة:", Model.CategoryLabel);
            Field(column, "طريقة الصرف:", PaymentMethodLabel(Model.PaymentMethod));

            column.Item().Text($"البيان: {(string.IsNullOrWhiteSpace(Model.Description) ? "—" : Model.Description)}")
                .FontSize(8).FontColor(Colors.Grey.Darken2);

            column.Item().AlignCenter().Background("#fdecec").Border(1).BorderColor(Accent).Padding(6).Column(box =>
            {
                box.Item().Text("المبلغ المصروف").AlignCenter().FontSize(7).FontColor(Colors.Grey.Darken2);
                box.Item().Text($"{Model.Amount:N0} ر.ي").AlignCenter().Bold().FontSize(16).FontColor(Accent);
            });

            if (!string.IsNullOrWhiteSpace(Model.Notes))
                column.Item().Text($"ملاحظات: {Model.Notes}").FontSize(7).FontColor(Colors.Grey.Darken1);

            column.Item().PaddingTop(6).Row(row =>
            {
                row.RelativeItem().AlignCenter().Column(col =>
                {
                    col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                    col.Item().Text("المُستلِم").FontSize(7).FontColor(Colors.Grey.Darken1);
                });
                row.ConstantItem(20);
                row.RelativeItem().AlignCenter().Column(col =>
                {
                    col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                    col.Item().Text("المُصرِّف / الصندوق").FontSize(7).FontColor(Colors.Grey.Darken1);
                });
            });
        });
    }

    void ComposeFooter(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().LineHorizontal(0.5f).LineColor(Accent);
            column.Item().PaddingTop(3).AlignCenter().Text(Identity.ContactLine)
                .FontSize(6).FontColor("#1a3a5c");
        });
    }

    private static void Field(QuestPDF.Fluent.ColumnDescriptor column, string label, string value)
    {
        column.Item().Row(row =>
        {
            row.RelativeItem().Text(label).SemiBold().FontSize(8).FontColor("#1a3a5c");
            row.RelativeItem().Text(string.IsNullOrWhiteSpace(value) ? "—" : value).FontSize(8);
        });
    }

    private static string PaymentMethodLabel(string? method) => method?.ToLowerInvariant() switch
    {
        "cash" => "نقداً",
        "card" => "بطاقة",
        "bank_transfer" or "banktransfer" or "bank" => "تحويل بنكي",
        "mobilewallet" => "محفظة إلكترونية",
        _ => string.IsNullOrWhiteSpace(method) ? "—" : method,
    };
}
