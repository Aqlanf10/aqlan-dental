using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AqlanDentalPro.Infrastructure.Services;

public class PaymentReceiptDocument(AqlanDentalPro.Domain.Entities.Payment Payment) : IDocument
{
    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Receipt {Payment.ReceiptNumber}",
        Author = "Aqlan Dental Pro",
        Subject = "Payment Receipt"
    };

    public void Compose(IDocumentContainer container)
    {
        container
            .Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().Element(ComposeFooter);
            });
    }

    void ComposeHeader(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("AQLAN DENTAL PRO").Bold().FontSize(16).FontColor(Colors.Blue.Darken2);
                column.Item().Text("Dental Clinic").FontSize(8).FontColor(Colors.Grey.Darken1);
            });
            row.ConstantItem(80).AlignRight().Text($"#{Payment.ReceiptNumber}").FontSize(8).FontColor(Colors.Grey.Darken1);
        });
        container.LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
    }

    void ComposeContent(IContainer container)
    {
        container.PaddingVertical(0.5f, Unit.Centimetre).Column(column =>
        {
            // Patient info
            column.Spacing(4);
            column.Item().Text($"Patient: {Payment.Patient?.FirstName} {Payment.Patient?.LastName}").SemiBold();
            column.Item().Text($"Date: {Payment.PaymentDate:yyyy-MM-dd}");
            column.Item().Text($"Payment Method: {Payment.PaymentMethod?.ToUpper()}");
            column.Item().Text($"Doctor: {Payment.Doctor?.Name ?? "N/A"}");

            column.Spacing(8);
            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
            column.Spacing(8);

            // Service description
            column.Item().Text(Payment.ServiceDescription ?? "Payment").SemiBold().FontSize(12);
            
            column.Spacing(12);

            // Amount
            column.Item().AlignCenter().Text($"{Payment.Amount:N0} YER").Bold().FontSize(20).FontColor(Colors.Blue.Darken2);

            if (!string.IsNullOrEmpty(Payment.Notes))
            {
                column.Spacing(8);
                column.Item().Text($"Notes: {Payment.Notes}").FontSize(8).FontColor(Colors.Grey.Darken1);
            }
        });
    }

    void ComposeFooter(IContainer container)
    {
        container.LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        container.PaddingTop(5).Row(row =>
        {
            row.RelativeItem().Text("Thank you for choosing Aqlan Dental Pro").FontSize(8).FontColor(Colors.Grey.Darken1);
            row.ConstantItem(80).Text($"Printed: {DateTime.Today:yyyy-MM-dd}").FontSize(7).FontColor(Colors.Grey.Lighten2);
        });
    }
}

public class FinancialStatementDocument(AqlanDentalPro.Domain.Entities.Patient Patient, List<AqlanDentalPro.Domain.Entities.Payment> Payments) : IDocument
{
    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Financial Statement - {Patient.FirstName} {Patient.LastName}",
        Author = "Aqlan Dental Pro",
        Subject = "Patient Financial Statement"
    };

    public void Compose(IDocumentContainer container)
    {
        container
            .Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().Element(ComposeFooter);
            });
    }

    void ComposeHeader(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("AQLAN DENTAL PRO").Bold().FontSize(18).FontColor(Colors.Blue.Darken2);
                column.Item().Text("Patient Financial Statement").FontSize(10).FontColor(Colors.Grey.Darken1);
            });
        });
        container.LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
    }

    void ComposeContent(IContainer container)
    {
        container.PaddingVertical(0.5f, Unit.Centimetre).Column(column =>
        {
            column.Spacing(6);
            
            // Patient info section
            column.Item().Element(c => ComposePatientInfo(c));
            column.Spacing(10);
            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
            column.Spacing(10);

            // Summary
            column.Item().Element(c => ComposeSummary(c));
            column.Spacing(10);

            // Payments table
            column.Item().Text("Payment History").SemiBold().FontSize(12);
            column.Spacing(4);
            column.Item().Element(ComposePaymentsTable);
        });
    }

    void ComposePatientInfo(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Text($"Patient: {Patient.FirstName} {Patient.LastName}").SemiBold().FontSize(12);
            column.Item().Text($"Patient #: {Patient.PatientNumber}").FontSize(9);
            column.Item().Text($"Phone: {Patient.Phone ?? "N/A"}").FontSize(9);
        });
    }

    void ComposeSummary(IContainer container)
    {
        var totalCost = Patient.Contracts.Where(c => c.Status == "active").Sum(c => c.TotalAmount - c.DiscountAmount);
        var totalPaid = Payments.Sum(p => p.Amount);
        var remaining = totalCost - totalPaid;

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.ConstantColumn(120);
            });

            table.Header(header =>
            {
                header.Cell().Text("").SemiBold();
                header.Cell().AlignRight().Text("Amount (YER)").SemiBold();
            });

            table.Cell().Text("Total Treatment Cost");
            table.Cell().AlignRight().Text($"{totalCost:N0}");

            table.Cell().Text("Total Paid");
            table.Cell().AlignRight().Text($"{totalPaid:N0}");

            table.Cell().Text("Remaining Balance");
            table.Cell().AlignRight().Text($"{remaining:N0}").SemiBold().FontColor(remaining > 0 ? Colors.Red.Darken1 : Colors.Green.Darken2);
        });
    }

    void ComposePaymentsTable(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(80);
                columns.ConstantColumn(80);
                columns.ConstantColumn(80);
                columns.RelativeColumn();
                columns.ConstantColumn(100);
            });

            table.Header(header =>
            {
                header.Cell().Text("Date").SemiBold().FontSize(8);
                header.Cell().Text("Receipt #").SemiBold().FontSize(8);
                header.Cell().Text("Method").SemiBold().FontSize(8);
                header.Cell().Text("Description").SemiBold().FontSize(8);
                header.Cell().AlignRight().Text("Amount").SemiBold().FontSize(8);
            });

            foreach (var payment in Payments)
            {
                table.Cell().Text(payment.PaymentDate.ToString("yyyy-MM-dd")).FontSize(8);
                table.Cell().Text(payment.ReceiptNumber ?? "-").FontSize(8);
                table.Cell().Text(payment.PaymentMethod ?? "-").FontSize(8);
                table.Cell().Text(payment.ServiceDescription ?? "-").FontSize(8);
                table.Cell().AlignRight().Text($"{payment.Amount:N0}").FontSize(8);
            }
        });
    }

    void ComposeFooter(IContainer container)
    {
        container.LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        container.PaddingTop(5).Row(row =>
        {
            row.RelativeItem().Text("Aqlan Dental Pro - Financial Statement").FontSize(7).FontColor(Colors.Grey.Darken1);
            row.ConstantItem(120).Text($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm}").FontSize(7).FontColor(Colors.Grey.Lighten2);
        });
    }
}

/// <summary>
/// QuestPDF document for printing invoices with Arabic/RTL support.
/// Uses NotoNaskhArabic font registered in PdfService.
/// </summary>
public class InvoiceDocument(Invoice Invoice) : IDocument
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
            column.Item().Row(row =>
            {
                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text("مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان")
                        .Bold().FontSize(14).FontFamily(FontName).FontColor(Colors.Blue.Darken2);
                    col.Item().Text("تعز، اليمن — شارع التحرير الأعلى")
                        .FontSize(8).FontFamily(FontName).FontColor(Colors.Grey.Darken1);
                    col.Item().Text("هاتف: 04-253028")
                        .FontSize(8).FontFamily(FontName).FontColor(Colors.Grey.Darken1);
                });
            });
            column.Spacing(6);
            column.Item().AlignCenter().Text("فاتورة")
                .Bold().FontSize(20).FontFamily(FontName).FontColor(Colors.Blue.Darken2);
            column.Item().AlignCenter().Text(Invoice.InvoiceNumber)
                .FontSize(10).FontFamily(FontName).FontColor(Colors.Grey.Darken1);
            column.Spacing(4);
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

            column.Spacing(8);
            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
            column.Spacing(8);

            // Line items table
            column.Item().Text("بنود الفاتورة").SemiBold().FontSize(12).FontFamily(FontName);
            column.Spacing(4);
            column.Item().Element(ComposeLineItemsTable);

            column.Spacing(12);

            // Totals
            column.Item().Element(ComposeTotals);

            // Notes
            if (!string.IsNullOrEmpty(Invoice.Notes))
            {
                column.Spacing(12);
                column.Item().Text($"ملاحظات: {Invoice.Notes}")
                    .FontSize(9).FontFamily(FontName).FontColor(Colors.Grey.Darken1);
            }

            // Signature area
            column.Spacing(40);
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
                columns.ConstantColumn(40);   // #
                columns.RelativeColumn(2);     // Service
                columns.RelativeColumn(2);     // Description
                columns.ConstantColumn(50);    // Qty
                columns.ConstantColumn(80);    // Unit Price
                columns.ConstantColumn(80);    // Total
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
        container.LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        container.PaddingTop(5).Row(row =>
        {
            row.RelativeItem().Text("مركز الدكتور عقلان الكامل — فاتورة").FontSize(7).FontFamily(FontName).FontColor(Colors.Grey.Darken1);
            row.ConstantItem(120).Text($"طبعت: {DateTime.UtcNow:yyyy-MM-dd HH:mm}").FontSize(7).FontColor(Colors.Grey.Lighten2);
        });
    }
}
