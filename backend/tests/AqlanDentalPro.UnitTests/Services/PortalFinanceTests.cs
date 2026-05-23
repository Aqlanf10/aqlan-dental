using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Services;

/// <summary>
/// Unit tests for PatientPortalService financial calculations.
/// Verifies that the portal only counts payments linked to ACTIVE contracts
/// in the summary totals, so that completed/cancelled contract payments
/// don't distort the patient's outstanding balance.
/// </summary>
public class PortalFinanceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static PatientPortalService CreateService(AppDbContext db)
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["Jwt:SecretKey"]).Returns("ThisIsAVeryLongSecretKeyForTestingPurposesOnly123!");
        config.Setup(c => c["Jwt:Issuer"]).Returns("AqlanDentalPro");
        config.Setup(c => c["Jwt:Audience"]).Returns("AqlanDentalPro");
        config.Setup(c => c["WhatsApp:ApiUrl"]).Returns(string.Empty);
        config.Setup(c => c["WhatsApp:ApiToken"]).Returns(string.Empty);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        var linkingService = new Mock<IPatientAccountLinkingService>();
        var logger = new Mock<ILogger<PatientPortalService>>();

        return new PatientPortalService(db, config.Object, httpClientFactory.Object, linkingService.Object, logger.Object);
    }

    /// <summary>
    /// Helper: seed a patient with a contract.
    /// </summary>
    private static async Task<(Guid patientId, Guid contractId)> SeedPatientWithContract(
        AppDbContext db,
        decimal contractTotal,
        decimal discount = 0m,
        ContractStatus status = ContractStatus.Active)
    {
        var patientId = Guid.NewGuid();
        var contractId = Guid.NewGuid();

        db.Patients.Add(new Patient
        {
            Id = patientId,
            FirstName = "أحمد",
            LastName = "العلي",
            PatientNumber = "P-T"
        });

        db.Contracts.Add(new Contract
        {
            Id = contractId,
            PatientId = patientId,
            TotalAmount = contractTotal,
            DiscountAmount = discount,
            Status = status
        });

        await db.SaveChangesAsync();
        return (patientId, contractId);
    }

    // ─── Core Bug Fix: Scope Mismatch ──────────────────────────────────────

    [Fact]
    public async Task GetFinancialSummary_CompletedContractPayments_NotCountedInTotalPaid()
    {
        // Arrange: 1 completed contract (100,000 paid) + 1 active contract (0 paid)
        // The portal should NOT count the completed contract's payments in TotalPaid,
        // because TotalContracted only includes active contracts.
        await using var db = CreateContext();
        var patientId = Guid.NewGuid();

        db.Patients.Add(new Patient
        {
            Id = patientId,
            FirstName = "سارة",
            LastName = "محمد",
            PatientNumber = "P-001"
        });

        var completedContractId = Guid.NewGuid();
        var activeContractId = Guid.NewGuid();

        db.Contracts.Add(new Contract
        {
            Id = completedContractId,
            PatientId = patientId,
            TotalAmount = 100_000m,
            DiscountAmount = 0m,
            Status = ContractStatus.Completed
        });
        db.Contracts.Add(new Contract
        {
            Id = activeContractId,
            PatientId = patientId,
            TotalAmount = 50_000m,
            DiscountAmount = 0m,
            Status = ContractStatus.Active
        });

        // Payment for completed contract
        db.Payments.Add(new Payment
        {
            PatientId = patientId,
            ContractId = completedContractId,
            Amount = 100_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = "cash"
        });

        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Act
        var result = await service.GetFinancialSummaryAsync(patientId);

        // Assert
        result.Should().NotBeNull();
        result.TotalPaid.Should().Be(0m, "completed contract payments must NOT be counted in TotalPaid");
        result.TotalAmount.Should().Be(50_000m, "only active contract total");
        result.TotalOutstanding.Should().Be(50_000m, "50,000 - 0 - 0 = 50,000");
        result.ActiveContracts.Should().Be(1, "only the active contract");
        result.Contracts.Should().ContainSingle(c => c.Id == activeContractId);
    }

    [Fact]
    public async Task GetFinancialSummary_ActiveContractWithPayments_CountsCorrectly()
    {
        // Arrange: 1 active contract (200,000) with linked payment (80,000)
        await using var db = CreateContext();
        var (patientId, contractId) = await SeedPatientWithContract(db, 200_000m);

        db.Payments.Add(new Payment
        {
            PatientId = patientId,
            ContractId = contractId,
            Amount = 80_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = "cash"
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Act
        var result = await service.GetFinancialSummaryAsync(patientId);

        // Assert
        result.Should().NotBeNull();
        result.TotalPaid.Should().Be(80_000m, "active contract linked payment");
        result.TotalAmount.Should().Be(200_000m);
        result.TotalOutstanding.Should().Be(120_000m, "200,000 - 0 - 80,000");

        // Per-contract
        result.Contracts.Should().ContainSingle();
        result.Contracts[0].PaidAmount.Should().Be(80_000m);
        result.Contracts[0].RemainingAmount.Should().Be(120_000m);
    }

    [Fact]
    public async Task GetFinancialSummary_InactivePayment_NotCounted()
    {
        // Arrange: 1 active contract (100,000) with inactive payment (30,000)
        await using var db = CreateContext();
        var (patientId, contractId) = await SeedPatientWithContract(db, 100_000m);

        db.Payments.Add(new Payment
        {
            PatientId = patientId,
            ContractId = contractId,
            Amount = 30_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = "cash",
            IsActive = false // soft-deleted / refunded
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Act
        var result = await service.GetFinancialSummaryAsync(patientId);

        // Assert
        result.Should().NotBeNull();
        result.TotalPaid.Should().Be(0m, "inactive payment must NOT be counted");
        result.TotalOutstanding.Should().Be(100_000m);
        result.Contracts[0].PaidAmount.Should().Be(0m);
        result.Contracts[0].RemainingAmount.Should().Be(100_000m);
    }

    [Fact]
    public async Task GetFinancialSummary_MultipleActiveContracts_SumsCorrectly()
    {
        // Arrange: 2 active contracts with linked payments
        await using var db = CreateContext();
        var patientId = Guid.NewGuid();

        db.Patients.Add(new Patient
        {
            Id = patientId,
            FirstName = "خالد",
            LastName = "علي",
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

        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Act
        var result = await service.GetFinancialSummaryAsync(patientId);

        // Assert
        result.Should().NotBeNull();
        result.TotalAmount.Should().Be(485_000m, "(300,000 - 10,000) + (200,000 - 5,000)");
        result.TotalPaid.Should().Be(100_000m, "only contract1's linked payment");
        result.TotalOutstanding.Should().Be(385_000m, "485,000 - 100,000 (no discounts double-counted)");
        result.ActiveContracts.Should().Be(2);

        // Per-contract
        var c1 = result.Contracts.First(c => c.Id == contract1Id);
        c1.TotalAmount.Should().Be(290_000m, "300,000 - 10,000");
        c1.PaidAmount.Should().Be(100_000m);
        c1.RemainingAmount.Should().Be(190_000m);

        var c2 = result.Contracts.First(c => c.Id == contract2Id);
        c2.TotalAmount.Should().Be(195_000m, "200,000 - 5,000");
        c2.PaidAmount.Should().Be(0m);
        c2.RemainingAmount.Should().Be(195_000m);
    }

    [Fact]
    public async Task GetFinancialSummary_MixedActiveAndCompleted_OnlyCountsActive()
    {
        // Arrange: 1 completed + 1 active contract, each with payments
        await using var db = CreateContext();
        var patientId = Guid.NewGuid();

        db.Patients.Add(new Patient
        {
            Id = patientId,
            FirstName = "ليلى",
            LastName = "أحمد",
            PatientNumber = "P-003"
        });

        var completedId = Guid.NewGuid();
        var activeId = Guid.NewGuid();

        db.Contracts.Add(new Contract
        {
            Id = completedId,
            PatientId = patientId,
            TotalAmount = 500_000m,
            DiscountAmount = 0m,
            Status = ContractStatus.Completed
        });
        db.Contracts.Add(new Contract
        {
            Id = activeId,
            PatientId = patientId,
            TotalAmount = 300_000m,
            DiscountAmount = 20_000m,
            Status = ContractStatus.Active
        });

        // Completed contract fully paid
        db.Payments.Add(new Payment
        {
            PatientId = patientId,
            ContractId = completedId,
            Amount = 500_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = "cash"
        });
        // Active contract partially paid
        db.Payments.Add(new Payment
        {
            PatientId = patientId,
            ContractId = activeId,
            Amount = 100_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = "card"
        });

        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Act
        var result = await service.GetFinancialSummaryAsync(patientId);

        // Assert: Only active contract's payments and totals
        result.Should().NotBeNull();
        result.TotalPaid.Should().Be(100_000m, "only active contract's linked payment (NOT the 500,000 from completed)");
        result.TotalAmount.Should().Be(280_000m, "300,000 - 20,000 discount");
        result.TotalOutstanding.Should().Be(180_000m, "280,000 - 100,000");
        result.ActiveContracts.Should().Be(1);
        result.Contracts.Should().ContainSingle(c => c.Id == activeId);
    }

    [Fact]
    public async Task GetFinancialSummary_CancelledContract_NotCounted()
    {
        // Arrange: 1 cancelled + 1 active contract
        await using var db = CreateContext();
        var patientId = Guid.NewGuid();

        db.Patients.Add(new Patient
        {
            Id = patientId,
            FirstName = "نور",
            LastName = "سعيد",
            PatientNumber = "P-004"
        });

        var cancelledId = Guid.NewGuid();
        var activeId = Guid.NewGuid();

        db.Contracts.Add(new Contract
        {
            Id = cancelledId,
            PatientId = patientId,
            TotalAmount = 400_000m,
            DiscountAmount = 0m,
            Status = ContractStatus.Cancelled
        });
        db.Contracts.Add(new Contract
        {
            Id = activeId,
            PatientId = patientId,
            TotalAmount = 150_000m,
            DiscountAmount = 10_000m,
            Status = ContractStatus.Active
        });

        // Payment on cancelled contract
        db.Payments.Add(new Payment
        {
            PatientId = patientId,
            ContractId = cancelledId,
            Amount = 200_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = "cash"
        });
        // Payment on active contract
        db.Payments.Add(new Payment
        {
            PatientId = patientId,
            ContractId = activeId,
            Amount = 50_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = "card"
        });

        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Act
        var result = await service.GetFinancialSummaryAsync(patientId);

        // Assert
        result.Should().NotBeNull();
        result.TotalPaid.Should().Be(50_000m, "only active contract's linked payment");
        result.TotalAmount.Should().Be(140_000m, "150,000 - 10,000");
        result.TotalOutstanding.Should().Be(90_000m, "140,000 - 50,000");
        result.ActiveContracts.Should().Be(1);
    }

    [Fact]
    public async Task GetFinancialSummary_NoContracts_ZeroTotals()
    {
        // Arrange: Patient with no contracts
        await using var db = CreateContext();
        var patientId = Guid.NewGuid();

        db.Patients.Add(new Patient
        {
            Id = patientId,
            FirstName = "فاطمة",
            LastName = "عبدالله",
            PatientNumber = "P-005"
        });

        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Act
        var result = await service.GetFinancialSummaryAsync(patientId);

        // Assert
        result.Should().NotBeNull();
        result.TotalPaid.Should().Be(0m);
        result.TotalAmount.Should().Be(0m);
        result.TotalOutstanding.Should().Be(0m);
        result.ActiveContracts.Should().Be(0);
        result.Contracts.Should().BeEmpty();
    }

    [Fact]
    public async Task GetFinancialSummary_DiscountApplied_CorrectTotals()
    {
        // Arrange: Active contract with discount
        await using var db = CreateContext();
        var (patientId, contractId) = await SeedPatientWithContract(db, 1_000_000m, 100_000m);

        db.Payments.Add(new Payment
        {
            PatientId = patientId,
            ContractId = contractId,
            Amount = 400_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = "cash"
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Act
        var result = await service.GetFinancialSummaryAsync(patientId);

        // Assert
        result.Should().NotBeNull();
        result.TotalAmount.Should().Be(900_000m, "1,000,000 - 100,000 discount");
        result.TotalPaid.Should().Be(400_000m);
        result.TotalOutstanding.Should().Be(500_000m, "900,000 - 400,000");

        // Per-contract
        result.Contracts[0].TotalAmount.Should().Be(900_000m);
        result.Contracts[0].PaidAmount.Should().Be(400_000m);
        result.Contracts[0].RemainingAmount.Should().Be(500_000m);
    }
}
