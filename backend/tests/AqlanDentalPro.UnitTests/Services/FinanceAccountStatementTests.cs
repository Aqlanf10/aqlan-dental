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

namespace AqlanDentalPro.UnitTests.Services;

/// <summary>
/// Unit tests for FinanceService.GetAccountStatementAsync.
/// Verifies that unlinked/orphan patient payments are included in TotalPaid
/// and that TotalRemaining is computed correctly.
/// Uses InMemory provider to verify calculations without requiring PostgreSQL.
/// </summary>
public class FinanceAccountStatementTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static FinanceService CreateService(AppDbContext db)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(c => c.BranchId).Returns((Guid?)null);
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

        return new FinanceService(db, currentUser.Object, notifications.Object, logger.Object, commissionService.Object, journalEntryService.Object);
    }

    /// <summary>
    /// Helper: seed a patient with a contract and optional payments.
    /// Returns the patientId and contractId.
    /// </summary>
    private static async Task<(Guid patientId, Guid contractId)> SeedPatientWithContract(
        AppDbContext db,
        decimal contractTotal,
        decimal discount = 0m)
    {
        var patientId = Guid.NewGuid();
        var contractId = Guid.NewGuid();

        db.Patients.Add(new Patient
        {
            Id = patientId,
            FirstName = "أحمد",
            LastName = "العلي",
            PatientNumber = "P-001"
        });

        db.Contracts.Add(new Contract
        {
            Id = contractId,
            PatientId = patientId,
            TotalAmount = contractTotal,
            DiscountAmount = discount,
            Status = ContractStatus.Active
        });

        await db.SaveChangesAsync();
        return (patientId, contractId);
    }

    // ─── Core Bug Fix Tests ─────────────────────────────────────────────────

    [Fact]
    public async Task GetAccountStatement_UnlinkedPayment_CountsInTotalPaid()
    {
        // Arrange: Contract 20,000,000 | Discount 10,000 | Unlinked payment 50,000
        await using var db = CreateContext();
        var (patientId, contractId) = await SeedPatientWithContract(db, 20_000_000m, 10_000m);

        db.Payments.Add(new Payment
        {
            PatientId = patientId,
            ContractId = null, // unlinked — the bug: this was not counted
            Amount = 50_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = "cash"
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Act
        var result = await service.GetAccountStatementAsync(patientId);

        // Assert
        result.Should().NotBeNull();
        result!.TotalPaid.Should().Be(50_000m, "unlinked payment must be counted in TotalPaid");
        result.TotalContracted.Should().Be(20_000_000m);
        result.TotalDiscounts.Should().Be(10_000m);
        result.TotalRemaining.Should().Be(19_940_000m, "TotalRemaining = TotalContracted - TotalDiscounts - TotalPaid");
        result.RecentPayments.Should().ContainSingle(p => p.Amount == 50_000m, "unlinked payment appears in recent list");
    }

    [Fact]
    public async Task GetAccountStatement_ContractLinkedPayment_CountsCorrectly()
    {
        // Arrange: Contract 500,000 | linked payment 100,000
        await using var db = CreateContext();
        var (patientId, contractId) = await SeedPatientWithContract(db, 500_000m);

        db.Payments.Add(new Payment
        {
            PatientId = patientId,
            ContractId = contractId, // linked to contract
            Amount = 100_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = "cash"
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Act
        var result = await service.GetAccountStatementAsync(patientId);

        // Assert
        result.Should().NotBeNull();
        result!.TotalPaid.Should().Be(100_000m);
        result.TotalRemaining.Should().Be(400_000m, "500,000 - 0 discount - 100,000 paid");

        // Per-contract PaidAmount also reflects the linked payment
        result.Contracts.Should().ContainSingle(c => c.Id == contractId);
        result.Contracts.First(c => c.Id == contractId).PaidAmount.Should().Be(100_000m);
        result.Contracts.First(c => c.Id == contractId).RemainingAmount.Should().Be(400_000m);
    }

    [Fact]
    public async Task GetAccountStatement_InactivePayment_NotCounted()
    {
        // Arrange: Contract 200,000 | inactive payment 30,000 (should NOT count)
        await using var db = CreateContext();
        var (patientId, contractId) = await SeedPatientWithContract(db, 200_000m);

        db.Payments.Add(new Payment
        {
            PatientId = patientId,
            ContractId = null,
            Amount = 30_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = "cash",
            IsActive = false // soft-deleted / refunded
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Act
        var result = await service.GetAccountStatementAsync(patientId);

        // Assert
        result.Should().NotBeNull();
        result!.TotalPaid.Should().Be(0m, "inactive payment must NOT be counted");
        result.TotalRemaining.Should().Be(200_000m);
        result.RecentPayments.Should().BeEmpty("inactive payment should not appear in recent list");
    }

    [Fact]
    public async Task GetAccountStatement_MixedLinkedAndUnlinked_CountedOnceOnly()
    {
        // Arrange: Contract 1,000,000 | linked payment 200,000 | unlinked payment 50,000
        await using var db = CreateContext();
        var (patientId, contractId) = await SeedPatientWithContract(db, 1_000_000m);

        db.Payments.Add(new Payment
        {
            PatientId = patientId,
            ContractId = contractId,
            Amount = 200_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = "cash"
        });
        db.Payments.Add(new Payment
        {
            PatientId = patientId,
            ContractId = null, // unlinked
            Amount = 50_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = "card"
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Act
        var result = await service.GetAccountStatementAsync(patientId);

        // Assert
        result.Should().NotBeNull();
        result!.TotalPaid.Should().Be(250_000m, "linked + unlinked = 200,000 + 50,000");
        result.TotalRemaining.Should().Be(750_000m, "1,000,000 - 0 - 250,000");

        // Per-contract: only linked payment counts
        result.Contracts.First(c => c.Id == contractId).PaidAmount.Should().Be(200_000m, "per-contract only counts linked payments");
        result.Contracts.First(c => c.Id == contractId).RemainingAmount.Should().Be(800_000m);

        // Both payments appear in recent list
        result.RecentPayments.Should().HaveCount(2);
    }

    // ─── Edge Cases ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAccountStatement_NoPayments_ZeroTotals()
    {
        // Arrange: Contract 100,000 | no payments
        await using var db = CreateContext();
        var (patientId, contractId) = await SeedPatientWithContract(db, 100_000m);

        var service = CreateService(db);

        // Act
        var result = await service.GetAccountStatementAsync(patientId);

        // Assert
        result.Should().NotBeNull();
        result!.TotalPaid.Should().Be(0m);
        result.TotalRemaining.Should().Be(100_000m);
        result.RecentPayments.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAccountStatement_MultipleContracts_SumsCorrectly()
    {
        // Arrange: Two contracts with different payments
        await using var db = CreateContext();
        var patientId = Guid.NewGuid();

        db.Patients.Add(new Patient
        {
            Id = patientId,
            FirstName = "سارة",
            LastName = "محمد",
            PatientNumber = "P-002"
        });

        var contract1Id = Guid.NewGuid();
        var contract2Id = Guid.NewGuid();

        db.Contracts.Add(new Contract
        {
            Id = contract1Id,
            PatientId = patientId,
            TotalAmount = 300_000m,
            DiscountAmount = 10_000m,
            Status = ContractStatus.Active
        });
        db.Contracts.Add(new Contract
        {
            Id = contract2Id,
            PatientId = patientId,
            TotalAmount = 200_000m,
            DiscountAmount = 5_000m,
            Status = ContractStatus.Active
        });

        // Payment linked to contract1
        db.Payments.Add(new Payment
        {
            PatientId = patientId,
            ContractId = contract1Id,
            Amount = 100_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = "cash"
        });
        // Unlinked payment
        db.Payments.Add(new Payment
        {
            PatientId = patientId,
            ContractId = null,
            Amount = 25_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = "card"
        });

        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Act
        var result = await service.GetAccountStatementAsync(patientId);

        // Assert
        result.Should().NotBeNull();
        result!.TotalContracted.Should().Be(500_000m, "300,000 + 200,000");
        result.TotalDiscounts.Should().Be(15_000m, "10,000 + 5,000");
        result.TotalPaid.Should().Be(125_000m, "100,000 linked + 25,000 unlinked");
        result.TotalRemaining.Should().Be(360_000m, "500,000 - 15,000 - 125,000");

        // Per-contract balances
        var c1 = result.Contracts.First(c => c.Id == contract1Id);
        c1.PaidAmount.Should().Be(100_000m, "only linked payment");
        c1.RemainingAmount.Should().Be(190_000m, "300,000 - 10,000 - 100,000");

        var c2 = result.Contracts.First(c => c.Id == contract2Id);
        c2.PaidAmount.Should().Be(0m, "no linked payment");
        c2.RemainingAmount.Should().Be(195_000m, "200,000 - 5,000 - 0");
    }

    [Fact]
    public async Task GetAccountStatement_NonexistentPatient_ReturnsNull()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        var result = await service.GetAccountStatementAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAccountStatement_OnlyUnlinkedPayments_CountedInTotalPaid()
    {
        // Arrange: No contracts at all, just orphan payments
        await using var db = CreateContext();
        var patientId = Guid.NewGuid();

        db.Patients.Add(new Patient
        {
            Id = patientId,
            FirstName = "خالد",
            LastName = "علي",
            PatientNumber = "P-003"
        });

        db.Payments.Add(new Payment
        {
            PatientId = patientId,
            ContractId = null,
            InvoiceId = null,
            Amount = 30_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = "cash"
        });
        db.Payments.Add(new Payment
        {
            PatientId = patientId,
            ContractId = null,
            InvoiceId = null,
            Amount = 20_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = "card"
        });

        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Act
        var result = await service.GetAccountStatementAsync(patientId);

        // Assert
        result.Should().NotBeNull();
        result!.TotalPaid.Should().Be(50_000m, "orphan payments are counted");
        result.TotalContracted.Should().Be(0m, "no contracts");
        result.TotalRemaining.Should().Be(-50_000m, "0 - 0 - 50,000 (negative: patient paid more than contracted)");
        result.RecentPayments.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAccountStatement_RecentPayments_ExcludesInactive()
    {
        // Arrange: 1 active + 1 inactive payment
        await using var db = CreateContext();
        var (patientId, _) = await SeedPatientWithContract(db, 100_000m);

        db.Payments.Add(new Payment
        {
            PatientId = patientId,
            Amount = 10_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = "cash",
            IsActive = true
        });
        db.Payments.Add(new Payment
        {
            PatientId = patientId,
            Amount = 20_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = "cash",
            IsActive = false // should NOT appear
        });

        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Act
        var result = await service.GetAccountStatementAsync(patientId);

        // Assert
        result.Should().NotBeNull();
        result!.TotalPaid.Should().Be(10_000m, "only active payment counted");
        result.RecentPayments.Should().ContainSingle(p => p.Amount == 10_000m, "inactive payment excluded from list");
    }

    [Fact]
    public async Task GetAccountStatement_FullScenario_MatchesExpected()
    {
        // Full scenario from the bug report:
        // Contract 20,000,000 | Discount 10,000 | Unlinked active payment 50,000
        // => TotalPaid = 50,000
        // => TotalRemaining = 19,940,000
        await using var db = CreateContext();
        var patientId = Guid.NewGuid();
        var contractId = Guid.NewGuid();

        db.Patients.Add(new Patient
        {
            Id = patientId,
            FirstName = "مريض",
            LastName = "تجريبي",
            PatientNumber = "P-BUG"
        });

        db.Contracts.Add(new Contract
        {
            Id = contractId,
            PatientId = patientId,
            TotalAmount = 20_000_000m,
            DiscountAmount = 10_000m,
            Status = ContractStatus.Active
        });

        db.Payments.Add(new Payment
        {
            PatientId = patientId,
            ContractId = null, // unlinked — root cause of the bug
            Amount = 50_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = "cash",
            ServiceDescription = "دفعة نقدية"
        });

        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetAccountStatementAsync(patientId);

        // Exact values from bug report
        result.Should().NotBeNull();
        result!.TotalPaid.Should().Be(50_000m);
        result.TotalRemaining.Should().Be(19_940_000m);
        result.TotalContracted.Should().Be(20_000_000m);
        result.TotalDiscounts.Should().Be(10_000m);
        result.RecentPayments.Should().ContainSingle();
        result.RecentPayments[0].Amount.Should().Be(50_000m);

        // Per-contract: still shows 0 paid (no linked payment)
        result.Contracts[0].PaidAmount.Should().Be(0m);
        result.Contracts[0].RemainingAmount.Should().Be(19_990_000m);
    }
}
