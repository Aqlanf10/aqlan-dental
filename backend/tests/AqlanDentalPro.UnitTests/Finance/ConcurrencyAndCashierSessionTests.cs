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
/// PR #231 — Concurrency safety and CashierSession TreasuryId tests.
///
/// These tests verify:
/// 1. Concurrent financial operations result in one posted movement / one treasury decrement
///    (idempotency / double-post prevention via SELECT FOR UPDATE + state re-check inside transaction).
/// 2. CashierSession.OpenSession explicitly sets TreasuryId mapped to the branch cash vault.
/// 3. Cash movements linked to a session use exactly that session.TreasuryId.
///
/// NOTE: Full PostgreSQL concurrent verification (two real DB connections with FOR UPDATE
/// serialization) cannot be proven with InMemory provider. These tests verify the business
/// rules (state re-check, idempotent rejection, correct treasury routing) that underpin
/// the concurrency safety. Concurrent PostgreSQL verification remains required in a
/// real integration test environment.
/// </summary>
public class ConcurrencyAndCashierSessionTests
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

    private static (Treasury vault, Treasury bank) SeedTreasuries(AppDbContext db, Guid branchId, decimal vaultBalance = 500_000m)
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

    // ═══════════════════════════════════════════════════════════════════════════
    // TEST 1: Concurrent expense approvals result in one posted expense and one treasury decrement
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExpenseApproval_SecondApprovalOnAlreadyApproved_ReturnsBadRequest_NoDoubleTreasuryDecrement()
    {
        await using var db = CreateContext();
        var (branchId, adminId) = SeedBranchAndAdmin(db);
        var (vault, bank) = SeedTreasuries(db, branchId);
        var session = CreateOpenSession(db, adminId, branchId, vault.Id);

        // Create a pending cash expense
        var expense = new OperationalExpense
        {
            Id = Guid.NewGuid(),
            ExpenseNumber = $"EXP-{DateTime.UtcNow:yyyyMMdd}-C01",
            Title = "مصروف نقدي للاختبار",
            Category = ExpenseCategory.Marketing,
            Amount = 10_000m,
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = "cash",
            PaidBy = adminId,
            BranchId = branchId,
            ApprovalStatus = ApprovalStatus.Pending,
            IsPostedToLedger = false,
            IsActive = true
        };
        db.OperationalExpenses.Add(expense);
        await db.SaveChangesAsync();

        var currentUser = CreateAdminCurrentUser(adminId, branchId);
        var audit = new Mock<IAuditService>().Object;
        var journalEntryService = new JournalEntryService(db, new Mock<ILogger<JournalEntryService>>().Object);
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);

        var controller = new OperationalExpensesController(db, currentUser, audit, journalEntryService, treasuryResolution);

        // First approval — should succeed
        var firstResult = await controller.Approve(expense.Id, new ApproveExpenseRequest());
        firstResult.Should().BeOfType<OkObjectResult>("first approval should succeed");

        var vaultAfterFirst = (await db.Treasuries.FindAsync(vault.Id))!.Balance;
        vaultAfterFirst.Should().Be(500_000m - 10_000m, "first approval decrements treasury");

        // Second approval attempt — should be rejected (already approved)
        var secondResult = await controller.Approve(expense.Id, new ApproveExpenseRequest());
        secondResult.Should().BeOfType<BadRequestObjectResult>("second concurrent approval must be rejected");

        // Treasury must NOT be decremented again
        var vaultAfterSecond = (await db.Treasuries.FindAsync(vault.Id))!.Balance;
        vaultAfterSecond.Should().Be(vaultAfterFirst, "second approval must NOT double-decrement treasury");

        // Only one CashFlowTransaction should exist
        var cashflowCount = await db.CashFlowTransactions.CountAsync(t => t.ReferenceId == expense.Id);
        cashflowCount.Should().Be(1, "only one cashflow should be created for the expense");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TEST 2: Concurrent expense reversals result in one reversal and one treasury restoration
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExpenseReversal_SecondReversalOnAlreadyReversed_ReturnsBadRequest_NoDoubleRestore()
    {
        await using var db = CreateContext();
        var (branchId, adminId) = SeedBranchAndAdmin(db);
        var (vault, bank) = SeedTreasuries(db, branchId, 200_000m);
        var session = CreateOpenSession(db, adminId, branchId, vault.Id);

        // Create a posted cash expense with a known treasury
        var expense = new OperationalExpense
        {
            Id = Guid.NewGuid(),
            ExpenseNumber = $"EXP-{DateTime.UtcNow:yyyyMMdd}-C02",
            Title = "مصروف مرحل للاختبار العكس",
            Category = ExpenseCategory.Miscellaneous,
            Amount = 15_000m,
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
            TransactionNumber = $"TX-{DateTime.UtcNow:yyyyMMdd}-OUT-C02",
            Type = TransactionType.Outflow,
            Category = FinancialCategory.OperationalExpense,
            Amount = 15_000m,
            PaymentMethod = "cash",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            ReferenceId = expense.Id,
            ReferenceNumber = expense.ExpenseNumber,
            Description = $"قيد مصروف: {expense.Title}",
            PerformedBy = adminId,
            BranchId = branchId,
            TreasuryId = vault.Id,
            CashierSessionId = session.Id,
            IsActive = true
        };

        expense.CashFlowTransactionId = cashflow.Id;
        db.OperationalExpenses.Add(expense);
        db.CashFlowTransactions.Add(cashflow);
        vault.Balance -= 15_000m;
        await db.SaveChangesAsync();

        var balanceBeforeReversal = vault.Balance; // 185,000

        var currentUser = CreateAdminCurrentUser(adminId, branchId);
        var audit = new Mock<IAuditService>().Object;
        var journalEntryService = new JournalEntryService(db, new Mock<ILogger<JournalEntryService>>().Object);
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);

        var controller = new OperationalExpensesController(db, currentUser, audit, journalEntryService, treasuryResolution);

        // First reversal — should succeed
        var firstResult = await controller.Delete(expense.Id);
        firstResult.Should().BeOfType<OkObjectResult>("first reversal should succeed");

        var vaultAfterFirst = (await db.Treasuries.FindAsync(vault.Id))!.Balance;
        vaultAfterFirst.Should().Be(balanceBeforeReversal + 15_000m, "first reversal restores treasury");

        // Second reversal attempt — the expense is now inactive, so it returns NotFound
        var secondResult = await controller.Delete(expense.Id);
        secondResult.Should().BeOfType<NotFoundObjectResult>("second reversal on inactive expense should be NotFound");

        // Treasury must NOT be double-restored
        var vaultAfterSecond = (await db.Treasuries.FindAsync(vault.Id))!.Balance;
        vaultAfterSecond.Should().Be(vaultAfterFirst, "second reversal must NOT double-restore treasury");

        // Only one reversal cashflow should exist
        var reversalCount = await db.CashFlowTransactions.CountAsync(t => t.IsReversal && t.ReversalOfTransactionId == cashflow.Id);
        reversalCount.Should().Be(1, "only one reversal cashflow should exist");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TEST 3: Concurrent salary payments result in one payment/outflow
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SalaryPayment_SecondPaymentOnAlreadyPaid_ReturnsBadRequest_NoDoubleOutflow()
    {
        await using var db = CreateContext();
        var (branchId, adminId) = SeedBranchAndAdmin(db);
        var (vault, bank) = SeedTreasuries(db, branchId);
        var session = CreateOpenSession(db, adminId, branchId, vault.Id);

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
            Deductions = 0,
            Advances = 0,
            Bonuses = 0,
            NetSalary = 50_000m,
            IsActive = true
        };
        db.SalaryRecords.Add(salaryRecord);
        await db.SaveChangesAsync();

        var initialVaultBalance = vault.Balance;

        var journalEntryService = new JournalEntryService(db, new Mock<ILogger<JournalEntryService>>().Object);
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        var controller = new SalaryController(db, journalEntryService, treasuryResolution);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
        };
        var claims = new System.Security.Claims.ClaimsIdentity(new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, adminId.ToString()),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Admin")
        });
        controller.ControllerContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(claims);

        // First payment — should succeed
        var firstResult = await controller.PaySalary(salaryRecord.Id, new PaySalaryRequest { PaymentMethod = "cash" });
        firstResult.Should().BeOfType<OkObjectResult>("first salary payment should succeed");

        var vaultAfterFirst = (await db.Treasuries.FindAsync(vault.Id))!.Balance;
        vaultAfterFirst.Should().Be(initialVaultBalance - 50_000m, "first payment decrements vault");

        // Second payment attempt — should be rejected
        var secondResult = await controller.PaySalary(salaryRecord.Id, new PaySalaryRequest { PaymentMethod = "cash" });
        secondResult.Should().BeOfType<BadRequestObjectResult>("second payment must be rejected");

        // Treasury must NOT be double-decremented
        var vaultAfterSecond = (await db.Treasuries.FindAsync(vault.Id))!.Balance;
        vaultAfterSecond.Should().Be(vaultAfterFirst, "second payment must NOT double-decrement vault");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TEST 4: Concurrent supplier payments cannot exceed outstanding bill amount
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SupplierPayment_ConcurrentPaymentsCannotExceedBillAmount()
    {
        await using var db = CreateContext();
        var (branchId, adminId) = SeedBranchAndAdmin(db);
        var (vault, bank) = SeedTreasuries(db, branchId);
        var session = CreateOpenSession(db, adminId, branchId, vault.Id);

        var supplier = new Supplier { Id = Guid.NewGuid(), Name = "مورد تجريبي", IsActive = true };
        db.Suppliers.Add(supplier);

        var bill = new SupplierBill
        {
            Id = Guid.NewGuid(),
            BillNumber = $"BILL-{DateTime.UtcNow:yyyyMMdd}-C04",
            SupplierId = supplier.Id,
            Description = "فاتورة اختبار الدفع المتزامن",
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

        var currentUser = CreateAdminCurrentUser(adminId, branchId);
        var audit = new Mock<IAuditService>().Object;
        var journalEntryService = new JournalEntryService(db, new Mock<ILogger<JournalEntryService>>().Object);
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        var controller = new SupplierBillsController(db, currentUser, audit, journalEntryService, treasuryResolution);

        // First payment of 20,000 — should succeed
        var firstResult = await controller.Pay(bill.Id, new PayBillInstallmentRequest
        {
            Amount = 20_000m,
            PaymentMethod = "bank_transfer"
        });
        firstResult.Should().BeOfType<OkObjectResult>("first payment should succeed");

        // Second payment of 20,000 — should fail (only 10,000 remaining)
        var secondResult = await controller.Pay(bill.Id, new PayBillInstallmentRequest
        {
            Amount = 20_000m,
            PaymentMethod = "bank_transfer"
        });
        secondResult.Should().BeOfType<BadRequestObjectResult>("second payment exceeding remaining must be rejected");

        // Verify the bill shows correct state
        var paidBill = await db.SupplierBills.FindAsync(bill.Id);
        paidBill!.PaidAmount.Should().Be(20_000m, "only the first payment should be recorded");
        paidBill.Status.Should().Be(BillStatus.PartiallyPaid, "bill should be partially paid");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TEST 5: Concurrent commission payments cannot exceed approved remaining commission
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CommissionPayment_SecondPaymentExceedingRemaining_ThrowsArgumentException()
    {
        await using var db = CreateContext();
        var (branchId, adminId) = SeedBranchAndAdmin(db);
        var (vault, bank) = SeedTreasuries(db, branchId);
        var session = CreateOpenSession(db, adminId, branchId, vault.Id);

        var doctor = new Doctor
        {
            Id = Guid.NewGuid(),
            Name = "دكتور تجريبي",
            BranchId = branchId,
            IsActive = true
        };
        db.Doctors.Add(doctor);

        // Create an approved commission line item of 10,000
        var service = new ClinicService { Id = Guid.NewGuid(), ArabicName = "تنظيف أسنان", IsActive = true };
        db.ClinicServices.Add(service);

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
            InvoiceNumber = $"INV-COMTEST",
            Status = InvoiceStatus.Issued,
            TotalAmount = 30_000m,
            Subtotal = 30_000m,
            CreatedBy = adminId,
            IsActive = true
        };
        db.Invoices.Add(invoice);

        var lineItem = new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            ServiceId = service.Id,
            ServiceNameSnapshot = "تنظيف أسنان",
            Description = "تنظيف",
            UnitPrice = 30_000m,
            Quantity = 1,
            TotalPrice = 30_000m,
            DoctorId = doctor.Id,
            MaterialCost = 5_000m,
            LabCost = 0,
            OtherDirectCost = 0,
            NetCommissionableAmount = 25_000m,
            DoctorCommissionPercentage = 40m,
            DoctorCommissionAmount = 10_000m,
            CenterShareAmount = 15_000m,
            CommissionStatus = CommissionStatus.Approved,
            CommissionApprovedBy = adminId,
            CommissionApprovedAt = DateTime.UtcNow,
            SortOrder = 0,
            IsActive = true
        };
        db.InvoiceLineItems.Add(lineItem);
        await db.SaveChangesAsync();

        var journalEntryService = new JournalEntryService(db, new Mock<ILogger<JournalEntryService>>().Object);
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        var commissionService = new CommissionService(db, journalEntryService, treasuryResolution, new Mock<ILogger<CommissionService>>().Object);

        // First payment of 8,000 — should succeed
        var firstPayment = await commissionService.RecordPaymentAsync(
            new Application.DTOs.Commission.RecordCommissionPaymentRequest(
                DoctorId: doctor.Id,
                Amount: 8_000m,
                PaymentDate: DateOnly.FromDateTime(DateTime.Today),
                PaymentMethod: "cash",
                ReferenceNumber: null,
                Notes: null,
                LineItemIds: new List<Guid> { lineItem.Id }
            ), adminId);

        firstPayment.Amount.Should().Be(8_000m, "first commission payment should succeed");

        // Second payment of 5,000 — should fail (only 2,000 remaining)
        var act = () => commissionService.RecordPaymentAsync(
            new Application.DTOs.Commission.RecordCommissionPaymentRequest(
                DoctorId: doctor.Id,
                Amount: 5_000m,
                PaymentDate: DateOnly.FromDateTime(DateTime.Today),
                PaymentMethod: "cash",
                ReferenceNumber: null,
                Notes: null,
                LineItemIds: null
            ), adminId);

        await act.Should().ThrowAsync<ArgumentException>("payment exceeding remaining commission must be rejected");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TEST 6: Open cashier session has a non-null TreasuryId mapped to the cash vault
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task OpenCashierSession_SetsNonNullTreasuryId_MappedToCashVault()
    {
        await using var db = CreateContext();
        var (branchId, adminId) = SeedBranchAndAdmin(db);
        var (vault, bank) = SeedTreasuries(db, branchId);

        var currentUser = CreateAdminCurrentUser(adminId, branchId);
        var audit = new Mock<IAuditService>().Object;
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        var logger = new Mock<ILogger<CashierSessionsController>>().Object;

        var controller = new CashierSessionsController(db, currentUser, audit, treasuryResolution, logger);

        // Act — open a session
        var result = await controller.OpenSession(new OpenSessionRequest
        {
            OpeningBalance = 50_000m,
            Notes = "اختبار تعيين الخزينة"
        });

        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;

        // Find the session in the DB and verify TreasuryId
        var session = await db.CashierSessions
            .FirstOrDefaultAsync(s => s.CashierId == adminId && s.Status == SessionStatus.Open && s.IsActive);

        session.Should().NotBeNull("a session should have been created");
        session!.TreasuryId.Should().NotBeNull("CashierSession must have a non-null TreasuryId");
        session.TreasuryId.Should().Be(vault.Id,
            "session TreasuryId must map to the branch cash vault treasury");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TEST 7: Cash movement linked to a session uses exactly that session.TreasuryId
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CashMovement_LinkedToSession_UsesExactSessionTreasuryId()
    {
        await using var db = CreateContext();
        var (branchId, adminId) = SeedBranchAndAdmin(db);
        var (vault, bank) = SeedTreasuries(db, branchId);

        // Create a session with a specific TreasuryId
        var session = CreateOpenSession(db, adminId, branchId, vault.Id);

        var currentUser = CreateAdminCurrentUser(adminId, branchId);
        var audit = new Mock<IAuditService>().Object;
        var journalEntryService = new JournalEntryService(db, new Mock<ILogger<JournalEntryService>>().Object);
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);

        // Create a pending cash expense that will be approved
        var expense = new OperationalExpense
        {
            Id = Guid.NewGuid(),
            ExpenseNumber = $"EXP-{DateTime.UtcNow:yyyyMMdd}-C07",
            Title = "مصروف نقدي لاختبار التوجيه",
            Category = ExpenseCategory.Miscellaneous,
            Amount = 5_000m,
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = "cash",
            PaidBy = adminId,
            BranchId = branchId,
            ApprovalStatus = ApprovalStatus.Pending,
            IsPostedToLedger = false,
            IsActive = true
        };
        db.OperationalExpenses.Add(expense);
        await db.SaveChangesAsync();

        var controller = new OperationalExpensesController(db, currentUser, audit, journalEntryService, treasuryResolution);

        // Act — approve the cash expense
        var result = await controller.Approve(expense.Id, new ApproveExpenseRequest());
        result.Should().BeOfType<OkObjectResult>();

        // Verify the cashflow uses the session's exact TreasuryId
        var cashflow = await db.CashFlowTransactions
            .FirstOrDefaultAsync(t => t.ReferenceId == expense.Id && t.Category == FinancialCategory.OperationalExpense);

        cashflow.Should().NotBeNull("a cashflow should have been created");
        cashflow!.TreasuryId.Should().Be(session.TreasuryId,
            "cash expense cashflow must use the exact session.TreasuryId");
        cashflow.CashierSessionId.Should().Be(session.Id,
            "cash expense cashflow must be linked to the cashier session");

        // Verify the journal entry also uses the same TreasuryId
        var je = await db.JournalEntries
            .FirstOrDefaultAsync(e => e.FinancialDocumentId == expense.Id && e.FinancialDocumentType == FinancialDocumentType.Expense);

        je.Should().NotBeNull("a journal entry should have been created");
        je!.TreasuryId.Should().Be(session.TreasuryId,
            "journal entry must use the same session.TreasuryId");
        je.CashierSessionId.Should().Be(session.Id,
            "journal entry must be linked to the cashier session");
    }
}
