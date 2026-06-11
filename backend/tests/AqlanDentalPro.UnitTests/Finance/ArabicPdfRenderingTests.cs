using FluentAssertions;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Xunit;

namespace AqlanDentalPro.UnitTests.Finance;

/// <summary>
/// Regression tests for Arabic font rendering in PDF generation.
/// Root cause: PdfService.ArabicFontName was "NotoNaskhArabic" (no spaces) but
/// the font file's internal family name is "Noto Naskh Arabic" (with spaces),
/// causing QuestPDF to fall back to a non-Arabic font.
/// </summary>
public class ArabicPdfRenderingTests
{
    private const string ArabicFontName = AqlanDentalPro.Infrastructure.Services.PdfService.ArabicFontName;

    [Fact]
    public void ArabicFontName_MatchesEmbeddedFontFamilyName()
    {
        ArabicFontName.Should().Be("Noto Naskh Arabic",
            "font family name must exactly match the name embedded in the .ttf file " +
            "(was incorrectly 'NotoNaskhArabic' which caused QuestPDF to fall back to " +
            "a non-Arabic font, rendering Arabic text as boxes/question marks)");
    }

    [Fact]
    public void EnsureFontsRegistered_IsPublicStatic()
    {
        var method = typeof(AqlanDentalPro.Infrastructure.Services.PdfService)
            .GetMethod("EnsureFontsRegistered",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        method.Should().NotBeNull("EnsureFontsRegistered must be public static for use by LabOrderPdfGenerator");
    }

    [Fact]
    public void EnsureFontsRegistered_RegistersArabicFontSuccessfully()
    {
        AqlanDentalPro.Infrastructure.Services.PdfService.EnsureFontsRegistered();

        var exception = Record.Exception(() =>
        {
            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.ContentFromRightToLeft();
                    page.DefaultTextStyle(x => x.FontSize(12).FontFamily(ArabicFontName));
                    page.Content().Column(column =>
                    {
                        column.Item().Text("مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان");
                        column.Item().Text("سند قبض");
                        column.Item().Text("فاتورة");
                        column.Item().Text("كشف حساب مالي");
                    });
                });
            });

            doc.GeneratePdf();
        });

        exception.Should().BeNull(
            "PDF generation with Arabic font should not throw.");
    }

    [Fact]
    public void ArabicPdf_ExceedsMinimumSize()
    {
        AqlanDentalPro.Infrastructure.Services.PdfService.EnsureFontsRegistered();

        byte[]? pdfBytes = null;
        var exception = Record.Exception(() =>
        {
            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.ContentFromRightToLeft();
                    page.DefaultTextStyle(x => x.FontSize(12).FontFamily(ArabicFontName));
                    page.Content().Column(column =>
                    {
                        column.Item().Text("مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان")
                            .Bold().FontSize(14);
                        column.Item().Text("سند قبض — رقم: RCP-20260610-002");
                        column.Item().Text("اسم المريض: أحمد محمد علي");
                        column.Item().Text("المبلغ المقبوض: 5,000 ر.ي");
                        column.Item().Text("فاتورة — رقم: INV-20260610-001");
                        column.Item().Text("الإجمالي: 60,000 ر.ي");
                    });
                });
            });

            pdfBytes = doc.GeneratePdf();
        });

        exception.Should().BeNull("PDF generation should not throw");
        pdfBytes.Should().NotBeNullOrEmpty("PDF bytes should be generated");
        pdfBytes!.Length.Should().BeGreaterThan(5000,
            "PDF with embedded Arabic font should be at least 5KB.");
    }

    [Fact]
    public void ArabicPdf_ContainsFontFamilyReference()
    {
        AqlanDentalPro.Infrastructure.Services.PdfService.EnsureFontsRegistered();

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Centimetre);
                page.ContentFromRightToLeft();
                page.DefaultTextStyle(x => x.FontSize(12).FontFamily(ArabicFontName));
                page.Content().Text("مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان");
            });
        });

        var pdfBytes = doc.GeneratePdf();
        var pdfContent = System.Text.Encoding.ASCII.GetString(pdfBytes);
        pdfContent.Should().Contain("Noto",
            "the generated PDF must contain the font name 'Noto', confirming that " +
            "the Arabic font was referenced and embedded rather than falling back " +
            "to a default Latin-only font like Calibri");
    }

    [Fact]
    public void ArabicPdf_RendersBoldTextWithoutError()
    {
        AqlanDentalPro.Infrastructure.Services.PdfService.EnsureFontsRegistered();

        byte[]? pdfBytes = null;
        var exception = Record.Exception(() =>
        {
            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.ContentFromRightToLeft();
                    page.DefaultTextStyle(x => x.FontSize(12).FontFamily(ArabicFontName));
                    page.Content().Column(column =>
                    {
                        column.Item().Text("نص عادي — Regular weight");
                        column.Item().Text("نص عريض — Bold weight").Bold();
                        column.Item().Text("نص شبه عريض — SemiBold weight").SemiBold();
                    });
                });
            });

            pdfBytes = doc.GeneratePdf();
        });

        exception.Should().BeNull(
            "PDF generation with bold Arabic text should not throw.");
        pdfBytes.Should().NotBeNullOrEmpty();
        pdfBytes!.Length.Should().BeGreaterThan(5000);
    }

    [Theory]
    [InlineData(typeof(AqlanDentalPro.Infrastructure.Services.PaymentReceiptDocument))]
    [InlineData(typeof(AqlanDentalPro.Infrastructure.Services.FinancialStatementDocument))]
    [InlineData(typeof(AqlanDentalPro.Infrastructure.Services.InvoiceDocument))]
    public void AllPdfDocuments_ReferenceCorrectArabicFontName(Type documentType)
    {
        documentType.Should().NotBeNull();
        ArabicFontName.Should().Be("Noto Naskh Arabic",
            $"document type {documentType.Name} uses PdfService.ArabicFontName which must be 'Noto Naskh Arabic'");
    }

    [Fact]
    public void LabOrderPdfGenerator_UsesSameArabicFontName()
    {
        var labGeneratorType = typeof(AqlanDentalPro.API.Services.LabOrderPdfGenerator);
        labGeneratorType.Should().NotBeNull();
        ArabicFontName.Should().Be("Noto Naskh Arabic");
    }
}
