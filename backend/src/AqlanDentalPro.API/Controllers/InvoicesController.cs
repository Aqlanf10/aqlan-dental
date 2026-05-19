using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

/// <summary>
/// Invoice management — list, view, edit draft, issue, cancel.
/// Draft invoices are financial preparation documents, NOT completed payments.
/// Actual payments are recorded via the existing Payments module (FinanceService).
/// </summary>
[ApiController]
[Route("api/invoices")]
[Authorize(Policy = "FinanceAccess")]
public class InvoicesController(AppDbContext db, IPdfService pdfService, ILogger<InvoicesController> logger) : ControllerBase
{
    // ─── F5: POST /api/invoices — Create standalone invoice ──────────────────
    /// <summary>
    /// Creates a new draft invoice. Unlike PatientJourneyController.CreateDraftInvoice
    /// which is tied to a Visit workflow, this endpoint allows creating standalone
    /// invoices for products, lab fees, or services not tied to a visit.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInvoiceRequest req)
    {
        // Validate patient exists
        var patient = await db.Patients.FindAsync(req.PatientId);
        if (patient == null || !patient.IsActive)
            return BadRequest(new { message = "المريض غير موجود أو محذوف" });

        // Validate appointment if provided
        if (req.AppointmentId.HasValue)
        {
            var appointment = await db.Appointments.FindAsync(req.AppointmentId.Value);
            if (appointment == null)
                return BadRequest(new { message = "الموعد غير موجود" });
            if (appointment.PatientId != req.PatientId)
                return BadRequest(new { message = "الموعد لا ينتمي لهذا المريض" });
        }

        // Validate visit if provided
        if (req.VisitId.HasValue)
        {
            var visit = await db.Visits.FindAsync(req.VisitId.Value);
            if (visit == null)
                return BadRequest(new { message = "الزيارة غير موجودة" });
            if (visit.PatientId != req.PatientId)
                return BadRequest(new { message = "الزيارة لا تنتمي لهذا المريض" });
        }

        // Use advisory lock for invoice number generation to prevent duplicates
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var lockKey = (int)(DateTime.UtcNow.ToString("yyyyMMdd").GetHashCode() % 100000);
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

            var invoiceNumber = await GenerateInvoiceNumberAsync(db);
            var userId = GetCurrentUserId();

            var invoice = new Invoice
            {
                InvoiceNumber = invoiceNumber,
                PatientId = req.PatientId,
                VisitId = req.VisitId,
                AppointmentId = req.AppointmentId,
                Status = InvoiceStatus.Draft,
                Notes = req.Notes,
                CreatedBy = userId,
                UpdatedBy = userId
            };

            db.Invoices.Add(invoice);

            // Add line items if provided
            if (req.LineItems != null && req.LineItems.Count > 0)
            {
                var sortOrder = 0;
                foreach (var itemReq in req.LineItems)
                {
                    string serviceNameSnapshot = itemReq.ServiceNameSnapshot ?? "خدمة علاجية";
                    string description = itemReq.Description ?? serviceNameSnapshot;

                    // If service is provided, look up price and name
                    if (itemReq.ServiceId.HasValue)
                    {
                        var service = await db.ClinicServices.FindAsync(itemReq.ServiceId.Value);
                        if (service != null)
                        {
                            if (string.IsNullOrWhiteSpace(itemReq.ServiceNameSnapshot))
                                serviceNameSnapshot = service.ArabicName;
                        }
                    }

                    var quantity = itemReq.Quantity > 0 ? itemReq.Quantity : 1;
                    var unitPrice = itemReq.UnitPrice;
                    var totalPrice = quantity * unitPrice;

                    var lineItem = new InvoiceLineItem
                    {
                        InvoiceId = invoice.Id,
                        ServiceId = itemReq.ServiceId,
                        ServiceNameSnapshot = serviceNameSnapshot,
                        Description = description,
                        Quantity = quantity,
                        UnitPrice = unitPrice,
                        TotalPrice = totalPrice,
                        RelatedTreatmentPlanStepId = itemReq.RelatedTreatmentPlanStepId,
                        RelatedVisitId = itemReq.RelatedVisitId,
                        SortOrder = sortOrder++
                    };

                    db.InvoiceLineItems.Add(lineItem);
                }
            }

            await db.SaveChangesAsync();

            // Recalculate totals from line items
            var allLineItems = await db.InvoiceLineItems
                .Where(l => l.InvoiceId == invoice.Id && l.IsActive)
                .ToListAsync();
            invoice.Subtotal = allLineItems.Sum(l => l.TotalPrice);
            var discount = req.DiscountAmount ?? 0;
            var tax = req.TaxAmount ?? 0;
            invoice.DiscountAmount = discount;
            invoice.TaxAmount = tax;
            invoice.TotalAmount = invoice.Subtotal - discount + tax;

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return Created($"/api/invoices/{invoice.Id}", new
            {
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.PatientId,
                invoice.VisitId,
                invoice.AppointmentId,
                Status = invoice.Status.ToString(),
                StatusArabic = GetStatusArabic(invoice.Status),
                invoice.Subtotal,
                invoice.DiscountAmount,
                invoice.TaxAmount,
                invoice.TotalAmount,
                invoice.Notes,
                message = "تم إنشاء الفاتورة بنجاح"
            });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ─── 1. GET /api/invoices — List all invoices ──────────────────────────
    /// <summary>Returns paginated list of invoices with optional filters.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] Guid? patientId = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = db.Invoices
            .Include(i => i.Patient)
            .Include(i => i.LineItems)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<InvoiceStatus>(status, true, out var statusFilter))
            query = query.Where(i => i.Status == statusFilter);

        if (patientId.HasValue)
            query = query.Where(i => i.PatientId == patientId.Value);

        var total = await query.CountAsync();

        var invoices = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new
            {
                i.Id,
                i.InvoiceNumber,
                i.PatientId,
                PatientName = i.Patient != null ? BuildPatientDisplayName(i.Patient) : "",
                i.VisitId,
                i.AppointmentId,
                Status = i.Status.ToString(),
                StatusArabic = GetStatusArabic(i.Status),
                i.Subtotal,
                i.DiscountAmount,
                i.TaxAmount,
                i.TotalAmount,
                LineItemCount = i.LineItems.Count,
                i.Notes,
                i.CreatedAt,
                i.UpdatedAt
            })
            .ToListAsync();

        return Ok(new { invoices, total, page, pageSize });
    }

    // ─── 2. GET /api/invoices/{id} — Invoice detail ───────────────────────
    /// <summary>Returns full invoice with line items and payment summary.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var invoice = await db.Invoices
            .Include(i => i.Patient)
            .Include(i => i.Visit)
            .Include(i => i.Appointment)
            .Include(i => i.LineItems.OrderBy(l => l.SortOrder))
                .ThenInclude(l => l.Service)
            .Include(i => i.Payments.Where(p => p.IsActive))
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null)
            return NotFound(new { message = "الفاتورة غير موجودة" });

        var paidAmount = invoice.Payments.Sum(p => p.Amount);
        var remainingAmount = Math.Max(0, invoice.TotalAmount - paidAmount);

        return Ok(new
        {
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.PatientId,
            PatientName = invoice.Patient != null ? BuildPatientDisplayName(invoice.Patient) : "",
            invoice.VisitId,
            invoice.AppointmentId,
            Status = invoice.Status.ToString(),
            StatusArabic = GetStatusArabic(invoice.Status),
            invoice.Subtotal,
            invoice.DiscountAmount,
            invoice.TaxAmount,
            invoice.TotalAmount,
            PaidAmount = paidAmount,
            RemainingAmount = remainingAmount,
            invoice.Notes,
            invoice.CreatedAt,
            invoice.UpdatedAt,
            invoice.CreatedBy,
            invoice.UpdatedBy,
            LineItems = invoice.LineItems.Select(l => new
            {
                l.Id,
                l.InvoiceId,
                l.ServiceId,
                ServiceName = l.Service != null ? l.Service.ArabicName : l.ServiceNameSnapshot,
                l.ServiceNameSnapshot,
                l.Description,
                l.Quantity,
                l.UnitPrice,
                l.TotalPrice,
                l.RelatedTreatmentPlanStepId,
                l.RelatedVisitId,
                l.SortOrder
            }),
            Payments = invoice.Payments
                .OrderByDescending(p => p.PaymentDate)
                .Select(p => new
                {
                    p.Id,
                    p.Amount,
                    p.PaymentDate,
                    p.PaymentMethod,
                    p.ReceiptNumber,
                    p.Notes
                })
        });
    }

    // ─── 3. GET /api/patients/{patientId}/invoices — Patient invoices ──────
    /// <summary>Returns all invoices for a specific patient.</summary>
    [HttpGet("/api/patients/{patientId:guid}/invoices")]
    public async Task<IActionResult> GetByPatient(Guid patientId)
    {
        var patientExists = await db.Patients.AnyAsync(p => p.Id == patientId && p.IsActive);
        if (!patientExists)
            return NotFound(new { message = "المريض غير موجود" });

        var invoices = await db.Invoices
            .Include(i => i.LineItems)
            .Where(i => i.PatientId == patientId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new
            {
                i.Id,
                i.InvoiceNumber,
                Status = i.Status.ToString(),
                StatusArabic = GetStatusArabic(i.Status),
                i.TotalAmount,
                LineItemCount = i.LineItems.Count,
                i.CreatedAt,
                i.UpdatedAt
            })
            .ToListAsync();

        return Ok(invoices);
    }

    // ─── 4. PUT /api/invoices/{id} — Update draft invoice ─────────────────
    /// <summary>Updates a draft invoice (line items, notes, discount). Only Draft status allowed.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateInvoiceRequest req)
    {
        var invoice = await db.Invoices
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null)
            return NotFound(new { message = "الفاتورة غير موجودة" });
        if (!invoice.IsActive)
            return BadRequest(new { message = "الفاتورة محذوفة" });
        if (invoice.Status != InvoiceStatus.Draft)
            return BadRequest(new { message = "يمكن تعديل الفواتير المسودة فقط" });

        var userId = GetCurrentUserId();
        invoice.UpdatedBy = userId;

        // Update notes if provided
        if (req.Notes != null)
            invoice.Notes = req.Notes;

        // Update discount if provided
        if (req.DiscountAmount.HasValue)
            invoice.DiscountAmount = req.DiscountAmount.Value;

        // Update tax if provided
        if (req.TaxAmount.HasValue)
            invoice.TaxAmount = req.TaxAmount.Value;

        // Replace line items if provided
        if (req.LineItems != null && req.LineItems.Count > 0)
        {
            // Remove existing line items
            db.InvoiceLineItems.RemoveRange(invoice.LineItems);

            // Add new line items
            var sortOrder = 0;
            foreach (var itemReq in req.LineItems)
            {
                string serviceNameSnapshot = itemReq.ServiceNameSnapshot ?? "خدمة علاجية";
                string description = itemReq.Description ?? serviceNameSnapshot;

                // If service is provided, look up price and name
                if (itemReq.ServiceId.HasValue)
                {
                    var service = await db.ClinicServices.FindAsync(itemReq.ServiceId.Value);
                    if (service != null)
                    {
                        if (string.IsNullOrWhiteSpace(itemReq.ServiceNameSnapshot))
                            serviceNameSnapshot = service.ArabicName;
                    }
                }

                var quantity = itemReq.Quantity > 0 ? itemReq.Quantity : 1;
                var unitPrice = itemReq.UnitPrice;
                var totalPrice = quantity * unitPrice;

                var lineItem = new InvoiceLineItem
                {
                    InvoiceId = invoice.Id,
                    ServiceId = itemReq.ServiceId,
                    ServiceNameSnapshot = serviceNameSnapshot,
                    Description = description,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    TotalPrice = totalPrice,
                    RelatedTreatmentPlanStepId = itemReq.RelatedTreatmentPlanStepId,
                    RelatedVisitId = itemReq.RelatedVisitId,
                    SortOrder = sortOrder++
                };

                db.InvoiceLineItems.Add(lineItem);
            }
        }

        // Recalculate totals
        var allLineItems = await db.InvoiceLineItems
            .Where(l => l.InvoiceId == invoice.Id && l.IsActive)
            .ToListAsync();
        invoice.Subtotal = allLineItems.Sum(l => l.TotalPrice);
        var discount = invoice.DiscountAmount ?? 0;
        var tax = invoice.TaxAmount ?? 0;
        invoice.TotalAmount = invoice.Subtotal - discount + tax;

        await db.SaveChangesAsync();

        return Ok(new
        {
            invoice.Id,
            invoice.InvoiceNumber,
            Status = invoice.Status.ToString(),
            invoice.Subtotal,
            invoice.DiscountAmount,
            invoice.TaxAmount,
            invoice.TotalAmount,
            message = "تم تحديث الفاتورة بنجاح"
        });
    }

    // ─── 5. PATCH /api/invoices/{id}/issue — Issue draft invoice ──────────
    /// <summary>Changes invoice status from Draft to Issued. No payment is created.</summary>
    [HttpPatch("{id:guid}/issue")]
    public async Task<IActionResult> Issue(Guid id)
    {
        var invoice = await db.Invoices.FindAsync(id);
        if (invoice == null)
            return NotFound(new { message = "الفاتورة غير موجودة" });
        if (!invoice.IsActive)
            return BadRequest(new { message = "الفاتورة محذوفة" });
        if (invoice.Status != InvoiceStatus.Draft)
            return BadRequest(new { message = "يمكن إصدار الفواتير المسودة فقط" });

        var userId = GetCurrentUserId();
        invoice.Status = InvoiceStatus.Issued;
        invoice.UpdatedBy = userId;

        // IMPORTANT: No Payment is created. No Contract is changed.
        // No patient balance is altered. Payments module remains source of truth.

        await db.SaveChangesAsync();

        return Ok(new
        {
            invoice.Id,
            invoice.InvoiceNumber,
            Status = invoice.Status.ToString(),
            StatusArabic = GetStatusArabic(invoice.Status),
            message = "تم إصدار الفاتورة بنجاح"
        });
    }

    // ─── 6. PATCH /api/invoices/{id}/cancel — Cancel draft invoice ────────
    /// <summary>Changes invoice status to Cancelled. Only Draft can be cancelled.</summary>
    [HttpPatch("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelInvoiceRequest? req = null)
    {
        var invoice = await db.Invoices.FindAsync(id);
        if (invoice == null)
            return NotFound(new { message = "الفاتورة غير موجودة" });
        if (!invoice.IsActive)
            return BadRequest(new { message = "الفاتورة محذوفة" });
        if (invoice.Status != InvoiceStatus.Draft)
            return BadRequest(new { message = "يمكن إلغاء الفواتير المسودة فقط" });

        var userId = GetCurrentUserId();
        invoice.Status = InvoiceStatus.Cancelled;
        invoice.UpdatedBy = userId;

        if (req?.Notes != null)
            invoice.Notes = string.IsNullOrWhiteSpace(invoice.Notes)
                ? $"[إلغاء] {req.Notes}"
                : $"{invoice.Notes}\n[إلغاء] {req.Notes}";

        // IMPORTANT: No Payment is created or reversed. No Contract is changed.
        // No patient balance is altered.

        await db.SaveChangesAsync();

        return Ok(new
        {
            invoice.Id,
            invoice.InvoiceNumber,
            Status = invoice.Status.ToString(),
            StatusArabic = GetStatusArabic(invoice.Status),
            message = "تم إلغاء الفاتورة بنجاح"
        });
    }

    // ─── 7. GET /api/invoices/{id}/pdf — Invoice PDF ──────────────────────
    /// <summary>Generates a PDF for the invoice with Arabic/RTL support.</summary>
    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> GetInvoicePdf(Guid id)
    {
        try
        {
            var pdfBytes = await pdfService.GenerateInvoicePdfAsync(id);
            return File(pdfBytes, "application/pdf", $"invoice-{id}.pdf");
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invoice PDF generation failed for invoice {InvoiceId}", id);
            return NotFound(new { message = ex.Message });
        }
    }

    // ─── Private helpers ───────────────────────────────────────────────────

    private static string GetStatusArabic(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Draft => "مسودة",
        InvoiceStatus.Issued => "مصدرة",
        InvoiceStatus.Cancelled => "ملغاة",
        InvoiceStatus.Paid => "مدفوعة",
        _ => status.ToString()
    };

    private static string BuildPatientDisplayName(Patient? patient)
    {
        if (patient == null) return "";
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(patient.FirstName)) parts.Add(patient.FirstName.Trim());
        if (!string.IsNullOrWhiteSpace(patient.MiddleName)) parts.Add(patient.MiddleName.Trim());
        if (!string.IsNullOrWhiteSpace(patient.LastName)) parts.Add(patient.LastName.Trim());
        return string.Join(" ", parts);
    }

    /// <summary>Generates a unique invoice number: INV-yyyyMMdd-NNN.</summary>
    public static async Task<string> GenerateInvoiceNumberAsync(AppDbContext db)
    {
        var today = DateTime.UtcNow;
        var datePart = today.ToString("yyyyMMdd");
        var prefix = $"INV-{datePart}-";

        var lastNumber = await db.Invoices
            .IgnoreQueryFilters()
            .Where(i => i.InvoiceNumber.StartsWith(prefix))
            .OrderByDescending(i => i.InvoiceNumber)
            .Select(i => i.InvoiceNumber)
            .FirstOrDefaultAsync();

        var nextSeq = 1;
        if (!string.IsNullOrEmpty(lastNumber) && lastNumber.Length > prefix.Length)
        {
            var seqPart = lastNumber[prefix.Length..];
            if (int.TryParse(seqPart, out var lastSeq))
                nextSeq = lastSeq + 1;
        }

        return $"{prefix}{nextSeq:D3}";
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
