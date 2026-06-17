using System;
using System.IO;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AqlanDentalPro.Infrastructure.Services;

public class PaymentReceiptDocument(AqlanDentalPro.Domain.Entities.Payment Payment, FinanceClinicIdentity Identity) : IDocument
{
    private const string FontName = PdfService.ArabicFontName;

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

            // Header row: center name on right, logo on left
            column.Item().Row(row =>
            {
                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text(Identity.Name)
                        .Bold().FontSize(9).FontColor("#1a3a5c");
                    if (Identity.HasLeadDoctor)
                        col.Item().Text(Identity.HasLeadDoctorTitle
                                ? $"{Identity.LeadDoctor} — {Identity.LeadDoctorTitle}"
                                : Identity.LeadDoctor)
                            .FontSize(7).FontColor(Colors.Grey.Darken1);
                });

                var logoPath = ResolveLogoPath();
                if (logoPath != null)
                {
                    row.ConstantItem(45).AlignLeft().Image(logoPath);
                }
            });

            column.Item().LineHorizontal(0.5f).LineColor("#1a3a5c");

            column.Item().AlignCenter().Text("سند قبض")
                .Bold().FontSize(12).FontColor("#f5922e");
            column.Item().AlignCenter().Text($"رقم السند: {Payment.ReceiptNumber ?? "-"}")
                .FontSize(7).FontColor(Colors.Grey.Darken1);
            column.Item().AlignCenter().Text($"التاريخ: {Payment.PaymentDate:yyyy-MM-dd}")
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
                row.RelativeItem().Text("اسم المريض:").SemiBold().FontSize(8).FontColor("#1a3a5c");
                row.RelativeItem().Text($"{Payment.Patient?.FirstName} {Payment.Patient?.MiddleName} {Payment.Patient?.LastName}").FontSize(8);
            });

            if (!string.IsNullOrEmpty(Payment.Patient?.PatientNumber))
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text("رقم الملف:").SemiBold().FontSize(8).FontColor("#1a3a5c");
                    row.RelativeItem().Text(Payment.Patient.PatientNumber).FontSize(8);
                });
            }

            column.Item().Row(row =>
            {
                row.RelativeItem().Text("الطبيب المعالج:").SemiBold().FontSize(8).FontColor("#1a3a5c");
                row.RelativeItem().Text(Payment.Doctor?.Name ?? "غير محدد").FontSize(8);
            });

            // Payment method
            var methodLabel = Payment.PaymentMethod?.ToLower() switch
            {
                "cash" => "نقداً",
                "card" => "بطاقة",
                "banktransfer" => "تحويل بنكي",
                "mobilewallet" => "محفظة إلكترونية",
                _ => Payment.PaymentMethod ?? "—"
            };
            column.Item().Row(row =>
            {
                row.RelativeItem().Text("طريقة الدفع:").SemiBold().FontSize(8).FontColor("#1a3a5c");
                row.RelativeItem().Text(methodLabel).FontSize(8);
            });

            // Service description
            column.Item().Text($"البيان: {Payment.ServiceDescription ?? "دفعة مالية"}").FontSize(8).FontColor(Colors.Grey.Darken2);

            // Amount Box
            column.Item().AlignCenter().Background("#eef3f9").Border(1).BorderColor("#1a3a5c").Padding(6).Column(box =>
            {
                box.Item().Text("المبلغ المقبوض").AlignCenter().FontSize(7).FontColor(Colors.Grey.Darken2);
                box.Item().Text($"{Payment.Amount:N0} ر.ي").AlignCenter().Bold().FontSize(16).FontColor("#1a3a5c");
            });

            // Notes
            if (!string.IsNullOrEmpty(Payment.Notes))
            {
                column.Item().Text($"ملاحظات: {Payment.Notes}").FontSize(7).FontColor(Colors.Grey.Darken1);
            }

            // Signatures
            column.Item().Row(row =>
            {
                row.RelativeItem().AlignCenter().Column(col =>
                {
                    col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                    col.Item().Text("المستلم / الصندوق").FontSize(7).FontColor(Colors.Grey.Darken1);
                });
                row.ConstantItem(20);
                row.RelativeItem().AlignCenter().Column(col =>
                {
                    col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                    col.Item().Text("توقيع المريض").FontSize(7).FontColor(Colors.Grey.Darken1);
                });
                row.ConstantItem(20);
                row.RelativeItem().AlignCenter().Column(col =>
                {
                    col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                    col.Item().Text("ختم المركز").FontSize(7).FontColor(Colors.Grey.Darken1);
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
            column.Item().AlignCenter().Text("شكراً لثقتكم بنا — نتمنى لكم دوام الصحة والعافية")
                .FontSize(6).FontColor(Colors.Grey.Darken1);
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
                    col.Item().Text($"{Identity.Name} — كشف الحساب المالي").FontSize(7).FontColor(Colors.Grey.Darken1);
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
                    col.Item().Text($"{Identity.Name} — فاتورة").FontSize(7).FontFamily(FontName).FontColor(Colors.Grey.Darken1);
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
