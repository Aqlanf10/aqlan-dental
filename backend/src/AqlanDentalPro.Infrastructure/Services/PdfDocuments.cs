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
    private string L(string ar, string en) => Identity.PrintsEnglish ? en : ar;

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

            // Note for anyone tempted again: column.Item().Extend() here swallows the
            // remaining height and the signature row vanishes from the rendered page. The
            // 43 mm of white space below the signatures is the footer being anchored to the
            // bottom of a fixed-height slip; leave it rather than break the signatures.

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

            // Signatures
            column.Item().Row(row =>
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
    private const string FontName = PdfService.ArabicFontName;

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"كشف حساب - {Patient.FirstName} {Patient.LastName}",
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

            column.Item().AlignCenter().Text("كشف الحساب المالي للمريض")
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
                row.RelativeItem().Text($"اسم المريض: {Patient.FirstName} {Patient.LastName}").Bold().FontSize(10);
                row.RelativeItem().Text($"رقم الملف: {Patient.PatientNumber}");
                row.RelativeItem().Text($"هاتف: {Patient.Phone ?? "غير متوفر"}");
            });

            column.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

            // Cost summaries
            column.Item().Element(ComposeSummary);

            // History table
            column.Item().Text("سجل الحركات والدفعات").Bold().FontSize(11).FontColor("#1a3a5c");
            column.Item().Element(ComposePaymentsTable);

            // Signature block
            column.Item().Row(row =>
            {
                row.RelativeItem().AlignCenter().Column(col =>
                {
                    col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                    col.Item().Text("المحاسب المالي").FontSize(8).FontColor(Colors.Grey.Darken1);
                });
                row.ConstantItem(60);
                row.RelativeItem().AlignCenter().Column(col =>
                {
                    col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                    col.Item().Text("ختم الإدارة المالي").FontSize(8).FontColor(Colors.Grey.Darken1);
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

            table.Cell().Text("إجمالي تكلفة الخطة العلاجية المقررة:").SemiBold();
            table.Cell().AlignLeft().Text($"{totalCost:N0} ر.ي").SemiBold();

            table.Cell().Text("إجمالي المبالغ المدفوعة والمقبوضة:").SemiBold().FontColor(Colors.Green.Darken3);
            table.Cell().AlignLeft().Text($"{totalPaid:N0} ر.ي").SemiBold().FontColor(Colors.Green.Darken3);

            table.Cell().Text("الرصيد المتبقي المستحق:").Bold();
            table.Cell().AlignLeft().Text($"{remaining:N0} ر.ي").Bold().FontColor(remaining > 0 ? Colors.Red.Darken2 : Colors.Green.Darken3);
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
                header.Cell().Text("التاريخ").SemiBold().FontSize(8);
                header.Cell().Text("رقم السند").SemiBold().FontSize(8);
                header.Cell().Text("طريقة الدفع").SemiBold().FontSize(8);
                header.Cell().Text("البيان / الخدمة").SemiBold().FontSize(8);
                header.Cell().AlignLeft().Text("المبلغ المقبوض").SemiBold().FontSize(8);
            });

            foreach (var payment in Payments)
            {
                table.Cell().Text(payment.PaymentDate.ToString("yyyy-MM-dd")).FontSize(8);
                table.Cell().Text(payment.ReceiptNumber ?? "-").FontSize(8);
                table.Cell().Text(payment.PaymentMethod == "cash" ? "نقداً" : payment.PaymentMethod == "card" ? "بطاقة" : "تحويل").FontSize(8);
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
                    col.Item().Text($"{Identity.Name} — كشف الحساب المالي").FontSize(7).FontColor(Colors.Grey.Darken1);
                    col.Item().Text("شكراً لثقتكم بنا").FontSize(7).FontColor(Colors.Grey.Darken1);
                });
                row.ConstantItem(120).Text($"طبع في: {DateTime.UtcNow:yyyy-MM-dd HH:mm}").FontSize(6).FontColor(Colors.Grey.Lighten1);
            });
        });
    }
}

/// <summary>
/// QuestPDF document for printing invoices with Arabic/RTL support.
/// </summary>
public class InvoiceDocument(Invoice Invoice, FinanceClinicIdentity Identity) : IDocument
{
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

            column.Item().AlignCenter().Text("فاتورة")
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
                    col.Item().Text($"المريض: {Invoice.Patient?.FirstName} {Invoice.Patient?.MiddleName} {Invoice.Patient?.LastName}")
                        .SemiBold().FontFamily(FontName);
                    col.Item().Text($"رقم المريض: {Invoice.Patient?.PatientNumber}")
                        .FontSize(9).FontFamily(FontName).FontColor(Colors.Grey.Darken1);
                });
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text($"التاريخ: {Invoice.CreatedAt:yyyy-MM-dd}")
                        .FontFamily(FontName);
                    var statusArabic = Invoice.Status switch
                    {
                        InvoiceStatus.Draft => "مسودة",
                        InvoiceStatus.Issued => "مصدرة",
                        InvoiceStatus.Cancelled => "ملغاة",
                        InvoiceStatus.Paid => "مدفوعة",
                        _ => Invoice.Status.ToString()
                    };
                    col.Item().Text($"الحالة: {statusArabic}")
                        .FontFamily(FontName).SemiBold();
                });
            });

            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

            // Line items table
            column.Item().Text("بنود الفاتورة").SemiBold().FontSize(12).FontFamily(FontName);
            column.Item().Element(ComposeLineItemsTable);

            // Totals
            column.Item().Element(ComposeTotals);

            // Notes
            if (!string.IsNullOrEmpty(Invoice.Notes))
            {
                column.Item().Text($"ملاحظات: {Invoice.Notes}")
                    .FontSize(9).FontFamily(FontName).FontColor(Colors.Grey.Darken1);
            }

            // Signature area
            column.Item().Row(row =>
            {
                row.RelativeItem().AlignCenter().Column(col =>
                {
                    col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                    col.Item().Text("المستلم").FontSize(9).FontFamily(FontName).FontColor(Colors.Grey.Darken1);
                });
                row.ConstantItem(60);
                row.RelativeItem().AlignCenter().Column(col =>
                {
                    col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                    col.Item().Text("المحاسب/الإدارة").FontSize(9).FontFamily(FontName).FontColor(Colors.Grey.Darken1);
                });
                row.ConstantItem(60);
                row.RelativeItem().AlignCenter().Column(col =>
                {
                    col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                    col.Item().Text("ختم المركز").FontSize(9).FontFamily(FontName).FontColor(Colors.Grey.Darken1);
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
                header.Cell().Text("الخدمة").SemiBold().FontSize(9).FontFamily(FontName);
                header.Cell().Text("الوصف").SemiBold().FontSize(9).FontFamily(FontName);
                header.Cell().Text("الكمية").SemiBold().FontSize(9).FontFamily(FontName);
                header.Cell().AlignLeft().Text("سعر الوحدة").SemiBold().FontSize(9).FontFamily(FontName);
                header.Cell().AlignLeft().Text("الإجمالي").SemiBold().FontSize(9).FontFamily(FontName);
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
                row.ConstantItem(120).Text("المجموع الفرعي:").FontFamily(FontName);
                row.ConstantItem(100).AlignLeft().Text($"{Invoice.Subtotal:N0} ر.ي").FontFamily(FontName);
            });

            if (Invoice.DiscountAmount.HasValue && Invoice.DiscountAmount > 0)
            {
                column.Item().Row(row =>
                {
                    row.ConstantItem(120).Text("الخصم:").FontFamily(FontName);
                    row.ConstantItem(100).AlignLeft().Text($"{Invoice.DiscountAmount:N0} ر.ي").FontFamily(FontName).FontColor(Colors.Red.Darken1);
                });
            }

            if (Invoice.TaxAmount.HasValue && Invoice.TaxAmount > 0)
            {
                column.Item().Row(row =>
                {
                    row.ConstantItem(120).Text("الضريبة:").FontFamily(FontName);
                    row.ConstantItem(100).AlignLeft().Text($"{Invoice.TaxAmount:N0} ر.ي").FontFamily(FontName);
                });
            }

            column.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);

            column.Item().Row(row =>
            {
                row.ConstantItem(120).Text("الإجمالي:").Bold().FontFamily(FontName).FontSize(12);
                row.ConstantItem(100).AlignLeft().Text($"{Invoice.TotalAmount:N0} ر.ي").Bold().FontFamily(FontName).FontSize(12).FontColor(Colors.Blue.Darken2);
            });

            if (Invoice.Status == InvoiceStatus.Issued || Invoice.Status == InvoiceStatus.Paid)
            {
                column.Item().Row(row =>
                {
                    row.ConstantItem(120).Text("المدفوع:").FontFamily(FontName);
                    row.ConstantItem(100).AlignLeft().Text($"{paidAmount:N0} ر.ي").FontFamily(FontName).FontColor(Colors.Green.Darken2);
                });
                column.Item().Row(row =>
                {
                    row.ConstantItem(120).Text("المتبقي:").FontFamily(FontName).SemiBold();
                    row.ConstantItem(100).AlignLeft().Text($"{remaining:N0} ر.ي").FontFamily(FontName).SemiBold()
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
                    col.Item().Text($"{Identity.Name} — فاتورة").FontSize(7).FontFamily(FontName).FontColor(Colors.Grey.Darken1);
                    col.Item().Text("شكراً لثقتكم بنا").FontSize(7).FontFamily(FontName).FontColor(Colors.Grey.Darken1);
                });
                row.ConstantItem(120).Text($"طبعت: {DateTime.UtcNow:yyyy-MM-dd HH:mm}").FontSize(7).FontColor(Colors.Grey.Lighten1);
            });
        });
    }
}
