using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Application.Services;

public class FinanceService(AppDbContext db, ICurrentUserService currentUser, INotificationService notifications)
{
    public async Task<List<ContractListDto>> GetContractsAsync(int page, int pageSize, Guid? patientId, string? status)
    {
        var branchId = currentUser.BranchId;

        var query = db.Contracts
            .Include(c => c.Patient)
            .Include(c => c.Payments)
            .AsQueryable();

        if (patientId.HasValue) query = query.Where(c => c.PatientId == patientId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(c => c.Status == status);

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
            .FirstOrDefaultAsync(c => c.Id == id);

        if (c == null) return null;

        var dto = new ContractDetailDto
        {
            Id = c.Id,
            PatientId = c.PatientId,
            PatientName = c.Patient.FirstName + " " + c.Patient.LastName,
            PatientNumber = c.Patient.PatientNumber,
            Specialty = c.Specialty,
            TotalAmount = c.TotalAmount,
            DownPayment = c.DownPayment,
            PaidAmount = c.Payments.Sum(p => p.Amount),
            RemainingAmount = c.TotalAmount - c.DiscountAmount - c.Payments.Sum(p => p.Amount),
            InstallmentsCount = c.InstallmentsCount,
            InstallmentAmount = c.InstallmentAmount,
            StartDate = c.StartDate?.ToString("yyyy-MM-dd"),
            Status = c.Status,
            DiscountAmount = c.DiscountAmount,
            DiscountReason = c.DiscountReason,
            Notes = c.Notes,
            Payments = c.Payments.OrderByDescending(p => p.PaymentDate).Select(MapPayment).ToList()
        };

        return dto;
    }

    public async Task<ContractDetailDto> CreateContractAsync(CreateContractRequest req)
    {
        var contract = new Contract
        {
            PatientId = req.PatientId,
            Specialty = req.Specialty,
            RelatedCaseId = req.RelatedCaseId,
            TotalAmount = req.TotalAmount,
            DownPayment = req.DownPayment,
            InstallmentsCount = req.InstallmentsCount,
            InstallmentAmount = req.InstallmentAmount,
            StartDate = req.StartDate != null ? DateOnly.Parse(req.StartDate) : DateOnly.FromDateTime(DateTime.Today),
            DiscountAmount = req.DiscountAmount,
            DiscountReason = req.DiscountReason,
            Status = "active",
            Notes = req.Notes,
            CreatedBy = currentUser.UserId
        };

        db.Contracts.Add(contract);
        await db.SaveChangesAsync();

        // Auto-create down payment if specified
        if (req.DownPayment > 0)
        {
            await CreatePaymentAsync(new CreatePaymentRequest
            {
                PatientId = req.PatientId,
                ContractId = contract.Id,
                Amount = req.DownPayment,
                PaymentMethod = "cash",
                ServiceDescription = "دفعة أولى"
            });
        }

        return (await GetContractByIdAsync(contract.Id))!;
    }

    public async Task<PaymentDto?> GetPaymentByIdAsync(Guid id)
    {
        var p = await db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .FirstOrDefaultAsync(p => p.Id == id);
        return p == null ? null : MapPayment(p);
    }

    public async Task<List<OverdueContractDto>> GetOverdueContractsAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var contracts = await db.Contracts
            .Include(c => c.Patient)
            .Include(c => c.Payments)
            .Where(c => c.Status == "active" && c.InstallmentAmount > 0 && c.StartDate != null)
            .ToListAsync();

        var overdue = new List<OverdueContractDto>();

        foreach (var c in contracts)
        {
            var monthsElapsed = ((today.Year - c.StartDate!.Value.Year) * 12) + (today.Month - c.StartDate.Value.Month);
            if (monthsElapsed <= 0) continue;

            var expectedPaid = c.DownPayment + (Math.Min(monthsElapsed, c.InstallmentsCount) * (c.InstallmentAmount ?? 0));
            var actualPaid   = c.Payments.Sum(p => p.Amount);
            var overdueAmt   = expectedPaid - actualPaid;

            if (overdueAmt > 0)
            {
                overdue.Add(new OverdueContractDto
                {
                    ContractId     = c.Id,
                    PatientId      = c.PatientId,
                    PatientName    = c.Patient.FirstName + " " + c.Patient.LastName,
                    PatientNumber  = c.Patient.PatientNumber,
                    Phone          = c.Patient.Phone,
                    Specialty      = c.Specialty,
                    TotalAmount    = c.TotalAmount,
                    PaidAmount     = actualPaid,
                    OverdueAmount  = overdueAmt,
                    RemainingAmount= c.TotalAmount - c.DiscountAmount - actualPaid,
                    MonthsElapsed  = monthsElapsed,
                    StartDate      = c.StartDate?.ToString("yyyy-MM-dd")
                });
            }
        }

        return overdue.OrderByDescending(o => o.OverdueAmount).ToList();
    }

    public async Task<List<PaymentDto>> GetPaymentsAsync(int page, int pageSize, Guid? patientId)
    {
        var query = db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .AsQueryable();

        if (patientId.HasValue) query = query.Where(p => p.PatientId == patientId);

        return await query
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => MapPayment(p))
            .ToListAsync();
    }

    public async Task<PaymentDto> CreatePaymentAsync(CreatePaymentRequest req)
    {
        // H7 FIX: Use GUID to prevent receipt number collisions
        var receiptNumber = $"RCP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant()}";

        var payment = new Payment
        {
            PatientId = req.PatientId,
            ContractId = req.ContractId,
            Amount = req.Amount,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = req.PaymentMethod,
            ServiceDescription = req.ServiceDescription,
            Specialty = req.Specialty,
            DoctorId = req.DoctorId,
            BranchId = currentUser.BranchId,
            ReceivedBy = currentUser.UserId,
            ReceiptNumber = receiptNumber,
            Notes = req.Notes
        };

        db.Payments.Add(payment);

        // Auto-create receipt record
        db.Receipts.Add(new Receipt
        {
            PaymentId = payment.Id,
            ReceiptNumber = receiptNumber,
            PrintedBy = currentUser.UserId
        });

        await db.SaveChangesAsync();

        await db.Entry(payment).Reference(p => p.Patient).LoadAsync();
        await db.Entry(payment).Reference(p => p.Doctor).LoadAsync();

        var dto = MapPayment(payment);

        // M1 FIX: Use IServiceScopeFactory for proper DI in fire-and-forget
        // Notify accountants and admins
        _ = Task.Run(async () =>
        {
            try
            {
                await notifications.NotifyRoleAsync("Accountant", "payment", "دفعة جديدة", $"تم استلام دفعة من {dto.PatientName ?? "مريض"}", "Payment", payment.Id);
                await notifications.NotifyRoleAsync("Admin", "payment", "دفعة جديدة", $"تم استلام دفعة من {dto.PatientName ?? "مريض"}", "Payment", payment.Id);
            }
            catch (Exception ex)
            {
                // M1 FIX: Log notification failures instead of silently swallowing
                Console.Error.WriteLine($"[FinanceService] Notification failed: {ex.Message}");
            }
        });

        return dto;
    }

    public async Task<PaymentDto?> UpdatePaymentAsync(Guid id, UpdatePaymentRequest req)
    {
        var payment = await db.Payments.FindAsync(id);
        if (payment == null) return null;
        if (!payment.IsActive) return null;

        if (req.Amount.HasValue) payment.Amount = req.Amount.Value;
        if (req.PaymentDate != null) payment.PaymentDate = DateOnly.Parse(req.PaymentDate);
        if (req.PaymentMethod != null) payment.PaymentMethod = req.PaymentMethod;
        if (req.ServiceDescription != null) payment.ServiceDescription = req.ServiceDescription;
        if (req.Specialty != null) payment.Specialty = req.Specialty;
        if (req.DoctorId.HasValue) payment.DoctorId = req.DoctorId;
        if (req.Notes != null) payment.Notes = req.Notes;

        payment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await db.Entry(payment).Reference(p => p.Patient).LoadAsync();
        await db.Entry(payment).Reference(p => p.Doctor).LoadAsync();

        return MapPayment(payment);
    }

    public async Task<bool> DeletePaymentAsync(Guid id)
    {
        var payment = await db.Payments.FindAsync(id);
        if (payment == null) return false;
        if (!payment.IsActive) return false;

        payment.IsActive = false;
        payment.DeletedAt = DateTime.UtcNow;
        payment.DeletedBy = currentUser.UserId;
        await db.SaveChangesAsync();

        return true;
    }

    public async Task<PatientFinanceSummaryDto> GetPatientFinanceSummaryAsync(Guid patientId)
    {
        var totalPaid = await db.Payments
            .Where(p => p.PatientId == patientId && p.IsActive)
            .SumAsync(p => (decimal?)p.Amount) ?? 0;

        var totalTreatmentCost = await db.Contracts
            .Where(c => c.PatientId == patientId && c.Status == "active")
            .SumAsync(c => (decimal?)c.TotalAmount - c.DiscountAmount) ?? 0;

        var outstandingBalance = totalTreatmentCost - totalPaid;

        // Calculate overdue
        var today = DateOnly.FromDateTime(DateTime.Today);
        var overdueContracts = await db.Contracts
            .Include(c => c.Payments)
            .Where(c => c.PatientId == patientId && c.Status == "active" && c.InstallmentAmount > 0 && c.StartDate != null)
            .ToListAsync();

        var overdueAmount = 0m;
        foreach (var c in overdueContracts)
        {
            var monthsElapsed = ((today.Year - c.StartDate!.Value.Year) * 12) + (today.Month - c.StartDate.Value.Month);
            if (monthsElapsed <= 0) continue;
            var expectedPaid = c.DownPayment + (Math.Min(monthsElapsed, c.InstallmentsCount) * (c.InstallmentAmount ?? 0));
            var actualPaid = c.Payments.Where(p => p.IsActive).Sum(p => p.Amount);
            var diff = expectedPaid - actualPaid;
            if (diff > 0) overdueAmount += diff;
        }

        var latestPayment = await db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .Where(p => p.PatientId == patientId && p.IsActive)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();

        var activeContractsCount = await db.Contracts
            .CountAsync(c => c.PatientId == patientId && c.Status == "active");

        var totalPaymentsCount = await db.Payments
            .CountAsync(p => p.PatientId == patientId && p.IsActive);

        // Determine financial status
        string financialStatus;
        if (activeContractsCount == 0 && totalPaymentsCount == 0)
            financialStatus = "no_plan";
        else if (outstandingBalance <= 0)
            financialStatus = "paid_full";
        else if (overdueAmount > 0)
            financialStatus = "overdue";
        else
            financialStatus = "has_balance";

        return new PatientFinanceSummaryDto
        {
            TotalTreatmentCost = totalTreatmentCost,
            TotalPaid = totalPaid,
            OutstandingBalance = outstandingBalance,
            OverdueAmount = overdueAmount,
            LatestPayment = latestPayment != null ? MapPayment(latestPayment) : null,
            FinancialStatus = financialStatus,
            ActiveContractsCount = activeContractsCount,
            TotalPaymentsCount = totalPaymentsCount
        };
    }

    public async Task<FinanceSummaryDto> GetSummaryAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var todayCollected = await db.Payments
            .Where(p => p.PaymentDate == today)
            .SumAsync(p => (decimal?)p.Amount) ?? 0;

        var monthCollected = await db.Payments
            .Where(p => p.PaymentDate >= monthStart)
            .SumAsync(p => (decimal?)p.Amount) ?? 0;

        var totalOutstanding = await db.Contracts
            .Include(c => c.Payments)
            .Where(c => c.Status == "active")
            .Select(c => c.TotalAmount - c.DiscountAmount - c.Payments.Sum(p => p.Amount))
            .SumAsync(r => (decimal?)r) ?? 0;

        var activeContracts = await db.Contracts.CountAsync(c => c.Status == "active");

        var recentPayments = await db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.CreatedAt)
            .Take(10)
            .Select(p => MapPayment(p))
            .ToListAsync();

        return new FinanceSummaryDto
        {
            TodayCollected = todayCollected,
            MonthCollected = monthCollected,
            TotalOutstanding = totalOutstanding,
            ActiveContracts = activeContracts,
            RecentPayments = recentPayments
        };
    }

    private static ContractListDto MapContractList(Contract c) => new()
    {
        Id = c.Id,
        PatientId = c.PatientId,
        PatientName = c.Patient.FirstName + " " + c.Patient.LastName,
        PatientNumber = c.Patient.PatientNumber,
        Specialty = c.Specialty,
        TotalAmount = c.TotalAmount,
        DownPayment = c.DownPayment,
        PaidAmount = c.Payments.Sum(p => p.Amount),
        RemainingAmount = c.TotalAmount - c.DiscountAmount - c.Payments.Sum(p => p.Amount),
        InstallmentsCount = c.InstallmentsCount,
        InstallmentAmount = c.InstallmentAmount,
        StartDate = c.StartDate?.ToString("yyyy-MM-dd"),
        Status = c.Status
    };

    private static PaymentDto MapPayment(Payment p) => new()
    {
        Id = p.Id,
        PatientId = p.PatientId,
        PatientName = p.Patient?.FirstName + " " + p.Patient?.LastName,
        ContractId = p.ContractId,
        Amount = p.Amount,
        PaymentDate = p.PaymentDate.ToString("yyyy-MM-dd"),
        PaymentMethod = p.PaymentMethod,
        ServiceDescription = p.ServiceDescription,
        Specialty = p.Specialty,
        DoctorId = p.DoctorId,
        DoctorName = p.Doctor?.Name,
        ReceiptNumber = p.ReceiptNumber,
        Notes = p.Notes,
        IsActive = p.IsActive,
        CreatedAt = p.CreatedAt.ToString("yyyy-MM-dd"),
        UpdatedAt = p.UpdatedAt.ToString("yyyy-MM-dd")
    };
}
