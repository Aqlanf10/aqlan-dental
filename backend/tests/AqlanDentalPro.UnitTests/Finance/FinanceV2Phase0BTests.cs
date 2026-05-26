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
/// Unit tests for Phase 0B financial integrity fixes.
/// Covers: C1 (locked payment fields), C2 (closed-session delete guard),
/// H3 (contract TotalAmount ≥ paid), M9 (payment Amount > 0),
/// M10 (double-refund prevention), M4 (contract cancel reverses CashFlow).
/// Uses InMemory provider to verify business rules without requiring PostgreSQL.
/// </summary>
public class FinanceV2Phase0BTests
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

    private static (FinanceService service, Guid branchId, Guid cashierId) CreateService(AppDbContext db)
    {
        var (branchId, cashierId) = SeedBranchAndUser(db);

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(cashierId);
        currentUser.SetupGet(c => c.BranchId).Returns(branchId);
        currentUser.SetupGet(c => c.IsAdmin).Returns(true);

        var notifications = new Mock<INotificationService>();
        var logger = new Mock<ILogger<FinanceService>>();
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
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((FinancialDocumentType docType, Guid docId, string desc, DateOnly date, Guid branch, Guid performedBy, Guid? sessionId, Guid? treasuryId, IEnumerable<(JournalAccountType, Guid, decimal, decimal, string?)> lines, CancellationToken ct) =>
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

        var service = new FinanceService(db, currentUser.Object, notifications.Object, logger.Object, commissionService.Object, journalEntryService.Object);

        return (service, branchId, cashierId);
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

    // ─── C1: Payment Amount/Method/Date locked from updates ───────────────

    [Fact]
    public async Task UpdatePaymentAsync_ChangeAmount_ThrowsArgumentException()
    {
        await using var db = CreateContext();
        var (service, branchId, cashierId) = CreateService(db);
        var patient = SeedPatient(db, branchId);
        var session = CreateOpenSession(db, cashierId, branchId);

        // Create a payment via the service so all linked records are set up
        var paymentDto = await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 10_000m,
            PaymentMethod = "cash",
            ServiceDescription = "خدمة أصلية"
        });

        // Attempt to update the Amount — should throw
        var act = () => service.UpdatePaymentAsync(paymentDto.Id, new UpdatePaymentRequest
        {
            Amount = 20_000m  // different from original 10,000
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*مبلغ الدفعة*");
    }

    [Fact]
    public async Task UpdatePaymentAsync_ChangePaymentMethod_ThrowsArgumentException()
    {
        await using var db = CreateContext();
        var (service, branchId, cashierId) = CreateService(db);
        var patient = SeedPatient(db, branchId);
        var session = CreateOpenSession(db, cashierId, branchId);

        var paymentDto = await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 10_000m,
            PaymentMethod = "cash"
        });

        // Attempt to change PaymentMethod — should throw
        var act = () => service.UpdatePaymentAsync(paymentDto.Id, new UpdatePaymentRequest
        {
            PaymentMethod = "card"  // different from "cash"
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*طريقة الدفع*");
    }

    [Fact]
    public async Task UpdatePaymentAsync_ChangePaymentDate_ThrowsArgumentException()
    {
        await using var db = CreateContext();
        var (service, branchId, cashierId) = CreateService(db);
        var patient = SeedPatient(db, branchId);
        var session = CreateOpenSession(db, cashierId, branchId);

        var paymentDto = await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 10_000m,
            PaymentMethod = "cash"
        });

        // Attempt to change PaymentDate — should throw
        var act = () => service.UpdatePaymentAsync(paymentDto.Id, new UpdatePaymentRequest
        {
            PaymentDate = "2020-01-01"  // different from today
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*تاريخ الدفعة*");
    }

    [Fact]
    public async Task UpdatePaymentAsync_UpdateNotes_Succeeds()
    {
        await using var db = CreateContext();
        var (service, branchId, cashierId) = CreateService(db);
        var patient = SeedPatient(db, branchId);
        var session = CreateOpenSession(db, cashierId, branchId);

        var paymentDto = await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 10_000m,
            PaymentMethod = "cash"
        });

        // Update Notes — should succeed
        var result = await service.UpdatePaymentAsync(paymentDto.Id, new UpdatePaymentRequest
        {
            Notes = "ملاحظة محدثة"
        });

        result.Should().NotBeNull();
        result!.Notes.Should().Be("ملاحظة محدثة");
    }

    [Fact]
    public async Task UpdatePaymentAsync_UpdateServiceDescription_Succeeds()
    {
        await using var db = CreateContext();
        var (service, branchId, cashierId) = CreateService(db);
        var patient = SeedPatient(db, branchId);
        var session = CreateOpenSession(db, cashierId, branchId);

        var paymentDto = await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 10_000m,
            PaymentMethod = "cash",
            ServiceDescription = "خدمة أصلية"
        });

        // Update ServiceDescription — should succeed
        var result = await service.UpdatePaymentAsync(paymentDto.Id, new UpdatePaymentRequest
        {
            ServiceDescription = "خدمة محدثة"
        });

        result.Should().NotBeNull();
        result!.ServiceDescription.Should().Be("خدمة محدثة");
    }

    // ─── C2: Closed-session guard for DeletePayment ──────────────────────

    [Fact]
    public async Task DeletePaymentAsync_ClosedSession_ThrowsArgumentException()
    {
        await using var db = CreateContext();
        var (service, branchId, cashierId) = CreateService(db);
        var patient = SeedPatient(db, branchId);
        var session = CreateOpenSession(db, cashierId, branchId);

        // Create a payment (which links to the open session via CashFlowTransaction)
        var paymentDto = await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 10_000m,
            PaymentMethod = "cash"
        });

        // Close the session
        session.Status = SessionStatus.Closed;
        session.ClosingTime = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Attempt to delete the payment — should throw because session is closed
        var act = () => service.DeletePaymentAsync(paymentDto.Id);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*وردية مقفلة*");
    }

    [Fact]
    public async Task DeletePaymentAsync_OpenSession_Succeeds()
    {
        await using var db = CreateContext();
        var (service, branchId, cashierId) = CreateService(db);
        var patient = SeedPatient(db, branchId);
        var session = CreateOpenSession(db, cashierId, branchId);

        // Create a payment linked to the open session
        var paymentDto = await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 10_000m,
            PaymentMethod = "cash"
        });

        // Session is still open — deletion should succeed
        var result = await service.DeletePaymentAsync(paymentDto.Id);

        result.Should().BeTrue();

        // Verify the payment is soft-deleted
        var payment = await db.Payments.FindAsync(paymentDto.Id);
        payment.Should().NotBeNull();
        payment!.IsActive.Should().BeFalse("payment should be soft-deleted");
    }

    [Fact]
    public async Task DeletePaymentAsync_NoSession_Succeeds()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndUser(db);
        var patient = SeedPatient(db, branchId);

        // Create a payment manually without linking it to any CashFlowTransaction/session
        var paymentId = Guid.NewGuid();
        db.Payments.Add(new Payment
        {
            Id = paymentId,
            PatientId = patient.Id,
            Amount = 5_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = "cash",
            BranchId = branchId,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(cashierId);
        currentUser.SetupGet(c => c.BranchId).Returns(branchId);
        currentUser.SetupGet(c => c.IsAdmin).Returns(true);

        var notifications = new Mock<INotificationService>();
        var logger = new Mock<ILogger<FinanceService>>();
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
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((FinancialDocumentType docType, Guid docId, string desc, DateOnly date, Guid branch, Guid performedBy, Guid? sessionId, Guid? treasuryId, IEnumerable<(JournalAccountType, Guid, decimal, decimal, string?)> lines, CancellationToken ct) =>
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

        var service = new FinanceService(db, currentUser.Object, notifications.Object, logger.Object, commissionService.Object, journalEntryService.Object);

        // Deleting a payment with no linked CashFlowTransaction should succeed
        var result = await service.DeletePaymentAsync(paymentId);

        result.Should().BeTrue();

        var payment = await db.Payments.FindAsync(paymentId);
        payment.Should().NotBeNull();
        payment!.IsActive.Should().BeFalse("payment should be soft-deleted");
    }

    // ─── H3: Contract TotalAmount >= paid validation ────────────────────

    [Fact]
    public async Task UpdateContractAsync_TotalBelowPaid_ThrowsArgumentException()
    {
        await using var db = CreateContext();
        var (service, branchId, cashierId) = CreateService(db);
        var patient = SeedPatient(db, branchId);
        var session = CreateOpenSession(db, cashierId, branchId);

        // Create a contract with a down payment
        var contractDto = await service.CreateContractAsync(new CreateContractRequest
        {
            PatientId = patient.Id,
            TotalAmount = 100_000m,
            DownPayment = 50_000m,
            InstallmentsCount = 5,
            InstallmentAmount = 10_000m
        });

        // Attempt to reduce TotalAmount below what's been paid (50,000)
        var act = () => service.UpdateContractAsync(contractDto.Id, new UpdateContractRequest
        {
            TotalAmount = 30_000m,  // below paid 50,000
            InstallmentsCount = 5,
            DiscountAmount = 0
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*لا يمكن تقليل إجمالي العقد*");
    }

    [Fact]
    public async Task UpdateContractAsync_TotalAbovePaid_Succeeds()
    {
        await using var db = CreateContext();
        var (service, branchId, cashierId) = CreateService(db);
        var patient = SeedPatient(db, branchId);
        var session = CreateOpenSession(db, cashierId, branchId);

        // Create a contract with a down payment
        var contractDto = await service.CreateContractAsync(new CreateContractRequest
        {
            PatientId = patient.Id,
            TotalAmount = 100_000m,
            DownPayment = 50_000m,
            InstallmentsCount = 5,
            InstallmentAmount = 10_000m
        });

        // Update TotalAmount to above what's been paid — should succeed
        var result = await service.UpdateContractAsync(contractDto.Id, new UpdateContractRequest
        {
            TotalAmount = 200_000m,  // above paid 50,000
            InstallmentsCount = 5,
            DiscountAmount = 0
        });

        result.Should().NotBeNull();
        result!.TotalAmount.Should().Be(200_000m);
    }

    // ─── M9: Payment Amount > 0 validation ──────────────────────────────

    [Fact]
    public async Task CreatePaymentAsync_ZeroAmount_ThrowsArgumentException()
    {
        await using var db = CreateContext();
        var (service, branchId, cashierId) = CreateService(db);
        var patient = SeedPatient(db, branchId);
        var session = CreateOpenSession(db, cashierId, branchId);

        // Attempt to create a payment with Amount = 0
        var act = () => service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 0m,
            PaymentMethod = "cash"
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*أكبر من الصفر*");
    }

    [Fact]
    public async Task CreatePaymentAsync_NegativeAmount_ThrowsArgumentException()
    {
        await using var db = CreateContext();
        var (service, branchId, cashierId) = CreateService(db);
        var patient = SeedPatient(db, branchId);
        var session = CreateOpenSession(db, cashierId, branchId);

        // Attempt to create a payment with Amount < 0
        var act = () => service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = -5_000m,
            PaymentMethod = "cash"
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*أكبر من الصفر*");
    }

    // ─── M10: Double-refund prevention ───────────────────────────────────

    [Fact]
    public async Task RefundPaymentAsync_DoubleRefund_ThrowsArgumentException()
    {
        await using var db = CreateContext();
        var (service, branchId, cashierId) = CreateService(db);
        var patient = SeedPatient(db, branchId);
        var session = CreateOpenSession(db, cashierId, branchId);

        // Create a payment
        var paymentDto = await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 10_000m,
            PaymentMethod = "cash",
            ServiceDescription = "خلع ضرس"
        });

        // First refund — should succeed
        await service.RefundPaymentAsync(paymentDto.Id, "استرداد أول");

        // Second refund of the same payment — should throw
        var act = () => service.RefundPaymentAsync(paymentDto.Id, "استرداد ثاني");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*استرداد هذه الدفعة مسبقاً*");
    }

    [Fact]
    public async Task RefundPaymentAsync_FirstRefund_Succeeds()
    {
        await using var db = CreateContext();
        var (service, branchId, cashierId) = CreateService(db);
        var patient = SeedPatient(db, branchId);
        var session = CreateOpenSession(db, cashierId, branchId);

        // Create a payment
        var paymentDto = await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 10_000m,
            PaymentMethod = "cash",
            ServiceDescription = "خلع ضرس"
        });

        // First refund — should succeed
        var result = await service.RefundPaymentAsync(paymentDto.Id, "استرداد أول");

        result.Should().NotBeNull();
        result!.Amount.Should().Be(-10_000m, "refund payment amount should be negative of original");
        result.ServiceDescription.Should().StartWith("استرداد:");
    }

    // ─── M4: Contract cancel reverses CashFlow ───────────────────────────

    [Fact]
    public async Task CancelContract_SoftDeletesCashFlowTransactions()
    {
        await using var db = CreateContext();
        var (service, branchId, cashierId) = CreateService(db);
        var patient = SeedPatient(db, branchId);
        var session = CreateOpenSession(db, cashierId, branchId);

        // Create a contract with a down payment (which creates a CashFlowTransaction)
        var contractDto = await service.CreateContractAsync(new CreateContractRequest
        {
            PatientId = patient.Id,
            TotalAmount = 100_000m,
            DownPayment = 30_000m,
            InstallmentsCount = 7,
            InstallmentAmount = 10_000m
        });

        // Verify the CashFlowTransaction is active before cancellation
        var activeCashflowsBefore = await db.CashFlowTransactions
            .Where(t => t.Category == FinancialCategory.PatientPayment && t.IsActive)
            .ToListAsync();
        activeCashflowsBefore.Should().NotBeEmpty("payment should have created a cashflow");

        // Cancel the contract
        await service.UpdateContractStatusAsync(contractDto.Id, "cancelled");

        // Verify CashFlowTransactions use reversal pattern (NOT soft-delete).
        // The original PatientPayment cashflow stays IsActive=true but gets a
        // ReversedByTransactionId pointing to the reversal entry.
        var originalCashflowsAfter = await db.CashFlowTransactions
            .Where(t => t.Category == FinancialCategory.PatientPayment && t.IsActive && !t.IsReversal)
            .ToListAsync();
        originalCashflowsAfter.Should().NotBeEmpty("original cashflow should remain active (reversal pattern)");

        // Verify reversal cashflow entries were created
        var reversalCashflows = await db.CashFlowTransactions
            .Where(t => t.Category == FinancialCategory.Reversal && t.IsReversal && t.IsActive)
            .ToListAsync();
        reversalCashflows.Should().NotBeEmpty("reversal cashflow entries should be created for cancelled contract");

        // Verify original cashflow is linked to its reversal
        foreach (var original in originalCashflowsAfter.Where(c => c.ReversedByTransactionId.HasValue))
        {
            var reversal = reversalCashflows.FirstOrDefault(r => r.Id == original.ReversedByTransactionId.Value);
            reversal.Should().NotBeNull("original cashflow should reference its reversal entry");
            reversal!.ReversalOfTransactionId.Should().Be(original.Id, "reversal should reference the original");
            reversal.Type.Should().Be(TransactionType.Outflow, "reversal of Inflow should be Outflow");
        }
    }

    [Fact]
    public async Task CancelContract_ReversesTreasuryBalance()
    {
        await using var db = CreateContext();
        var (service, branchId, cashierId) = CreateService(db);
        var patient = SeedPatient(db, branchId);
        var session = CreateOpenSession(db, cashierId, branchId);

        // Create a contract with a cash down payment
        var contractDto = await service.CreateContractAsync(new CreateContractRequest
        {
            PatientId = patient.Id,
            TotalAmount = 100_000m,
            DownPayment = 30_000m,
            InstallmentsCount = 7,
            InstallmentAmount = 10_000m
        });

        // Check Treasury balance after payment creation
        var treasuryBefore = await db.Treasuries
            .FirstOrDefaultAsync(t => t.BranchId == branchId && t.Type == TreasuryType.Vault && t.IsActive);
        treasuryBefore.Should().NotBeNull();
        var balanceAfterPayment = treasuryBefore!.Balance;
        balanceAfterPayment.Should().Be(30_000m, "treasury should reflect the 30,000 cash down payment");

        // Cancel the contract — should reverse the Treasury balance
        await service.UpdateContractStatusAsync(contractDto.Id, "cancelled");

        // Reload the treasury to get the updated balance
        await db.Entry(treasuryBefore).ReloadAsync();

        treasuryBefore.Balance.Should().Be(0m, "treasury balance should be reversed after contract cancellation");
    }
}
