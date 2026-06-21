using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Enums;
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

    // IMPORTANT: This must match the font family name embedded in the .ttf file.
    // The NotoNaskhArabic-Regular.ttf has NameID 1 = "Noto Naskh Arabic" (with spaces).
    // Previously this was incorrectly set to "NotoNaskhArabic" (no spaces), causing
    // QuestPDF to fall back to a non-Arabic font, rendering Arabic text as boxes.
    public const string ArabicFontName = "Noto Naskh Arabic";

    // Font registration is done once statically (thread-safe)
    private static bool _fontRegistered = false;
    private static readonly object _fontLock = new();

    public PdfService(AppDbContext db, ILogger<PdfService> logger)
    {
        _db = db;
        _logger = logger;
        EnsureFontsRegistered();
    }

    /// <summary>
    /// Registers Arabic fonts for QuestPDF. Call once at startup.
    /// Thread-safe, idempotent. Falls back gracefully if font files are missing.
    /// Public static so that LabOrderPdfGenerator and other consumers can call it directly.
    /// </summary>
    public static void EnsureFontsRegistered()
    {
        lock (_fontLock)
        {
            if (_fontRegistered) return;

            QuestPDF.Settings.License = LicenseType.Community;

            // Register Arabic fonts for RTL PDF support (both Regular and Bold)
            var regularFontPaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Fonts", "NotoNaskhArabic-Regular.ttf"),
                Path.Combine(Directory.GetCurrentDirectory(), "Fonts", "NotoNaskhArabic-Regular.ttf"),
                "/usr/share/fonts/truetype/noto/NotoNaskhArabic-Regular.ttf",
                "/usr/share/fonts/opentype/noto/NotoNaskhArabic-Regular.ttf",
                "/usr/share/fonts/noto/NotoNaskhArabic-Regular.ttf",
            };

            var boldFontPaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Fonts", "NotoNaskhArabic-Bold.ttf"),
                Path.Combine(Directory.GetCurrentDirectory(), "Fonts", "NotoNaskhArabic-Bold.ttf"),
                "/usr/share/fonts/truetype/noto/NotoNaskhArabic-Bold.ttf",
                "/usr/share/fonts/opentype/noto/NotoNaskhArabic-Bold.ttf",
                "/usr/share/fonts/noto/NotoNaskhArabic-Bold.ttf",
            };

            var regularRegistered = RegisterFontFromPaths(regularFontPaths, "NotoNaskhArabic-Regular");
            var boldRegistered = RegisterFontFromPaths(boldFontPaths, "NotoNaskhArabic-Bold");

            if (!regularRegistered)
            {
                Console.Error.WriteLine("[PdfService] WARNING: Arabic font 'NotoNaskhArabic-Regular.ttf' not found in any search path. " +
                    "PDF Arabic text will render with system fallback font. " +
                    $"Searched: {string.Join(", ", regularFontPaths)}");
            }

            if (!boldRegistered)
            {
                Console.Error.WriteLine("[PdfService] WARNING: Arabic bold font 'NotoNaskhArabic-Bold.ttf' not found. " +
                    "Bold Arabic text will use regular weight as fallback.");
            }

            // Only mark as registered if at least the regular font was found,
            // so that subsequent calls can retry if the first attempt failed entirely.
            if (regularRegistered)
            {
                _fontRegistered = true;
            }

            // Also try to register common system fonts as fallbacks for QuestPDF
            try
            {
                var systemFontDirs = new[] { "/usr/share/fonts/truetype/", "/usr/share/fonts/opentype/" };
                foreach (var dir in systemFontDirs)
                {
                    if (!Directory.Exists(dir)) continue;
                    foreach (var fontFile in Directory.GetFiles(dir, "*.ttf", SearchOption.AllDirectories))
                    {
                        try
                        {
                            using var stream = File.OpenRead(fontFile);
                            FontManager.RegisterFont(stream);
                        }
                        catch
                        {
                            // Silently skip fonts that can't be registered
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[PdfService] System font scan failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Attempts to register a font from an ordered list of file paths.
    /// Returns true if the font was successfully registered from any path.
    /// </summary>
    private static bool RegisterFontFromPaths(string[] paths, string fontLabel)
    {
        foreach (var path in paths)
        {
            try
            {
                if (File.Exists(path))
                {
                    using var stream = File.OpenRead(path);
                    FontManager.RegisterFont(stream);
                    Console.WriteLine($"[PdfService] Arabic font ({fontLabel}) registered from: {path}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[PdfService] Failed to register font ({fontLabel}) from {path}: {ex.Message}");
            }
        }
        return false;
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

        EnsureFontsRegistered();

        var identity = await FinanceClinicIdentity.ResolveAsync(_db);
        var document = new PaymentReceiptDocument(payment, identity);
        // CLIN-12: QuestPDF rasterization is CPU-bound — offload to the thread pool
        // so the request thread is released while the PDF is composed.
        var bytes = await Task.Run(() => document.GeneratePdf());
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

        EnsureFontsRegistered();

        var identity = await FinanceClinicIdentity.ResolveAsync(_db);
        var document = new FinancialStatementDocument(patient, payments, identity);
        // CLIN-12: CPU-bound QuestPDF generation offloaded to the thread pool.
        var bytes = await Task.Run(() => document.GeneratePdf());
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

        EnsureFontsRegistered();

        var identity = await FinanceClinicIdentity.ResolveAsync(_db);
        var document = new InvoiceDocument(invoice, identity);
        // CLIN-12: CPU-bound QuestPDF generation offloaded to the thread pool.
        var bytes = await Task.Run(() => document.GeneratePdf());
        return bytes;
    }

    public async Task<byte[]> GenerateExpenseDisbursementVoucherAsync(Guid expenseId)
    {
        var expense = await _db.OperationalExpenses
            .Include(e => e.Supplier)
            .FirstOrDefaultAsync(e => e.Id == expenseId);

        if (expense == null)
            throw new ArgumentException("المصروف غير موجود");

        EnsureFontsRegistered();

        var identity = await FinanceClinicIdentity.ResolveAsync(_db);
        var model = new DisbursementVoucherModel
        {
            VoucherNumber = expense.ExpenseNumber,
            Date = expense.ExpenseDate,
            CategoryLabel = ExpenseCategoryLabel(expense.Category),
            PayeeName = expense.Supplier?.Name ?? expense.Title,
            Amount = expense.Amount,
            Description = expense.Title,
            PaymentMethod = expense.PaymentMethod,
            Notes = expense.Notes,
        };

        return await Task.Run(() => new DisbursementVoucherDocument(model, identity).GeneratePdf());
    }

    private static string ExpenseCategoryLabel(ExpenseCategory category) => category switch
    {
        ExpenseCategory.Rent => "إيجار",
        ExpenseCategory.Utilities => "خدمات (كهرباء/مياه/اتصالات)",
        ExpenseCategory.LabFees => "أتعاب مختبر",
        ExpenseCategory.Marketing => "تسويق وإعلان",
        ExpenseCategory.ClinicSupplies => "مستلزمات عيادية",
        ExpenseCategory.Maintenance => "صيانة",
        ExpenseCategory.Salaries => "رواتب وأجور",
        ExpenseCategory.Commissions => "عمولات أطباء",
        ExpenseCategory.Taxes => "ضرائب ورسوم",
        ExpenseCategory.Miscellaneous => "نثريات",
        _ => category.ToString(),
    };
}
