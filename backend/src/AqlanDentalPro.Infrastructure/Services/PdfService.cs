using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AqlanDentalPro.Infrastructure.Services;

public class PdfService : IPdfService
{
    private readonly AppDbContext _db;
    private readonly ILogger<PdfService> _logger;

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

        // Use standard fonts as fallback — for Arabic, the browser-side print works better
        // QuestPDF will use the font for Latin characters
        // NOTE: For full Arabic PDF support, add a custom Arabic font file (e.g., NotoSansArabic.ttf)
        // and register it here: FontManager.RegisterFont(File.ReadAllBytes("path/to/font.ttf"));
        QuestPDF.Settings.License = LicenseType.Community;
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
}
