using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// Contract service — extracted from <c>FinanceService</c> as part of TD-021 PR A3.
/// Owns the read + clean-write side of the contracts cluster.
///
/// This is a pure code move (no logic change). The previous implementation lived in
/// <c>FinanceService.GetContractsAsync</c>, <c>GetContractByIdAsync</c>, and
/// <c>UpdateContractAsync</c>.
///
/// <see cref="MapContractList"/> was a private static helper in FinanceService and
/// moved here as a private static helper (only used by GetContractsAsync).
///
/// The entangled contract methods (<c>CreateContractAsync</c>,
/// <c>UpdateContractStatusAsync</c>, <c>TryReconcileContractStatusAsync</c>) stay
/// in FinanceService because they depend on payment-side helpers. They will move
/// naturally when PR A4 extracts the PaymentService cluster.
/// </summary>
public class ContractService(
    AppDbContext db,
    ICurrentUserService currentUser) : IContractService
{
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
            Currency = FinanceMappers.NormalizeCurrency(c.Currency),
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
            Payments = c.Payments.OrderByDescending(p => p.PaymentDate).Select(FinanceMappers.MapPayment).ToList(),
            // YOLO-S2: package link
            PackageId = c.PackageId,
            PackageName = c.Package?.Name,
            PackageColor = c.Package?.Color,
        };

        return dto;
    }

    public async Task<ContractDetailDto?> UpdateContractAsync(Guid id, UpdateContractRequest req)
    {
        var contract = await db.Contracts.FindAsync(id);
        if (contract == null) return null;

        // Phase 0B: Validate that TotalAmount is not reduced below what's already been paid
        {
            var alreadyPaid = await db.Payments
                .Where(p => p.ContractId == id && p.IsActive)
                .SumAsync(p => (decimal?)(p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount)) ?? 0m;
            if (req.TotalAmount < alreadyPaid)
                throw new ArgumentException($"لا يمكن تقليل إجمالي العقد ({req.TotalAmount:N0} ر.ي) إلى أقل من المبلغ المدفوع فعلياً ({alreadyPaid:N0} ر.ي).");
        }

        contract.Specialty        = req.Specialty;
        contract.Currency         = FinanceMappers.NormalizeCurrency(req.Currency ?? contract.Currency);
        contract.TotalAmount      = req.TotalAmount;
        contract.InstallmentsCount = req.InstallmentsCount;
        contract.InstallmentAmount = req.InstallmentAmount;
        contract.StartDate        = req.StartDate != null ? DateOnly.Parse(req.StartDate) : contract.StartDate;
        contract.DiscountAmount   = req.DiscountAmount;
        contract.DiscountReason   = req.DiscountReason;
        contract.Notes            = req.Notes;
        contract.UpdatedAt        = DateTime.UtcNow;

        // YOLO-S2: update the package link. Resolve Guid.Empty → null ("clear").
        // null on the request body leaves the existing value unchanged (PATCH semantics).
        if (req.PackageId.HasValue)
        {
            var newPkgId = req.PackageId.Value == Guid.Empty ? null : (Guid?)req.PackageId.Value;
            if (newPkgId.HasValue && newPkgId != contract.PackageId)
            {
                var pkgExists = await db.TreatmentPackages.AnyAsync(p => p.Id == newPkgId.Value && p.IsActive);
                if (!pkgExists)
                    throw new ArgumentException("الباقة المحددة غير موجودة أو معطّلة");
            }
            contract.PackageId = newPkgId;
        }

        await db.SaveChangesAsync();
        return await GetContractByIdAsync(id);
    }

    // ─── Private helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Maps a Contract entity (with Patient + Payments + Package navigations loaded)
    /// to its list DTO. Uses <see cref="FinanceMappers.NormalizeCurrency"/> for the
    /// currency code. Moved from FinanceService as a private static helper (only
    /// used by GetContractsAsync).
    /// </summary>
    private static ContractListDto MapContractList(Contract c) => new()
    {
        Id = c.Id,
        PatientId = c.PatientId,
        PatientName = c.Patient.FirstName + " " + c.Patient.LastName,
        PatientNumber = c.Patient.PatientNumber,
        Specialty = c.Specialty,
        Currency = FinanceMappers.NormalizeCurrency(c.Currency),
        TotalAmount = c.TotalAmount,
        DownPayment = c.DownPayment,
        PaidAmount = c.Payments.Where(p => p.IsActive).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount),
        RemainingAmount = c.TotalAmount - c.DiscountAmount - c.Payments.Where(p => p.IsActive).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount),
        InstallmentsCount = c.InstallmentsCount,
        InstallmentAmount = c.InstallmentAmount,
        StartDate = c.StartDate?.ToString("yyyy-MM-dd"),
        Status = c.Status.ToString(),
        // YOLO-S2: package link (display-only — pricing still driven by TotalAmount)
        PackageId = c.PackageId,
        PackageName = c.Package?.Name,
        PackageColor = c.Package?.Color,
    };
}
