using AqlanDentalPro.Application.DTOs.Commission;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.Infrastructure.Services;

public class CommissionService(
    AppDbContext db,
    ICurrentUserService currentUser,
    ILogger<CommissionService> logger) : ICommissionService
{
    // ── Line item commission ──────────────────────────────────────────────────

    public async Task<LineItemCommissionDto?> GetLineItemCommissionAsync(Guid lineItemId)
    {
        var item = await LoadLineItemAsync(lineItemId);
        return item == null ? null : MapLineItem(item);
    }

    public async Task<List<LineItemCommissionDto>> GetInvoiceCommissionsAsync(Guid invoiceId)
    {
        var items = await db.InvoiceLineItems
            .Include(i => i.Invoice).ThenInclude(inv => inv.Patient)
            .Include(i => i.Service)
            .Include(i => i.Doctor)
            .Where(i => i.InvoiceId == invoiceId && i.IsActive)
            .OrderBy(i => i.SortOrder)
            .ToListAsync();

        return items.Select(MapLineItem).ToList();
    }

    public async Task<LineItemCommissionDto?> RecalculateAsync(Guid lineItemId)
    {
        var item = await LoadLineItemAsync(lineItemId);
        if (item == null) return null;

        ApplyCalculation(item);
        await db.SaveChangesAsync();

        logger.LogInformation("Commission recalculated for line item {LineItemId}", lineItemId);
        return MapLineItem(item);
    }

    public async Task<LineItemCommissionDto?> UpdateCostsAsync(
        Guid lineItemId, UpdateLineItemCommissionRequest req, Guid updatedBy)
    {
        var item = await LoadLineItemAsync(lineItemId);
        if (item == null) return null;

        if (item.CommissionStatus == CommissionStatus.Approved)
            throw new InvalidOperationException("العمولة معتمدة — يجب فتحها من قِبَل المدير قبل التعديل");

        if (req.MaterialCost.HasValue)                item.MaterialCost               = req.MaterialCost.Value;
        if (req.LabCost.HasValue)                     item.LabCost                    = req.LabCost.Value;
        if (req.OtherDirectCost.HasValue)             item.OtherDirectCost            = req.OtherDirectCost.Value;
        if (req.DoctorCommissionPercentage.HasValue)  item.DoctorCommissionPercentage = req.DoctorCommissionPercentage.Value;
        if (req.CommissionBaseRule.HasValue)          item.CommissionBaseRule         = req.CommissionBaseRule.Value;
        if (req.DoctorId.HasValue)                    item.DoctorId                   = req.DoctorId.Value;
        if (req.CommissionNotes != null)              item.CommissionNotes            = req.CommissionNotes;

        ApplyCalculation(item);
        await db.SaveChangesAsync();

        await LogAuditAsync(lineItemId, "UpdateCommissionCosts", updatedBy,
            $"MaterialCost={item.MaterialCost} LabCost={item.LabCost} Pct={item.DoctorCommissionPercentage}");

        return MapLineItem(item);
    }

    public async Task<LineItemCommissionDto?> ApproveAsync(
        Guid lineItemId, ApproveCommissionRequest req, Guid approvedBy)
    {
        var item = await LoadLineItemAsync(lineItemId);
        if (item == null) return null;

        if (item.CommissionStatus == CommissionStatus.Paid)
            throw new InvalidOperationException("العمولة مدفوعة بالفعل");

        // Warn if lab order linked but lab cost is zero
        if (item.LabOrderId.HasValue && item.LabCost == 0)
            throw new InvalidOperationException("يوجد طلب معمل مرتبط ولكن تكلفة المعمل = 0. يرجى تحديث التكلفة قبل الاعتماد");

        ApplyCalculation(item);
        item.CommissionStatus     = CommissionStatus.Approved;
        item.CommissionApprovedBy = approvedBy;
        item.CommissionApprovedAt = DateTime.UtcNow;
        if (req.Notes != null) item.CommissionNotes = req.Notes;

        await db.SaveChangesAsync();
        await LogAuditAsync(lineItemId, "ApproveCommission", approvedBy,
            $"DoctorCommission={item.DoctorCommissionAmount} Net={item.NetCommissionableAmount}");

        return MapLineItem(item);
    }

    public async Task<LineItemCommissionDto?> UnlockAsync(Guid lineItemId, Guid unlockedBy)
    {
        var item = await LoadLineItemAsync(lineItemId);
        if (item == null) return null;

        if (item.CommissionStatus == CommissionStatus.Paid)
            throw new InvalidOperationException("لا يمكن فتح عمولة مدفوعة");

        item.CommissionStatus     = CommissionStatus.Calculated;
        item.CommissionApprovedBy = null;
        item.CommissionApprovedAt = null;

        await db.SaveChangesAsync();
        await LogAuditAsync(lineItemId, "UnlockCommission", unlockedBy, "Commission unlocked for re-edit");

        return MapLineItem(item);
    }

    // ── Report ────────────────────────────────────────────────────────────────

    public async Task<CommissionReportResponse> GetReportAsync(
        DateOnly from, DateOnly to,
        Guid? doctorId, Guid? branchId,
        string? serviceCategory, string? commissionStatus, string? paymentStatus)
    {
        var query = db.InvoiceLineItems
            .Include(i => i.Invoice).ThenInclude(inv => inv.Patient)
            .Include(i => i.Service)
            .Include(i => i.Doctor)
            .Where(i => i.IsActive
                     && i.Invoice.IsActive
                     && i.Invoice.CreatedAt.Date >= from.ToDateTime(TimeOnly.MinValue).Date
                     && i.Invoice.CreatedAt.Date <= to.ToDateTime(TimeOnly.MaxValue).Date);

        if (doctorId.HasValue)
            query = query.Where(i => i.DoctorId == doctorId.Value);

        if (!string.IsNullOrWhiteSpace(commissionStatus)
            && Enum.TryParse<CommissionStatus>(commissionStatus, true, out var cs))
            query = query.Where(i => i.CommissionStatus == cs);

        var items = await query.OrderBy(i => i.Invoice.CreatedAt).ToListAsync();

        // Compute paid commission per line item from DoctorCommissionPayments
        // (simplified: paid commission tracked at doctor level, not per line item)
        // For the report we show DoctorCommissionAmount as "earned" and 0 paid (pending payment tracking)
        var rows = items.Select(i => new CommissionReportRow(
            Date: i.Invoice.CreatedAt,
            PatientName: i.Invoice.Patient != null
                ? $"{i.Invoice.Patient.FirstName} {i.Invoice.Patient.LastName}".Trim()
                : "",
            InvoiceNumber: i.Invoice.InvoiceNumber,
            ServiceName: i.ServiceNameSnapshot.Length > 0 ? i.ServiceNameSnapshot : i.Description,
            DoctorName: i.Doctor?.Name,
            GrossAmount: i.TotalPrice,
            Discount: i.LineDiscountAmount,
            MaterialCost: i.MaterialCost,
            LabCost: i.LabCost,
            OtherCosts: i.OtherDirectCost,
            NetCommissionableAmount: i.NetCommissionableAmount,
            DoctorPercentage: i.DoctorCommissionPercentage,
            DoctorCommission: i.DoctorCommissionAmount,
            PaidCommission: i.CommissionStatus == CommissionStatus.Paid ? i.DoctorCommissionAmount : 0,
            RemainingCommission: i.CommissionStatus == CommissionStatus.Paid ? 0 : i.DoctorCommissionAmount,
            Status: i.CommissionStatus.ToString()
        )).ToList();

        var summary = new CommissionReportSummary(
            TotalGross:           rows.Sum(r => r.GrossAmount),
            TotalDiscount:        rows.Sum(r => r.Discount),
            TotalMaterialCost:    rows.Sum(r => r.MaterialCost),
            TotalLabCost:         rows.Sum(r => r.LabCost),
            TotalOtherCosts:      rows.Sum(r => r.OtherCosts),
            TotalNet:             rows.Sum(r => r.NetCommissionableAmount),
            TotalDoctorCommission:rows.Sum(r => r.DoctorCommission),
            TotalPaid:            rows.Sum(r => r.PaidCommission),
            TotalRemaining:       rows.Sum(r => r.RemainingCommission));

        return new CommissionReportResponse(summary, rows);
    }

    // ── Commission payment disbursement ───────────────────────────────────────

    public async Task<DoctorCommissionPaymentDto> RecordPaymentAsync(
        RecordCommissionPaymentRequest req, Guid recordedBy)
    {
        var doctor = await db.Doctors.FindAsync(req.DoctorId)
            ?? throw new ArgumentException("الطبيب غير موجود");

        var payment = new DoctorCommissionPayment
        {
            DoctorId        = req.DoctorId,
            Amount          = req.Amount,
            PaymentDate     = req.PaymentDate,
            PaymentMethod   = req.PaymentMethod,
            ReferenceNumber = req.ReferenceNumber,
            Notes           = req.Notes,
            PaidBy          = recordedBy,
        };

        db.DoctorCommissionPayments.Add(payment);

        // Mark specified line items as Paid
        if (req.LineItemIds is { Count: > 0 })
        {
            var lineItems = await db.InvoiceLineItems
                .Where(i => req.LineItemIds.Contains(i.Id) && i.IsActive)
                .ToListAsync();

            foreach (var item in lineItems)
            {
                if (item.CommissionStatus == CommissionStatus.Approved)
                    item.CommissionStatus = CommissionStatus.Paid;
            }
        }

        await db.SaveChangesAsync();

        return new DoctorCommissionPaymentDto(
            Id: payment.Id,
            DoctorId: payment.DoctorId,
            DoctorName: doctor.Name,
            Amount: payment.Amount,
            PaymentDate: payment.PaymentDate,
            PaymentMethod: payment.PaymentMethod,
            ReferenceNumber: payment.ReferenceNumber,
            Notes: payment.Notes,
            CreatedAt: payment.CreatedAt);
    }

    public async Task<List<DoctorCommissionPaymentDto>> GetPaymentsAsync(Guid? doctorId)
    {
        var query = db.DoctorCommissionPayments
            .Include(p => p.Doctor)
            .Where(p => p.IsActive);

        if (doctorId.HasValue)
            query = query.Where(p => p.DoctorId == doctorId.Value);

        var list = await query.OrderByDescending(p => p.PaymentDate).ToListAsync();

        return list.Select(p => new DoctorCommissionPaymentDto(
            Id: p.Id,
            DoctorId: p.DoctorId,
            DoctorName: p.Doctor.Name,
            Amount: p.Amount,
            PaymentDate: p.PaymentDate,
            PaymentMethod: p.PaymentMethod,
            ReferenceNumber: p.ReferenceNumber,
            Notes: p.Notes,
            CreatedAt: p.CreatedAt)).ToList();
    }

    // ── Service commission defaults ───────────────────────────────────────────

    public async Task<ServiceCommissionDefaultsDto?> GetServiceDefaultsAsync(Guid serviceId)
    {
        var svc = await db.ClinicServices.FindAsync(serviceId);
        return svc == null ? null : MapServiceDefaults(svc);
    }

    public async Task<ServiceCommissionDefaultsDto?> UpdateServiceDefaultsAsync(
        Guid serviceId, UpdateServiceCommissionDefaultsRequest req)
    {
        var svc = await db.ClinicServices.FindAsync(serviceId);
        if (svc == null) return null;

        svc.DefaultMaterialCost               = req.DefaultMaterialCost;
        svc.DefaultMaterialCostType           = req.DefaultMaterialCostType;
        svc.DefaultLabCost                    = req.DefaultLabCost;
        svc.DefaultDoctorCommissionPercentage = req.DefaultDoctorCommissionPercentage;
        svc.CommissionBaseRule                = req.CommissionBaseRule;

        await db.SaveChangesAsync();
        return MapServiceDefaults(svc);
    }

    // ── Auto-fill from service defaults ──────────────────────────────────────

    public async Task AutoFillFromServiceAsync(Guid lineItemId)
    {
        var item = await LoadLineItemAsync(lineItemId);
        if (item == null || item.ServiceId == null) return;

        var svc = await db.ClinicServices.FindAsync(item.ServiceId.Value);
        if (svc == null) return;

        // Resolve material cost (fixed or percentage)
        item.MaterialCost = CommissionCalculator.ResolveMaterialCost(
            item.UnitPrice,
            svc.DefaultMaterialCost,
            svc.DefaultMaterialCostType);

        item.LabCost = svc.DefaultLabCost;
        item.CommissionBaseRule = svc.CommissionBaseRule;

        // Doctor commission %: service default overrides doctor default
        if (svc.DefaultDoctorCommissionPercentage.HasValue)
        {
            item.DoctorCommissionPercentage = svc.DefaultDoctorCommissionPercentage.Value;
        }
        else if (item.DoctorId.HasValue)
        {
            var doctor = await db.Doctors.FindAsync(item.DoctorId.Value);
            if (doctor?.DefaultCommissionPercentage.HasValue == true)
                item.DoctorCommissionPercentage = doctor.DefaultCommissionPercentage.Value;
        }

        // If linked lab order exists, pull actual lab cost
        if (item.LabOrderId.HasValue)
        {
            var labOrder = await db.LabOrders.FindAsync(item.LabOrderId.Value);
            if (labOrder != null)
            {
                var labCostProp = labOrder.GetType().GetProperty("Cost")
                               ?? labOrder.GetType().GetProperty("TotalCost")
                               ?? labOrder.GetType().GetProperty("Fee");
                if (labCostProp != null && labCostProp.GetValue(labOrder) is decimal labFee)
                    item.LabCost = labFee;
            }
        }

        ApplyCalculation(item);
        await db.SaveChangesAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<InvoiceLineItem?> LoadLineItemAsync(Guid id) =>
        await db.InvoiceLineItems
            .Include(i => i.Invoice).ThenInclude(inv => inv.Patient)
            .Include(i => i.Service)
            .Include(i => i.Doctor)
            .FirstOrDefaultAsync(i => i.Id == id && i.IsActive);

    private static void ApplyCalculation(InvoiceLineItem item)
    {
        var result = CommissionCalculator.Calculate(new CommissionCalculator.Input(
            TotalPrice:                item.TotalPrice,
            LineDiscountAmount:        item.LineDiscountAmount,
            MaterialCost:              item.MaterialCost,
            LabCost:                   item.LabCost,
            OtherDirectCost:           item.OtherDirectCost,
            DoctorCommissionPercentage:item.DoctorCommissionPercentage,
            BaseRule:                  item.CommissionBaseRule));

        item.NetCommissionableAmount = result.NetCommissionableAmount;
        item.DoctorCommissionAmount  = result.DoctorCommissionAmount;
        item.CenterShareAmount       = result.CenterShareAmount;

        if (item.CommissionStatus == CommissionStatus.Pending && item.DoctorCommissionPercentage > 0)
            item.CommissionStatus = CommissionStatus.Calculated;
    }

    private static LineItemCommissionDto MapLineItem(InvoiceLineItem i) => new(
        LineItemId:                i.Id,
        InvoiceId:                 i.InvoiceId,
        InvoiceNumber:             i.Invoice.InvoiceNumber,
        PatientName:               i.Invoice.Patient != null
            ? $"{i.Invoice.Patient.FirstName} {i.Invoice.Patient.LastName}".Trim()
            : "",
        ServiceName:               i.ServiceNameSnapshot.Length > 0 ? i.ServiceNameSnapshot : i.Description,
        DoctorId:                  i.DoctorId,
        DoctorName:                i.Doctor?.Name,
        TotalPrice:                i.TotalPrice,
        LineDiscountAmount:        i.LineDiscountAmount,
        MaterialCost:              i.MaterialCost,
        LabCost:                   i.LabCost,
        OtherDirectCost:           i.OtherDirectCost,
        NetCommissionableAmount:   i.NetCommissionableAmount,
        DoctorCommissionPercentage:i.DoctorCommissionPercentage,
        DoctorCommissionAmount:    i.DoctorCommissionAmount,
        CenterShareAmount:         i.CenterShareAmount,
        CommissionStatus:          i.CommissionStatus.ToString(),
        CommissionNotes:           i.CommissionNotes,
        HasLabOrder:               i.LabOrderId.HasValue,
        LabCostMissing:            i.LabOrderId.HasValue && i.LabCost == 0,
        IsApproved:                i.CommissionStatus == CommissionStatus.Approved || i.CommissionStatus == CommissionStatus.Paid,
        CommissionApprovedAt:      i.CommissionApprovedAt,
        CreatedAt:                 i.CreatedAt);

    private static ServiceCommissionDefaultsDto MapServiceDefaults(Domain.Entities.ClinicService svc) => new(
        ServiceId:                        svc.Id,
        DefaultMaterialCost:              svc.DefaultMaterialCost,
        DefaultMaterialCostType:          svc.DefaultMaterialCostType.ToString(),
        DefaultLabCost:                   svc.DefaultLabCost,
        DefaultDoctorCommissionPercentage:svc.DefaultDoctorCommissionPercentage,
        CommissionBaseRule:               svc.CommissionBaseRule.ToString());

    public async Task<Guid?> GetDoctorIdForUserAsync(Guid userId)
    {
        var doctor = await db.Doctors.FirstOrDefaultAsync(d => d.UserId == userId && d.IsActive);
        return doctor?.Id;
    }

    private async Task LogAuditAsync(Guid entityId, string action, Guid userId, string details)
    {
        try
        {
            var auditAction = action.Contains("Approve") ? AuditAction.Approve : AuditAction.Update;
            db.AuditLogs.Add(new AuditLog
            {
                Resource   = $"InvoiceLineItem.Commission",
                ResourceId = entityId,
                Action     = auditAction,
                UserId     = userId,
                NewData    = System.Text.Json.JsonSerializer.SerializeToDocument(new { action, details }),
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Audit log failed for {Action} on {EntityId}", action, entityId);
        }
    }
}
