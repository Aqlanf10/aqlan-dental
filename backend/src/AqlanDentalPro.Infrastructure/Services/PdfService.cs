using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AqlanDentalPro.Infrastructure.Services;

public class PdfService : IPdfService
{
    private readonly AppDbContext _db;
    private readonly ILogger<PdfService> _logger;

    public const string ArabicFontName = "NotoNaskhArabic";

    // Font registration is done once statically
    private static bool _fontRegistered = false;

    public PdfService(AppDbContext db, ILogger<PdfService> logger)
    {
        _db = db;
        _logger = logger;
        RegisterFonts();
    }

    private static void RegisterFonts()
    {
        if (_fontRegistered) return;
        _fontRegistered = true;

        QuestPDF.Settings.License = LicenseType.Community;

        // Register Arabic font for RTL PDF support
        var fontPaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Fonts", "NotoNaskhArabic-Regular.ttf"),
            Path.Combine(Directory.GetCurrentDirectory(), "Fonts", "NotoNaskhArabic-Regular.ttf"),
        };

        foreach (var path in fontPaths)
        {
            if (File.Exists(path))
            {
                using var stream = File.OpenRead(path);
                FontManager.RegisterFont(stream);
                break;
            }
        }
    }

    public async Task<byte[]> GeneratePaymentReceiptAsync(Guid paymentId)
    {
        var payment = await _db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .Include(p => p.Receipt)
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment == null)
            throw new ArgumentException("الدفعة غير موجودة");

        var document = new PaymentReceiptDocument(payment);
        var bytes = document.GeneratePdf();
        return bytes;
    }

    public async Task<byte[]> GenerateFinancialStatementAsync(Guid patientId)
    {
        var patient = await _db.Patients
            .Include(p => p.Contracts)
                .ThenInclude(c => c.Payments)
            .FirstOrDefaultAsync(p => p.Id == patientId);

        if (patient == null)
            throw new ArgumentException("المريض غير موجود");

        var payments = await _db.Payments
            .Include(p => p.Doctor)
            .Where(p => p.PatientId == patientId && p.IsActive)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();

        var document = new FinancialStatementDocument(patient, payments);
        var bytes = document.GeneratePdf();
        return bytes;
    }

    public async Task<byte[]> GenerateInvoicePdfAsync(Guid invoiceId)
    {
        var invoice = await _db.Invoices
            .Include(i => i.Patient)
            .Include(i => i.LineItems.OrderBy(l => l.SortOrder))
                .ThenInclude(l => l.Service)
            .Include(i => i.Payments.Where(p => p.IsActive))
            .FirstOrDefaultAsync(i => i.Id == invoiceId);

        if (invoice == null)
            throw new ArgumentException("الفاتورة غير موجودة");

        var document = new InvoiceDocument(invoice);
        var bytes = document.GeneratePdf();
        return bytes;
    }
}
