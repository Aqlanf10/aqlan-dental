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
        var receiptNumber = $"RCP-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";

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

        // Notify accountants and admins
        _ = Task.Run(async () =>
        {
            try
            {
                var patientName = dto.PatientName ?? "مريض";
                var amountStr = req.Amount.ToString("N0");
                var msg = $"تم استلام دفعة {amountStr} ر.ي من {patientName}";
                await notifications.NotifyRoleAsync("Accountant", "payment", "دفعة جديدة", msg, "Payment", payment.Id);
                await notifications.NotifyRoleAsync("Admin", "payment", "دفعة جديدة", msg, "Payment", payment.Id);
            }
            catch { /* non-blocking */ }
        });

        return dto;
    }

    public async Task<ContractDetailDto?> UpdateContractAsync(Guid id, UpdateContractRequest req)
    {
        var contract = await db.Contracts.FindAsync(id);
        if (contract == null) return null;

        contract.Specialty        = req.Specialty;
        contract.TotalAmount      = req.TotalAmount;
        contract.InstallmentsCount = req.InstallmentsCount;
        contract.InstallmentAmount = req.InstallmentAmount;
        contract.StartDate        = req.StartDate != null ? DateOnly.Parse(req.StartDate) : contract.StartDate;
        contract.DiscountAmount   = req.DiscountAmount;
        contract.DiscountReason   = req.DiscountReason;
        contract.Notes            = req.Notes;
        contract.UpdatedAt        = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return await GetContractByIdAsync(id);
    }

    public async Task<ContractDetailDto?> UpdateContractStatusAsync(Guid id, string status)
    {
        var allowed = new[] { "active", "completed", "cancelled" };
        if (!allowed.Contains(status)) return null;

        var contract = await db.Contracts.FindAsync(id);
        if (contract == null) return null;

        contract.Status    = status;
        contract.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return await GetContractByIdAsync(id);
    }

    public async Task<AccountStatementDto?> GetAccountStatementAsync(Guid patientId)
    {
        var patient = await db.Patients.FindAsync(patientId);
        if (patient == null) return null;

        var contracts = await db.Contracts
            .Include(c => c.Payments)
            .Where(c => c.PatientId == patientId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        var recentPayments = await db.Payments
            .Include(p => p.Doctor)
            .Where(p => p.PatientId == patientId)
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.CreatedAt)
            .Take(20)
            .ToListAsync();

        return new AccountStatementDto
        {
            PatientId        = patientId,
            PatientName      = patient.FirstName + " " + patient.LastName,
            PatientNumber    = patient.PatientNumber,
            TotalContracted  = contracts.Sum(c => c.TotalAmount),
            TotalDiscounts   = contracts.Sum(c => c.DiscountAmount),
            TotalPaid        = contracts.Sum(c => c.Payments.Sum(p => p.Amount)),
            TotalRemaining   = contracts.Sum(c => c.TotalAmount - c.DiscountAmount - c.Payments.Sum(p => p.Amount)),
            ActiveContracts  = contracts.Count(c => c.Status == "active"),
            CompletedContracts = contracts.Count(c => c.Status == "completed"),
            Contracts        = contracts.Select(c => new ContractStatementDto
            {
                Id              = c.Id,
                Specialty       = c.Specialty,
                TotalAmount     = c.TotalAmount,
                DiscountAmount  = c.DiscountAmount,
                PaidAmount      = c.Payments.Sum(p => p.Amount),
                RemainingAmount = c.TotalAmount - c.DiscountAmount - c.Payments.Sum(p => p.Amount),
                StartDate       = c.StartDate?.ToString("yyyy-MM-dd"),
                Status          = c.Status,
                InstallmentsCount  = c.InstallmentsCount,
                InstallmentAmount  = c.InstallmentAmount
            }).ToList(),
            RecentPayments = recentPayments.Select(p =>
            {
                p.Patient = patient;
                return MapPayment(p);
            }).ToList()
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
        DoctorName = p.Doctor?.Name,
        ReceiptNumber = p.ReceiptNumber,
        Notes = p.Notes
    };
}
