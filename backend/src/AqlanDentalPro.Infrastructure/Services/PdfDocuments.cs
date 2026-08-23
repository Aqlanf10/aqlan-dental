using System;
using System.IO;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AqlanDentalPro.Infrastructure.Services;

public class PaymentReceiptDocument(
    AqlanDentalPro.Domain.Entities.Payment Payment,
    FinanceClinicIdentity Identity,
    decimal? OutstandingBalance = null) : IDocument
{
    private const string FontName = PdfService.ArabicFontName;

    /// <summary>
    /// Currency marker for the receipt amount, in the language the receipt prints in.
    /// An Arabic «ر.ي» on an otherwise fully English receipt is the last thing the reader
    /// cannot parse, so English documents use the ISO code instead.
    /// </summary>
    private string CurrencySymbol(string? currency) => currency switch
    {
        "SAR" => L("ر.س", "SAR"),
        "USD" => "$",
        _ => L("ر.ي", "YER")
    };

    /// <summary>
    /// CORE-REQ-006 — a label in the language this document prints in.
    ///
    /// <para>
    /// The identity already switches language; leaving the body Arabic produced an English
    /// letterhead over an Arabic receipt, which reads worse to the patient than either
    /// language on its own. Only fixed chrome is translated — the patient's name, the service
    /// description and the doctor's name are data and print exactly as entered.
    /// </para>
    /// </summary>
    private string L(string ar, string en) => Identity.T(ar, en);

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"{L("سند قبض", "Payment Receipt")} {Payment.ReceiptNumber}",
        Author = "Aqlan Dental Pro",
        Subject = "Payment Receipt"
    };

    public void Compose(IDocumentContainer container)
    {
        container
            .Page(page =>
            {
                page.Size(105, 148, Unit.Millimetre);
                page.Margin(0.5f, Unit.Centimetre);
                page.PageColor(Colors.White);
                // The page direction has to follow the print language too: an English receipt
                // laid out right-to-left puts the labels on the wrong side of their values.
                if (!Identity.PrintsEnglish)
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

            // Header row: center name on right, logo on left
            column.Item().Row(row =>
            {
                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text(Identity.Name)
                        .Bold().FontSize(9).FontColor("#1a3a5c");
                    // FIN-SETTINGS: lead-doctor block is gated on both clinic.lead_doctor
                    // being configured AND finance.receipt.show_lead_doctor=true (default).
                    if (Identity.ShouldRenderLeadDoctor)
                        col.Item().Text(Identity.HasLeadDoctorTitle
                                ? $"{Identity.LeadDoctor} — {Identity.LeadDoctorTitle}"
                                : Identity.LeadDoctor)
                            .FontSize(7).FontColor(Colors.Grey.Darken1);
                });

                // CORE-REQ-006: the logo the clinic configured, resolved with the rest of the
                // identity. CLIN-12's no-per-render-I/O property is preserved by caching in
                // PdfLogoCache against the setting value.
                var receiptLogoBytes = Identity.LogoBytes;
                if (receiptLogoBytes is { Length: > 0 })
                {
                    row.ConstantItem(45).AlignLeft().Image(receiptLogoBytes);
                }
            });

            column.Item().LineHorizontal(0.5f).LineColor("#1a3a5c");

            column.Item().AlignCenter().Text(L("سند قبض", "Payment Receipt"))
                .Bold().FontSize(12).FontColor("#f5922e");
            column.Item().AlignCenter().Text($"{L("رقم السند", "Receipt no.")}: {Payment.ReceiptNumber ?? "-"}")
                .FontSize(7).FontColor(Colors.Grey.Darken1);
            column.Item().AlignCenter().Text($"{L("التاريخ", "Date")}: {Payment.PaymentDate:yyyy-MM-dd}")
                .FontSize(7).FontColor(Colors.Grey.Darken1);

            column.Item().LineHorizontal(0.5f).LineColor("#f5922e");
        });
    }

    void ComposeContent(IContainer container)
    {
        container.PaddingVertical(0.2f, Unit.Centimetre).Column(column =>
        {
            column.Spacing(3);

            // Patient info
            column.Item().Row(row =>
            {
                row.RelativeItem().Text(L("اسم المريض:", "Patient name:")).SemiBold().FontSize(8).FontColor("#1a3a5c");
                row.RelativeItem().Text($"{Payment.Patient?.FirstName} {Payment.Patient?.MiddleName} {Payment.Patient?.LastName}").FontSize(8);
            });

            if (!string.IsNullOrEmpty(Payment.Patient?.PatientNumber))
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text(L("رقم الملف:", "File no.:")).SemiBold().FontSize(8).FontColor("#1a3a5c");
                    row.RelativeItem().Text(Payment.Patient.PatientNumber).FontSize(8);
                });
            }

            column.Item().Row(row =>
            {
                row.RelativeItem().Text(L("الطبيب المعالج:", "Treating doctor:")).SemiBold().FontSize(8).FontColor("#1a3a5c");
                row.RelativeItem().Text(Payment.Doctor?.Name ?? L("غير محدد", "Not specified")).FontSize(8);
            });

            // Payment method
            var methodLabel = Payment.PaymentMethod?.ToLower() switch
            {
                "cash" => L("نقداً", "Cash"),
                "card" => L("بطاقة", "Card"),
                "banktransfer" => L("تحويل بنكي", "Bank transfer"),
                "mobilewallet" => L("محفظة إلكترونية", "Mobile wallet"),
                _ => Payment.PaymentMethod ?? "—"
            };
            column.Item().Row(row =>
            {
                row.RelativeItem().Text(L("طريقة الدفع:", "Payment method:")).SemiBold().FontSize(8).FontColor("#1a3a5c");
                row.RelativeItem().Text(methodLabel).FontSize(8);
            });

            // Service description
            column.Item().Text($"{L("البيان", "Description")}: {Payment.ServiceDescription ?? L("دفعة مالية", "Payment")}").FontSize(8).FontColor(Colors.Grey.Darken2);

            // Amount Box
            column.Item().AlignCenter().Background("#eef3f9").Border(1).BorderColor("#1a3a5c").Padding(6).Column(box =>
            {
                box.Item().Text(L("المبلغ المقبوض", "Amount received")).AlignCenter().FontSize(7).FontColor(Colors.Grey.Darken2);
                box.Item().Text($"{Payment.Amount:N0} {CurrencySymbol(Payment.Currency)}").AlignCenter().Bold().FontSize(16).FontColor("#1a3a5c");
            });

            // Notes
            if (!string.IsNullOrEmpty(Payment.Notes))
            {
                column.Item().Text($"{L("ملاحظات", "Notes")}: {Payment.Notes}").FontSize(7).FontColor(Colors.Grey.Darken1);
            }

            // The balance still owed. The browser-printed receipt in the patient file has
            // always shown this and it is usually the first thing the patient asks; the PDF
            // did not. Omitted rather than printed as zero when it could not be computed, so
            // a blank is never mistaken for "nothing due".
            if (OutstandingBalance is { } balance)
            {
                column.Item().PaddingTop(2).Row(row =>
                {
                    row.RelativeItem().Text(L("الرصيد المتبقي:", "Balance due:"))
                        .SemiBold().FontSize(8).FontColor("#1a3a5c");
                    row.RelativeItem().AlignLeft()
                        .Text($"{balance:N0} {CurrencySymbol(Payment.Currency)}")
                        .SemiBold().FontSize(8)
                        .FontColor(balance > 0 ? "#92400e" : "#166534");
                });
            }

            // Signatures, held at the bottom of the content area.
            //
            // The slip prints on fixed-height quarter-A4 stock with the footer anchored to the
            // bottom, so this row used to stop ~43 mm short of it: a third of the slip blank
            // and the footer stranded under the gap. Extending this item and aligning its
            // content to the bottom moves that space above the lines, which is where someone
            // signs anyway.
            //
            // A bare `column.Item().Extend()` spacer does NOT work: an extending item with no
            // child swallows the remaining height and the signature row disappears from the
            // rendered page, with a clean build and no failing test. The extending item has to
            // carry the row, as it does here. Both variants were checked by rasterising the
            // page and looking at it — text extraction cannot read this font's shaped Arabic
            // and reports every one of these labels as missing either way.
            column.Item().Extend().AlignBottom().Row(row =>
            {
                row.RelativeItem().AlignCenter().Column(col =>
                {
                    col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                    col.Item().Text(L("المستلم / الصندوق", "Received by / Cashier")).FontSize(7).FontColor(Colors.Grey.Darken1);
                });
                row.ConstantItem(20);
                row.RelativeItem().AlignCenter().Column(col =>
                {
                    col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                    col.Item().Text(L("توقيع المريض", "Patient signature")).FontSize(7).FontColor(Colors.Grey.Darken1);
                });
                row.ConstantItem(20);
                row.RelativeItem().AlignCenter().Column(col =>
                {
                    col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                    col.Item().Text(L("ختم المركز", "Clinic stamp")).FontSize(7).FontColor(Colors.Grey.Darken1);
                });
            });
        });
    }

    void ComposeFooter(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().LineHorizontal(0.5f).LineColor("#f5922e");
            column.Item().PaddingTop(3).AlignCenter().Text(Identity.ContactLine)
                .FontSize(6).FontColor("#1a3a5c");
            // FIN-SETTINGS: when finance.receipt.footer_text is configured, print it
            // instead of the legacy hard-coded thank-you line. Empty → legacy default
            // (preserves the prior behavior until the owner customizes it).
            var footerLine = !string.IsNullOrWhiteSpace(Identity.ReceiptFooterText)
                ? Identity.ReceiptFooterText.Trim()
                : L("شكراً لثقتكم بنا — نتمنى لكم دوام الصحة والعافية",
              "Thank you for your trust — we wish you continued good health");
            column.Item().AlignCenter().Text(footerLine)
                .FontSize(6).FontColor(Colors.Grey.Darken1);
        });
    }

}

public class FinancialStatementDocument(AqlanDentalPro.Domain.Entities.Patient Patient, List<AqlanDentalPro.Domain.Entities.Payment> Payments, FinanceClinicIdentity Identity) : IDocument
{
    /// <summary>A fixed label in this document's print language. See FinanceClinicIdentity.T.</summary>
    private string L(string ar, string en) => Identity.T(ar, en);

    private const string FontName = PdfService.ArabicFontName;

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"{L("كشف حساب", "Account Statement")} - {Patient.FirstName} {Patient.LastName}",
        Author = "Aqlan Dental Pro",
        Subject = "Patient Financial Statement"
    };

    public void Compose(IDocumentContainer container)
    {
        container
            .Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(Colors.White);
                // The page direction follows the print language, as on the receipt: an English
                // document laid out right-to-left puts every label on the wrong side.
                if (!Identity.PrintsEnglish)
                    page.ContentFromRightToLeft();
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily(FontName));

                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().Element(ComposeFooter);
            });
    }

    void ComposeHeader(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(4);

            column.Item().Row(row =>
            {
                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text(Identity.Name)
                        .Bold().FontSize(13).FontColor("#1a3a5c");
                    if (Identity.HasLeadDoctor)
                    {
                        col.Item().Text(Identity.LeadDoctor).Bold().FontSize(9).FontColor("#1a3a5c");
                        if (Identity.HasLeadDoctorTitle)
                            col.Item().Text(Identity.LeadDoctorTitle).FontSize(8).FontColor(Colors.Grey.Darken2);
                        if (Identity.HasLeadDoctorCredentials)
                            col.Item().Text(Identity.LeadDoctorCredentials).FontSize(7).FontColor(Colors.Grey.Darken1);
                    }
                    col.Item().Text(Identity.Location)
                        .FontSize(8).FontColor(Colors.Grey.Darken2);
                    col.Item().Text(Identity.Phones)
                        .FontSize(8).FontColor(Colors.Grey.Darken2);
                });

                // CORE-REQ-006: the configured logo, resolved with the rest of the identity.
                var statementLogoBytes = Identity.LogoBytes;
                if (statementLogoBytes is { Length: > 0 })
                {
                    row.ConstantItem(60).AlignLeft().Image(statementLogoBytes);
                }
            });

            column.Item().AlignCenter().Text(L("كشف الحساب المالي للمريض", "Patient Financial Statement"))
                .Bold().FontSize(16).FontColor("#f5922e");

            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    void ComposeContent(IContainer container)
    {
        container.PaddingVertical(0.4f, Unit.Centimetre).Column(column =>
        {
            column.Spacing(8);

            // Patient details card
            column.Item().Row(row =>
            {
                row.RelativeItem().Text($"{L("اسم المريض", "Patient name")}: {Patient.FirstName} {Patient.LastName}").Bold().FontSize(10);
                row.RelativeItem().Text($"{L("رقم الملف", "File no.")}: {Patient.PatientNumber}");
                row.RelativeItem().Text($"{L("هاتف", "Phone")}: {Patient.Phone ?? L("غير متوفر", "Not provided")}");
            });

            column.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

            // Cost summaries
            column.Item().Element(ComposeSummary);

            // History table
            column.Item().Text(L("سجل الحركات والدفعات", "Transactions and payments")).Bold().FontSize(11).FontColor("#1a3a5c");
            column.Item().Element(ComposePaymentsTable);

            // Signature block
            column.Item().Row(row =>
            {
                row.RelativeItem().AlignCenter().Column(col =>
                {
                    col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                    col.Item().Text(L("المحاسب المالي", "Accountant")).FontSize(8).FontColor(Colors.Grey.Darken1);
                });
                row.ConstantItem(60);
                row.RelativeItem().AlignCenter().Column(col =>
                {
                    col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                    col.Item().Text(L("ختم الإدارة المالي", "Finance department stamp")).FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        });
    }

    void ComposeSummary(IContainer container)
    {
        var totalCost = (Patient.Contracts ?? []).Where(c => c.Status == ContractStatus.Active).Sum(c => c.TotalAmount - c.DiscountAmount);
        var totalPaid = Payments.Sum(p => p.Amount);
        var remaining = totalCost - totalPaid;

        container.Background("#f8fafc").Padding(10).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.ConstantColumn(120);
            });

            table.Cell().Text(L("إجمالي تكلفة الخطة العلاجية المقررة:", "Total planned treatment cost:")).SemiBold();
            table.Cell().AlignLeft().Text($"{totalCost:N0} {Identity.T("ر.ي", "YER")}").SemiBold();

            table.Cell().Text(L("إجمالي المبالغ المدفوعة والمقبوضة:", "Total paid and received:")).SemiBold().FontColor(Colors.Green.Darken3);
            table.Cell().AlignLeft().Text($"{totalPaid:N0} {Identity.T("ر.ي", "YER")}").SemiBold().FontColor(Colors.Green.Darken3);

            table.Cell().Text(L("الرصيد المتبقي المستحق:", "Balance due:")).Bold();
            table.Cell().AlignLeft().Text($"{remaining:N0} {Identity.T("ر.ي", "YER")}").Bold().FontColor(remaining > 0 ? Colors.Red.Darken2 : Colors.Green.Darken3);
        });
    }

    void ComposePaymentsTable(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(70);
                columns.ConstantColumn(80);
                columns.ConstantColumn(70);
                columns.RelativeColumn();
                columns.ConstantColumn(95);
            });

            table.Header(header =>
            {
                header.Cell().Text(L("التاريخ", "Date")).SemiBold().FontSize(8);
                header.Cell().Text(L("رقم السند", "Receipt no.")).SemiBold().FontSize(8);
                header.Cell().Text(L("طريقة الدفع", "Payment method")).SemiBold().FontSize(8);
                header.Cell().Text(L("البيان / الخدمة", "Description / Service")).SemiBold().FontSize(8);
                header.Cell().AlignLeft().Text(L("المبلغ المقبوض", "Amount received")).SemiBold().FontSize(8);
            });

            foreach (var payment in Payments)
            {
                table.Cell().Text(payment.PaymentDate.ToString("yyyy-MM-dd")).FontSize(8);
                table.Cell().Text(payment.ReceiptNumber ?? "-").FontSize(8);
                table.Cell().Text(payment.PaymentMethod == "cash" ? L("نقداً", "Cash") : payment.PaymentMethod == "card" ? L("بطاقة", "Card") : L("تحويل", "Transfer")).FontSize(8);
                table.Cell().Text(payment.ServiceDescription ?? "-").FontSize(8);
                table.Cell().AlignLeft().Text($"{payment.Amount:N0}").FontSize(8);
            }
        });
    }

    void ComposeFooter(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
            column.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text($"{Identity.Name} — {L("كشف الحساب المالي", "Patient Financial Statement")}").FontSize(7).FontColor(Colors.Grey.Darken1);
                    col.Item().Text(L("شكراً لثقتكم بنا", "Thank you for your trust")).FontSize(7).FontColor(Colors.Grey.Darken1);
                });
                row.ConstantItem(120).Text($"{L("طبع في", "Printed")}: {DateTime.UtcNow:yyyy-MM-dd HH:mm}").FontSize(6).FontColor(Colors.Grey.Lighten1);
            });
        });
    }
}

/// <summary>
/// QuestPDF document for printing invoices with Arabic/RTL support.
/// </summary>
public class InvoiceDocument(Invoice Invoice, FinanceClinicIdentity Identity) : IDocument
{
    /// <summary>A fixed label in this document's print language. See FinanceClinicIdentity.T.</summary>
    private string L(string ar, string en) => Identity.T(ar, en);

    private const string FontName = PdfService.ArabicFontName;

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Invoice {Invoice.InvoiceNumber}",
        Author = "Aqlan Dental Pro",
        Subject = "Invoice"
    };

    public void Compose(IDocumentContainer container)
    {
        container
            .Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                // The page direction follows the print language, as on the receipt: an English
                // document laid out right-to-left puts every label on the wrong side.
                if (!Identity.PrintsEnglish)
                    page.ContentFromRightToLeft();
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(FontName));

                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().Element(ComposeFooter);
            });
    }

    void ComposeHeader(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(4);

            column.Item().Row(row =>
            {
                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text(Identity.Name)
                        .Bold().FontSize(14).FontFamily(FontName).FontColor("#1a3a5c");
                    if (Identity.HasLeadDoctor)
                    {
                        col.Item().Text(Identity.LeadDoctor).Bold().FontSize(9).FontFamily(FontName).FontColor("#1a3a5c");
                        if (Identity.HasLeadDoctorTitle)
                            col.Item().Text(Identity.LeadDoctorTitle).FontSize(8).FontFamily(FontName).FontColor(Colors.Grey.Darken2);
                        if (Identity.HasLeadDoctorCredentials)
                            col.Item().Text(Identity.LeadDoctorCredentials).FontSize(7).FontFamily(FontName).FontColor(Colors.Grey.Darken1);
                    }
                    col.Item().Text(Identity.Location)
                        .FontSize(8).FontFamily(FontName).FontColor(Colors.Grey.Darken2);
                    col.Item().Text(Identity.Phones)
                        .FontSize(8).FontFamily(FontName).FontColor(Colors.Grey.Darken2);
                });

                // CORE-REQ-006: the configured logo, resolved with the rest of the identity.
                var statementLogoBytes = Identity.LogoBytes;
                if (statementLogoBytes is { Length: > 0 })
                {
                    row.ConstantItem(60).AlignLeft().Image(statementLogoBytes);
                }
            });

            column.Item().AlignCenter().Text(L("فاتورة", "Invoice"))
                .Bold().FontSize(20).FontFamily(FontName).FontColor("#1a3a5c");
            column.Item().AlignCenter().Text(Invoice.InvoiceNumber)
                .FontSize(10).FontFamily(FontName).FontColor(Colors.Grey.Darken1);

            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    void ComposeContent(IContainer container)
    {
        container.PaddingVertical(0.5f, Unit.Centimetre).Column(column =>
        {
            column.Spacing(8);

            // Patient & Invoice info
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text($"{L("المريض", "Patient")}: {Invoice.Patient?.FirstName} {Invoice.Patient?.MiddleName} {Invoice.Patient?.LastName}")
                        .SemiBold().FontFamily(FontName);
                    col.Item().Text($"{L("رقم المريض", "Patient no.")}: {Invoice.Patient?.PatientNumber}")
                        .FontSize(9).FontFamily(FontName).FontColor(Colors.Grey.Darken1);
                });
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text($"{L("التاريخ", "Date")}: {Invoice.CreatedAt:yyyy-MM-dd}")
                        .FontFamily(FontName);
                    var statusArabic = Invoice.Status switch
                    {
                        InvoiceStatus.Draft => L("مسودة", "Draft"),
                        InvoiceStatus.Issued => L("مصدرة", "Issued"),
                        InvoiceStatus.Cancelled => L("ملغاة", "Cancelled"),
                        InvoiceStatus.Paid => L("مدفوعة", "Paid"),
                        _ => Invoice.Status.ToString()
                    };
                    col.Item().Text($"{L("الحالة", "Status")}: {statusArabic}")
                        .FontFamily(FontName).SemiBold();
                });
            });

            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

            // Line items table
            column.Item().Text(L("بنود الفاتورة", "Invoice items")).SemiBold().FontSize(12).FontFamily(FontName);
            column.Item().Element(ComposeLineItemsTable);

            // Totals
            column.Item().Element(ComposeTotals);

            // Notes
            if (!string.IsNullOrEmpty(Invoice.Notes))
            {
                column.Item().Text($"{L("ملاحظات", "Notes")}: {Invoice.Notes}")
                    .FontSize(9).FontFamily(FontName).FontColor(Colors.Grey.Darken1);
            }

            // Signature area
            column.Item().Row(row =>
            {
                row.RelativeItem().AlignCenter().Column(col =>
                {
                    col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                    col.Item().Text(L("المستلم", "Received by")).FontSize(9).FontFamily(FontName).FontColor(Colors.Grey.Darken1);
                });
                row.ConstantItem(60);
                row.RelativeItem().AlignCenter().Column(col =>
                {
                    col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                    col.Item().Text(L("المحاسب/الإدارة", "Accountant / Management")).FontSize(9).FontFamily(FontName).FontColor(Colors.Grey.Darken1);
                });
                row.ConstantItem(60);
                row.RelativeItem().AlignCenter().Column(col =>
                {
                    col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                    col.Item().Text(L("ختم المركز", "Clinic stamp")).FontSize(9).FontFamily(FontName).FontColor(Colors.Grey.Darken1);
                });
            });
        });
    }

    void ComposeLineItemsTable(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(40);
                columns.RelativeColumn(2);
                columns.RelativeColumn(2);
                columns.ConstantColumn(50);
                columns.ConstantColumn(80);
                columns.ConstantColumn(80);
            });

            table.Header(header =>
            {
                header.Cell().Text("#").SemiBold().FontSize(9).FontFamily(FontName);
                header.Cell().Text(L("الخدمة", "Service")).SemiBold().FontSize(9).FontFamily(FontName);
                header.Cell().Text(L("الوصف", "Description")).SemiBold().FontSize(9).FontFamily(FontName);
                header.Cell().Text(L("الكمية", "Qty")).SemiBold().FontSize(9).FontFamily(FontName);
                header.Cell().AlignLeft().Text(L("سعر الوحدة", "Unit price")).SemiBold().FontSize(9).FontFamily(FontName);
                header.Cell().AlignLeft().Text(L("الإجمالي", "Total")).SemiBold().FontSize(9).FontFamily(FontName);
            });

            var idx = 1;
            foreach (var item in Invoice.LineItems.OrderBy(l => l.SortOrder))
            {
                table.Cell().Text($"{idx++}").FontSize(9).FontFamily(FontName);
                table.Cell().Text(item.ServiceNameSnapshot ?? "—").FontSize(9).FontFamily(FontName);
                table.Cell().Text(item.Description ?? "—").FontSize(9).FontFamily(FontName);
                table.Cell().Text($"{item.Quantity}").FontSize(9).FontFamily(FontName);
                table.Cell().AlignLeft().Text($"{item.UnitPrice:N0}").FontSize(9).FontFamily(FontName);
                table.Cell().AlignLeft().Text($"{item.TotalPrice:N0}").FontSize(9).FontFamily(FontName).SemiBold();
            }
        });
    }

    void ComposeTotals(IContainer container)
    {
        var paidAmount = Invoice.Payments?.Sum(p => p.Amount) ?? 0;
        var remaining = Invoice.TotalAmount - paidAmount;

        container.AlignLeft().Column(column =>
        {
            column.Spacing(4);

            column.Item().Row(row =>
            {
                row.ConstantItem(120).Text(L("المجموع الفرعي:", "Subtotal:")).FontFamily(FontName);
                row.ConstantItem(100).AlignLeft().Text($"{Invoice.Subtotal:N0} {Identity.T("ر.ي", "YER")}").FontFamily(FontName);
            });

            if (Invoice.DiscountAmount.HasValue && Invoice.DiscountAmount > 0)
            {
                column.Item().Row(row =>
                {
                    row.ConstantItem(120).Text(L("الخصم:", "Discount:")).FontFamily(FontName);
                    row.ConstantItem(100).AlignLeft().Text($"{Invoice.DiscountAmount:N0} {Identity.T("ر.ي", "YER")}").FontFamily(FontName).FontColor(Colors.Red.Darken1);
                });
            }

            if (Invoice.TaxAmount.HasValue && Invoice.TaxAmount > 0)
            {
                column.Item().Row(row =>
                {
                    row.ConstantItem(120).Text(L("الضريبة:", "Tax:")).FontFamily(FontName);
                    row.ConstantItem(100).AlignLeft().Text($"{Invoice.TaxAmount:N0} {Identity.T("ر.ي", "YER")}").FontFamily(FontName);
                });
            }

            column.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);

            column.Item().Row(row =>
            {
                row.ConstantItem(120).Text(L("الإجمالي:", "Total:")).Bold().FontFamily(FontName).FontSize(12);
                row.ConstantItem(100).AlignLeft().Text($"{Invoice.TotalAmount:N0} {Identity.T("ر.ي", "YER")}").Bold().FontFamily(FontName).FontSize(12).FontColor(Colors.Blue.Darken2);
            });

            if (Invoice.Status == InvoiceStatus.Issued || Invoice.Status == InvoiceStatus.Paid)
            {
                column.Item().Row(row =>
                {
                    row.ConstantItem(120).Text(L("المدفوع:", "Paid:")).FontFamily(FontName);
                    row.ConstantItem(100).AlignLeft().Text($"{paidAmount:N0} {Identity.T("ر.ي", "YER")}").FontFamily(FontName).FontColor(Colors.Green.Darken2);
                });
                column.Item().Row(row =>
                {
                    row.ConstantItem(120).Text(L("المتبقي:", "Balance:")).FontFamily(FontName).SemiBold();
                    row.ConstantItem(100).AlignLeft().Text($"{remaining:N0} {Identity.T("ر.ي", "YER")}").FontFamily(FontName).SemiBold()
                        .FontColor(remaining > 0 ? Colors.Red.Darken1 : Colors.Green.Darken2);
                });
            }
        });
    }

    void ComposeFooter(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
            column.Item().PaddingTop(5).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text($"{Identity.Name} — {L("فاتورة", "Invoice")}").FontSize(7).FontFamily(FontName).FontColor(Colors.Grey.Darken1);
                    col.Item().Text(L("شكراً لثقتكم بنا", "Thank you for your trust")).FontSize(7).FontFamily(FontName).FontColor(Colors.Grey.Darken1);
                });
                row.ConstantItem(120).Text($"{L("طبعت", "Printed")}: {DateTime.UtcNow:yyyy-MM-dd HH:mm}").FontSize(7).FontColor(Colors.Grey.Lighten1);
            });
        });
    }
}
