using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

public sealed class GenerateSalaryRequest
{
    public Guid EmployeeId { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public decimal? BaseSalary { get; init; }
    public decimal Deductions { get; init; }
    public decimal Advances { get; init; }
    public decimal Bonuses { get; init; }
    public string? Notes { get; init; }
}

public sealed class PaySalaryRequest
{
    public string? PaymentMethod { get; init; }
    public string? Notes { get; init; }
}

[ApiController]
[Route("api/salaries")]
[Authorize]
public class SalaryController(AppDbContext db, IJournalEntryService journalEntryService) : ControllerBase
{
    /// <summary>
    /// Get salary records with filters
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? employeeId,
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] Guid? branchId,
        [FromQuery] bool? paid,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = db.SalaryRecords
            .Include(s => s.Employee)
            .AsQueryable();

        if (employeeId.HasValue)
            query = query.Where(s => s.EmployeeId == employeeId.Value);

        if (year.HasValue)
            query = query.Where(s => s.Year == year.Value);

        if (month.HasValue)
            query = query.Where(s => s.Month == month.Value);

        if (branchId.HasValue)
            query = query.Where(s => s.Employee.BranchId == branchId.Value);

        if (paid.HasValue)
            query = query.Where(s => s.PaidAt.HasValue == paid.Value);

        var total = await query.CountAsync();

        var records = await query
            .OrderByDescending(s => s.Year)
            .ThenByDescending(s => s.Month)
            .ThenBy(s => s.Employee.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id,
                s.EmployeeId,
                EmployeeName = s.Employee.FullName,
                s.Year,
                s.Month,
                s.BaseSalary,
                s.Deductions,
                s.Advances,
                s.Bonuses,
                s.NetSalary,
                s.PaidAt,
                s.PaymentMethod,
                s.Notes,
                s.CreatedAt,
            })
            .ToListAsync();

        return Ok(new { data = records, total, page, pageSize, totalPages = (int)Math.Ceiling(total / (double)pageSize) });
    }

    /// <summary>
    /// Generate monthly salary for an employee
    /// </summary>
    [HttpPost("generate")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GenerateSalary([FromBody] GenerateSalaryRequest req)
    {
        var employee = await db.Employees.FindAsync(req.EmployeeId);
        if (employee is null)
            return BadRequest(new { message = "الموظف غير موجود" });

        // Check if salary already generated for this month
        var exists = await db.SalaryRecords
            .AnyAsync(s => s.EmployeeId == req.EmployeeId && s.Year == req.Year && s.Month == req.Month);
        if (exists)
            return Conflict(new { message = "تم إنشاء كشف راتب لهذا الشهر مسبقاً" });

        // Calculate total advances for this month
        var totalAdvances = await db.AdvancePayments
            .Where(a => a.EmployeeId == req.EmployeeId
                && a.Status == RequestStatus.Approved
                && a.DeductFromMonth == req.Month
                && a.DeductFromYear == req.Year
                && !a.IsDeducted)
            .SumAsync(a => a.Amount);

        var advances = req.Advances > 0 ? req.Advances : totalAdvances;
        var baseSalary = req.BaseSalary ?? employee.BaseSalary ?? 0;
        var netSalary = baseSalary - req.Deductions - advances + req.Bonuses;

        var salaryRecord = new SalaryRecord
        {
            EmployeeId = req.EmployeeId,
            Year = req.Year,
            Month = req.Month,
            BaseSalary = baseSalary,
            Deductions = req.Deductions,
            Advances = advances,
            Bonuses = req.Bonuses,
            NetSalary = netSalary,
            Notes = req.Notes?.Trim(),
        };

        db.SalaryRecords.Add(salaryRecord);

        // Mark advances as deducted
        var pendingAdvances = await db.AdvancePayments
            .Where(a => a.EmployeeId == req.EmployeeId
                && a.Status == RequestStatus.Approved
                && a.DeductFromMonth == req.Month
                && a.DeductFromYear == req.Year
                && !a.IsDeducted)
            .ToListAsync();

        foreach (var advance in pendingAdvances)
        {
            advance.IsDeducted = true;
        }

        await db.SaveChangesAsync();

        return Created($"/api/salaries/{salaryRecord.Id}", new
        {
            salaryRecord.Id,
            salaryRecord.EmployeeId,
            EmployeeName = employee.FullName,
            salaryRecord.Year,
            salaryRecord.Month,
            salaryRecord.BaseSalary,
            salaryRecord.Deductions,
            salaryRecord.Advances,
            salaryRecord.Bonuses,
            salaryRecord.NetSalary,
            salaryRecord.Notes,
        });
    }

    /// <summary>
    /// Generate salary for all active employees for a month
    /// </summary>
    [HttpPost("generate-all")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GenerateAllSalaries(
        [FromBody] GenerateAllSalariesRequest req)
    {
        var employees = await db.Employees
            .Where(e => e.IsActive && e.BaseSalary.HasValue)
            .ToListAsync();

        var generated = 0;
        var skipped = 0;
        var errors = new List<string>();

        foreach (var employee in employees)
        {
            var exists = await db.SalaryRecords
                .AnyAsync(s => s.EmployeeId == employee.Id && s.Year == req.Year && s.Month == req.Month);
            if (exists)
            {
                skipped++;
                continue;
            }

            var totalAdvances = await db.AdvancePayments
                .Where(a => a.EmployeeId == employee.Id
                    && a.Status == RequestStatus.Approved
                    && a.DeductFromMonth == req.Month
                    && a.DeductFromYear == req.Year
                    && !a.IsDeducted)
                .SumAsync(a => a.Amount);

            var baseSalary = employee.BaseSalary!.Value;
            var netSalary = baseSalary - totalAdvances;

            var salaryRecord = new SalaryRecord
            {
                EmployeeId = employee.Id,
                Year = req.Year,
                Month = req.Month,
                BaseSalary = baseSalary,
                Deductions = 0,
                Advances = totalAdvances,
                Bonuses = 0,
                NetSalary = netSalary,
            };

            db.SalaryRecords.Add(salaryRecord);

            // Mark advances as deducted
            var pendingAdvances = await db.AdvancePayments
                .Where(a => a.EmployeeId == employee.Id
                    && a.Status == RequestStatus.Approved
                    && a.DeductFromMonth == req.Month
                    && a.DeductFromYear == req.Year
                    && !a.IsDeducted)
                .ToListAsync();

            foreach (var advance in pendingAdvances)
            {
                advance.IsDeducted = true;
            }

            generated++;
        }

        await db.SaveChangesAsync();

        return Ok(new
        {
            message = $"تم إنشاء كشوف الرواتب: {generated} كشف، تم تخطي: {skipped}",
            generated,
            skipped,
        });
    }

    /// <summary>
    /// Mark salary as paid — dual-writes CashFlowTransaction + JournalEntry atomically.
    /// Rejects if the employee's branch cannot be resolved (never writes Guid.Empty).
    /// </summary>
    [HttpPut("{id:guid}/pay")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> PaySalary(Guid id, [FromBody] PaySalaryRequest req)
    {
        var salary = await db.SalaryRecords.FindAsync(id);
        if (salary is null)
            return NotFound(new { message = "كشف الراتب غير موجود" });

        if (salary.PaidAt.HasValue)
            return BadRequest(new { message = "تم صرف هذا الراتب مسبقاً" });

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var paidByUid = Guid.TryParse(userId, out var uid) ? uid : Guid.Empty;

        // Fetch employee details — reject if branch is unknown
        var employee = await db.Employees.FindAsync(salary.EmployeeId);
        if (employee is null)
            return BadRequest(new { message = "الموظف غير موجود" });

        var branchId = employee.BranchId ?? Guid.Empty;
        if (branchId == Guid.Empty)
            return BadRequest(new { message = "عذراً، لا يمكن صرف الراتب — الفرع غير محدد للموظف. تواصل مع الإدارة." });

        // Resolve treasury for JournalEntry posting
        var treasury = await db.Treasuries
            .FirstOrDefaultAsync(t => t.BranchId == branchId && t.IsActive);
        if (treasury == null)
            return BadRequest(new { message = "عذراً، لا توجد خزينة للفرع. تواصل مع الإدارة لإنشاء خزينة." });

        // Resolve cashier session for cash payments
        CashierSession? activeSession = null;
        var paymentMethod = req.PaymentMethod?.Trim() ?? "cash";
        if (string.Equals(paymentMethod, "cash", StringComparison.OrdinalIgnoreCase))
        {
            activeSession = await db.CashierSessions
                .FirstOrDefaultAsync(s => s.CashierId == paidByUid && s.Status == SessionStatus.Open && s.IsActive);
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            salary.PaidAt = DateTime.UtcNow;
            salary.PaidBy = paidByUid == Guid.Empty ? null : paidByUid;
            salary.PaymentMethod = paymentMethod;
            salary.Notes = req.Notes?.Trim();

            // Dual-write: CashFlowTransaction (transitional)
            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            var nextSeq = await db.CashFlowTransactions.CountAsync(t => t.Category == FinancialCategory.SalaryPayment) + 1;
            var cashflow = new CashFlowTransaction
            {
                TransactionNumber = $"TX-{datePart}-SAL-{nextSeq:D3}",
                Type = TransactionType.Outflow,
                Category = FinancialCategory.SalaryPayment,
                Amount = salary.NetSalary,
                PaymentMethod = paymentMethod,
                TransactionDate = DateOnly.FromDateTime(DateTime.Today),
                ReferenceId = salary.Id,
                ReferenceNumber = $"SAL-{salary.Year}{salary.Month:D2}",
                Description = $"صرف راتب الموظف: {employee.FullName} لشهر {salary.Month}/{salary.Year}",
                PerformedBy = paidByUid,
                BranchId = branchId,
                TreasuryId = treasury.Id,
                CashierSessionId = activeSession?.Id
            };
            db.CashFlowTransactions.Add(cashflow);

            // JournalEntry: Debit Expense / Credit Treasury
            var je = await journalEntryService.CreateEntryAsync(
                documentType: FinancialDocumentType.SalaryPayment,
                financialDocumentId: salary.Id,
                description: $"صرف راتب: {employee.FullName} — {salary.Month}/{salary.Year}",
                entryDate: DateOnly.FromDateTime(DateTime.Today),
                branchId: branchId,
                performedBy: paidByUid == Guid.Empty ? Guid.Empty : paidByUid,
                cashierSessionId: activeSession?.Id,
                treasuryId: treasury.Id,
                lines: new[]
                {
                    (JournalAccountType.Expense, salary.Id, salary.NetSalary, 0m, (string?)$"راتب: {employee.FullName}"),
                    (JournalAccountType.Treasury, treasury.Id, 0m, salary.NetSalary, (string?)$"سداد من: {treasury.Name}")
                });
            je.IsPosted = true;
            je.PostedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(new { message = "تم تسجيل صرف الراتب وترحيله للأستاذ العام بنجاح" });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Update salary record
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Update(Guid id, [FromBody] GenerateSalaryRequest req)
    {
        var salary = await db.SalaryRecords.FindAsync(id);
        if (salary is null)
            return NotFound(new { message = "كشف الراتب غير موجود" });

        if (salary.PaidAt.HasValue)
            return BadRequest(new { message = "لا يمكن تعديل كشف راتب تم صرفه" });

        var baseSalary = req.BaseSalary ?? salary.BaseSalary;
        salary.BaseSalary = baseSalary;
        salary.Deductions = req.Deductions;
        salary.Advances = req.Advances;
        salary.Bonuses = req.Bonuses;
        salary.NetSalary = baseSalary - req.Deductions - req.Advances + req.Bonuses;
        salary.Notes = req.Notes?.Trim();

        await db.SaveChangesAsync();

        return Ok(new { message = "تم تحديث كشف الراتب بنجاح" });
    }

    /// <summary>
    /// Delete salary record
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var salary = await db.SalaryRecords.FindAsync(id);
        if (salary is null)
            return NotFound(new { message = "كشف الراتب غير موجود" });

        if (salary.PaidAt.HasValue)
            return BadRequest(new { message = "لا يمكن حذف كشف راتب تم صرفه" });

        salary.IsActive = false;
        salary.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { message = "تم حذف كشف الراتب بنجاح" });
    }
}

public sealed class GenerateAllSalariesRequest
{
    public int Year { get; init; }
    public int Month { get; init; }
}
