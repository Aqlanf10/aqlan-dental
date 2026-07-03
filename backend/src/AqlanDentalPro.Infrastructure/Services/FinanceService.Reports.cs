using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// QA-598: Partial class extracting read-only reporting methods from FinanceService.
/// Reduces the god-service size by moving query-only methods into a focused file.
/// Shares the same constructor, db context, and helpers as the main partial.
/// </summary>
public partial class FinanceService
{
    // ─── Reporting / Query methods (read-only) ──────────────────────────────

    public async Task<List<ContractListDto>> GetContractsAsync(int page, int pageSize, Guid? patientId, string? status)
    {
        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;

        var query = db.Contracts
            .Include(c => c.Patient)
            .Include(c => c.Payments)
            .Include(c => c.Package) // YOLO-S2: package name + color for display
            .AsQueryable();

        if (branchId.HasValue) query = query.Where(c => c.Patient.BranchId == branchId.Value);
        if (patientId.HasValue) query = query.Where(c => c.PatientId == patientId);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ContractStatus>(status, true, out var contractStatus))
            query = query.Where(c => c.Status == contractStatus);

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => MapContractList(c))
            .ToListAsync();
    }

    public async Task<ContractDetailDto?> GetContractByIdAsync(Guid id)
    {
        var c = await db.Contracts
            .Include(c => c.Patient)
            .Include(c => c.Payments)
                .ThenInclude(p => p.Doctor)
            .Include(c => c.Package) // YOLO-S2: package name + color for display
            .FirstOrDefaultAsync(c => c.Id == id);

        if (c == null) return null;

        var dto = new ContractDetailDto
        {
            Id = c.Id,
            PatientId = c.PatientId,
            PatientName = c.Patient.FirstName + " " + c.Patient.LastName,
            PatientNumber = c.Patient.PatientNumber,
            Specialty = c.Specialty,
            Currency = NormalizeCurrency(c.Currency),
            TotalAmount = c.TotalAmount,
            DownPayment = c.DownPayment,
            PaidAmount = c.Payments.Where(p => p.IsActive).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount),
            RemainingAmount = c.TotalAmount - c.DiscountAmount - c.Payments.Where(p => p.IsActive).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount),
            InstallmentsCount = c.InstallmentsCount,
            InstallmentAmount = c.InstallmentAmount,
            StartDate = c.StartDate?.ToString("yyyy-MM-dd"),
            Status = c.Status.ToString(),
            DiscountAmount = c.DiscountAmount,
            DiscountReason = c.DiscountReason,
            Notes = c.Notes,
            Payments = c.Payments.OrderByDescending(p => p.PaymentDate).Select(MapPayment).ToList(),
            // YOLO-S2: package link
            PackageId = c.PackageId,
            PackageName = c.Package?.Name,
            PackageColor = c.Package?.Color,
        };

        return dto;
    }

    public async Task<PaymentDto?> GetPaymentByIdAsync(Guid id)
    {
        var payment = await db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

        return payment == null ? null : MapPayment(payment);
    }

    public async Task<List<PaymentDto>> GetPaymentsAsync(int page, int pageSize, Guid? patientId)
    {
        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;

        var query = db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .Where(p => p.IsActive)
            .AsQueryable();

        if (branchId.HasValue) query = query.Where(p => p.BranchId == branchId.Value);
        if (patientId.HasValue) query = query.Where(p => p.PatientId == patientId);

        return await query
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => MapPayment(p))
            .ToListAsync();
    }

    public async Task<List<OverdueContractDto>> GetOverdueContractsAsync()
    {
        var today = ClinicTimeProvider.ClinicToday();

        // FIN-13: Project only the fields needed for the overdue calculation + the
        // per-contract PaidAmount (computed in SQL via correlated subquery).
        // Previously this loaded Contracts.Include(c => c.Payments).ToListAsync()
        // which fetched every Payment row for every active installment contract.
        // The month-since-StartDate calc remains in C# (EF can't translate DateOnly
        // year/month arithmetic cleanly) but operates on a tiny projected set.
        var candidates = await db.Contracts
            .Where(c => c.Status == ContractStatus.Active && c.InstallmentAmount > 0 && c.StartDate != null)
            .Select(c => new
            {
                c.Id,
                c.PatientId,
                PatientName   = c.Patient.FirstName + " " + c.Patient.LastName,
                PatientNumber = c.Patient.PatientNumber,
                Phone         = c.Patient.Phone,
                c.Specialty,
                c.TotalAmount,
                c.DiscountAmount,
                c.DownPayment,
                c.StartDate,
                c.InstallmentsCount,
                c.InstallmentAmount,
                PaidAmount = c.Payments.Where(p => p.IsActive).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount)
            })
            .ToListAsync();

        var overdue = new List<OverdueContractDto>();

        foreach (var c in candidates)
        {
            var monthsElapsed = ((today.Year - c.StartDate!.Value.Year) * 12) + (today.Month - c.StartDate.Value.Month);
            if (monthsElapsed <= 0) continue;

            var expectedPaid = c.DownPayment + (Math.Min(monthsElapsed, c.InstallmentsCount) * (c.InstallmentAmount ?? 0));
            var overdueAmt   = expectedPaid - c.PaidAmount;

            if (overdueAmt > 0)
            {
                overdue.Add(new OverdueContractDto
                {
                    ContractId     = c.Id,
                    PatientId      = c.PatientId,
                    PatientName    = c.PatientName,
                    PatientNumber  = c.PatientNumber,
                    Phone          = c.Phone,
                    Specialty      = c.Specialty,
                    TotalAmount    = c.TotalAmount,
                    PaidAmount     = c.PaidAmount,
                    OverdueAmount  = overdueAmt,
                    RemainingAmount= c.TotalAmount - c.DiscountAmount - c.PaidAmount,
                    MonthsElapsed  = monthsElapsed,
                    StartDate      = c.StartDate?.ToString("yyyy-MM-dd")
                });
            }
        }

        return overdue.OrderByDescending(o => o.OverdueAmount).ToList();
    }
}
