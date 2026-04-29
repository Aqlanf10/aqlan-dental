using AqlanDentalPro.Application.DTOs;
using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Application.Services;

public class FinanceService(AppDbContext db, ICurrentUserService currentUser)
{
    private static readonly Dictionary<string, string> SpecialtyArNames = new()
    {
        ["orthodontics"] = "تقويم الأسنان",
        ["general"] = "طب الأسنان العام",
        ["oral_surgery"] = "جراحة الفم",
        ["periodontics"] = "أمراض اللثة",
        ["endodontics"] = "علاج العصب",
        ["prosthodontics"] = "التركيبات",
        ["pediatric"] = "طب أسنان الأطفال",
        ["cosmetic"] = "تجميل الأسنان",
        ["other"] = "أخرى"
    };

    private static readonly Dictionary<string, string> PaymentMethodArNames = new()
    {
        ["cash"] = "نقدي",
        ["bank_transfer"] = "تحويل بنكي",
        ["card"] = "بطاقة",
        ["check"] = "شيك",
        ["other"] = "أخرى"
    };

    private static readonly string[] ArabicMonths =
    [
        "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
        "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر"
    ];

    public async Task<PaginatedResponse<ContractListDto>> GetContractsAsync(int page, int pageSize, Guid? patientId, string? status)
    {
        var branchId = currentUser.BranchId;

        var query = db.Contracts
            .Include(c => c.Patient)
            .Include(c => c.Payments)
            .AsQueryable();

        if (patientId.HasValue) query = query.Where(c => c.PatientId == patientId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(c => c.Status == status);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => MapContractList(c))
            .ToListAsync();

        return new PaginatedResponse<ContractListDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
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

    public async Task<ContractDetailDto?> UpdateContractAsync(Guid id, UpdateContractRequest req)
    {
        var contract = await db.Contracts.FindAsync(id);
        if (contract == null || !contract.IsActive)
            return null;

        if (req.Specialty is not null) contract.Specialty = req.Specialty;
        if (req.TotalAmount.HasValue) contract.TotalAmount = req.TotalAmount.Value;
        if (req.DownPayment.HasValue) contract.DownPayment = req.DownPayment.Value;
        if (req.InstallmentsCount.HasValue) contract.InstallmentsCount = req.InstallmentsCount.Value;
        if (req.InstallmentAmount.HasValue) contract.InstallmentAmount = req.InstallmentAmount.Value;
        if (req.StartDate is not null) contract.StartDate = DateOnly.Parse(req.StartDate);
        if (req.DiscountAmount.HasValue) contract.DiscountAmount = req.DiscountAmount.Value;
        if (req.DiscountReason is not null) contract.DiscountReason = req.DiscountReason;
        if (req.Status is not null) contract.Status = req.Status;
        if (req.Notes is not null) contract.Notes = req.Notes;

        contract.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return await GetContractByIdAsync(id);
    }

    public async Task<bool> DeleteContractAsync(Guid id)
    {
        var contract = await db.Contracts.FindAsync(id);
        if (contract == null || !contract.IsActive)
            return false;

        contract.IsActive = false;
        contract.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<PaymentDto?> GetPaymentByIdAsync(Guid id)
    {
        var p = await db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .FirstOrDefaultAsync(p => p.Id == id);
        return p == null ? null : MapPayment(p);
    }

    public async Task<ReceiptDto?> GetReceiptByPaymentIdAsync(Guid paymentId)
    {
        var receipt = await db.Receipts
            .Include(r => r.Payment)
                .ThenInclude(p => p.Patient)
            .Include(r => r.Payment)
                .ThenInclude(p => p.Doctor)
            .Include(r => r.Payment)
                .ThenInclude(p => p.Contract)
                    .ThenInclude(c => c!.Payments)
            .FirstOrDefaultAsync(r => r.PaymentId == paymentId);

        if (receipt == null) return null;

        var payment = receipt.Payment;
        var patient = payment.Patient;
        var contract = payment.Contract;

        var centerInfo = new CenterInfoDto();

        var receiptDto = new ReceiptDto
        {
            Id = receipt.Id,
            ReceiptNumber = receipt.ReceiptNumber,
            PrintedAt = receipt.PrintedAt,
            CenterInfo = centerInfo,
            Payment = new ReceiptPaymentInfo
            {
                PaymentId = payment.Id,
                Amount = payment.Amount,
                PaymentDate = payment.PaymentDate.ToString("yyyy-MM-dd"),
                PaymentMethod = payment.PaymentMethod,
                ServiceDescription = payment.ServiceDescription,
                Specialty = payment.Specialty,
                DoctorName = payment.Doctor?.Name,
                Notes = payment.Notes
            },
            Patient = new ReceiptPatientInfo
            {
                PatientId = patient.Id,
                PatientName = patient.FirstName + " " + patient.LastName,
                PatientNumber = patient.PatientNumber,
                Phone = patient.Phone
            }
        };

        if (contract != null)
        {
            receiptDto.Contract = new ReceiptContractInfo
            {
                ContractId = contract.Id,
                TotalAmount = contract.TotalAmount,
                PaidAmount = contract.Payments.Sum(p => p.Amount),
                RemainingAmount = contract.TotalAmount - contract.DiscountAmount - contract.Payments.Sum(p => p.Amount),
                InstallmentsCount = contract.InstallmentsCount,
                InstallmentAmount = contract.InstallmentAmount
            };
        }

        return receiptDto;
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

    public async Task<PaginatedResponse<PaymentDto>> GetPaymentsAsync(int page, int pageSize, Guid? patientId)
    {
        var query = db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .AsQueryable();

        if (patientId.HasValue) query = query.Where(p => p.PatientId == patientId);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => MapPayment(p))
            .ToListAsync();

        return new PaginatedResponse<PaymentDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
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

        // Auto-complete contract if fully paid
        if (payment.ContractId.HasValue)
        {
            var contract = await db.Contracts
                .Include(c => c.Payments)
                .FirstOrDefaultAsync(c => c.Id == payment.ContractId.Value);

            if (contract != null && contract.Status == "active")
            {
                var totalPaid = contract.Payments.Sum(p => p.Amount);
                var netAmount = contract.TotalAmount - contract.DiscountAmount;
                if (totalPaid >= netAmount)
                {
                    contract.Status = "completed";
                    contract.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }
            }
        }

        await db.Entry(payment).Reference(p => p.Patient).LoadAsync();
        await db.Entry(payment).Reference(p => p.Doctor).LoadAsync();

        return MapPayment(payment);
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

        var activeContractsQuery = db.Contracts
            .Include(c => c.Payments)
            .Where(c => c.Status == "active");

        var activeContractsList = await activeContractsQuery.ToListAsync();
        var activeContracts = activeContractsList.Count;

        var totalOutstanding = activeContractsList
            .Sum(c => c.TotalAmount - c.DiscountAmount - c.Payments.Sum(p => p.Amount));

        var recentPayments = await db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.CreatedAt)
            .Take(10)
            .Select(p => MapPayment(p))
            .ToListAsync();

        // KPI calculations
        var allContracts = await db.Contracts.Include(c => c.Payments).ToListAsync();
        var totalContractsValue = allContracts.Sum(c => c.TotalAmount);
        var totalPaidValue = allContracts.Sum(c => c.Payments.Sum(p => p.Amount));
        var totalExpected = allContracts.Sum(c => c.TotalAmount - c.DiscountAmount);

        var averageContractValue = allContracts.Count > 0 ? allContracts.Average(c => c.TotalAmount) : 0;
        var collectionRate = totalExpected > 0 ? Math.Round((totalPaidValue / totalExpected) * 100, 1) : 0;

        // Overdue calculation
        var overdueContracts = 0;
        foreach (var c in activeContractsList.Where(c => c.InstallmentAmount > 0 && c.StartDate != null))
        {
            var monthsElapsed = ((today.Year - c.StartDate!.Value.Year) * 12) + (today.Month - c.StartDate.Value.Month);
            if (monthsElapsed <= 0) continue;
            var expectedPaid = c.DownPayment + (Math.Min(monthsElapsed, c.InstallmentsCount) * (c.InstallmentAmount ?? 0));
            var actualPaid = c.Payments.Sum(p => p.Amount);
            if (expectedPaid > actualPaid) overdueContracts++;
        }

        var overduePercentage = activeContracts > 0 ? Math.Round((decimal)overdueContracts / activeContracts * 100, 1) : 0;

        return new FinanceSummaryDto
        {
            TodayCollected = todayCollected,
            MonthCollected = monthCollected,
            TotalOutstanding = totalOutstanding,
            ActiveContracts = activeContracts,
            RecentPayments = recentPayments,
            AverageContractValue = averageContractValue,
            CollectionRate = collectionRate,
            OverduePercentage = overduePercentage,
            OverdueContractsCount = overdueContracts,
            TotalContractsValue = totalContractsValue,
            TotalPaidValue = totalPaidValue
        };
    }

    // ──────────────────── Finance Reports ────────────────────

    public async Task<List<FinanceBySpecialtyDto>> GetFinanceBySpecialtyAsync(DateOnly from, DateOnly to)
    {
        // Revenue by specialty from payments
        var paymentsBySpecialty = await db.Payments
            .Where(p => p.PaymentDate >= from && p.PaymentDate <= to)
            .GroupBy(p => p.Specialty ?? "other")
            .Select(g => new { Specialty = g.Key, Revenue = g.Sum(p => p.Amount), Count = g.Count() })
            .ToListAsync();

        // Contract values by specialty
        var contractsBySpecialty = await db.Contracts
            .Include(c => c.Payments)
            .Where(c => c.StartDate >= from && c.StartDate <= to)
            .ToListAsync();

        var contractGroups = contractsBySpecialty
            .GroupBy(c => c.Specialty ?? "other")
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<FinanceBySpecialtyDto>();

        foreach (var pg in paymentsBySpecialty)
        {
            var specialty = pg.Specialty;
            var hasContracts = contractGroups.TryGetValue(specialty, out var contracts);

            result.Add(new FinanceBySpecialtyDto
            {
                Specialty = specialty,
                SpecialtyAr = GetSpecialtyAr(specialty),
                PaymentCount = pg.Count,
                TotalRevenue = pg.Revenue,
                ContractCount = hasContracts ? contracts!.Count : 0,
                TotalContractValue = hasContracts ? contracts!.Sum(c => c.TotalAmount) : 0,
                TotalOutstanding = hasContracts
                    ? contracts!.Sum(c => c.TotalAmount - c.DiscountAmount - c.Payments.Sum(p => p.Amount))
                    : 0
            });
        }

        // Add specialties with contracts but no payments in range
        foreach (var cg in contractGroups)
        {
            if (paymentsBySpecialty.Any(p => p.Specialty == cg.Key)) continue;

            result.Add(new FinanceBySpecialtyDto
            {
                Specialty = cg.Key,
                SpecialtyAr = GetSpecialtyAr(cg.Key),
                PaymentCount = 0,
                TotalRevenue = 0,
                ContractCount = cg.Value.Count,
                TotalContractValue = cg.Value.Sum(c => c.TotalAmount),
                TotalOutstanding = cg.Value.Sum(c => c.TotalAmount - c.DiscountAmount - c.Payments.Sum(p => p.Amount))
            });
        }

        return result.OrderByDescending(r => r.TotalRevenue).ToList();
    }

    public async Task<List<FinanceByDoctorDto>> GetFinanceByDoctorAsync(DateOnly from, DateOnly to)
    {
        var doctors = await db.Doctors.Where(d => d.IsActive).ToListAsync();

        var result = new List<FinanceByDoctorDto>();

        foreach (var doctor in doctors)
        {
            var revenue = await db.Payments
                .Where(p => p.DoctorId == doctor.Id && p.PaymentDate >= from && p.PaymentDate <= to)
                .SumAsync(p => (decimal?)p.Amount) ?? 0;

            var paymentCount = await db.Payments
                .CountAsync(p => p.DoctorId == doctor.Id && p.PaymentDate >= from && p.PaymentDate <= to);

            var contracts = await db.Contracts
                .Include(c => c.Payments)
                .Where(c => c.Payments.Any(p => p.DoctorId == doctor.Id && p.PaymentDate >= from && p.PaymentDate <= to))
                .ToListAsync();

            result.Add(new FinanceByDoctorDto
            {
                DoctorId = doctor.Id,
                DoctorName = doctor.Name,
                Specialty = doctor.Specialty,
                PaymentCount = paymentCount,
                ContractCount = contracts.Count,
                TotalRevenue = revenue,
                TotalContractValue = contracts.Sum(c => c.TotalAmount),
                TotalOutstanding = contracts.Sum(c => c.TotalAmount - c.DiscountAmount - c.Payments.Sum(p => p.Amount))
            });
        }

        return result.OrderByDescending(r => r.TotalRevenue).ToList();
    }

    public async Task<DailyReportDto> GetDailyReportAsync(DateOnly date)
    {
        var payments = await db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .Where(p => p.PaymentDate == date)
            .ToListAsync();

        var newContracts = await db.Contracts
            .Include(c => c.Payments)
            .Where(c => c.StartDate == date)
            .ToListAsync();

        var activeContracts = await db.Contracts.CountAsync(c => c.Status == "active");

        var byMethod = payments
            .GroupBy(p => p.PaymentMethod ?? "cash")
            .Select(g => new DailyPaymentMethodBreakdown
            {
                Method = g.Key,
                MethodAr = GetPaymentMethodAr(g.Key),
                Total = g.Sum(p => p.Amount),
                Count = g.Count()
            })
            .OrderByDescending(m => m.Total)
            .ToList();

        var bySpecialty = payments
            .GroupBy(p => p.Specialty ?? "other")
            .Select(g => new DailySpecialtyBreakdown
            {
                Specialty = g.Key,
                SpecialtyAr = GetSpecialtyAr(g.Key),
                Total = g.Sum(p => p.Amount),
                Count = g.Count()
            })
            .OrderByDescending(s => s.Total)
            .ToList();

        return new DailyReportDto
        {
            Date = date.ToString("yyyy-MM-dd"),
            TotalCollected = payments.Sum(p => p.Amount),
            PaymentCount = payments.Count,
            NewContracts = newContracts.Count,
            NewContractValue = newContracts.Sum(c => c.TotalAmount),
            ActiveContracts = activeContracts,
            ByMethod = byMethod,
            BySpecialty = bySpecialty,
            Payments = payments.OrderByDescending(p => p.CreatedAt).Select(MapPayment).ToList()
        };
    }

    public async Task<MonthlyReportDto> GetMonthlyReportAsync(int year, int month)
    {
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var payments = await db.Payments
            .Where(p => p.PaymentDate >= monthStart && p.PaymentDate <= monthEnd)
            .ToListAsync();

        var newContracts = await db.Contracts
            .Include(c => c.Payments)
            .Where(c => c.StartDate >= monthStart && c.StartDate <= monthEnd)
            .ToListAsync();

        var activeContracts = await db.Contracts.CountAsync(c => c.Status == "active");

        var totalOutstanding = await db.Contracts
            .Include(c => c.Payments)
            .Where(c => c.Status == "active")
            .Select(c => c.TotalAmount - c.DiscountAmount - c.Payments.Sum(p => p.Amount))
            .SumAsync(r => (decimal?)r) ?? 0;

        // Daily breakdown
        var dailyBreakdown = payments
            .GroupBy(p => p.PaymentDate)
            .OrderBy(g => g.Key)
            .Select(g => new MonthlyDailyBreakdown
            {
                Date = g.Key.ToString("yyyy-MM-dd"),
                TotalCollected = g.Sum(p => p.Amount),
                PaymentCount = g.Count()
            })
            .ToList();

        // Fill in missing days with zero
        var allDays = new List<MonthlyDailyBreakdown>();
        for (var d = monthStart; d <= monthEnd; d = d.AddDays(1))
        {
            var existing = dailyBreakdown.FirstOrDefault(x => x.Date == d.ToString("yyyy-MM-dd"));
            allDays.Add(existing ?? new MonthlyDailyBreakdown
            {
                Date = d.ToString("yyyy-MM-dd"),
                TotalCollected = 0,
                PaymentCount = 0
            });
        }

        // By method
        var byMethod = payments
            .GroupBy(p => p.PaymentMethod ?? "cash")
            .Select(g => new DailyPaymentMethodBreakdown
            {
                Method = g.Key,
                MethodAr = GetPaymentMethodAr(g.Key),
                Total = g.Sum(p => p.Amount),
                Count = g.Count()
            })
            .OrderByDescending(m => m.Total)
            .ToList();

        // By specialty
        var bySpecialty = payments
            .GroupBy(p => p.Specialty ?? "other")
            .Select(g => new DailySpecialtyBreakdown
            {
                Specialty = g.Key,
                SpecialtyAr = GetSpecialtyAr(g.Key),
                Total = g.Sum(p => p.Amount),
                Count = g.Count()
            })
            .OrderByDescending(s => s.Total)
            .ToList();

        return new MonthlyReportDto
        {
            Year = year,
            Month = month,
            MonthName = ArabicMonths[month - 1],
            TotalCollected = payments.Sum(p => p.Amount),
            TotalPayments = payments.Count,
            NewContracts = newContracts.Count,
            NewContractValue = newContracts.Sum(c => c.TotalAmount),
            TotalOutstanding = totalOutstanding,
            ActiveContracts = activeContracts,
            DailyBreakdown = allDays,
            ByMethod = byMethod,
            BySpecialty = bySpecialty
        };
    }

    // ──────────────────── Expenses ────────────────────

    private static readonly Dictionary<string, string> ExpenseCategoryArNames = new()
    {
        ["rent"] = "إيجار",
        ["utilities"] = "خدمات (كهرباء/ماء)",
        ["salaries"] = "رواتب",
        ["supplies"] = "مستلزمات طبية",
        ["equipment"] = "أجهزة ومعدات",
        ["maintenance"] = "صيانة",
        ["marketing"] = "تسويق وإعلان",
        ["transport"] = "مواصلات",
        ["lab_fees"] = "رسوم مختبر",
        ["insurance"] = "تأمين",
        ["taxes"] = "ضرائب",
        ["other"] = "أخرى"
    };

    public async Task<PaginatedResponse<ExpenseDto>> GetExpensesAsync(int page, int pageSize, string? category, DateOnly? from, DateOnly? to)
    {
        var query = db.Set<Expense>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(e => e.Category == category);
        if (from.HasValue) query = query.Where(e => e.ExpenseDate >= from.Value);
        if (to.HasValue) query = query.Where(e => e.ExpenseDate <= to.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(e => e.ExpenseDate).ThenByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new ExpenseDto
            {
                Id = e.Id,
                Description = e.Description,
                Amount = e.Amount,
                ExpenseDate = e.ExpenseDate.ToString("yyyy-MM-dd"),
                Category = e.Category,
                CategoryAr = GetExpenseCategoryAr(e.Category),
                PaymentMethod = e.PaymentMethod,
                PaymentMethodAr = GetPaymentMethodAr(e.PaymentMethod ?? ""),
                Recipient = e.Recipient,
                Notes = e.Notes,
                CreatedAt = e.CreatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();

        return new PaginatedResponse<ExpenseDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ExpenseDto?> GetExpenseByIdAsync(Guid id)
    {
        var e = await db.Set<Expense>().FindAsync(id);
        if (e == null) return null;
        return new ExpenseDto
        {
            Id = e.Id,
            Description = e.Description,
            Amount = e.Amount,
            ExpenseDate = e.ExpenseDate.ToString("yyyy-MM-dd"),
            Category = e.Category,
            CategoryAr = GetExpenseCategoryAr(e.Category),
            PaymentMethod = e.PaymentMethod,
            PaymentMethodAr = GetPaymentMethodAr(e.PaymentMethod ?? ""),
            Recipient = e.Recipient,
            Notes = e.Notes,
            CreatedAt = e.CreatedAt.ToString("yyyy-MM-dd")
        };
    }

    public async Task<ExpenseDto> CreateExpenseAsync(CreateExpenseRequest req)
    {
        var expense = new Expense
        {
            Description = req.Description,
            Amount = req.Amount,
            ExpenseDate = !string.IsNullOrWhiteSpace(req.ExpenseDate) ? DateOnly.Parse(req.ExpenseDate) : DateOnly.FromDateTime(DateTime.Today),
            Category = req.Category,
            PaymentMethod = req.PaymentMethod,
            Recipient = req.Recipient,
            BranchId = currentUser.BranchId,
            RecordedBy = currentUser.UserId,
            Notes = req.Notes
        };

        db.Set<Expense>().Add(expense);
        await db.SaveChangesAsync();

        return (await GetExpenseByIdAsync(expense.Id))!;
    }

    public async Task<ExpenseDto?> UpdateExpenseAsync(Guid id, UpdateExpenseRequest req)
    {
        var expense = await db.Set<Expense>().FindAsync(id);
        if (expense == null || !expense.IsActive) return null;

        if (req.Description is not null) expense.Description = req.Description;
        if (req.Amount.HasValue) expense.Amount = req.Amount.Value;
        if (req.ExpenseDate is not null) expense.ExpenseDate = DateOnly.Parse(req.ExpenseDate);
        if (req.Category is not null) expense.Category = req.Category;
        if (req.PaymentMethod is not null) expense.PaymentMethod = req.PaymentMethod;
        if (req.Recipient is not null) expense.Recipient = req.Recipient;
        if (req.Notes is not null) expense.Notes = req.Notes;

        expense.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return await GetExpenseByIdAsync(id);
    }

    public async Task<bool> DeleteExpenseAsync(Guid id)
    {
        var expense = await db.Set<Expense>().FindAsync(id);
        if (expense == null || !expense.IsActive) return false;

        expense.IsActive = false;
        expense.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<ExpenseSummaryDto> GetExpenseSummaryAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var allExpenses = await db.Set<Expense>().ToListAsync();

        var todayExpenses = allExpenses.Where(e => e.ExpenseDate == today).Sum(e => e.Amount);
        var monthExpenses = allExpenses.Where(e => e.ExpenseDate >= monthStart).Sum(e => e.Amount);
        var totalExpenses = allExpenses.Sum(e => e.Amount);

        var byCategory = allExpenses
            .GroupBy(e => e.Category ?? "other")
            .Select(g => new ExpenseByCategoryDto
            {
                Category = g.Key,
                CategoryAr = GetExpenseCategoryAr(g.Key),
                Total = g.Sum(e => e.Amount),
                Count = g.Count()
            })
            .OrderByDescending(c => c.Total)
            .ToList();

        return new ExpenseSummaryDto
        {
            TodayExpenses = todayExpenses,
            MonthExpenses = monthExpenses,
            TotalExpenses = totalExpenses,
            ExpenseCount = allExpenses.Count,
            ByCategory = byCategory
        };
    }

    private static string GetExpenseCategoryAr(string? category) =>
        !string.IsNullOrWhiteSpace(category) && ExpenseCategoryArNames.TryGetValue(category, out var ar) ? ar : category ?? "أخرى";

    // ──────────────────── Helpers ────────────────────

    private static string GetSpecialtyAr(string specialty) =>
        SpecialtyArNames.TryGetValue(specialty, out var ar) ? ar : specialty;

    private static string GetPaymentMethodAr(string method) =>
        PaymentMethodArNames.TryGetValue(method, out var ar) ? ar : method;

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
