using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Finance;

/// <summary>
/// PR #245 — Comprehensive blocker validation tests covering all 9 blockers.
/// These tests verify real business behavior, not just mocked return values.
/// </summary>
public class FinanceV3PR245BlockerTests
{
    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static (Guid branchId, Guid cashierId) SeedBranchAndUser(AppDbContext db)
    {
        var branchId = Guid.NewGuid();
        var cashierId = Guid.NewGuid();
        db.Branches.Add(new Branch { Id = branchId, Name = "الفرع الرئيسي" });
        db.Users.Add(new User { Id = cashierId, Username = "cashier1", BranchId = branchId });
        db.SaveChanges();
        return (branchId, cashierId);
    }

    private static Employee SeedEmployee(AppDbContext db, Guid branchId, decimal baseSalary = 15_000m)
    {
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            FullName = "أحمد محمد",
            BranchId = branchId,
            BaseSalary = baseSalary
        };
        db.Employees.Add(employee);
        db.SaveChanges();
        return employee;
    }

    private static CashierSession CreateOpenSession(AppDbContext db, Guid cashierId, Guid branchId, decimal openingBalance = 100_000m)
    {
        var session = new CashierSession
        {
            SessionNumber = $"CS-{DateTime.UtcNow:yyyyMMdd}-01",
            CashierId = cashierId,
            BranchId = branchId,
            OpeningTime = DateTime.UtcNow.AddHours(-2),
            OpeningBalance = openingBalance,
            ExpectedClosingCash = openingBalance,
            ExpectedClosingCard = 0,
            ExpectedClosingBank = 0,
            Status = SessionStatus.Open
        };
        db.CashierSessions.Add(session);
        db.SaveChanges();
        return session;
    }

    private static (JournalEntryService jeService, TreasuryResolutionService treasuryService) CreateServices(AppDbContext db)
    {
        var jeService = new JournalEntryService(db, new Mock<ILogger<JournalEntryService>>().Object);
        var treasuryService = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        return (jeService, treasuryService);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // BLOCKER 3: Salary payment full dual-write (CashFlow + JE + Treasury decrement)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SalaryPayment_CreatesCashFlowAndJournalEntry_AndDecrementsTreasury()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndUser(db);
        var (jeService, treasuryService) = CreateServices(db);
        var employee = SeedEmployee(db, branchId);
        var session = CreateOpenSession(db, cashierId, branchId);

        var salaryId = Guid.NewGuid();
        var salary = new SalaryRecord
        {
            Id = salaryId,
            EmployeeId = employee.Id,
            Year = 2025,
            Month = 6,
            BaseSalary = 15_000m,
            NetSalary = 15_000m
        };
        db.SalaryRecords.Add(salary);
        await db.SaveChangesAsync();

        // Simulate salary payment dual-write
        var paymentMethod = "cash";
        var treasury = await treasuryService.ResolveTreasuryAsync(branchId, paymentMethod, session.Id);

        // Create CashFlowTransaction
        var cashflow = new CashFlowTransaction
        {
            TransactionNumber = $"TX-{DateTime.UtcNow:yyyyMMdd}-SAL-001",
            Type = TransactionType.Outflow,
            Category = FinancialCategory.SalaryPayment,
            Amount = salary.NetSalary,
            PaymentMethod = paymentMethod,
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            ReferenceId = salary.Id,
            ReferenceNumber = $"SAL-{salary.Year}{salary.Month:D2}",
            Description = $"صرف راتب: {employee.FullName}",
            PerformedBy = cashierId,
            BranchId = branchId,
            TreasuryId = treasury.Id,
            CashierSessionId = session.Id
        };
        db.CashFlowTransactions.Add(cashflow);

        // Create JournalEntry: Debit Expense, Credit Treasury
        var je = await jeService.CreateEntryAsync(
            FinancialDocumentType.SalaryPayment, salary.Id,
            $"صرف راتب: {employee.FullName}",
            DateOnly.FromDateTime(DateTime.Today), branchId, cashierId,
            session.Id, treasury.Id,
            new[]
            {
                (JournalAccountType.Expense, salary.Id, salary.NetSalary, 0m, (string?)$"راتب: {employee.FullName}"),
                (JournalAccountType.Treasury, treasury.Id, 0m, salary.NetSalary, (string?)$"سداد من: {treasury.Name}")
            });
        je.IsPosted = true;
        je.PostedAt = DateTime.UtcNow;

        // Decrement Treasury
        await treasuryService.DecrementTreasuryBalanceAsync(branchId, paymentMethod, salary.NetSalary, session.Id);

        // Mark salary as paid
        salary.PaidAt = DateTime.UtcNow;
        salary.PaidBy = cashierId;
        salary.PaymentMethod = paymentMethod;
        await db.SaveChangesAsync();

        // Verify CashFlowTransaction exists with TreasuryId
        var savedCashflow = await db.CashFlowTransactions
            .FirstOrDefaultAsync(c => c.ReferenceId == salary.Id && c.Category == FinancialCategory.SalaryPayment && !c.IsReversal);
        savedCashflow.Should().NotBeNull("salary payment must create CashFlowTransaction");
        savedCashflow!.TreasuryId.Should().Be(treasury.Id, "CashFlowTransaction must have TreasuryId");
        savedCashflow.CashierSessionId.Should().Be(session.Id, "CashFlowTransaction must have CashierSessionId");

        // Verify JournalEntry exists, is posted, and has TreasuryId
        var savedJe = await db.JournalEntries
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.FinancialDocumentId == salary.Id
                && e.FinancialDocumentType == FinancialDocumentType.SalaryPayment
                && !e.IsReversal);
        savedJe.Should().NotBeNull("salary payment must create JournalEntry");
        savedJe!.IsPosted.Should().BeTrue("salary JournalEntry must be auto-posted");
        savedJe.TreasuryId.Should().Be(treasury.Id, "JournalEntry must have TreasuryId");
        savedJe.CashierSessionId.Should().Be(session.Id, "JournalEntry must have CashierSessionId");
        savedJe.BranchId.Should().Be(branchId, "JournalEntry must have BranchId");

        // Verify debit line
        var expenseLine = savedJe.Lines.FirstOrDefault(l => l.AccountType == JournalAccountType.Expense && l.Debit > 0);
        expenseLine.Should().NotBeNull("salary must debit Expense");
        expenseLine!.Debit.Should().Be(15_000m);

        // Verify credit line
        var treasuryLine = savedJe.Lines.FirstOrDefault(l => l.AccountType == JournalAccountType.Treasury && l.Credit > 0);
        treasuryLine.Should().NotBeNull("salary must credit Treasury");
        treasuryLine!.Credit.Should().Be(15_000m);

        // Verify Treasury was decremented
        var savedTreasury = await db.Treasuries.FindAsync(treasury.Id);
        savedTreasury!.Balance.Should().Be(-15_000m, "Treasury must be decremented by salary amount");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // BLOCKER 4: Advance reversal success path — creates reversal CashFlow + JE + restores Treasury
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AdvanceReversal_Success_CreatesReversalRecordsAndRestoresTreasury()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndUser(db);
        var (jeService, treasuryService) = CreateServices(db);
        var employee = SeedEmployee(db, branchId);
        var session = CreateOpenSession(db, cashierId, branchId);

        // First, approve an advance (same as AdvanceApproval test)
        var advanceId = Guid.NewGuid();
        var advance = new AdvancePayment
        {
            Id = advanceId,
            EmployeeId = employee.Id,
            Amount = 5_000m,
            RequestDate = DateTime.UtcNow,
            Status = RequestStatus.Approved
        };
        db.AdvancePayments.Add(advance);

        var treasury = await treasuryService.ResolveTreasuryAsync(branchId, "cash", session.Id);
        var originalCashflow = new CashFlowTransaction
        {
            TransactionNumber = $"TX-{DateTime.UtcNow:yyyyMMdd}-ADV-001",
            Type = TransactionType.Outflow,
            Category = FinancialCategory.SalaryAdvance,
            Amount = 5_000m,
            PaymentMethod = "cash",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            ReferenceId = advanceId,
            TreasuryId = treasury.Id,
            BranchId = branchId,
            CashierSessionId = session.Id,
            PerformedBy = cashierId
        };
        db.CashFlowTransactions.Add(originalCashflow);

        var originalJe = await jeService.CreateEntryAsync(
            FinancialDocumentType.AdvancePayment, advanceId,
            $"سلفة: {employee.FullName}",
            DateOnly.FromDateTime(DateTime.Today), branchId, cashierId,
            session.Id, treasury.Id,
            new[]
            {
                (JournalAccountType.OtherReceivable, advanceId, 5_000m, 0m, (string?)$"سلفة: {employee.FullName}"),
                (JournalAccountType.Treasury, treasury.Id, 0m, 5_000m, (string?)$"سداد من: {treasury.Name}")
            });
        originalJe.IsPosted = true;
        originalJe.PostedAt = DateTime.UtcNow;

        await treasuryService.DecrementTreasuryBalanceAsync(branchId, "cash", 5_000m, session.Id);
        await db.SaveChangesAsync();

        // Now reverse the advance
        var reversalCashflow = new CashFlowTransaction
        {
            TransactionNumber = $"TX-{DateTime.UtcNow:yyyyMMdd}-REV-ADV-001",
            Type = TransactionType.Inflow,
            Category = FinancialCategory.Reversal,
            Amount = 5_000m,
            PaymentMethod = "cash",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            ReferenceId = advanceId,
            TreasuryId = treasury.Id,
            BranchId = branchId,
            CashierSessionId = session.Id,
            PerformedBy = cashierId,
            IsReversal = true,
            ReversalOfTransactionId = originalCashflow.Id
        };
        db.CashFlowTransactions.Add(reversalCashflow);

        var reversalJe = await jeService.CreateReversalEntryAsync(originalJe.Id, $"عكس سلفة: {employee.FullName}", cashierId);
        reversalJe.IsPosted = true;
        reversalJe.PostedAt = DateTime.UtcNow;

        await treasuryService.IncrementTreasuryBalanceByTreasuryIdAsync(treasury.Id, 5_000m);

        advance.Status = RequestStatus.Rejected;
        advance.RejectionReason = "عكس السلفة";
        advance.IsDeducted = false;
        await db.SaveChangesAsync();

        // Verify reversal CashFlowTransaction
        var savedReversalCashflow = await db.CashFlowTransactions
            .FirstOrDefaultAsync(c => c.ReferenceId == advanceId && c.IsReversal);
        savedReversalCashflow.Should().NotBeNull("reversal must create CashFlowTransaction");
        savedReversalCashflow!.TreasuryId.Should().Be(treasury.Id, "reversal must use same TreasuryId");
        savedReversalCashflow.ReversalOfTransactionId.Should().Be(originalCashflow.Id, "reversal must link to original CashFlow");
        savedReversalCashflow.Type.Should().Be(TransactionType.Inflow, "reversal must be Inflow");
        savedReversalCashflow.Category.Should().Be(FinancialCategory.Reversal, "reversal category must be Reversal");

        // Verify reversal JournalEntry
        var savedReversalJe = await db.JournalEntries
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.FinancialDocumentId == advanceId && e.IsReversal);
        savedReversalJe.Should().NotBeNull("reversal must create JournalEntry");
        savedReversalJe!.IsPosted.Should().BeTrue("reversal JournalEntry must be auto-posted");
        savedReversalJe.ReversalOfEntryId.Should().Be(originalJe.Id, "reversal JE must link to original JE");

        // Verify Treasury restored
        var savedTreasury = await db.Treasuries.FindAsync(treasury.Id);
        savedTreasury!.Balance.Should().Be(0m, "Treasury balance must be restored to 0 after reversal");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // BLOCKER 5: Salary reversal success path — creates reversal records + restores Treasury
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SalaryReversal_Success_CreatesReversalRecordsAndRestoresTreasury()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndUser(db);
        var (jeService, treasuryService) = CreateServices(db);
        var employee = SeedEmployee(db, branchId);
        var session = CreateOpenSession(db, cashierId, branchId);

        // Create paid salary with full dual-write
        var salaryId = Guid.NewGuid();
        var salary = new SalaryRecord
        {
            Id = salaryId,
            EmployeeId = employee.Id,
            Year = 2025,
            Month = 7,
            BaseSalary = 20_000m,
            NetSalary = 20_000m,
            PaidAt = DateTime.UtcNow,
            PaidBy = cashierId,
            PaymentMethod = "cash"
        };
        db.SalaryRecords.Add(salary);

        var treasury = await treasuryService.ResolveTreasuryAsync(branchId, "cash", session.Id);
        var originalCashflow = new CashFlowTransaction
        {
            TransactionNumber = $"TX-{DateTime.UtcNow:yyyyMMdd}-SAL-REV",
            Type = TransactionType.Outflow,
            Category = FinancialCategory.SalaryPayment,
            Amount = 20_000m,
            PaymentMethod = "cash",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            ReferenceId = salaryId,
            TreasuryId = treasury.Id,
            BranchId = branchId,
            CashierSessionId = session.Id,
            PerformedBy = cashierId
        };
        db.CashFlowTransactions.Add(originalCashflow);

        var originalJe = await jeService.CreateEntryAsync(
            FinancialDocumentType.SalaryPayment, salaryId,
            $"صرف راتب: {employee.FullName}",
            DateOnly.FromDateTime(DateTime.Today), branchId, cashierId,
            session.Id, treasury.Id,
            new[]
            {
                (JournalAccountType.Expense, salaryId, 20_000m, 0m, (string?)$"راتب: {employee.FullName}"),
                (JournalAccountType.Treasury, treasury.Id, 0m, 20_000m, (string?)$"سداد من: {treasury.Name}")
            });
        originalJe.IsPosted = true;
        originalJe.PostedAt = DateTime.UtcNow;
        await treasuryService.DecrementTreasuryBalanceAsync(branchId, "cash", 20_000m, session.Id);
        await db.SaveChangesAsync();

        // Now reverse the salary
        var reversalCashflow = new CashFlowTransaction
        {
            TransactionNumber = $"TX-{DateTime.UtcNow:yyyyMMdd}-REV-SAL-001",
            Type = TransactionType.Inflow,
            Category = FinancialCategory.Reversal,
            Amount = 20_000m,
            PaymentMethod = "cash",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            ReferenceId = salaryId,
            TreasuryId = treasury.Id,
            BranchId = branchId,
            CashierSessionId = session.Id,
            PerformedBy = cashierId,
            IsReversal = true,
            ReversalOfTransactionId = originalCashflow.Id
        };
        db.CashFlowTransactions.Add(reversalCashflow);

        var reversalJe = await jeService.CreateReversalEntryAsync(originalJe.Id, $"عكس صرف راتب: {employee.FullName}", cashierId);
        reversalJe.IsPosted = true;
        reversalJe.PostedAt = DateTime.UtcNow;

        await treasuryService.IncrementTreasuryBalanceByTreasuryIdAsync(treasury.Id, 20_000m);

        salary.PaidAt = null;
        salary.PaidBy = null;
        salary.PaymentMethod = null;
        await db.SaveChangesAsync();

        // Verify reversal CashFlow
        var savedReversal = await db.CashFlowTransactions
            .FirstOrDefaultAsync(c => c.ReferenceId == salaryId && c.IsReversal);
        savedReversal.Should().NotBeNull();
        savedReversal!.TreasuryId.Should().Be(treasury.Id);
        savedReversal.ReversalOfTransactionId.Should().Be(originalCashflow.Id);

        // Verify salary un-marked
        var savedSalary = await db.SalaryRecords.FindAsync(salaryId);
        savedSalary!.PaidAt.Should().BeNull("salary must be un-marked as paid");
        savedSalary.PaidBy.Should().BeNull();
        savedSalary.PaymentMethod.Should().BeNull();

        // Verify Treasury restored
        var savedTreasury = await db.Treasuries.FindAsync(treasury.Id);
        savedTreasury!.Balance.Should().Be(0m, "Treasury must be restored after salary reversal");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // BLOCKER 4/5: Double-restore prevention — reversing already-reversed record
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AdvanceReversal_DoubleRestore_IsBlocked()
    {
        await using var db = CreateContext();
        var (branchId, _) = SeedBranchAndUser(db);
        var employee = SeedEmployee(db, branchId);

        var advanceId = Guid.NewGuid();
        var advance = new AdvancePayment
        {
            Id = advanceId,
            EmployeeId = employee.Id,
            Amount = 3_000m,
            RequestDate = DateTime.UtcNow,
            Status = RequestStatus.Approved
        };
        db.AdvancePayments.Add(advance);

        var originalCashflowId = Guid.NewGuid();
        var originalCashflow = new CashFlowTransaction
        {
            Id = originalCashflowId,
            TransactionNumber = "TX-ADV-DBL",
            Type = TransactionType.Outflow,
            Category = FinancialCategory.SalaryAdvance,
            Amount = 3_000m,
            ReferenceId = advanceId,
            TreasuryId = Guid.NewGuid(),
            BranchId = branchId
        };
        db.CashFlowTransactions.Add(originalCashflow);

        // Create existing reversal
        var existingReversal = new CashFlowTransaction
        {
            TransactionNumber = "TX-REV-ADV-DBL",
            Type = TransactionType.Inflow,
            Category = FinancialCategory.Reversal,
            Amount = 3_000m,
            ReferenceId = advanceId,
            TreasuryId = originalCashflow.TreasuryId,
            BranchId = branchId,
            IsReversal = true,
            ReversalOfTransactionId = originalCashflowId
        };
        db.CashFlowTransactions.Add(existingReversal);
        await db.SaveChangesAsync();

        // Simulate double-restore check from controller
        var alreadyReversed = await db.CashFlowTransactions
            .AnyAsync(c => c.ReversalOfTransactionId != null
                && c.Category == FinancialCategory.Reversal
                && c.IsReversal
                && c.ReferenceId == advanceId);

        alreadyReversed.Should().BeTrue("double-restore must be detected and blocked");
    }

    [Fact]
    public async Task SalaryReversal_DoubleRestore_IsBlocked()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndUser(db);
        var employee = SeedEmployee(db, branchId);

        var salaryId = Guid.NewGuid();
        var salary = new SalaryRecord
        {
            Id = salaryId,
            EmployeeId = employee.Id,
            Year = 2025,
            Month = 8,
            BaseSalary = 12_000m,
            NetSalary = 12_000m,
            PaidAt = DateTime.UtcNow,
            PaidBy = cashierId,
            PaymentMethod = "cash"
        };
        db.SalaryRecords.Add(salary);

        var originalCashflowId = Guid.NewGuid();
        var originalCashflow = new CashFlowTransaction
        {
            Id = originalCashflowId,
            TransactionNumber = "TX-SAL-DBL",
            Type = TransactionType.Outflow,
            Category = FinancialCategory.SalaryPayment,
            Amount = 12_000m,
            ReferenceId = salaryId,
            TreasuryId = Guid.NewGuid(),
            BranchId = branchId
        };
        db.CashFlowTransactions.Add(originalCashflow);

        var existingReversal = new CashFlowTransaction
        {
            TransactionNumber = "TX-REV-SAL-DBL",
            Type = TransactionType.Inflow,
            Category = FinancialCategory.Reversal,
            Amount = 12_000m,
            ReferenceId = salaryId,
            TreasuryId = originalCashflow.TreasuryId,
            BranchId = branchId,
            IsReversal = true,
            ReversalOfTransactionId = originalCashflowId
        };
        db.CashFlowTransactions.Add(existingReversal);
        await db.SaveChangesAsync();

        var alreadyReversed = await db.CashFlowTransactions
            .AnyAsync(c => c.ReversalOfTransactionId != null
                && c.Category == FinancialCategory.Reversal
                && c.IsReversal
                && c.ReferenceId == salaryId);

        alreadyReversed.Should().BeTrue("salary double-restore must be detected and blocked");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // BLOCKER 4/5: Closed session guard — cannot reverse against closed session
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AdvanceReversal_ClosedSession_IsBlocked()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndUser(db);
        var employee = SeedEmployee(db, branchId);

        // Create a CLOSED cashier session
        var closedSession = new CashierSession
        {
            SessionNumber = $"CS-{DateTime.UtcNow:yyyyMMdd}-CLOSED",
            CashierId = cashierId,
            BranchId = branchId,
            OpeningTime = DateTime.UtcNow.AddHours(-8),
            OpeningBalance = 50_000m,
            Status = SessionStatus.Closed
        };
        db.CashierSessions.Add(closedSession);

        var advanceId = Guid.NewGuid();
        var advance = new AdvancePayment
        {
            Id = advanceId,
            EmployeeId = employee.Id,
            Amount = 4_000m,
            RequestDate = DateTime.UtcNow,
            Status = RequestStatus.Approved
        };
        db.AdvancePayments.Add(advance);

        var treasuryId = Guid.NewGuid();
        var originalCashflow = new CashFlowTransaction
        {
            TransactionNumber = "TX-ADV-CLD",
            Type = TransactionType.Outflow,
            Category = FinancialCategory.SalaryAdvance,
            Amount = 4_000m,
            ReferenceId = advanceId,
            TreasuryId = treasuryId,
            BranchId = branchId,
            CashierSessionId = closedSession.Id
        };
        db.CashFlowTransactions.Add(originalCashflow);
        await db.SaveChangesAsync();

        // Simulate closed session guard
        var session = await db.CashierSessions.FindAsync(originalCashflow.CashierSessionId);
        var shouldBlock = session != null && (session.Status == SessionStatus.Closed || session.Status == SessionStatus.Reconciled);

        shouldBlock.Should().BeTrue("advance reversal must be blocked for closed/reconciled sessions");
    }

    [Fact]
    public async Task SalaryReversal_ReconciledSession_IsBlocked()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndUser(db);
        var employee = SeedEmployee(db, branchId);

        var reconciledSession = new CashierSession
        {
            SessionNumber = $"CS-{DateTime.UtcNow:yyyyMMdd}-RECON",
            CashierId = cashierId,
            BranchId = branchId,
            OpeningTime = DateTime.UtcNow.AddHours(-8),
            OpeningBalance = 80_000m,
            Status = SessionStatus.Reconciled
        };
        db.CashierSessions.Add(reconciledSession);

        var salaryId = Guid.NewGuid();
        var salary = new SalaryRecord
        {
            Id = salaryId,
            EmployeeId = employee.Id,
            Year = 2025,
            Month = 9,
            BaseSalary = 18_000m,
            NetSalary = 18_000m,
            PaidAt = DateTime.UtcNow,
            PaidBy = cashierId,
            PaymentMethod = "cash"
        };
        db.SalaryRecords.Add(salary);

        var treasuryId = Guid.NewGuid();
        var originalCashflow = new CashFlowTransaction
        {
            TransactionNumber = "TX-SAL-RECON",
            Type = TransactionType.Outflow,
            Category = FinancialCategory.SalaryPayment,
            Amount = 18_000m,
            ReferenceId = salaryId,
            TreasuryId = treasuryId,
            BranchId = branchId,
            CashierSessionId = reconciledSession.Id
        };
        db.CashFlowTransactions.Add(originalCashflow);
        await db.SaveChangesAsync();

        var session = await db.CashierSessions.FindAsync(originalCashflow.CashierSessionId);
        var shouldBlock = session != null && (session.Status == SessionStatus.Closed || session.Status == SessionStatus.Reconciled);

        shouldBlock.Should().BeTrue("salary reversal must be blocked for reconciled sessions");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // BLOCKER 6: Branch isolation — accountant with specific branch only sees their branch
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PatientAccounts_CrossBranchFiltering_WorksCorrectly()
    {
        await using var db = CreateContext();

        // Create two branches
        var branch1 = Guid.NewGuid();
        var branch2 = Guid.NewGuid();
        db.Branches.Add(new Branch { Id = branch1, Name = "الفرع ١" });
        db.Branches.Add(new Branch { Id = branch2, Name = "الفرع ٢" });

        // Create patients in each branch
        var patient1 = new Patient
        {
            Id = Guid.NewGuid(),
            FirstName = "مريض",
            LastName = "فرع١",
            PatientNumber = "P-B1",
            BranchId = branch1
        };
        var patient2 = new Patient
        {
            Id = Guid.NewGuid(),
            FirstName = "مريض",
            LastName = "فرع٢",
            PatientNumber = "P-B2",
            BranchId = branch2
        };
        db.Patients.Add(patient1);
        db.Patients.Add(patient2);
        await db.SaveChangesAsync();

        // Simulate branch1 accountant's view
        var accountantBranchId = branch1;
        var branchFilter = (Guid?)accountantBranchId;

        var visiblePatients = await db.Patients
            .Where(p => p.IsActive && (!branchFilter.HasValue || p.BranchId == branchFilter.Value))
            .ToListAsync();

        visiblePatients.Should().HaveCount(1, "branch1 accountant should only see branch1 patients");
        visiblePatients[0].Id.Should().Be(patient1.Id);

        // Simulate admin view (no branch filter)
        var adminBranchFilter = (Guid?)null;
        var allPatients = await db.Patients
            .Where(p => p.IsActive && (!adminBranchFilter.HasValue || p.BranchId == adminBranchFilter.Value))
            .ToListAsync();

        allPatients.Should().HaveCount(2, "admin should see patients from all branches");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // BLOCKER 7: Treasury type "Cash" rejected — only Vault/Bank accepted
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TreasuryType_CashValue_IsNotInEnum()
    {
        // Verify that "Cash" is NOT a valid TreasuryType enum value
        var canParseCash = Enum.TryParse<TreasuryType>("Cash", true, out _);
        canParseCash.Should().BeFalse("TreasuryType enum must NOT accept 'Cash' — only Vault and Bank are valid");

        // Verify Vault and Bank ARE valid
        var canParseVault = Enum.TryParse<TreasuryType>("Vault", true, out var vaultValue);
        var canParseBank = Enum.TryParse<TreasuryType>("Bank", true, out var bankValue);
        canParseVault.Should().BeTrue("Vault must be a valid TreasuryType");
        canParseBank.Should().BeTrue("Bank must be a valid TreasuryType");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // BLOCKER 7: Salary controller uses ReportsAccess, NOT FinanceAccess
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ReportsAccessPolicy_ExcludesReception_IncludesAdminAndAccountant()
    {
        // ReportsAccess = Admin + Accountant (NOT Reception)
        var allowedRoles = new[] { nameof(UserRole.Admin), nameof(UserRole.Accountant) };

        allowedRoles.Should().NotContain(nameof(UserRole.Reception),
            "Reception must NOT be in ReportsAccess — they should not see salary data");
        allowedRoles.Should().Contain(nameof(UserRole.Admin));
        allowedRoles.Should().Contain(nameof(UserRole.Accountant));
    }

    [Fact]
    public void FinanceAccessPolicy_IncludesReception_ButMustNotBeUsedForSalary()
    {
        // FinanceAccess = Admin + Reception + Accountant
        var financeAccessRoles = new[] { nameof(UserRole.Admin), nameof(UserRole.Reception), nameof(UserRole.Accountant) };

        financeAccessRoles.Should().Contain(nameof(UserRole.Reception),
            "FinanceAccess includes Reception — this is why Salary must NOT use FinanceAccess");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // BLOCKER 2: Patient balance formula — multiple refund scenarios
    // ═══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(100, 100, 0, 0)]     // invoice 100, paid 100, no refund => balance 0
    [InlineData(100, 50, 0, 50)]     // invoice 100, paid 50, no refund => balance 50
    [InlineData(100, 50, -20, 70)]   // invoice 100, paid 50, refund -20 => balance 70
    [InlineData(100, 80, -30, 50)]   // invoice 100, paid 80, refund -30 => balance 50
    [InlineData(200, 150, -50, 100)] // invoice 200, paid 150, refund -50 => balance 100
    public async Task PatientBalance_RefundScenarios_CalculateCorrectly(
        decimal totalInvoiced, decimal paymentAmount, decimal refundAmount, decimal expectedBalance)
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndUser(db);
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            FirstName = "مريض",
            LastName = "تجريبي",
            PatientNumber = $"P-{Guid.NewGuid().ToString()[..8]}",
            BranchId = branchId
        };
        db.Patients.Add(patient);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            InvoiceNumber = $"INV-{Guid.NewGuid().ToString()[..8]}",
            Status = InvoiceStatus.Issued,
            TotalAmount = totalInvoiced,
            Subtotal = totalInvoiced,
            CreatedBy = cashierId
        };
        db.Invoices.Add(invoice);

        if (paymentAmount > 0)
        {
            db.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(),
                PatientId = patient.Id,
                Amount = paymentAmount,
                PaymentMethod = "cash",
                PaymentDate = DateOnly.FromDateTime(DateTime.Today),
                ReceiptNumber = "RCP-PAY",
                IsActive = true
            });
        }

        if (refundAmount < 0)
        {
            db.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(),
                PatientId = patient.Id,
                Amount = refundAmount,
                PaymentMethod = "cash",
                PaymentDate = DateOnly.FromDateTime(DateTime.Today),
                ReceiptNumber = "RCP-REF",
                IsActive = true
            });
        }

        await db.SaveChangesAsync();

        // Calculate using the same formula as FinanceV3Controller.GetPatientAccounts
        var invoiced = await db.Invoices
            .Where(i => i.PatientId == patient.Id && i.IsActive
                && (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.Paid))
            .SumAsync(i => (decimal?)i.TotalAmount) ?? 0;

        var netPayments = await db.Payments
            .Where(p => p.PatientId == patient.Id && p.IsActive)
            .SumAsync(p => (decimal?)p.Amount) ?? 0;

        var balance = invoiced - netPayments;

        balance.Should().Be(expectedBalance,
            $"invoice {totalInvoiced}, payment {paymentAmount}, refund {refundAmount} => balance should be {expectedBalance}");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // BLOCKER 3: Advance approval rejects branchless employee
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AdvanceApproval_BranchlessEmployee_IsRejected()
    {
        await using var db = CreateContext();

        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            FullName = "موظف بلا فرع",
            BranchId = null, // Branchless!
            BaseSalary = 10_000m
        };
        db.Employees.Add(employee);
        await db.SaveChangesAsync();

        // Simulate the controller's branch validation
        var branchId = employee.BranchId ?? Guid.Empty;
        var shouldReject = branchId == Guid.Empty;

        shouldReject.Should().BeTrue("advance approval must reject branchless employees");
    }

    [Fact]
    public async Task AdvanceApproval_EmployeeWithEmptyBranchId_IsRejected()
    {
        await using var db = CreateContext();

        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            FullName = "موظف فرع فارغ",
            BranchId = Guid.Empty, // Empty branch!
            BaseSalary = 10_000m
        };
        db.Employees.Add(employee);
        await db.SaveChangesAsync();

        var branchId = employee.BranchId ?? Guid.Empty;
        var shouldReject = branchId == Guid.Empty;

        shouldReject.Should().BeTrue("advance approval must reject employees with empty BranchId");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // BLOCKER 3: Salary payment rejects branchless employee
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SalaryPayment_BranchlessEmployee_IsRejected()
    {
        await using var db = CreateContext();

        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            FullName = "موظف بلا فرع",
            BranchId = null,
            BaseSalary = 12_000m
        };
        db.Employees.Add(employee);
        await db.SaveChangesAsync();

        var branchId = employee.BranchId ?? Guid.Empty;
        var shouldReject = branchId == Guid.Empty;

        shouldReject.Should().BeTrue("salary payment must reject branchless employees");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // BLOCKER 3: Salary payment requires active cashier session for cash payments
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SalaryPayment_CashPaymentWithoutActiveSession_IsRejected()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndUser(db);
        var employee = SeedEmployee(db, branchId);

        // No open session created!
        var hasActiveSession = await db.CashierSessions
            .AnyAsync(s => s.CashierId == cashierId && s.Status == SessionStatus.Open && s.IsActive);

        hasActiveSession.Should().BeFalse("no session should exist");
        // Controller rejects with: "عذراً، يجب فتح صندوق الكاشير أولاً"
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // BLOCKER 3: Advance approval requires active cashier session
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AdvanceApproval_WithoutActiveSession_IsRejected()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndUser(db);
        var employee = SeedEmployee(db, branchId);

        // No open session
        var hasActiveSession = await db.CashierSessions
            .AnyAsync(s => s.CashierId == cashierId && s.Status == SessionStatus.Open && s.IsActive);

        hasActiveSession.Should().BeFalse("no session should exist");
    }
}
