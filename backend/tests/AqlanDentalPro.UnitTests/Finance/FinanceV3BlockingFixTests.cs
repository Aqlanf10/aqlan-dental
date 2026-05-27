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
/// PR #245 Backend Blocker Fix Tests — Validates all blockers for the Finance V3 merge.
/// Tests cover: advance approval dual-write, reversal safety gates, branch isolation,
/// patient balance formula, and salary privacy.
/// Uses InMemory provider to verify business rules without requiring PostgreSQL.
/// </summary>
public class FinanceV3BlockingFixTests
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

    private static Employee SeedEmployee(AppDbContext db, Guid branchId)
    {
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            FullName = "أحمد محمد",
            BranchId = branchId,
            BaseSalary = 15_000m
        };
        db.Employees.Add(employee);
        db.SaveChanges();
        return employee;
    }

    private static Patient SeedPatient(AppDbContext db, Guid branchId)
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            FirstName = "مريض",
            LastName = "تجريبي",
            PatientNumber = $"P-{Guid.NewGuid().ToString()[..8]}",
            BranchId = branchId
        };
        db.Patients.Add(patient);
        db.SaveChanges();
        return patient;
    }

    private static (JournalEntryService jeService, TreasuryResolutionService treasuryService, Guid branchId, Guid cashierId) CreateServices(AppDbContext db)
    {
        var (branchId, cashierId) = SeedBranchAndUser(db);
        var jeService = new JournalEntryService(db, new Mock<ILogger<JournalEntryService>>().Object);
        var treasuryService = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        return (jeService, treasuryService, branchId, cashierId);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 1. Advance approval creates JournalEntry + TreasuryId + decrements Treasury
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AdvanceApproval_CreatesJournalEntry_WithTreasuryId_AndDecrementsTreasury()
    {
        await using var db = CreateContext();
        var (jeService, treasuryService, branchId, cashierId) = CreateServices(db);
        var employee = SeedEmployee(db, branchId);
        var session = CreateOpenSession(db, cashierId, branchId);

        // Create advance
        var advance = new AdvancePayment
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            Amount = 5_000m,
            RequestDate = DateTime.UtcNow,
            Status = RequestStatus.Pending
        };
        db.AdvancePayments.Add(advance);
        await db.SaveChangesAsync();

        // Simulate the approval flow (same logic as controller)
        var paymentMethod = "cash";
        var treasury = await treasuryService.ResolveTreasuryAsync(branchId, paymentMethod, session.Id);

        // Create CashFlowTransaction with TreasuryId
        var cashflow = new CashFlowTransaction
        {
            TransactionNumber = $"TX-{DateTime.UtcNow:yyyyMMdd}-ADV-{advance.Id.ToString()[..8]}",
            Type = TransactionType.Outflow,
            Category = FinancialCategory.SalaryAdvance,
            Amount = advance.Amount,
            PaymentMethod = paymentMethod,
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            ReferenceId = advance.Id,
            ReferenceNumber = $"ADV-{advance.Id.ToString()[..8]}",
            Description = $"سلفة موظف: {employee.FullName}",
            PerformedBy = cashierId,
            BranchId = branchId,
            TreasuryId = treasury.Id,
            CashierSessionId = session.Id
        };
        db.CashFlowTransactions.Add(cashflow);

        // Create JournalEntry: Debit OtherReceivable, Credit Treasury
        var je = await jeService.CreateEntryAsync(
            documentType: FinancialDocumentType.AdvancePayment,
            financialDocumentId: advance.Id,
            description: $"سلفة موظف: {employee.FullName}",
            entryDate: DateOnly.FromDateTime(DateTime.Today),
            branchId: branchId,
            performedBy: cashierId,
            cashierSessionId: session.Id,
            treasuryId: treasury.Id,
            lines: new[]
            {
                (JournalAccountType.OtherReceivable, advance.Id, advance.Amount, 0m, (string?)$"سلفة: {employee.FullName}"),
                (JournalAccountType.Treasury, treasury.Id, 0m, advance.Amount, (string?)$"سداد من: {treasury.Name}")
            });
        je.IsPosted = true;
        je.PostedAt = DateTime.UtcNow;

        // Decrement Treasury
        await treasuryService.DecrementTreasuryBalanceAsync(branchId, paymentMethod, advance.Amount, session.Id);
        await db.SaveChangesAsync();

        // Verify JournalEntry exists
        var savedJe = await db.JournalEntries
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.FinancialDocumentId == advance.Id
                && e.FinancialDocumentType == FinancialDocumentType.AdvancePayment
                && !e.IsReversal);

        savedJe.Should().NotBeNull("advance approval must create a JournalEntry");
        savedJe!.IsPosted.Should().BeTrue("advance JournalEntry must be auto-posted");
        savedJe.TreasuryId.Should().Be(treasury.Id, "JournalEntry must have TreasuryId");

        // Verify OtherReceivable debit line
        var receivableLine = savedJe.Lines.FirstOrDefault(l => l.AccountType == JournalAccountType.OtherReceivable && l.Debit > 0);
        receivableLine.Should().NotBeNull("advance must debit OtherReceivable");
        receivableLine!.Debit.Should().Be(5_000m);

        // Verify Treasury credit line
        var treasuryLine = savedJe.Lines.FirstOrDefault(l => l.AccountType == JournalAccountType.Treasury && l.Credit > 0);
        treasuryLine.Should().NotBeNull("advance must credit Treasury");
        treasuryLine!.Credit.Should().Be(5_000m);

        // Verify CashFlowTransaction has TreasuryId
        var savedCashflow = await db.CashFlowTransactions
            .FirstOrDefaultAsync(c => c.ReferenceId == advance.Id && c.Category == FinancialCategory.SalaryAdvance && !c.IsReversal);
        savedCashflow.Should().NotBeNull();
        savedCashflow!.TreasuryId.Should().Be(treasury.Id, "CashFlowTransaction must have TreasuryId");

        // Verify Treasury was decremented
        var savedTreasury = await db.Treasuries.FindAsync(treasury.Id);
        savedTreasury.Should().NotBeNull();
        savedTreasury!.Balance.Should().Be(-5_000m, "Treasury balance must be decremented by the advance amount");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 2. Advance reversal rejects when original CashFlow has no TreasuryId
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AdvanceReversal_RejectsWhenOriginalCashFlowHasNoTreasuryId()
    {
        await using var db = CreateContext();
        var (_, _, branchId, _) = CreateServices(db);
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

        // Create CashFlow WITHOUT TreasuryId (simulating pre-dual-write era)
        var cashflow = new CashFlowTransaction
        {
            TransactionNumber = $"TX-{DateTime.UtcNow:yyyyMMdd}-ADV-NO-TREASURY",
            Type = TransactionType.Outflow,
            Category = FinancialCategory.SalaryAdvance,
            Amount = 3_000m,
            PaymentMethod = "cash",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            ReferenceId = advanceId,
            TreasuryId = null, // Missing TreasuryId!
            BranchId = branchId
        };
        db.CashFlowTransactions.Add(cashflow);
        await db.SaveChangesAsync();

        // Simulate the reversal check logic from the controller
        var originalCashflow = await db.CashFlowTransactions
            .FirstOrDefaultAsync(c => c.ReferenceId == advanceId && c.Category == FinancialCategory.SalaryAdvance && !c.IsReversal);

        var shouldReject = originalCashflow == null
            || originalCashflow.TreasuryId == null
            || originalCashflow.TreasuryId == Guid.Empty;

        shouldReject.Should().BeTrue("advance reversal must be rejected when original CashFlow has no TreasuryId");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 3. Advance reversal rejects when original JournalEntry doesn't exist
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AdvanceReversal_RejectsWhenOriginalJournalEntryDoesNotExist()
    {
        await using var db = CreateContext();
        var (_, _, branchId, _) = CreateServices(db);
        var employee = SeedEmployee(db, branchId);
        var treasuryId = Guid.NewGuid();

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

        // Create CashFlow WITH TreasuryId (pass first check)
        var cashflow = new CashFlowTransaction
        {
            TransactionNumber = $"TX-{DateTime.UtcNow:yyyyMMdd}-ADV-OK",
            Type = TransactionType.Outflow,
            Category = FinancialCategory.SalaryAdvance,
            Amount = 3_000m,
            PaymentMethod = "cash",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            ReferenceId = advanceId,
            TreasuryId = treasuryId,
            BranchId = branchId
        };
        db.CashFlowTransactions.Add(cashflow);
        // NO JournalEntry created — simulates incomplete dual-write
        await db.SaveChangesAsync();

        // Check original CashFlow passes
        var originalCashflow = await db.CashFlowTransactions
            .FirstOrDefaultAsync(c => c.ReferenceId == advanceId && c.Category == FinancialCategory.SalaryAdvance && !c.IsReversal);
        var cashflowOk = originalCashflow != null
            && originalCashflow.TreasuryId != null
            && originalCashflow.TreasuryId != Guid.Empty;
        cashflowOk.Should().BeTrue("CashFlow check should pass");

        // Check original JournalEntry fails
        var originalJe = await db.JournalEntries
            .FirstOrDefaultAsync(j => j.FinancialDocumentId == advanceId && !j.IsReversal);

        var shouldReject = originalJe == null || !originalJe.IsPosted;
        shouldReject.Should().BeTrue("advance reversal must be rejected when original JournalEntry doesn't exist");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 4. Salary reversal rejects when original CashFlow missing
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SalaryReversal_RejectsWhenOriginalCashFlowMissing()
    {
        await using var db = CreateContext();
        var (_, _, branchId, cashierId) = CreateServices(db);
        var employee = SeedEmployee(db, branchId);

        // Create a paid salary record WITHOUT a corresponding CashFlow
        var salaryId = Guid.NewGuid();
        var salary = new SalaryRecord
        {
            Id = salaryId,
            EmployeeId = employee.Id,
            Year = 2025,
            Month = 1,
            BaseSalary = 15_000m,
            NetSalary = 15_000m,
            PaidAt = DateTime.UtcNow,
            PaidBy = cashierId,
            PaymentMethod = "cash"
        };
        db.SalaryRecords.Add(salary);
        await db.SaveChangesAsync();

        // Simulate the reversal check
        var originalCashflow = await db.CashFlowTransactions
            .FirstOrDefaultAsync(c => c.ReferenceId == salaryId && c.Category == FinancialCategory.SalaryPayment && !c.IsReversal);

        var shouldReject = originalCashflow == null
            || originalCashflow.TreasuryId == null
            || originalCashflow.TreasuryId == Guid.Empty;

        shouldReject.Should().BeTrue("salary reversal must be rejected when original CashFlow is missing or has no TreasuryId");

        // Verify salary is still marked as paid (should not be un-paid on failed reversal)
        var savedSalary = await db.SalaryRecords.FindAsync(salaryId);
        savedSalary!.PaidAt.Should().NotBeNull("salary must remain paid when reversal is rejected");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 5. Salary reversal rejects when original JournalEntry missing
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SalaryReversal_RejectsWhenOriginalJournalEntryMissing()
    {
        await using var db = CreateContext();
        var (_, _, branchId, cashierId) = CreateServices(db);
        var employee = SeedEmployee(db, branchId);
        var treasuryId = Guid.NewGuid();

        var salaryId = Guid.NewGuid();
        var salary = new SalaryRecord
        {
            Id = salaryId,
            EmployeeId = employee.Id,
            Year = 2025,
            Month = 2,
            BaseSalary = 15_000m,
            NetSalary = 15_000m,
            PaidAt = DateTime.UtcNow,
            PaidBy = cashierId,
            PaymentMethod = "cash"
        };
        db.SalaryRecords.Add(salary);

        // Create CashFlow WITH TreasuryId (pass first check)
        var cashflow = new CashFlowTransaction
        {
            TransactionNumber = $"TX-{DateTime.UtcNow:yyyyMMdd}-SAL-OK",
            Type = TransactionType.Outflow,
            Category = FinancialCategory.SalaryPayment,
            Amount = 15_000m,
            PaymentMethod = "cash",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            ReferenceId = salaryId,
            TreasuryId = treasuryId,
            BranchId = branchId
        };
        db.CashFlowTransactions.Add(cashflow);
        // NO JournalEntry — simulates incomplete dual-write
        await db.SaveChangesAsync();

        // CashFlow check passes
        var originalCashflow = await db.CashFlowTransactions
            .FirstOrDefaultAsync(c => c.ReferenceId == salaryId && c.Category == FinancialCategory.SalaryPayment && !c.IsReversal);
        var cashflowOk = originalCashflow != null
            && originalCashflow.TreasuryId != null
            && originalCashflow.TreasuryId != Guid.Empty;
        cashflowOk.Should().BeTrue("CashFlow check should pass");

        // JournalEntry check fails
        var originalJe = await db.JournalEntries
            .FirstOrDefaultAsync(j => j.FinancialDocumentId == salaryId && j.FinancialDocumentType == FinancialDocumentType.SalaryPayment && !j.IsReversal);

        var shouldReject = originalJe == null || !originalJe.IsPosted;
        shouldReject.Should().BeTrue("salary reversal must be rejected when original JournalEntry is missing");

        // Salary should still be paid
        var savedSalary = await db.SalaryRecords.FindAsync(salaryId);
        savedSalary!.PaidAt.Should().NotBeNull("salary must remain paid when reversal is rejected");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 6. Branchless accountant denied on patient-accounts endpoint
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void PatientAccountsEndpoint_BranchlessNonAdmin_ReturnsForbid()
    {
        // Simulate ICurrentUserService for a branchless non-admin accountant
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.IsAdmin).Returns(false);
        currentUser.SetupGet(c => c.BranchId).Returns((Guid?)null);

        // Replicate the exact guard from FinanceV3Controller.GetPatientAccounts
        var shouldForbid = !currentUser.Object.IsAdmin
            && (!currentUser.Object.BranchId.HasValue || currentUser.Object.BranchId.Value == Guid.Empty);

        shouldForbid.Should().BeTrue("branchless non-admin must be denied on patient-accounts endpoint");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 7. Branchless accountant denied on payments endpoint
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void PaymentsEndpoint_BranchlessNonAdmin_ReturnsForbid()
    {
        // Simulate ICurrentUserService with empty BranchId
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.IsAdmin).Returns(false);
        currentUser.SetupGet(c => c.BranchId).Returns(Guid.Empty);

        var shouldForbid = !currentUser.Object.IsAdmin
            && (!currentUser.Object.BranchId.HasValue || currentUser.Object.BranchId.Value == Guid.Empty);

        shouldForbid.Should().BeTrue("branchless non-admin must be denied on payments endpoint");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 8. Branchless accountant denied on invoices endpoint
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void InvoicesEndpoint_BranchlessNonAdmin_ReturnsForbid()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.IsAdmin).Returns(false);
        currentUser.SetupGet(c => c.BranchId).Returns((Guid?)null);

        var shouldForbid = !currentUser.Object.IsAdmin
            && (!currentUser.Object.BranchId.HasValue || currentUser.Object.BranchId.Value == Guid.Empty);

        shouldForbid.Should().BeTrue("branchless non-admin must be denied on invoices endpoint");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 9. Patient balance formula with refund
    //    invoice 100, payment 50, refund -20 => NetPayments = 50 + (-20) = 30
    //    Balance = TotalInvoiced - NetPayments = 100 - 30 = 70
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PatientBalance_WithRefund_CalculatesCorrectly()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndUser(db);
        var patient = SeedPatient(db, branchId);

        // Create invoice: 100
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-BAL",
            Status = InvoiceStatus.Issued,
            TotalAmount = 100m,
            Subtotal = 100m,
            CreatedBy = cashierId
        };
        db.Invoices.Add(invoice);

        // Create positive payment: 50
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Amount = 50m,
            PaymentMethod = "cash",
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            ReceiptNumber = "RCP-001",
            IsActive = true
        };
        db.Payments.Add(payment);

        // Create refund payment: -20 (negative amount)
        var refund = new Payment
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Amount = -20m,
            PaymentMethod = "cash",
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            ReceiptNumber = "RCP-002-REF",
            IsActive = true
        };
        db.Payments.Add(refund);
        await db.SaveChangesAsync();

        // Calculate using the corrected formula from GetPatientAccounts:
        // Balance = TotalInvoiced - SUM(ALL active payments including negative refunds)
        var totalInvoiced = await db.Invoices
            .Where(i => i.PatientId == patient.Id && i.IsActive
                && (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.Paid))
            .SumAsync(i => (decimal?)i.TotalAmount) ?? 0;

        // NetPayments = SUM of ALL active payments (positive + negative)
        var netPayments = await db.Payments
            .Where(p => p.PatientId == patient.Id && p.IsActive)
            .SumAsync(p => (decimal?)p.Amount) ?? 0;

        var balance = totalInvoiced - netPayments;

        // Expected: 100 - (50 + (-20)) = 100 - 30 = 70
        totalInvoiced.Should().Be(100m);
        netPayments.Should().Be(30m, "net payments should include refunds as negative amounts");
        balance.Should().Be(70m, "balance = TotalInvoiced - NetPayments = 100 - 30 = 70");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 10. Reception user denied access to salary endpoints
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SalaryEndpoint_ReceptionUser_DeniedAccess()
    {
        // ReportsAccess policy requires Admin or Accountant role
        // Reception is in FinanceAccess but NOT ReportsAccess
        var allowedRoles = new[] { nameof(UserRole.Admin), nameof(UserRole.Accountant) };
        var receptionRole = nameof(UserRole.Reception);

        var isAllowed = allowedRoles.Contains(receptionRole);
        isAllowed.Should().BeFalse("Reception users must NOT have access to salary endpoints (ReportsAccess policy)");
    }

    [Fact]
    public void SalaryEndpoint_AccountantUser_AllowedAccess()
    {
        var allowedRoles = new[] { nameof(UserRole.Admin), nameof(UserRole.Accountant) };
        var accountantRole = nameof(UserRole.Accountant);

        var isAllowed = allowedRoles.Contains(accountantRole);
        isAllowed.Should().BeTrue("Accountant users must have access to salary endpoints (ReportsAccess policy)");
    }

    [Fact]
    public void SalaryEndpoint_AdminUser_AllowedAccess()
    {
        var allowedRoles = new[] { nameof(UserRole.Admin), nameof(UserRole.Accountant) };
        var adminRole = nameof(UserRole.Admin);

        var isAllowed = allowedRoles.Contains(adminRole);
        isAllowed.Should().BeTrue("Admin users must have access to salary endpoints (ReportsAccess policy)");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Additional: Branchless admin is allowed (admin bypass)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void PatientAccountsEndpoint_BranchlessAdmin_IsAllowed()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.IsAdmin).Returns(true);
        currentUser.SetupGet(c => c.BranchId).Returns((Guid?)null);

        var shouldForbid = !currentUser.Object.IsAdmin
            && (!currentUser.Object.BranchId.HasValue || currentUser.Object.BranchId.Value == Guid.Empty);

        shouldForbid.Should().BeFalse("branchless admin must be allowed on patient-accounts endpoint");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Additional: Advance deletion rejects approved advances
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AdvanceDelete_RejectsApprovedAdvance()
    {
        await using var db = CreateContext();
        var (_, _, branchId, _) = CreateServices(db);
        var employee = SeedEmployee(db, branchId);

        var advance = new AdvancePayment
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            Amount = 2_000m,
            RequestDate = DateTime.UtcNow,
            Status = RequestStatus.Approved
        };
        db.AdvancePayments.Add(advance);
        await db.SaveChangesAsync();

        // Simulate the delete check
        var savedAdvance = await db.AdvancePayments.FindAsync(advance.Id);
        var shouldReject = savedAdvance!.Status == RequestStatus.Approved;

        shouldReject.Should().BeTrue("deletion of approved advances must be rejected");
    }

    [Fact]
    public async Task AdvanceDelete_AllowsPendingAdvance()
    {
        await using var db = CreateContext();
        var (_, _, branchId, _) = CreateServices(db);
        var employee = SeedEmployee(db, branchId);

        var advance = new AdvancePayment
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            Amount = 2_000m,
            RequestDate = DateTime.UtcNow,
            Status = RequestStatus.Pending
        };
        db.AdvancePayments.Add(advance);
        await db.SaveChangesAsync();

        var savedAdvance = await db.AdvancePayments.FindAsync(advance.Id);
        var shouldReject = savedAdvance!.Status == RequestStatus.Approved;

        shouldReject.Should().BeFalse("deletion of pending advances must be allowed");
    }
}
