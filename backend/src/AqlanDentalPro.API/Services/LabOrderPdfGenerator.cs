using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using AqlanDentalPro.Domain.Entities;

namespace AqlanDentalPro.API.Services;

/// <summary>
/// Lab Sprint 5 — Generates PDF for lab orders using QuestPDF.
/// </summary>
public static class LabOrderPdfGenerator
{
    public static byte[] Generate(LabOrder order, string clinicName, string clinicPhone, string clinicAddress)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var culture = new System.Globalization.CultureInfo("ar-SA");
        var items = order.Items?.OrderBy(i => i.SortOrder).ToList() ?? [];

        var doc = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Tajawal"));

                page.Header().Element(compose => ComposeHeader(compose, order, clinicName, clinicPhone, clinicAddress));
                page.Content().Element(compose => ComposeContent(compose, order, items));
                page.Footer().Element(ComposeFooter);
            });
        });

        return doc.GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, LabOrder order, string clinicName, string clinicPhone, string clinicAddress)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text(clinicName).Bold().FontSize(14);
                column.Item().Text(clinicPhone).FontSize(9);
                column.Item().Text(clinicAddress).FontSize(9);
            });
            row.RelativeItem().AlignRight().Column(column =>
            {
                column.Item().Text($"أمر عمل معمل").Bold().FontSize(16);
                column.Item().Text($"رقم الطلب: {order.OrderNumber}").FontSize(10);
                column.Item().Text($"التاريخ: {order.SentDate?.ToString("yyyy-MM-dd") ?? DateTime.Today.ToString("yyyy-MM-dd")}").FontSize(9);
                column.Item().Text($"الأولوية: {order.Priority}").FontSize(9);
            });
        });

        container.PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
    }

    private static void ComposeContent(IContainer container, LabOrder order, List<LabOrderItem> items)
    {
        container.Column(column =>
        {
            // Patient & Lab info
            column.Spacing(10);

            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("بيانات المريض").Bold().FontSize(11);
                    col.Item().Text($"الاسم: {order.Patient?.FirstName} {order.Patient?.LastName}");
                    col.Item().Text($"رقم الملف: {order.Patient?.PatientNumber}");
                    col.Item().Text($"الطبيب: {order.Doctor?.Name ?? "—"}");
                });
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("بيانات المعمل").Bold().FontSize(11);
                    col.Item().Text($"الاسم: {order.Lab?.Name ?? order.LabName ?? "—"}");
                    col.Item().Text($"الهاتف: {order.Lab?.Phone ?? "—"}");
                    col.Item().Text($"تاريخ الاستلام المتوقع: {order.ExpectedDate?.ToString("yyyy-MM-dd") ?? "—"}");
                });
            });

            // Items table
            if (items.Count > 0)
            {
                column.Spacing(5);
                column.Item().Text("بنود العمل").Bold().FontSize(11);

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(30);  // #
                        columns.RelativeColumn(2);    // Type
                        columns.RelativeColumn(1);    // Tooth
                        columns.RelativeColumn(1);    // Shade
                        columns.RelativeColumn(1);    // Units
                        columns.RelativeColumn(1);    // Price
                        columns.RelativeColumn(1);    // Total
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(CellStyle).Text("#");
                        header.Cell().Element(CellStyle).Text("نوع العمل");
                        header.Cell().Element(CellStyle).Text("السن");
                        header.Cell().Element(CellStyle).Text("الظل");
                        header.Cell().Element(CellStyle).Text("الوحدات");
                        header.Cell().Element(CellStyle).Text("سعر الوحدة");
                        header.Cell().Element(CellStyle).Text("الإجمالي");
                    });

                    for (var i = 0; i < items.Count; i++)
                    {
                        var item = items[i];
                        table.Cell().Element(CellStyle).Text($"{i + 1}");
                        table.Cell().Element(CellStyle).Text(item.WorkType?.Name ?? "—");
                        table.Cell().Element(CellStyle).Text(item.ToothNumber ?? "—");
                        table.Cell().Element(CellStyle).Text(item.Shade ?? "—");
                        table.Cell().Element(CellStyle).Text($"{item.UnitsCount}");
                        table.Cell().Element(CellStyle).Text(item.UnitPrice.HasValue ? $"{item.UnitPrice:N0}" : "—");
                        table.Cell().Element(CellStyle).Text(item.TotalPrice.HasValue ? $"{item.TotalPrice:N0}" : "—");
                    }
                });
            }

            // Total
            if (order.TotalCost.HasValue || order.Cost.HasValue)
            {
                column.Spacing(5);
                var total = order.TotalCost ?? order.Cost ?? 0;
                column.Item().AlignRight().Text($"التكلفة الإجمالية: {total:N0}").Bold().FontSize(12);
            }

            // Instructions
            if (!string.IsNullOrWhiteSpace(order.Instructions))
            {
                column.Spacing(5);
                column.Item().Text("تعليمات خاصة").Bold().FontSize(11);
                column.Item().Text(order.Instructions).FontSize(9);
            }

            // Doctor signature
            column.Spacing(20);
            column.Item().Row(row =>
            {
                row.RelativeItem();
                row.ConstantItem(200).Column(col =>
                {
                    col.Item().PaddingBottom(30).LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                    col.Item().AlignCenter().Text("توقيع الطبيب").FontSize(9);
                });
            });
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(x =>
        {
            x.Span("تم إنشاؤه آلياً بواسطة نظام عقلان لطب الأسنان").FontSize(8).FontColor(Colors.Grey.Medium);
        });
    }

    private static IContainer CellStyle(IContainer container)
    {
        return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).PaddingHorizontal(5);
    }
}
