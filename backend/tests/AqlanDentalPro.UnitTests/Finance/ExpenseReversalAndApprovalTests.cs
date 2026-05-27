using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Finance;

/// <summary>
/// PR #231 — Blocker 1 & 2: Real controller tests for expense reversal integrity
/// and cash expense approval enforcement.
///
/// These tests instantiate the actual OperationalExpensesController, SalaryController,
/// and SupplierBillsController, call their real action methods, and assert persisted
/// database effects. No simulated logic.
/// </summary>
public class ExpenseReversalAndApprovalTests
{
    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static (Guid branchId, Guid adminId) SeedBranchAndAdmin(AppDbContext db)
    {
        var branchId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        db.Branches.Add(new Branch { Id = branchId, Name = "الفرع الرئيسي" });
        db.Users.Add(new User { Id = adminId, Username = "admin1", BranchId = branchId, Role = UserRole.Admin });
        db.SaveChanges();

        return (branchId, adminId);
    }

    private static ICurrentUserService CreateAdminCurrentUser(Guid userId, Guid branchId)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.SetupGet(c => c.UserId).Returns(userId);
        mock.SetupGet(c => c.BranchId).Returns(branchId);
        mock.SetupGet(c => c.IsAdmin).Returns(true);
        mock.SetupGet(c => c.IsAuthenticated).Returns(true);
        mock.SetupGet(c => c.Role).Returns(UserRole.Admin);
        return mock.Object;
    }

    private static CashierSession CreateOpenSession(AppDbContext db, Guid cashierId, Guid branchId, Guid? treasuryId = null, decimal openingBalance = 100_000m)
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
            Status = SessionStatus.Open,
            TreasuryId = treasuryId
        };
        db.CashierSessions.Add(session);
        db.SaveChanges();
        return session;
    }

    private static (Treasury vault, Treasury bank) SeedTreasuries(AppDbContext db, Guid branchId)
    {
        var vault = new Treasury
        {
            Id = Guid.NewGuid(),
            Name = "درج كاشير الاستقبال",
            Type = TreasuryType.Vault,
            Balance = 500_000m,
            BranchId = branchId,
            IsActive = true
        };
        var bank = new Treasury
        {
            Id = Guid.NewGuid(),
            Name = "حساب بنك التضامن",
            Type = TreasuryType.Bank,
            Balance = 1_000_000m,
            BranchId = branchId,
            IsActive = true
        };
        db.Treasuries.Add(vault);
        db.Treasuries.Add(bank);
        db.SaveChanges();
        return (vault, bank);
    }

    private static (OperationalExpensesController controller, AppDbContext db, Guid branchId, Guid adminId, Treasury vault, Treasury bank, CashierSession session)
        SetupExpenseControllerWithSession(decimal vaultBalance = 500_000m)
    {
        var db = CreateContext();
        var (branchId, adminId) = SeedBranchAndAdmin(db);
        var (vault, bank) = SeedTreasuriesWithBalance(db, branchId, vaultBalance);
        var session = CreateOpenSession(db, adminId, branchId, vault.Id);

        var currentUser = CreateAdminCurrentUser(adminId, branchId);
        var audit = new Mock<IAuditService>().Object;
        var journalEntryService = new JournalEntryService(db, new Mock<ILogger<JournalEntryService>>().Object);
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);

        var controller = new OperationalExpensesController(db, currentUser, audit, journalEntryService, treasuryResolution);

        return (controller, db, branchId, adminId, vault, bank, session);
    }

    private static (Treasury vault, Treasury bank) SeedTreasuriesWithBalance(AppDbContext db, Guid branchId, decimal vaultBalance = 500_000m)
    {
        var vault = new Treasury
        {
            Id = Guid.NewGuid(),
            Name = "درج كاشير الاستقبال",
            Type = TreasuryType.Vault,
            Balance = vaultBalance,
            BranchId = branchId,
            IsActive = true
        };
        var bank = new Treasury
        {
            Id = Guid.NewGuid(),
            Name = "حساب بنك التضامن",
            Type = TreasuryType.Bank,
            Balance = 1_000_000m,
            BranchId = branchId,
            IsActive = true
        };
        db.Treasuries.Add(vault);
        db.Treasuries.Add(bank);
        db.SaveChanges();
        return (vault, bank);
    }

    /// <summary>
    /// Creates a posted cash expense in the DB so we can test reversal.
    /// </summary>
    private static (OperationalExpense expense, CashFlowTransaction cashflow) CreatePostedCashExpense(
        AppDbContext db, Guid branchId, Guid adminId, Guid treasuryId, Guid? cashierSessionId, decimal amount = 15_000m)
    {
        var expense = new OperationalExpense
        {
            Id = Guid.NewGuid(),
            ExpenseNumber = $"EXP-{DateTime.UtcNow:yyyyMMdd}-001",
            Title = "مصروف تجريبي",
            Category = ExpenseCategory.Miscellaneous,
            Amount = amount,
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = "cash",
            PaidBy = adminId,
            BranchId = branchId,
            ApprovalStatus = ApprovalStatus.NotRequired,
            IsPostedToLedger = true,
            IsActive = true
        };

        var cashflow = new CashFlowTransaction
        {
            Id = Guid.NewGuid(),
            TransactionNumber = $"TX-{DateTime.UtcNow:yyyyMMdd}-OUT-001",
            Type = TransactionType.Outflow,
            Category = FinancialCategory.OperationalExpense,
            Amount = amount,
            PaymentMethod = "cash",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            ReferenceId = expense.Id,
            ReferenceNumber = expense.ExpenseNumber,
            Description = $"قيد مصروف تشغيلي: {expense.Title}",
            PerformedBy = adminId,
            BranchId = branchId,
            TreasuryId = treasuryId,
            CashierSessionId = cashierSessionId,
            IsActive = true
        };

        expense.CashFlowTransactionId = cashflow.Id;

        db.OperationalExpenses.Add(expense);
        db.CashFlowTransactions.Add(cashflow);
        db.SaveChanges();

        return (expense, cashflow);
    }

    /// <summary>
    /// Creates a posted bank expense in the DB so we can test reversal.
    /// </summary>
    private static (OperationalExpense expense, CashFlowTransaction cashflow) CreatePostedBankExpense(
        AppDbContext db, Guid branchId, Guid adminId, Guid treasuryId, decimal amount = 25_000m)
    {
        var expense = new OperationalExpense
        {
            Id = Guid.NewGuid(),
            ExpenseNumber = $"EXP-{DateTime.UtcNow:yyyyMMdd}-002",
            Title = "مصروف بنكي تجريبي",
            Category = ExpenseCategory.Utilities,
            Amount = amount,
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = "bank_transfer",
            PaidBy = adminId,
            BranchId = branchId,
            ApprovalStatus = ApprovalStatus.NotRequired,
            IsPostedToLedger = true,
            IsActive = true
        };

        var cashflow = new CashFlowTransaction
        {
            Id = Guid.NewGuid(),
            TransactionNumber = $"TX-{DateTime.UtcNow:yyyyMMdd}-OUT-002",
            Type = TransactionType.Outflow,
            Category = FinancialCategory.OperationalExpense,
            Amount = amount,
            PaymentMethod = "bank_transfer",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            ReferenceId = expense.Id,
            ReferenceNumber = expense.ExpenseNumber,
            Description = $"قيد مصروف بنكي: {expense.Title}",
            PerformedBy = adminId,
            BranchId = branchId,
            TreasuryId = treasuryId,
            IsActive = true
        };

        expense.CashFlowTransactionId = cashflow.Id;

        db.OperationalExpenses.Add(expense);
        db.CashFlowTransactions.Add(cashflow);
        db.SaveChanges();

        return (expense, cashflow);
    }

    /// <summary>
    /// Creates a pending cash expense requiring approval.
    /// </summary>
    private static OperationalExpense CreatePendingCashExpense(
        AppDbContext db, Guid branchId, Guid adminId, decimal amount = 60_000m)
    {
        var expense = new OperationalExpense
        {
            Id = Guid.NewGuid(),
            ExpenseNumber = $"EXP-{DateTime.UtcNow:yyyyMMdd}-003",
            Title = "مصروف نقدي بانتظار الاعتماد",
            Category = ExpenseCategory.Marketing,
            Amount = amount,
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = "cash",
            PaidBy = adminId,
            BranchId = branchId,
            ApprovalStatus = ApprovalStatus.Pending,
            IsPostedToLedger = false,
            IsActive = true
        };

        db.OperationalExpenses.Add(expense);
        db.SaveChanges();

        return expense;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TEST 1: Approve cash expense without open cashier session → BadRequest, no writes
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task OperationalExpensesController_ApproveCashWithoutOpenSession_ReturnsBadRequest_NoWrites()
    {
        await using var db = CreateContext();
        var (branchId, adminId) = SeedBranchAndAdmin(db);
        var (vault, bank) = SeedTreasuriesWithBalance(db, branchId);

        // Do NOT create an open cashier session — the admin has no active session
        var currentUser = CreateAdminCurrentUser(adminId, branchId);
        var audit = new Mock<IAuditService>().Object;
        var journalEntryService = new JournalEntryService(db, new Mock<ILogger<JournalEntryService>>().Object);
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        var controller = new OperationalExpensesController(db, currentUser, audit, journalEntryService, treasuryResolution);

        var expense = CreatePendingCashExpense(db, branchId, adminId);

        var initialCashflowCount = await db.CashFlowTransactions.CountAsync();
        var initialJeCount = await db.JournalEntries.CountAsync();
        var initialVaultBalance = vault.Balance;

        // Act — approve cash expense without an open session
        var result = await controller.Approve(expense.Id, new ApproveExpenseRequest());

        // Assert — must be BadRequest
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = (BadRequestObjectResult)result;

        // No new financial records should have been written
        (await db.CashFlowTransactions.CountAsync()).Should().Be(initialCashflowCount,
            "no cashflow should be created when session is missing");
        (await db.JournalEntries.CountAsync()).Should().Be(initialJeCount,
            "no journal entry should be created when session is missing");

        // Expense should still be pending
        var unchanged = await db.OperationalExpenses.FindAsync(expense.Id);
        unchanged!.ApprovalStatus.Should().Be(ApprovalStatus.Pending,
            "expense must remain pending when approval fails");
        unchanged.IsPostedToLedger.Should().BeFalse();

        // Treasury balance unchanged
        var vaultAfter = await db.Treasuries.FindAsync(vault.Id);
        vaultAfter!.Balance.Should().Be(initialVaultBalance,
            "treasury balance must not change on failed approval");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TEST 2: Approve cash expense with open session → decrements correct treasury, links session
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task OperationalExpensesController_ApproveCashWithOpenSession_DecrementsCorrectTreasuryAndLinksSession()
    {
        await using var db = CreateContext();
        var (branchId, adminId) = SeedBranchAndAdmin(db);
        var (vault, bank) = SeedTreasuriesWithBalance(db, branchId);
        var session = CreateOpenSession(db, adminId, branchId, vault.Id);

        var currentUser = CreateAdminCurrentUser(adminId, branchId);
        var audit = new Mock<IAuditService>().Object;
        var journalEntryService = new JournalEntryService(db, new Mock<ILogger<JournalEntryService>>().Object);
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        var controller = new OperationalExpensesController(db, currentUser, audit, journalEntryService, treasuryResolution);

        var expense = CreatePendingCashExpense(db, branchId, adminId, amount: 10_000m);
        var initialVaultBalance = vault.Balance;

        // Act
        var result = await controller.Approve(expense.Id, new ApproveExpenseRequest());

        // Assert — must succeed
        result.Should().BeOfType<OkObjectResult>();

        // Expense should be approved and posted
        var approved = await db.OperationalExpenses.FindAsync(expense.Id);
        approved!.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
        approved.IsPostedToLedger.Should().BeTrue();
        approved.CashFlowTransactionId.Should().NotBeNull();

        // CashFlowTransaction should be linked to the session and treasury
        var cashflow = await db.CashFlowTransactions.FindAsync(approved.CashFlowTransactionId!.Value);
        cashflow.Should().NotBeNull();
        cashflow!.CashierSessionId.Should().Be(session.Id,
            "cash expense approval must link the cashier session");
        cashflow.TreasuryId.Should().Be(vault.Id,
            "cash expense must debit the vault (cash) treasury");

        // JournalEntry should be linked to the session
        var je = await db.JournalEntries.FirstOrDefaultAsync(e =>
            e.FinancialDocumentId == expense.Id && e.FinancialDocumentType == FinancialDocumentType.Expense);
        je.Should().NotBeNull();
        je!.CashierSessionId.Should().Be(session.Id,
            "journal entry must link the cashier session for cash expenses");

        // Treasury balance should be decremented
        var vaultAfter = await db.Treasuries.FindAsync(vault.Id);
        vaultAfter!.Balance.Should().Be(initialVaultBalance - 10_000m,
            "cash expense approval must decrement the vault treasury balance");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TEST 3: Delete posted expense → restores exact original treasury
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task OperationalExpensesController_DeletePostedExpense_RestoresExactOriginalTreasury()
    {
        await using var db = CreateContext();
        var (branchId, adminId) = SeedBranchAndAdmin(db);
        var (vault, bank) = SeedTreasuriesWithBalance(db, branchId, 200_000m);
        var session = CreateOpenSession(db, adminId, branchId, vault.Id);

        // Create a posted cash expense with a known treasury
        var (expense, originalCashflow) = CreatePostedCashExpense(db, branchId, adminId, vault.Id, session.Id, amount: 15_000m);

        // Manually decrement the vault balance to simulate the original posting
        vault.Balance -= 15_000m;
        db.SaveChanges();

        var balanceBeforeReversal = vault.Balance; // 200,000 - 15,000 = 185,000

        var currentUser = CreateAdminCurrentUser(adminId, branchId);
        var audit = new Mock<IAuditService>().Object;
        var journalEntryService = new JournalEntryService(db, new Mock<ILogger<JournalEntryService>>().Object);
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        var controller = new OperationalExpensesController(db, currentUser, audit, journalEntryService, treasuryResolution);

        // Act — delete the posted expense (triggers reversal)
        var result = await controller.Delete(expense.Id);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        // Original treasury balance should be restored
        var vaultAfter = await db.Treasuries.FindAsync(vault.Id);
        vaultAfter!.Balance.Should().Be(balanceBeforeReversal + 15_000m,
            "reversal must restore the exact original treasury balance");

        // Reversal cashflow should reference the original treasury
        var reversalCashflow = await db.CashFlowTransactions
            .FirstOrDefaultAsync(t => t.ReversalOfTransactionId == originalCashflow.Id);
        reversalCashflow.Should().NotBeNull();
        reversalCashflow!.TreasuryId.Should().Be(vault.Id,
            "reversal cashflow must reference the original treasury ID");

        // Original cashflow should be linked to the reversal
        var updatedOriginal = await db.CashFlowTransactions.FindAsync(originalCashflow.Id);
        updatedOriginal!.ReversedByTransactionId.Should().Be(reversalCashflow.Id,
            "original cashflow must be linked to the reversal for double-restore prevention");

        // Expense should be deactivated
        var deleted = await db.OperationalExpenses.FindAsync(expense.Id);
        deleted!.IsActive.Should().BeFalse("posted expense should be deactivated on reversal");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TEST 4: Delete posted expense with missing original treasury → rejects without reversal writes
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task OperationalExpensesController_DeletePostedExpense_MissingOriginalTreasury_RejectsWithoutReversalWrites()
    {
        await using var db = CreateContext();
        var (branchId, adminId) = SeedBranchAndAdmin(db);
        var (vault, bank) = SeedTreasuriesWithBalance(db, branchId);
        var session = CreateOpenSession(db, adminId, branchId, vault.Id);

        // Create a posted expense whose cashflow references a NON-EXISTENT treasury
        var fakeTreasuryId = Guid.NewGuid();
        var (expense, originalCashflow) = CreatePostedCashExpense(db, branchId, adminId, fakeTreasuryId, session.Id, amount: 15_000m);

        var initialReversalCount = await db.CashFlowTransactions.CountAsync(t => t.IsReversal);
        var initialJeCount = await db.JournalEntries.CountAsync();

        var currentUser = CreateAdminCurrentUser(adminId, branchId);
        var audit = new Mock<IAuditService>().Object;
        var journalEntryService = new JournalEntryService(db, new Mock<ILogger<JournalEntryService>>().Object);
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        var controller = new OperationalExpensesController(db, currentUser, audit, journalEntryService, treasuryResolution);

        // Act — attempt to delete the posted expense with missing treasury
        var result = await controller.Delete(expense.Id);

        // Assert — must be BadRequest
        result.Should().BeOfType<BadRequestObjectResult>();

        // No reversal records should have been written
        (await db.CashFlowTransactions.CountAsync(t => t.IsReversal)).Should().Be(initialReversalCount,
            "no reversal cashflow should be created when original treasury is missing");

        (await db.JournalEntries.CountAsync()).Should().Be(initialJeCount,
            "no reversal journal entry should be created when original treasury is missing");

        // Expense should still be active (not reversed)
        var unchanged = await db.OperationalExpenses.FindAsync(expense.Id);
        unchanged!.IsActive.Should().BeTrue("expense must remain active when reversal is rejected");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TEST 5: Delete posted expense — second attempt does NOT double-restore balance
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task OperationalExpensesController_DeletePostedExpense_SecondAttempt_DoesNotDoubleRestoreBalance()
    {
        await using var db = CreateContext();
        var (branchId, adminId) = SeedBranchAndAdmin(db);
        var (vault, bank) = SeedTreasuriesWithBalance(db, branchId, 200_000m);
        var session = CreateOpenSession(db, adminId, branchId, vault.Id);

        // Create and "post" a cash expense
        var (expense, originalCashflow) = CreatePostedCashExpense(db, branchId, adminId, vault.Id, session.Id, amount: 20_000m);
        vault.Balance -= 20_000m;
        db.SaveChanges();

        var currentUser = CreateAdminCurrentUser(adminId, branchId);
        var audit = new Mock<IAuditService>().Object;
        var journalEntryService = new JournalEntryService(db, new Mock<ILogger<JournalEntryService>>().Object);
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        var controller = new OperationalExpensesController(db, currentUser, audit, journalEntryService, treasuryResolution);

        // First deletion — should succeed and restore balance
        var firstResult = await controller.Delete(expense.Id);
        firstResult.Should().BeOfType<OkObjectResult>();

        var vaultAfterFirstDelete = (await db.Treasuries.FindAsync(vault.Id))!.Balance;

        // Second deletion attempt — should be rejected (expense is no longer active)
        var secondResult = await controller.Delete(expense.Id);

        // The expense is now inactive, so Delete returns NotFound
        secondResult.Should().BeOfType<NotFoundObjectResult>("already-deleted expense should not be found");

        // Treasury balance must not change on the second attempt
        var vaultAfterSecondDelete = (await db.Treasuries.FindAsync(vault.Id))!.Balance;
        vaultAfterSecondDelete.Should().Be(vaultAfterFirstDelete,
            "second deletion must NOT double-restore treasury balance");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TEST 6: Delete posted bank expense → restores original bank treasury
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task OperationalExpensesController_DeletePostedBankExpense_RestoresOriginalBankTreasury()
    {
        await using var db = CreateContext();
        var (branchId, adminId) = SeedBranchAndAdmin(db);
        var (vault, bank) = SeedTreasuriesWithBalance(db, branchId);

        // Create a posted bank expense with the bank treasury
        var (expense, originalCashflow) = CreatePostedBankExpense(db, branchId, adminId, bank.Id, amount: 25_000m);

        // Simulate the original posting's balance decrement
        bank.Balance -= 25_000m;
        db.SaveChanges();

        var bankBalanceBeforeReversal = bank.Balance; // 1,000,000 - 25,000 = 975,000

        var currentUser = CreateAdminCurrentUser(adminId, branchId);
        var audit = new Mock<IAuditService>().Object;
        var journalEntryService = new JournalEntryService(db, new Mock<ILogger<JournalEntryService>>().Object);
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        var controller = new OperationalExpensesController(db, currentUser, audit, journalEntryService, treasuryResolution);

        // Act
        var result = await controller.Delete(expense.Id);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        // Bank treasury balance should be restored to the exact original
        var bankAfter = await db.Treasuries.FindAsync(bank.Id);
        bankAfter!.Balance.Should().Be(bankBalanceBeforeReversal + 25_000m,
            "bank expense reversal must restore the exact original bank treasury balance");

        // Vault treasury should be UNCHANGED
        var vaultAfter = await db.Treasuries.FindAsync(vault.Id);
        vaultAfter!.Balance.Should().Be(500_000m,
            "vault treasury must not be affected by a bank expense reversal");

        // Reversal cashflow should reference the bank treasury
        var reversalCashflow = await db.CashFlowTransactions
            .FirstOrDefaultAsync(t => t.ReversalOfTransactionId == originalCashflow.Id);
        reversalCashflow.Should().NotBeNull();
        reversalCashflow!.TreasuryId.Should().Be(bank.Id,
            "bank expense reversal cashflow must reference the original bank treasury");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TEST 7: SalaryController PaySalary → decrements correct treasury
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SalaryController_PaySalary_CallsRealEndpoint_DecrementsCorrectTreasury()
    {
        await using var db = CreateContext();
        var (branchId, adminId) = SeedBranchAndAdmin(db);
        var (vault, bank) = SeedTreasuriesWithBalance(db, branchId);
        var session = CreateOpenSession(db, adminId, branchId, vault.Id);

        // Create employee and salary record
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            FullName = "موظف تجريبي",
            BranchId = branchId,
            BaseSalary = 50_000m,
            IsActive = true
        };
        db.Employees.Add(employee);

        var salaryRecord = new SalaryRecord
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            Year = 2026,
            Month = 5,
            BaseSalary = 50_000m,
            Deductions = 5_000m,
            Advances = 0,
            Bonuses = 5_000m,
            NetSalary = 50_000m,
            IsActive = true
        };
        db.SalaryRecords.Add(salaryRecord);
        await db.SaveChangesAsync();

        var initialVaultBalance = vault.Balance;

        var journalEntryService = new JournalEntryService(db, new Mock<ILogger<JournalEntryService>>().Object);
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        var audit = new Mock<IAuditService>().Object;
        var controller = new SalaryController(db, journalEntryService, treasuryResolution, audit);

        // We need to set up the controller's User claims for PaySalary
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
        };
        var claims = new System.Security.Claims.ClaimsIdentity(new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, adminId.ToString()),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Admin")
        });
        controller.ControllerContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(claims);

        // Act — pay salary with cash
        var result = await controller.PaySalary(salaryRecord.Id, new PaySalaryRequest
        {
            PaymentMethod = "cash"
        });

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        // Vault treasury should be decremented by the net salary amount
        var vaultAfter = await db.Treasuries.FindAsync(vault.Id);
        vaultAfter!.Balance.Should().Be(initialVaultBalance - 50_000m,
            "cash salary payment must decrement the vault treasury");

        // CashFlowTransaction should reference the vault and session
        var cashflow = await db.CashFlowTransactions
            .FirstOrDefaultAsync(t => t.ReferenceId == salaryRecord.Id && t.Category == FinancialCategory.SalaryPayment);
        cashflow.Should().NotBeNull();
        cashflow!.TreasuryId.Should().Be(vault.Id,
            "salary cash payment must reference the vault treasury");
        cashflow.CashierSessionId.Should().Be(session.Id,
            "salary cash payment must link the cashier session");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TEST 8: SupplierBillsController Pay → decrements correct treasury
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SupplierBillsController_Pay_CallsRealEndpoint_DecrementsCorrectTreasury()
    {
        await using var db = CreateContext();
        var (branchId, adminId) = SeedBranchAndAdmin(db);
        var (vault, bank) = SeedTreasuriesWithBalance(db, branchId);
        var session = CreateOpenSession(db, adminId, branchId, vault.Id);

        // Create supplier and bill
        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = "مورد تجريبي",
            IsActive = true
        };
        db.Suppliers.Add(supplier);

        var bill = new SupplierBill
        {
            Id = Guid.NewGuid(),
            BillNumber = $"BILL-{DateTime.UtcNow:yyyyMMdd}-001",
            SupplierId = supplier.Id,
            Description = "فاتورة تجريبية",
            TotalAmount = 30_000m,
            PaidAmount = 0,
            Status = BillStatus.Unpaid,
            BillDate = DateOnly.FromDateTime(DateTime.Today),
            BranchId = branchId,
            CreatedBy = adminId,
            IsActive = true
        };
        db.SupplierBills.Add(bill);
        await db.SaveChangesAsync();

        var initialBankBalance = bank.Balance;

        var currentUser = CreateAdminCurrentUser(adminId, branchId);
        var audit = new Mock<IAuditService>().Object;
        var journalEntryService = new JournalEntryService(db, new Mock<ILogger<JournalEntryService>>().Object);
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        var controller = new SupplierBillsController(db, currentUser, audit, journalEntryService, treasuryResolution);

        // Act — pay with bank transfer
        var result = await controller.Pay(bill.Id, new PayBillInstallmentRequest
        {
            Amount = 30_000m,
            PaymentMethod = "bank_transfer"
        });

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        // Bank treasury should be decremented by the payment amount
        var bankAfter = await db.Treasuries.FindAsync(bank.Id);
        bankAfter!.Balance.Should().Be(initialBankBalance - 30_000m,
            "bank supplier payment must decrement the bank treasury");

        // Vault should be UNCHANGED (this was a bank transfer)
        var vaultAfter = await db.Treasuries.FindAsync(vault.Id);
        vaultAfter!.Balance.Should().Be(500_000m,
            "vault must not be affected by a bank supplier payment");

        // CashFlowTransaction should reference the bank treasury
        var cashflow = await db.CashFlowTransactions
            .FirstOrDefaultAsync(t => t.ReferenceId == bill.Id && t.Category == FinancialCategory.SupplierPayment);
        cashflow.Should().NotBeNull();
        cashflow!.TreasuryId.Should().Be(bank.Id,
            "bank supplier payment must reference the bank treasury");

        // Bill should be fully paid
        var paidBill = await db.SupplierBills.FindAsync(bill.Id);
        paidBill!.Status.Should().Be(BillStatus.FullyPaid);
        paidBill.PaidAmount.Should().Be(30_000m);
    }
}
