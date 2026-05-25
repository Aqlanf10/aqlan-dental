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

namespace AqlanDentalPro.UnitTests.Services;

public class TreasuryVaultTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static FinanceService CreateService(AppDbContext db, Guid userId, Guid branchId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(userId);
        currentUser.SetupGet(c => c.BranchId).Returns(branchId);
        currentUser.SetupGet(c => c.IsAdmin).Returns(true);

        var notifications = new Mock<INotificationService>();
        var logger = new Mock<ILogger<FinanceService>>();
        var commissionService = new Mock<ICommissionService>();

        return new FinanceService(db, currentUser.Object, notifications.Object, logger.Object, commissionService.Object);
    }

    [Fact]
    public async Task CreatePayment_UpdatesTreasuryBalance_VaultOrBank()
    {
        // Arrange
        await using var db = CreateContext();
        var branchId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        
        // Seed patient
        db.Patients.Add(new Patient
        {
            Id = patientId,
            FirstName = "Test",
            LastName = "Patient",
            PatientNumber = "P-100",
            BranchId = branchId
        });

        // Seed open cashier session
        db.CashierSessions.Add(new CashierSession
        {
            Id = Guid.NewGuid(),
            CashierId = userId,
            Status = SessionStatus.Open,
            IsActive = true,
            BranchId = branchId,
            OpeningBalance = 1000,
            SessionNumber = "SESS-001",
            OpeningTime = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var service = CreateService(db, userId, branchId);

        // Act
        var req = new CreatePaymentRequest
        {
            PatientId = patientId,
            Amount = 50000m,
            PaymentMethod = "cash",
            Notes = "Test payment"
        };
        
        var paymentDto = await service.CreatePaymentAsync(req);

        // Assert
        var treasury = await db.Treasuries
            .FirstOrDefaultAsync(t => t.BranchId == branchId && t.Type == TreasuryType.Vault && t.IsActive);

        treasury.Should().NotBeNull();
        treasury!.Balance.Should().Be(50000m);
        treasury.Name.Should().Be("درج كاشير الاستقبال");
    }

    [Fact]
    public async Task RefundPayment_UpdatesTreasuryBalance_DecreasesCorrectly()
    {
        // Arrange
        await using var db = CreateContext();
        var branchId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        
        // Seed patient
        db.Patients.Add(new Patient
        {
            Id = patientId,
            FirstName = "Test",
            LastName = "Patient",
            PatientNumber = "P-100",
            BranchId = branchId
        });

        // Seed open cashier session
        db.CashierSessions.Add(new CashierSession
        {
            Id = Guid.NewGuid(),
            CashierId = userId,
            Status = SessionStatus.Open,
            IsActive = true,
            BranchId = branchId,
            OpeningBalance = 1000,
            SessionNumber = "SESS-001",
            OpeningTime = DateTime.UtcNow
        });

        // Seed a payment to refund
        var paymentId = Guid.NewGuid();
        var originalPayment = new Payment
        {
            Id = paymentId,
            PatientId = patientId,
            Amount = 40000m,
            PaymentMethod = "cash",
            ReceiptNumber = "RC-101",
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            BranchId = branchId,
            IsActive = true
        };
        db.Payments.Add(originalPayment);

        // Seed a treasury vault with initial balance of 100,000 YER
        db.Treasuries.Add(new Treasury
        {
            Id = Guid.NewGuid(),
            Name = "درج كاشير الاستقبال",
            Type = TreasuryType.Vault,
            Balance = 100000m,
            BranchId = branchId,
            IsActive = true
        });

        await db.SaveChangesAsync();

        var service = CreateService(db, userId, branchId);

        // Act
        var refundDto = await service.RefundPaymentAsync(paymentId, "Test refund reason");

        // Assert
        var treasury = await db.Treasuries
            .FirstOrDefaultAsync(t => t.BranchId == branchId && t.Type == TreasuryType.Vault && t.IsActive);

        treasury.Should().NotBeNull();
        treasury!.Balance.Should().Be(60000m); // 100,000 - 40,000
    }

    [Fact]
    public async Task DeletePayment_UpdatesTreasuryBalance_RevertsCorrectly()
    {
        // Arrange
        await using var db = CreateContext();
        var branchId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        
        // Seed patient
        db.Patients.Add(new Patient
        {
            Id = patientId,
            FirstName = "Test",
            LastName = "Patient",
            PatientNumber = "P-100",
            BranchId = branchId
        });

        // Seed open cashier session
        db.CashierSessions.Add(new CashierSession
        {
            Id = Guid.NewGuid(),
            CashierId = userId,
            Status = SessionStatus.Open,
            IsActive = true,
            BranchId = branchId,
            OpeningBalance = 1000,
            SessionNumber = "SESS-001",
            OpeningTime = DateTime.UtcNow
        });

        // Seed a treasury vault with 100,000 YER
        db.Treasuries.Add(new Treasury
        {
            Id = Guid.NewGuid(),
            Name = "درج كاشير الاستقبال",
            Type = TreasuryType.Vault,
            Balance = 100000m,
            BranchId = branchId,
            IsActive = true
        });

        // Seed a payment of 40,000 YER
        var paymentId = Guid.NewGuid();
        db.Payments.Add(new Payment
        {
            Id = paymentId,
            PatientId = patientId,
            Amount = 40000m,
            PaymentMethod = "cash",
            ReceiptNumber = "RC-101",
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            BranchId = branchId,
            IsActive = true
        });

        // Seed a cashflow transaction matching the payment (so DeletePayment doesn't fail trying to find it)
        db.CashFlowTransactions.Add(new CashFlowTransaction
        {
            Id = Guid.NewGuid(),
            TransactionNumber = "TX-RC-101",
            Type = TransactionType.Inflow,
            Category = FinancialCategory.PatientPayment,
            Amount = 40000m,
            PaymentMethod = "cash",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            ReferenceId = paymentId,
            ReferenceNumber = "RC-101",
            BranchId = branchId,
            IsActive = true
        });

        await db.SaveChangesAsync();

        var service = CreateService(db, userId, branchId);

        // Act
        var result = await service.DeletePaymentAsync(paymentId);

        // Assert
        var treasury = await db.Treasuries
            .FirstOrDefaultAsync(t => t.BranchId == branchId && t.Type == TreasuryType.Vault && t.IsActive);

        treasury.Should().NotBeNull();
        // Since the payment was created, the balance was 100,000 (manually seeded).
        // DeletePaymentAsync deletes the payment and deducts its amount from the treasury balance.
        // Balance = 100,000 - 40,000 = 60,000
        treasury!.Balance.Should().Be(60000m);
    }
}
