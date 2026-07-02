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
/// Hotfix tests for:
/// 1. CashFlowTransactions.Category query works with FinancialCategory enum (schema mismatch)
/// 2. Contract cancellation with active payments — treasury + CashFlow reversal
/// 3. DeletePayment — treasury adjustment + reversal CashFlow exists
/// 4. CreatePaymentAsync — no DbContext concurrent operation from notification;
///    notification failure does not fail the payment
///
/// Note: These tests use InMemory provider which stores enums as integer internally,
/// matching the production behavior AFTER the migration fix. The schema mismatch
/// (varchar vs integer) only manifests on PostgreSQL, so the InMemory tests verify
/// the behavioral contract: correct queries, correct state transitions.
/// </summary>
public class HotfixCashFlowCategoryAndNotificationTests
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

    private static Patient SeedPatient(AppDbContext db, Guid branchId)
    {
        var patient = new Patient
        {
            FirstName = "Test",
            LastName = "Patient",
            PatientNumber = $"P-{Guid.NewGuid().ToString()[..8]}",
            BranchId = branchId,
            Gender = Gender.Male
        };
        db.Patients.Add(patient);
        db.SaveChanges();
        return patient;
    }

    private static Mock<IJournalEntryService> CreateDefaultJournalEntryServiceMock()
    {
        var mock = new Mock<IJournalEntryService>();

        mock.Setup(s => s.CreateEntryAsync(
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

        mock.Setup(s => s.CreateReversalEntryAsync(
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

        return mock;
    }

    private static (FinanceService service, Guid branchId, Guid cashierId, Mock<INotificationService> notifications, Mock<ILogger<FinanceService>> logger) CreateServiceWithMocks(
        AppDbContext db)
    {
        var (branchId, cashierId) = SeedBranchAndUser(db);

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(cashierId);
        currentUser.SetupGet(c => c.BranchId).Returns(branchId);
        currentUser.SetupGet(c => c.IsAdmin).Returns(true);

        var notifications = new Mock<INotificationService>();
        var logger = new Mock<ILogger<FinanceService>>();
        var commissionService = new Mock<ICommissionService>();
        var journalEntryService = CreateDefaultJournalEntryServiceMock();

        var service = new FinanceService(db, currentUser.Object, notifications.Object, logger.Object, commissionService.Object, journalEntryService.Object, new ContractService(db, currentUser.Object));

        return (service, branchId, cashierId, notifications, logger);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Test 1: CashFlowTransactions.Category query works with FinancialCategory enum
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CashFlowTransaction_CategoryQuery_ByFinancialCategoryEnum_Works()
    {
        await using var db = CreateContext();
        var (service, branchId, cashierId, _, _) = CreateServiceWithMocks(db);
        var patient = SeedPatient(db, branchId);
        var session = CreateOpenSession(db, cashierId, branchId);

        // Create a payment which should create a CashFlowTransaction with Category = PatientPayment
        var paymentDto = await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 10_000m,
            PaymentMethod = "cash",
            ServiceDescription = "دفعة اختبار فئة"
        });

        // Query by FinancialCategory enum value — this is the query that fails in production
        // when the column is varchar but EF Core sends integer comparison
        var cashflows = await db.CashFlowTransactions
            .Where(t => t.Category == FinancialCategory.PatientPayment && t.IsActive)
            .ToListAsync();

        cashflows.Should().NotBeEmpty("CashFlowTransaction with PatientPayment category should be queryable");
        cashflows.Should().ContainSingle(t => t.ReferenceId == paymentDto.Id);

        // Verify the category value is correctly stored
        var cashflow = cashflows.First(t => t.ReferenceId == paymentDto.Id);
        cashflow.Category.Should().Be(FinancialCategory.PatientPayment);
        cashflow.Type.Should().Be(TransactionType.Inflow);
    }

    [Fact]
    public async Task CashFlowTransaction_ReversalCategoryQuery_Works()
    {
        await using var db = CreateContext();
        var (service, branchId, cashierId, _, _) = CreateServiceWithMocks(db);
        var patient = SeedPatient(db, branchId);
        var session = CreateOpenSession(db, cashierId, branchId);

        // Create a contract with down payment
        var contractDto = await service.CreateContractAsync(new CreateContractRequest
        {
            PatientId = patient.Id,
            TotalAmount = 50_000m,
            DownPayment = 15_000m,
            InstallmentsCount = 4,
            InstallmentAmount = 10_000m
        });

        // Cancel the contract — should create reversal CashFlow with Category = Reversal
        await service.UpdateContractStatusAsync(contractDto.Id, "cancelled");

        // Query by Reversal category
        var reversalCashflows = await db.CashFlowTransactions
            .Where(t => t.Category == FinancialCategory.Reversal && t.IsReversal && t.IsActive)
            .ToListAsync();

        reversalCashflows.Should().NotBeEmpty("Reversal CashFlowTransaction should be queryable by FinancialCategory.Reversal");
        reversalCashflows.Should().ContainSingle(t => t.Category == FinancialCategory.Reversal);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Test 2: Contract cancellation with active payments — full financial integrity
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CancelContract_WithActivePayment_RevertsTreasuryAndCreatesCashFlowReversal()
    {
        await using var db = CreateContext();
        var (service, branchId, cashierId, _, _) = CreateServiceWithMocks(db);
        var patient = SeedPatient(db, branchId);
        var session = CreateOpenSession(db, cashierId, branchId);

        // Create contract with down payment
        var contractDto = await service.CreateContractAsync(new CreateContractRequest
        {
            PatientId = patient.Id,
            TotalAmount = 100_000m,
            DownPayment = 25_000m,
            InstallmentsCount = 8,
            InstallmentAmount = 10_000m
        });

        // Verify treasury increased by payment amount
        var treasuryBefore = await db.Treasuries
            .FirstOrDefaultAsync(t => t.BranchId == branchId && t.Type == TreasuryType.Vault && t.IsActive);
        treasuryBefore.Should().NotBeNull();
        treasuryBefore!.Balance.Should().Be(25_000m, "down payment should be in treasury");

        // Verify payment is active
        var paymentsBefore = await db.Payments.Where(p => p.ContractId == contractDto.Id && p.IsActive).ToListAsync();
        paymentsBefore.Should().NotBeEmpty();

        // Verify CashFlow transaction exists with PatientPayment category
        var cashflowBefore = await db.CashFlowTransactions
            .FirstOrDefaultAsync(t => t.Category == FinancialCategory.PatientPayment && t.IsActive && !t.IsReversal);
        cashflowBefore.Should().NotBeNull("original CashFlow should exist");

        // Act: Cancel the contract
        await service.UpdateContractStatusAsync(contractDto.Id, "cancelled");

        // Assert: Contract is Cancelled
        var contract = await db.Contracts.FindAsync(contractDto.Id);
        contract!.Status.Should().Be(ContractStatus.Cancelled);

        // Assert: Payments are deactivated
        var activePayments = await db.Payments.Where(p => p.ContractId == contractDto.Id && p.IsActive).ToListAsync();
        activePayments.Should().BeEmpty("all payments should be deactivated after cancellation");

        // Assert: Treasury balance reverted
        await db.Entry(treasuryBefore).ReloadAsync();
        treasuryBefore.Balance.Should().Be(0m, "treasury should be fully reversed");

        // Assert: CashFlow reversal entry created
        var reversalCashflows = await db.CashFlowTransactions
            .Where(t => t.Category == FinancialCategory.Reversal && t.IsReversal && t.IsActive)
            .ToListAsync();
        reversalCashflows.Should().NotBeEmpty("CashFlow reversal should exist");

        // Assert: Original CashFlow still active and linked
        var originalCashflow = await db.CashFlowTransactions
            .FirstOrDefaultAsync(t => t.Category == FinancialCategory.PatientPayment && t.IsActive && !t.IsReversal);
        originalCashflow.Should().NotBeNull("original CashFlow should remain (immutable)");
        originalCashflow!.ReversedByTransactionId.Should().NotBeNull("original should be linked to reversal");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Test 3: DeletePayment — treasury adjusted, reversal exists, no errors
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DeletePayment_ReversesTreasuryAndCreatesCashFlowReversal()
    {
        await using var db = CreateContext();
        var (service, branchId, cashierId, _, _) = CreateServiceWithMocks(db);
        var patient = SeedPatient(db, branchId);
        var session = CreateOpenSession(db, cashierId, branchId);

        // Create a standalone payment
        var paymentDto = await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 7_500m,
            PaymentMethod = "cash",
            ServiceDescription = "دفعة اختبار حذف"
        });

        // Verify treasury has the payment
        var treasuryBefore = await db.Treasuries
            .FirstOrDefaultAsync(t => t.BranchId == branchId && t.Type == TreasuryType.Vault && t.IsActive);
        treasuryBefore!.Balance.Should().Be(7_500m);

        // Act: Delete the payment
        var result = await service.DeletePaymentAsync(paymentDto.Id);

        // Assert: Payment deleted successfully
        result.Should().BeTrue();

        // Assert: Payment is deactivated
        var payment = await db.Payments.FindAsync(paymentDto.Id);
        payment!.IsActive.Should().BeFalse();

        // Assert: Treasury reversed
        await db.Entry(treasuryBefore).ReloadAsync();
        treasuryBefore.Balance.Should().Be(0m, "treasury should be reversed after payment deletion");

        // Assert: CashFlow reversal entry exists
        var reversalCashflows = await db.CashFlowTransactions
            .Where(t => t.Category == FinancialCategory.Reversal && t.IsReversal && t.IsActive)
            .ToListAsync();
        reversalCashflows.Should().NotBeEmpty("reversal CashFlow should be created on payment deletion");

        // Assert: Original CashFlow still active and linked to reversal
        var originalCashflow = await db.CashFlowTransactions
            .FirstOrDefaultAsync(t => t.Category == FinancialCategory.PatientPayment && t.IsActive && !t.IsReversal);
        originalCashflow.Should().NotBeNull("original CashFlow should remain immutable");
        originalCashflow!.ReversedByTransactionId.Should().NotBeNull();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Test 4: CreatePaymentAsync — notification failure does not fail the payment
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreatePayment_NotificationFailure_DoesNotFailPayment()
    {
        await using var db = CreateContext();
        var (service, branchId, cashierId, notifications, logger) = CreateServiceWithMocks(db);
        var patient = SeedPatient(db, branchId);
        var session = CreateOpenSession(db, cashierId, branchId);

        // Setup: notification service throws an exception
        notifications.Setup(n => n.NotifyRoleAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>()))
            .ThrowsAsync(new Exception("Notification service unavailable"));

        // Act: Create payment — should succeed even though notification fails
        var paymentDto = await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 5_000m,
            PaymentMethod = "cash",
            ServiceDescription = "دفعة اختبار إشعار"
        });

        // Assert: Payment was created successfully despite notification failure
        paymentDto.Should().NotBeNull();
        paymentDto.Amount.Should().Be(5_000m);

        // Assert: Payment is persisted in the database
        var payment = await db.Payments.FindAsync(paymentDto.Id);
        payment.Should().NotBeNull();
        payment!.IsActive.Should().BeTrue();
        payment.Amount.Should().Be(5_000m);

        // Assert: Treasury was updated
        var treasury = await db.Treasuries
            .FirstOrDefaultAsync(t => t.BranchId == branchId && t.Type == TreasuryType.Vault && t.IsActive);
        treasury!.Balance.Should().Be(5_000m);

        // Assert: CashFlow transaction was created
        var cashflows = await db.CashFlowTransactions
            .Where(t => t.Category == FinancialCategory.PatientPayment && t.IsActive)
            .ToListAsync();
        cashflows.Should().NotBeEmpty();

        // Assert: Warning was logged for the notification failure
        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Payment notification failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "A warning should be logged when notification fails");
    }

    [Fact]
    public async Task CreatePayment_SuccessfulNotification_DoesNotThrow()
    {
        await using var db = CreateContext();
        var (service, branchId, cashierId, notifications, _) = CreateServiceWithMocks(db);
        var patient = SeedPatient(db, branchId);
        var session = CreateOpenSession(db, cashierId, branchId);

        // Setup: notification succeeds
        notifications.Setup(n => n.NotifyRoleAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        // Act: Create payment — notification should be awaited without issues
        var paymentDto = await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 3_000m,
            PaymentMethod = "cash",
            ServiceDescription = "دفعة اختبار إشعار ناجح"
        });

        // Assert: Payment created
        paymentDto.Should().NotBeNull();
        paymentDto.Amount.Should().Be(3_000m);

        // Assert: Notification was called for both Accountant and Admin
        notifications.Verify(n => n.NotifyRoleAsync("Accountant",
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<Guid>()), Times.Once);
        notifications.Verify(n => n.NotifyRoleAsync("Admin",
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<Guid>()), Times.Once);
    }

    [Fact]
    public async Task CreatePayment_NoDbContextConcurrentOperation_NoTaskRun()
    {
        // This test verifies that CreatePaymentAsync no longer uses Task.Run for
        // notifications. The Task.Run fire-and-forget pattern could cause DbContext
        // concurrent operations because the notification service might share the
        // same DI scope as the DbContext. By awaiting directly, we ensure the
        // notification runs in the same request scope after the financial transaction
        // is fully committed.
        //
        // We verify this behaviorally: if the DbContext was being used concurrently
        // (e.g., in a Task.Run after the method returned), the InMemory provider
        // might throw an ObjectDisposedException. Since we await the notification,
        // the method doesn't return until the notification is complete.

        await using var db = CreateContext();
        var (service, branchId, cashierId, notifications, _) = CreateServiceWithMocks(db);
        var patient = SeedPatient(db, branchId);
        var session = CreateOpenSession(db, cashierId, branchId);

        // Track notification call timing
        var notificationCallCount = 0;
        notifications.Setup(n => n.NotifyRoleAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>()))
            .Callback(() => notificationCallCount++)
            .Returns(Task.CompletedTask);

        // Act
        var paymentDto = await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 2_000m,
            PaymentMethod = "cash",
            ServiceDescription = "اختبار عدم التزامن"
        });

        // Assert: Notifications were called (not fire-and-forget)
        // Since we await, both calls should have been made before the method returns
        notificationCallCount.Should().Be(2, "both Accountant and Admin notifications should have been awaited");

        // Assert: Payment is still valid
        var payment = await db.Payments.FindAsync(paymentDto.Id);
        payment.Should().NotBeNull();
        payment!.Amount.Should().Be(2_000m);
    }
}
