using System;
using System.IO;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AqlanDentalPro.Infrastructure.Services;

public class PaymentReceiptDocument(AqlanDentalPro.Domain.Entities.Payment Payment) : IDocument
{
    private const string FontName = PdfService.ArabicFontName;
    private const string ColorPrimary = "#1a3a5c";
    private const string ColorAccent = "#f5922e";
    private const string ColorLightBg = "#eef3f9";
    private const string ColorLightAccentBg = "#fdf3e7";

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"سند قبض {Payment.ReceiptNumber}",
        Author = "Aqlan Dental Pro",
        Subject = "Payment Receipt"
    };

    public void Compose(IDocumentContainer container)
    {
        container
            .Page(page =>
            {
                page.Size(105, 148, Unit.Millimetre);
                page.Margin(8, Unit.Millimetre);
                page.PageColor(Colors.White);
                page.ContentFromRightToLeft();
                page.DefaultTextStyle(x => x.FontSize(7.5f).FontFamily(FontName));

                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().Element(ComposeFooter);
            });
    }

    void ComposeHeader(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(1);

            // Header row: center name on right, logo on left
            column.Item().Row(row =>
            {
                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text("مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان")
                        .Bold().FontSize(8.5f).FontColor(ColorPrimary);
                });

                var logoPath = ResolveLogoPath();
                if (logoPath != null)
                {
                    row.ConstantItem(38).AlignLeft().Image(logoPath);
                }
            });

            // Thin dark blue separator
            column.Item().LineHorizontal(0.75f).LineColor(ColorPrimary);

            // Title bar with accent background
            column.Item().Background(ColorLightAccentBg).PaddingVertical(3).PaddingHorizontal(6).Row(row =>
            {
                row.RelativeItem().AlignRight().Text("سند قبض")
                    .Bold().FontSize(11).FontColor(ColorAccent);
                row.RelativeItem().AlignLeft().Column(infoCol =>
                {
                    infoCol.Item().Text($"رقم السند: {Payment.ReceiptNumber ?? "-"}")
                        .FontSize(6.5f).FontColor(Colors.Grey.Darken2);
                    infoCol.Item().Text($"التاريخ: {Payment.PaymentDate:yyyy-MM-dd}")
                        .FontSize(6.5f).FontColor(Colors.Grey.Darken2);
                });
            });

            // Thin accent separator
            column.Item().LineHorizontal(0.75f).LineColor(ColorAccent);
        });
    }

    void ComposeContent(IContainer container)
    {
        container.PaddingVertical(2, Unit.Millimetre).Column(column =>
        {
            column.Spacing(2);

            // Data fields in a 2-column table (Label | Value)
            column.Item().Element(ComposeDataFields);

            // Amount Box — prominent and centered
            column.Item().Element(ComposeAmountBox);

            // Notes (if any)
            if (!string.IsNullOrEmpty(Payment.Notes))
            {
                column.Item().Background("#fefce8").Border(0.5f).BorderColor("#e5d28a").Padding(4)
                    .Text($"ملاحظات: {Payment.Notes}").FontSize(6.5f).FontColor(Colors.Grey.Darken2);
            }

            // Signature section
            column.Item().Element(ComposeSignatures);
        });
    }

    void ComposeDataFields(IContainer container)
    {
        var methodLabel = Payment.PaymentMethod?.ToLower() switch
        {
            "cash" => "نقداً",
            "card" => "بطاقة",
            "banktransfer" => "تحويل بنكي",
            "mobilewallet" => "محفظة إلكترونية",
            _ => Payment.PaymentMethod ?? "—"
        };

        var patientName = $"{Payment.Patient?.FirstName} {Payment.Patient?.MiddleName} {Payment.Patient?.LastName}".Trim();
        var patientNumber = Payment.Patient?.PatientNumber;

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2);   // Label column
                columns.RelativeColumn(3);   // Value column
            });

            // Row 1: Patient Name (shaded)
            table.Cell().Element(ShadedLabelCell).Text("اسم المريض");
            table.Cell().Element(ShadedValueCell).Text(patientName);

            // Row 2: File Number (if available)
            if (!string.IsNullOrEmpty(patientNumber))
            {
                table.Cell().Element(PlainLabelCell).Text("رقم الملف");
                table.Cell().Element(PlainValueCell).Text(patientNumber);
            }

            // Row 3: Doctor (shaded)
            table.Cell().Element(ShadedLabelCell).Text("الطبيب المعالج");
            table.Cell().Element(ShadedValueCell).Text(Payment.Doctor?.Name ?? "غير محدد");

            // Row 4: Payment Method
            table.Cell().Element(PlainLabelCell).Text("طريقة الدفع");
            table.Cell().Element(PlainValueCell).Text(methodLabel);

            // Row 5: Description (shaded)
            table.Cell().Element(ShadedLabelCell).Text("البيان");
            table.Cell().Element(ShadedValueCell).Text(Payment.ServiceDescription ?? "دفعة مالية");
        });
    }

    static IContainer ShadedLabelCell(IContainer cell) => cell
        .Background(ColorLightBg).PaddingVertical(2).PaddingHorizontal(4)
        .DefaultTextStyle(x => x.FontSize(7).FontFamily(FontName).SemiBold().FontColor(ColorPrimary));

    static IContainer ShadedValueCell(IContainer cell) => cell
        .Background(ColorLightBg).PaddingVertical(2).PaddingHorizontal(4)
        .DefaultTextStyle(x => x.FontSize(7.5f).FontFamily(FontName).FontColor(Colors.Grey.Darken3));

    static IContainer PlainLabelCell(IContainer cell) => cell
        .PaddingVertical(2).PaddingHorizontal(4)
        .DefaultTextStyle(x => x.FontSize(7).FontFamily(FontName).SemiBold().FontColor(ColorPrimary));

    static IContainer PlainValueCell(IContainer cell) => cell
        .PaddingVertical(2).PaddingHorizontal(4)
        .DefaultTextStyle(x => x.FontSize(7.5f).FontFamily(FontName).FontColor(Colors.Grey.Darken3));

    void ComposeAmountBox(IContainer container)
    {
        container.PaddingTop(2).AlignCenter().Width(85, Unit.Millimetre)
            .Background(ColorLightBg)
            .Border(1.5f).BorderColor(ColorPrimary)
            .PaddingVertical(4).PaddingHorizontal(8)
            .Column(box =>
            {
                box.Spacing(1);
                box.Item().AlignCenter().Text("المبلغ المقبوض")
                    .SemiBold().FontSize(7).FontColor(ColorAccent);
                box.Item().AlignCenter().Text($"{Payment.Amount:N0} ر.ي")
                    .Bold().FontSize(14).FontColor(ColorPrimary);
            });
    }

    void ComposeSignatures(IContainer container)
    {
        container.PaddingTop(6).Column(column =>
        {
            column.Spacing(2);

            // Three signature columns: ختم المركز | توقيع المريض | استلام / الصندوق
            column.Item().Row(row =>
            {
                row.RelativeItem().AlignCenter().Column(col =>
                {
                    col.Item().Height(18, Unit.Millimetre);  // Space for signature/stamp
                    col.Item().LineHorizontal(0.5f).LineColor(ColorPrimary);
                    col.Item().Text("ختم المركز").FontSize(6.5f).FontColor(ColorPrimary).SemiBold();
                });
                row.ConstantItem(8);
                row.RelativeItem().AlignCenter().Column(col =>
                {
                    col.Item().Height(18, Unit.Millimetre);  // Space for signature
                    col.Item().LineHorizontal(0.5f).LineColor(ColorPrimary);
                    col.Item().Text("توقيع المريض").FontSize(6.5f).FontColor(ColorPrimary).SemiBold();
                });
                row.ConstantItem(8);
                row.RelativeItem().AlignCenter().Column(col =>
                {
                    col.Item().Height(18, Unit.Millimetre);  // Space for signature
                    col.Item().LineHorizontal(0.5f).LineColor(ColorPrimary);
                    col.Item().Text("استلام / الصندوق").FontSize(6.5f).FontColor(ColorPrimary).SemiBold();
                });
            });
        });
    }

    void ComposeFooter(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(1);

            column.Item().LineHorizontal(0.75f).LineColor(ColorAccent);

            column.Item().PaddingTop(2).AlignCenter().Text("هاتف: 04-253028  |  تعز، اليمن — شارع التحرير الأعلى")
                .FontSize(6.5f).FontColor(ColorPrimary).SemiBold();

            column.Item().AlignCenter().Text("شكراً لثقتكم بنا ونتمنى لكم دوام الصحة والعافية")
                .FontSize(6f).FontColor(ColorAccent).SemiBold();
        });
    }

    private static string? ResolveLogoPath()
    {
        var paths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Fonts", "logo.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "Fonts", "logo.png"),
        };
        foreach (var path in paths)
        {
            if (File.Exists(path)) return path;
        }
        return null;
    }
}

public class FinancialStatementDocument(AqlanDentalPro.Domain.Entities.Patient Patient, List<AqlanDentalPro.Domain.Entities.Payment> Payments) : IDocument
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
                    col.Item().Text("مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان")
                        .Bold().FontSize(13).FontColor("#1a3a5c");
                    col.Item().Text("تعز، اليمن — شارع التحرير الأعلى")
                        .FontSize(8).FontColor(Colors.Grey.Darken2);
                    col.Item().Text("هاتف: 04-253028")
                        .FontSize(8).FontColor(Colors.Grey.Darken2);
                });

                var logoPath = ResolveLogoPath();
                if (logoPath != null)
                {
                    row.ConstantItem(60).AlignLeft().Image(logoPath);
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
                    col.Item().Text("مركز الدكتور عقلان الكامل — كشف الحساب المالي").FontSize(7).FontColor(Colors.Grey.Darken1);
                    col.Item().Text("شكراً لثقتكم بنا").FontSize(7).FontColor(Colors.Grey.Darken1);
                });
                row.ConstantItem(120).Text($"طبع في: {DateTime.UtcNow:yyyy-MM-dd HH:mm}").FontSize(6).FontColor(Colors.Grey.Lighten1);
            });
        });
    }

    private static string? ResolveLogoPath()
    {
        var paths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Fonts", "logo.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "Fonts", "logo.png"),
        };
        foreach (var path in paths)
        {
            if (File.Exists(path)) return path;
        }
        return null;
    }
}

/// <summary>
/// QuestPDF document for printing invoices with Arabic/RTL support.
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
            column.Spacing(4);

            column.Item().Row(row =>
            {
                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text("مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان")
                        .Bold().FontSize(14).FontFamily(FontName).FontColor("#1a3a5c");
                    col.Item().Text("تعز، اليمن — شارع التحرير الأعلى")
                        .FontSize(8).FontFamily(FontName).FontColor(Colors.Grey.Darken2);
                    col.Item().Text("هاتف: 04-253028")
                        .FontSize(8).FontFamily(FontName).FontColor(Colors.Grey.Darken2);
                });

                var logoPath = ResolveLogoPath();
                if (logoPath != null)
                {
                    row.ConstantItem(60).AlignLeft().Image(logoPath);
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
                    col.Item().Text("مركز الدكتور عقلان الكامل — فاتورة").FontSize(7).FontFamily(FontName).FontColor(Colors.Grey.Darken1);
                    col.Item().Text("شكراً لثقتكم بنا").FontSize(7).FontFamily(FontName).FontColor(Colors.Grey.Darken1);
                });
                row.ConstantItem(120).Text($"طبعت: {DateTime.UtcNow:yyyy-MM-dd HH:mm}").FontSize(7).FontColor(Colors.Grey.Lighten1);
            });
        });
    }

    private static string? ResolveLogoPath()
    {
        var paths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Fonts", "logo.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "Fonts", "logo.png"),
        };
        foreach (var path in paths)
        {
            if (File.Exists(path)) return path;
        }
        return null;
    }
}
