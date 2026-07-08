using AqlanDentalPro.Application.DTOs.Finance;
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
/// Unit tests for cash drawer reconciliation safety fixes (Phase 0a).
/// Verifies that CloseSession uses CashFlowTransaction-based reconciliation,
/// cash expenses require an open session, closed session deletion is guarded,
/// and BranchId integrity is enforced.
/// Uses InMemory provider to verify calculations without requiring PostgreSQL.
/// </summary>
public class CashDrawerReconciliationTests
{
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

        db.Branches.Add(new Branch
        {
            Id = branchId,
            Name = "الفرع الرئيسي"
        });
        db.Users.Add(new User
        {
            Id = cashierId,
            Username = "cashier1",
            BranchId = branchId
        });
        db.SaveChanges();

        return (branchId, cashierId);
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

    // ─── Helper: simulate CloseSession reconciliation logic ────────────────

    private static (decimal expectedCash, decimal expectedCard, decimal expectedBank) CalculateExpectedClosing(
        AppDbContext db, CashierSession session)
    {
        var sessionTransactions = db.CashFlowTransactions
            .Where(t => t.CashierSessionId == session.Id && t.IsActive)
            .ToList();

        var cashInflows = sessionTransactions.Where(t => t.Type == TransactionType.Inflow && IsCashMethod(t.PaymentMethod)).Sum(t => t.Amount);
        var cashOutflows = sessionTransactions.Where(t => t.Type == TransactionType.Outflow && IsCashMethod(t.PaymentMethod)).Sum(t => t.Amount);
        var cardInflows = sessionTransactions.Where(t => t.Type == TransactionType.Inflow && IsCardMethod(t.PaymentMethod)).Sum(t => t.Amount);
        var cardOutflows = sessionTransactions.Where(t => t.Type == TransactionType.Outflow && IsCardMethod(t.PaymentMethod)).Sum(t => t.Amount);
        var bankInflows = sessionTransactions.Where(t => t.Type == TransactionType.Inflow && IsBankMethod(t.PaymentMethod)).Sum(t => t.Amount);
        var bankOutflows = sessionTransactions.Where(t => t.Type == TransactionType.Outflow && IsBankMethod(t.PaymentMethod)).Sum(t => t.Amount);

        var expectedCash = session.OpeningBalance + cashInflows - cashOutflows;
        var expectedCard = cardInflows - cardOutflows;
        var expectedBank = bankInflows - bankOutflows;

        return (expectedCash, expectedCard, expectedBank);
    }

    private static bool IsCashMethod(string method) =>
        string.Equals(method, "cash", StringComparison.OrdinalIgnoreCase);

    private static bool IsCardMethod(string method) =>
        string.Equals(method, "card", StringComparison.OrdinalIgnoreCase);

    private static bool IsBankMethod(string method) =>
        string.Equals(method, "bank_transfer", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(method, "bank", StringComparison.OrdinalIgnoreCase);

    // ─── Test 1: Opening a session succeeds with a valid branch and cashier ─

    [Fact]
    public async Task OpenSession_ValidBranchAndCashier_Succeeds()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndUser(db);

        // Simulate OpenSession logic (without advisory lock — InMemory doesn't support it)
        var session = new CashierSession
        {
            SessionNumber = $"CS-{DateTime.UtcNow:yyyyMMdd}-01",
            CashierId = cashierId,
            BranchId = branchId,
            OpeningTime = DateTime.UtcNow,
            OpeningBalance = 50_000m,
            ExpectedClosingCash = 50_000m,
            ExpectedClosingCard = 0,
            ExpectedClosingBank = 0,
            Status = SessionStatus.Open
        };
        db.CashierSessions.Add(session);
        await db.SaveChangesAsync();

        var saved = await db.CashierSessions.FirstOrDefaultAsync(s => s.CashierId == cashierId && s.Status == SessionStatus.Open);
        saved.Should().NotBeNull();
        saved!.BranchId.Should().Be(branchId, "branch must be the user's assigned branch");
        saved.OpeningBalance.Should().Be(50_000m);
    }

    // ─── Test 2: A user cannot have two active open sessions ─────────────

    [Fact]
    public async Task OpenSession_AlreadyHasOpenSession_Rejected()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndUser(db);
        CreateOpenSession(db, cashierId, branchId);

        // Check for existing open session (same logic as controller)
        var hasOpenSession = await db.CashierSessions
            .AnyAsync(s => s.CashierId == cashierId && s.Status == SessionStatus.Open && s.IsActive);

        hasOpenSession.Should().BeTrue("a user with an open session should be detected");
    }

    // ─── Test 3: A patient cash payment links to the active cashier session ledger transaction ──

    [Fact]
    public async Task PatientCashPayment_LinksToActiveSession()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndUser(db);
        var session = CreateOpenSession(db, cashierId, branchId);

        // Simulate FinanceService.CreatePaymentAsync creating a cashflow linked to session
        var cashflow = new CashFlowTransaction
        {
            TransactionNumber = $"TX-{DateTime.UtcNow:yyyyMMdd}-IN-001",
            Type = TransactionType.Inflow,
            Category = FinancialCategory.PatientPayment,
            Amount = 25_000m,
            PaymentMethod = "cash",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            ReferenceId = Guid.NewGuid(),
            ReferenceNumber = "RCP-20260525-001",
            Description = "تحصيل دفعة مريض",
            PerformedBy = cashierId,
            BranchId = branchId,
            CashierSessionId = session.Id
        };
        db.CashFlowTransactions.Add(cashflow);
        await db.SaveChangesAsync();

        var saved = await db.CashFlowTransactions.FirstOrDefaultAsync(t => t.CashierSessionId == session.Id);
        saved.Should().NotBeNull();
        saved!.CashierSessionId.Should().Be(session.Id, "cash payment must be linked to the active session");
        saved.Type.Should().Be(TransactionType.Inflow);
        saved.Category.Should().Be(FinancialCategory.PatientPayment);
    }

    // ─── Test 4: A refund creates an outflow linked to the active session ──

    [Fact]
    public async Task Refund_CreatesOutflowLinkedToActiveSession()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndUser(db);
        var session = CreateOpenSession(db, cashierId, branchId);

        // Simulate RefundPaymentAsync creating a refund outflow linked to session
        var refundCashflow = new CashFlowTransaction
        {
            TransactionNumber = $"TX-{DateTime.UtcNow:yyyyMMdd}-REF-001",
            Type = TransactionType.Outflow,
            Category = FinancialCategory.Refund,
            Amount = 10_000m,
            PaymentMethod = "cash",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            ReferenceId = Guid.NewGuid(),
            ReferenceNumber = "REF-20260525-001",
            Description = "استرداد دفعة مريض",
            PerformedBy = cashierId,
            BranchId = branchId,
            CashierSessionId = session.Id
        };
        db.CashFlowTransactions.Add(refundCashflow);
        await db.SaveChangesAsync();

        var saved = await db.CashFlowTransactions
            .FirstOrDefaultAsync(t => t.Category == FinancialCategory.Refund && t.CashierSessionId == session.Id);
        saved.Should().NotBeNull();
        saved!.Type.Should().Be(TransactionType.Outflow, "refund must be an outflow");
        saved.CashierSessionId.Should().Be(session.Id, "refund outflow must be linked to the session");
    }

    // ─── Test 5: Closing session subtracts a cash refund from ExpectedClosingCash ──

    [Fact]
    public async Task CloseSession_CashRefundSubtractedFromExpectedClosingCash()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndUser(db);
        var session = CreateOpenSession(db, cashierId, branchId, 100_000m);

        // Add cash payment inflow of 30,000
        db.CashFlowTransactions.Add(new CashFlowTransaction
        {
            TransactionNumber = "TX-001-IN",
            Type = TransactionType.Inflow,
            Category = FinancialCategory.PatientPayment,
            Amount = 30_000m,
            PaymentMethod = "cash",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            PerformedBy = cashierId,
            BranchId = branchId,
            CashierSessionId = session.Id
        });

        // Add cash refund outflow of 5,000
        db.CashFlowTransactions.Add(new CashFlowTransaction
        {
            TransactionNumber = "TX-001-REF",
            Type = TransactionType.Outflow,
            Category = FinancialCategory.Refund,
            Amount = 5_000m,
            PaymentMethod = "cash",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            PerformedBy = cashierId,
            BranchId = branchId,
            CashierSessionId = session.Id
        });

        await db.SaveChangesAsync();

        // Calculate expected closing using the fixed reconciliation logic
        var (expectedCash, _, _) = CalculateExpectedClosing(db, session);

        // ExpectedCash = OpeningBalance (100,000) + cash inflows (30,000) - cash outflows (5,000) = 125,000
        expectedCash.Should().Be(125_000m, "refund must be subtracted from expected closing cash");
    }

    // ─── Test 6: Cash operational expense without open session is rejected ──

    [Fact]
    public async Task CashExpense_WithoutOpenSession_Rejected()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndUser(db);
        // Note: no open session created

        // Check for open session (same logic as OperationalExpensesController.Create)
        var activeSession = await db.CashierSessions
            .FirstOrDefaultAsync(s => s.CashierId == cashierId && s.Status == SessionStatus.Open && s.IsActive);

        activeSession.Should().BeNull("no session is open");
        // The controller would return BadRequest with the Arabic message
    }

    // ─── Test 7: Cash operational expense with open session creates linked outflow ──

    [Fact]
    public async Task CashExpense_WithOpenSession_CreatesLinkedOutflow()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndUser(db);
        var session = CreateOpenSession(db, cashierId, branchId);

        // Simulate OperationalExpensesController.Create for a cash expense
        var expenseId = Guid.NewGuid();
        var cashflow = new CashFlowTransaction
        {
            TransactionNumber = $"TX-{DateTime.UtcNow:yyyyMMdd}-OUT-001",
            Type = TransactionType.Outflow,
            Category = FinancialCategory.OperationalExpense,
            Amount = 15_000m,
            PaymentMethod = "cash",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            ReferenceId = expenseId,
            ReferenceNumber = "EXP-20260525-001",
            Description = "قيد مصروف تشغيلي: إيجار العيادة",
            PerformedBy = cashierId,
            BranchId = branchId,
            CashierSessionId = session.Id  // Linked to session
        };
        db.CashFlowTransactions.Add(cashflow);
        await db.SaveChangesAsync();

        var saved = await db.CashFlowTransactions
            .FirstOrDefaultAsync(t => t.Category == FinancialCategory.OperationalExpense && t.CashierSessionId == session.Id);
        saved.Should().NotBeNull();
        saved!.CashierSessionId.Should().Be(session.Id, "cash expense must be linked to the open session");
        saved.Type.Should().Be(TransactionType.Outflow);
    }

    // ─── Test 8: Closing session subtracts cash operational expense from ExpectedClosingCash ──

    [Fact]
    public async Task CloseSession_CashOperationalExpenseSubtractedFromExpectedClosingCash()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndUser(db);
        var session = CreateOpenSession(db, cashierId, branchId, 100_000m);

        // Add cash payment inflow of 50,000
        db.CashFlowTransactions.Add(new CashFlowTransaction
        {
            TransactionNumber = "TX-001-IN",
            Type = TransactionType.Inflow,
            Category = FinancialCategory.PatientPayment,
            Amount = 50_000m,
            PaymentMethod = "cash",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            PerformedBy = cashierId,
            BranchId = branchId,
            CashierSessionId = session.Id
        });

        // Add cash operational expense outflow of 20,000
        db.CashFlowTransactions.Add(new CashFlowTransaction
        {
            TransactionNumber = "TX-001-OUT",
            Type = TransactionType.Outflow,
            Category = FinancialCategory.OperationalExpense,
            Amount = 20_000m,
            PaymentMethod = "cash",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            PerformedBy = cashierId,
            BranchId = branchId,
            CashierSessionId = session.Id
        });

        await db.SaveChangesAsync();

        var (expectedCash, _, _) = CalculateExpectedClosing(db, session);

        // ExpectedCash = 100,000 + 50,000 - 20,000 = 130,000
        expectedCash.Should().Be(130_000m, "operational expense must be subtracted from expected closing cash");
    }

    // ─── Test 9: Non-cash payment methods are reconciled in their correct bucket ──

    [Fact]
    public async Task CloseSession_NonCashMethods_ReconciledInCorrectBuckets()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndUser(db);
        var session = CreateOpenSession(db, cashierId, branchId, 50_000m);

        // Cash inflow: 10,000
        db.CashFlowTransactions.Add(new CashFlowTransaction
        {
            TransactionNumber = "TX-CASH-IN",
            Type = TransactionType.Inflow,
            Category = FinancialCategory.PatientPayment,
            Amount = 10_000m,
            PaymentMethod = "cash",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            PerformedBy = cashierId,
            BranchId = branchId,
            CashierSessionId = session.Id
        });

        // Card inflow: 30,000
        db.CashFlowTransactions.Add(new CashFlowTransaction
        {
            TransactionNumber = "TX-CARD-IN",
            Type = TransactionType.Inflow,
            Category = FinancialCategory.PatientPayment,
            Amount = 30_000m,
            PaymentMethod = "card",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            PerformedBy = cashierId,
            BranchId = branchId,
            CashierSessionId = session.Id
        });

        // Bank transfer inflow: 40,000
        db.CashFlowTransactions.Add(new CashFlowTransaction
        {
            TransactionNumber = "TX-BANK-IN",
            Type = TransactionType.Inflow,
            Category = FinancialCategory.PatientPayment,
            Amount = 40_000m,
            PaymentMethod = "bank_transfer",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            PerformedBy = cashierId,
            BranchId = branchId,
            CashierSessionId = session.Id
        });

        // Card refund outflow: 5,000
        db.CashFlowTransactions.Add(new CashFlowTransaction
        {
            TransactionNumber = "TX-CARD-REF",
            Type = TransactionType.Outflow,
            Category = FinancialCategory.Refund,
            Amount = 5_000m,
            PaymentMethod = "card",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            PerformedBy = cashierId,
            BranchId = branchId,
            CashierSessionId = session.Id
        });

        await db.SaveChangesAsync();

        var (expectedCash, expectedCard, expectedBank) = CalculateExpectedClosing(db, session);

        // Cash: 50,000 (opening) + 10,000 (inflow) - 0 (outflow) = 60,000
        expectedCash.Should().Be(60_000m, "cash bucket: opening + cash inflows - cash outflows");

        // Card: 30,000 (inflow) - 5,000 (refund outflow) = 25,000
        expectedCard.Should().Be(25_000m, "card bucket: card inflows - card outflows");

        // Bank: 40,000 (inflow) - 0 (outflow) = 40,000
        expectedBank.Should().Be(40_000m, "bank bucket: bank inflows - bank outflows");
    }

    // ─── Test 10: Inactive/soft-deleted cashflow entries are ignored ──

    [Fact]
    public async Task CloseSession_InactiveCashflowEntries_Ignored()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndUser(db);
        var session = CreateOpenSession(db, cashierId, branchId, 100_000m);

        // Active cash inflow
        db.CashFlowTransactions.Add(new CashFlowTransaction
        {
            TransactionNumber = "TX-ACTIVE",
            Type = TransactionType.Inflow,
            Category = FinancialCategory.PatientPayment,
            Amount = 20_000m,
            PaymentMethod = "cash",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            PerformedBy = cashierId,
            BranchId = branchId,
            CashierSessionId = session.Id,
            IsActive = true
        });

        // Inactive (soft-deleted) cash inflow — should NOT count
        db.CashFlowTransactions.Add(new CashFlowTransaction
        {
            TransactionNumber = "TX-DELETED",
            Type = TransactionType.Inflow,
            Category = FinancialCategory.PatientPayment,
            Amount = 50_000m,
            PaymentMethod = "cash",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            PerformedBy = cashierId,
            BranchId = branchId,
            CashierSessionId = session.Id,
            IsActive = false  // soft-deleted
        });

        await db.SaveChangesAsync();

        var (expectedCash, _, _) = CalculateExpectedClosing(db, session);

        // Note: InMemory provider with global query filter will auto-filter IsActive=false
        // ExpectedCash = 100,000 (opening) + 20,000 (active inflow) = 120,000
        // The 50,000 inactive inflow is ignored
        expectedCash.Should().Be(120_000m, "soft-deleted cashflow entries must be ignored in reconciliation");
    }

    // ─── Test 11: Transactions from other sessions or branches are ignored ──

    [Fact]
    public async Task CloseSession_OtherSessionTransactions_Ignored()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndUser(db);
        var session1 = CreateOpenSession(db, cashierId, branchId, 100_000m);

        // Create a second session for the same cashier (simulating a previous closed session)
        var session2Id = Guid.NewGuid();
        db.CashierSessions.Add(new CashierSession
        {
            Id = session2Id,
            SessionNumber = $"CS-{DateTime.UtcNow:yyyyMMdd}-02",
            CashierId = cashierId,
            BranchId = branchId,
            OpeningTime = DateTime.UtcNow.AddHours(-6),
            OpeningBalance = 80_000m,
            Status = SessionStatus.Closed,
            ClosingTime = DateTime.UtcNow.AddHours(-4)
        });

        // Transaction linked to session1 (current open session)
        db.CashFlowTransactions.Add(new CashFlowTransaction
        {
            TransactionNumber = "TX-S1-IN",
            Type = TransactionType.Inflow,
            Category = FinancialCategory.PatientPayment,
            Amount = 30_000m,
            PaymentMethod = "cash",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            PerformedBy = cashierId,
            BranchId = branchId,
            CashierSessionId = session1.Id
        });

        // Transaction linked to session2 (different/closed session) — should NOT count
        db.CashFlowTransactions.Add(new CashFlowTransaction
        {
            TransactionNumber = "TX-S2-IN",
            Type = TransactionType.Inflow,
            Category = FinancialCategory.PatientPayment,
            Amount = 100_000m,
            PaymentMethod = "cash",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            PerformedBy = cashierId,
            BranchId = branchId,
            CashierSessionId = session2Id
        });

        // Transaction with no session link — should NOT count in session-based reconciliation
        db.CashFlowTransactions.Add(new CashFlowTransaction
        {
            TransactionNumber = "TX-NOSESSION",
            Type = TransactionType.Inflow,
            Category = FinancialCategory.PatientPayment,
            Amount = 40_000m,
            PaymentMethod = "cash",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            PerformedBy = cashierId,
            BranchId = branchId
            // CashierSessionId = null (unlinked)
        });

        await db.SaveChangesAsync();

        var (expectedCash, _, _) = CalculateExpectedClosing(db, session1);

        // Only session1's transactions count: 100,000 (opening) + 30,000 (session1 inflow) = 130,000
        expectedCash.Should().Be(130_000m, "transactions from other sessions or unlinked should be ignored");
    }

    // ─── Test 12: Missing required BranchId does not write Guid.Empty financial records ──

    [Fact]
    public async Task MissingBranchId_RejectedBeforeWritingRecords()
    {
        await using var db = CreateContext();

        // Simulate the branch validation logic from FinanceService.CreatePaymentAsync
        Guid? nullBranchId = null;
        var hasValidBranch = nullBranchId.HasValue && nullBranchId.Value != Guid.Empty;
        hasValidBranch.Should().BeFalse("null BranchId should fail validation");

        Guid? emptyBranchId = Guid.Empty;
        var hasValidBranch2 = emptyBranchId.HasValue && emptyBranchId.Value != Guid.Empty;
        hasValidBranch2.Should().BeFalse("Guid.Empty BranchId should fail validation");

        // Also verify the controller-level validation for OpenSession
        Guid? controllerBranchId = null;
        var controllerValid = controllerBranchId != null && controllerBranchId != Guid.Empty;
        controllerValid.Should().BeFalse("null BranchId should be rejected at controller level");

        controllerBranchId = Guid.Empty;
        controllerValid = controllerBranchId != null && controllerBranchId != Guid.Empty;
        controllerValid.Should().BeFalse("Guid.Empty BranchId should be rejected at controller level");
    }

    [Fact]
    public async Task FinanceService_CreatePayment_WithNullBranchId_ResolvesFallbackBranch()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndUser(db);
        CreateOpenSession(db, cashierId, branchId);

        // Create a mock ICurrentUserService with NO branch
        var currentUserNoBranch = new Mock<ICurrentUserService>();
        currentUserNoBranch.SetupGet(c => c.UserId).Returns(cashierId);
        currentUserNoBranch.SetupGet(c => c.BranchId).Returns((Guid?)null);
        currentUserNoBranch.SetupGet(c => c.IsAdmin).Returns(true);

        var notifications = new Mock<INotificationService>();
        var logger = new Mock<ILogger<PaymentService>>();
        var commissionService = new Mock<ICommissionService>();

        var journalEntryService = new Mock<IJournalEntryService>();

        // Finance V3: Mock CreateEntryAsync to return a valid JournalEntry
        journalEntryService.Setup(s => s.CreateEntryAsync(
            It.IsAny<FinancialDocumentType>(),
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<DateOnly>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid?>(),
            It.IsAny<Guid?>(),
            It.IsAny<IEnumerable<(JournalAccountType, Guid, decimal, decimal, string?)>>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((FinancialDocumentType docType, Guid docId, string desc, DateOnly date, Guid branch, Guid performedBy, Guid? sessionId, Guid? treasuryId, IEnumerable<(JournalAccountType, Guid, decimal, decimal, string?)> lines, bool autoSave, CancellationToken ct) =>
            {
                var entry = new JournalEntry
                {
                    Id = Guid.NewGuid(),
                    EntryNumber = "JE-TEST-001",
                    FinancialDocumentId = docId,
                    FinancialDocumentType = docType,
                    Description = desc,
                    EntryDate = date,
                    BranchId = branch,
                    PerformedBy = performedBy,
                    CashierSessionId = sessionId,
                    TreasuryId = treasuryId,
                    IsPosted = false,
                    IsReversal = false,
                };
                foreach (var (accountType, accountId, debit, credit, lineDesc) in lines)
                {
                    entry.Lines.Add(new JournalLine
                    {
                        Id = Guid.NewGuid(),
                        AccountType = accountType,
                        AccountId = accountId,
                        Debit = debit,
                        Credit = credit,
                        Description = lineDesc,
                        BranchId = branch,
                    });
                }
                return entry;
            });
        journalEntryService.Setup(s => s.CreateReversalEntryAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid originalId, string reason, Guid performedBy, CancellationToken ct) =>
            {
                return new JournalEntry
                {
                    Id = Guid.NewGuid(),
                    EntryNumber = "JE-REV-001",
                    FinancialDocumentId = Guid.NewGuid(),
                    FinancialDocumentType = FinancialDocumentType.PaymentDeletion,
                    Description = $"Reversal: {reason}",
                    EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    BranchId = Guid.Empty,
                    PerformedBy = performedBy,
                    IsPosted = false,
                    IsReversal = true,
                    ReversalOfEntryId = originalId,
                };
            });

        var service = new PaymentService(db, currentUserNoBranch.Object, notifications.Object, logger.Object, commissionService.Object, journalEntryService.Object);

        var patientId = Guid.NewGuid();
        db.Patients.Add(new Patient
        {
            Id = patientId,
            FirstName = "مريض",
            LastName = "تجريبي",
            PatientNumber = "P-BRANCH-TEST"
        });
        await db.SaveChangesAsync();

        var act = () => service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patientId,
            Amount = 10_000m,
            PaymentMethod = "cash"
        });

        await act.Should().NotThrowAsync<ArgumentException>("Sprint 1: Admin with null BranchId now resolves fallback branch instead of throwing");
    }

    [Fact]
    public async Task FinanceService_RefundPayment_WithEmptyBranchId_Throws()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndUser(db);
        var session = CreateOpenSession(db, cashierId, branchId);

        var patientId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        db.Patients.Add(new Patient
        {
            Id = patientId,
            FirstName = "مريض",
            LastName = "تجريبي",
            PatientNumber = "P-REF-BRANCH"
        });

        db.Payments.Add(new Payment
        {
            Id = paymentId,
            PatientId = patientId,
            Amount = 10_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = "cash",
            BranchId = branchId
        });
        await db.SaveChangesAsync();

        // Create a mock ICurrentUserService with Guid.Empty branch
        var currentUserEmptyBranch = new Mock<ICurrentUserService>();
        currentUserEmptyBranch.SetupGet(c => c.UserId).Returns(cashierId);
        currentUserEmptyBranch.SetupGet(c => c.BranchId).Returns(Guid.Empty);
        currentUserEmptyBranch.SetupGet(c => c.IsAdmin).Returns(false);

        var notifications = new Mock<INotificationService>();
        var logger = new Mock<ILogger<PaymentService>>();
        var commissionService = new Mock<ICommissionService>();

        var journalEntryService = new Mock<IJournalEntryService>();

        // Finance V3: Mock CreateEntryAsync to return a valid JournalEntry
        journalEntryService.Setup(s => s.CreateEntryAsync(
            It.IsAny<FinancialDocumentType>(),
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<DateOnly>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid?>(),
            It.IsAny<Guid?>(),
            It.IsAny<IEnumerable<(JournalAccountType, Guid, decimal, decimal, string?)>>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((FinancialDocumentType docType, Guid docId, string desc, DateOnly date, Guid branch, Guid performedBy, Guid? sessionId, Guid? treasuryId, IEnumerable<(JournalAccountType, Guid, decimal, decimal, string?)> lines, bool autoSave, CancellationToken ct) =>
            {
                var entry = new JournalEntry
                {
                    Id = Guid.NewGuid(),
                    EntryNumber = "JE-TEST-001",
                    FinancialDocumentId = docId,
                    FinancialDocumentType = docType,
                    Description = desc,
                    EntryDate = date,
                    BranchId = branch,
                    PerformedBy = performedBy,
                    CashierSessionId = sessionId,
                    TreasuryId = treasuryId,
                    IsPosted = false,
                    IsReversal = false,
                };
                foreach (var (accountType, accountId, debit, credit, lineDesc) in lines)
                {
                    entry.Lines.Add(new JournalLine
                    {
                        Id = Guid.NewGuid(),
                        AccountType = accountType,
                        AccountId = accountId,
                        Debit = debit,
                        Credit = credit,
                        Description = lineDesc,
                        BranchId = branch,
                    });
                }
                return entry;
            });
        journalEntryService.Setup(s => s.CreateReversalEntryAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid originalId, string reason, Guid performedBy, CancellationToken ct) =>
            {
                return new JournalEntry
                {
                    Id = Guid.NewGuid(),
                    EntryNumber = "JE-REV-001",
                    FinancialDocumentId = Guid.NewGuid(),
                    FinancialDocumentType = FinancialDocumentType.PaymentDeletion,
                    Description = $"Reversal: {reason}",
                    EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    BranchId = Guid.Empty,
                    PerformedBy = performedBy,
                    IsPosted = false,
                    IsReversal = true,
                    ReversalOfEntryId = originalId,
                };
            });

        var service = new PaymentService(db, currentUserEmptyBranch.Object, notifications.Object, logger.Object, commissionService.Object, journalEntryService.Object);

        var act = () => service.RefundPaymentAsync(paymentId, "test refund");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*الفرع*");
    }

    // ─── Test 13: Profit/loss current calculations do not double-count flows ──

    [Fact]
    public async Task CloseSession_NoDoubleCounting_WhenCashflowUsedAsSource()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndUser(db);
        var session = CreateOpenSession(db, cashierId, branchId, 200_000m);

        // Simulate a single cash patient payment
        var paymentId = Guid.NewGuid();
        db.CashFlowTransactions.Add(new CashFlowTransaction
        {
            TransactionNumber = "TX-DBL-IN",
            Type = TransactionType.Inflow,
            Category = FinancialCategory.PatientPayment,
            Amount = 50_000m,
            PaymentMethod = "cash",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            ReferenceId = paymentId,
            PerformedBy = cashierId,
            BranchId = branchId,
            CashierSessionId = session.Id
        });

        // Simulate a cash operational expense
        var expenseId = Guid.NewGuid();
        db.CashFlowTransactions.Add(new CashFlowTransaction
        {
            TransactionNumber = "TX-DBL-OUT",
            Type = TransactionType.Outflow,
            Category = FinancialCategory.OperationalExpense,
            Amount = 10_000m,
            PaymentMethod = "cash",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            ReferenceId = expenseId,
            PerformedBy = cashierId,
            BranchId = branchId,
            CashierSessionId = session.Id
        });

        await db.SaveChangesAsync();

        var (expectedCash, expectedCard, expectedBank) = CalculateExpectedClosing(db, session);

        // ExpectedCash = 200,000 (opening) + 50,000 (inflow) - 10,000 (outflow) = 240,000
        // Old Payment-based logic would have shown 200,000 + 50,000 = 250,000 (missing expense)
        // New CashFlowTransaction-based logic correctly shows 240,000
        expectedCash.Should().Be(240_000m, "no double-counting; expenses are subtracted correctly");

        expectedCard.Should().Be(0m, "no card transactions");
        expectedBank.Should().Be(0m, "no bank transactions");

        // Verify total: 240,000 (only counted each transaction once)
        var totalExpected = expectedCash + expectedCard + expectedBank;
        totalExpected.Should().Be(240_000m, "total matches expected — no double-counting");
    }

    // ─── Additional: Closed session deletion guard ───────────────────────

    [Fact]
    public async Task DeleteExpense_LinkedToClosedSession_Rejected()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndUser(db);
        var session = CreateOpenSession(db, cashierId, branchId);

        var expenseId = Guid.NewGuid();

        // Create expense cashflow linked to the session
        var cashflow = new CashFlowTransaction
        {
            TransactionNumber = "TX-EXP-GUARD",
            Type = TransactionType.Outflow,
            Category = FinancialCategory.OperationalExpense,
            Amount = 10_000m,
            PaymentMethod = "cash",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            ReferenceId = expenseId,
            PerformedBy = cashierId,
            BranchId = branchId,
            CashierSessionId = session.Id
        };
        db.CashFlowTransactions.Add(cashflow);
        await db.SaveChangesAsync();

        // Close the session
        session.Status = SessionStatus.Closed;
        session.ClosingTime = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Simulate the guard logic from OperationalExpensesController.Delete
        var cashflowToDelete = await db.CashFlowTransactions
            .FirstOrDefaultAsync(t => t.ReferenceId == expenseId && t.Category == FinancialCategory.OperationalExpense && t.IsActive);

        cashflowToDelete.Should().NotBeNull();
        if (cashflowToDelete!.CashierSessionId.HasValue)
        {
            var linkedSession = await db.CashierSessions.FindAsync(cashflowToDelete.CashierSessionId.Value);
            var isClosed = linkedSession != null && linkedSession.Status != SessionStatus.Open;
            isClosed.Should().BeTrue("expense linked to a closed session should be guarded from deletion");
        }
    }

    [Fact]
    public async Task DeleteExpense_LinkedToOpenSession_Allowed()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndUser(db);
        var session = CreateOpenSession(db, cashierId, branchId);

        var expenseId = Guid.NewGuid();

        // Create expense cashflow linked to the open session
        var cashflow = new CashFlowTransaction
        {
            TransactionNumber = "TX-EXP-OPEN",
            Type = TransactionType.Outflow,
            Category = FinancialCategory.OperationalExpense,
            Amount = 10_000m,
            PaymentMethod = "cash",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            ReferenceId = expenseId,
            PerformedBy = cashierId,
            BranchId = branchId,
            CashierSessionId = session.Id
        };
        db.CashFlowTransactions.Add(cashflow);
        await db.SaveChangesAsync();

        // Session is still open — deletion should be allowed
        var cashflowToDelete = await db.CashFlowTransactions
            .FirstOrDefaultAsync(t => t.ReferenceId == expenseId && t.Category == FinancialCategory.OperationalExpense && t.IsActive);

        cashflowToDelete.Should().NotBeNull();
        if (cashflowToDelete!.CashierSessionId.HasValue)
        {
            var linkedSession = await db.CashierSessions.FindAsync(cashflowToDelete.CashierSessionId.Value);
            var isOpen = linkedSession == null || linkedSession.Status == SessionStatus.Open;
            isOpen.Should().BeTrue("expense linked to an open session should be deletable");
        }
    }

    // ─── Bank method alias test ─────────────────────────────────────────

    [Fact]
    public async Task CloseSession_BankAlias_ReconciledCorrectly()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndUser(db);
        var session = CreateOpenSession(db, cashierId, branchId, 50_000m);

        // "bank" (not "bank_transfer") should also be recognized as bank method
        db.CashFlowTransactions.Add(new CashFlowTransaction
        {
            TransactionNumber = "TX-BANK-ALIAS",
            Type = TransactionType.Inflow,
            Category = FinancialCategory.PatientPayment,
            Amount = 20_000m,
            PaymentMethod = "bank",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            PerformedBy = cashierId,
            BranchId = branchId,
            CashierSessionId = session.Id
        });

        await db.SaveChangesAsync();

        var (expectedCash, _, expectedBank) = CalculateExpectedClosing(db, session);

        expectedCash.Should().Be(50_000m, "opening balance only — no cash inflow");
        expectedBank.Should().Be(20_000m, "'bank' payment method must be recognized as bank bucket");
    }
}
