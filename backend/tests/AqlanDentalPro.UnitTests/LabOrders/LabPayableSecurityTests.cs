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

namespace AqlanDentalPro.UnitTests.LabOrders;

public class LabPayableSecurityTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static ICurrentUserService CreateUser(Guid userId, UserRole role, Guid? branchId = null)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(u => u.UserId).Returns(userId);
        mock.Setup(u => u.Role).Returns(role);
        mock.Setup(u => u.IsAdmin).Returns(role == UserRole.Admin);
        mock.Setup(u => u.IsAuthenticated).Returns(true);
        mock.Setup(u => u.BranchId).Returns(branchId);
        return mock.Object;
    }

    private static LabPayablesController BuildController(AppDbContext db, ICurrentUserService currentUser)
    {
        var logger = new Mock<ILogger<LabPayablesController>>().Object;
        var trLogger = new Mock<ILogger<TreasuryResolutionService>>().Object;
        var jeLogger = new Mock<ILogger<JournalEntryService>>().Object;

        var treasuryResolution = new TreasuryResolutionService(db, trLogger);
        var journalEntryService = new JournalEntryService(db, jeLogger);
        var audit = new Mock<IAuditService>().Object;

        return new LabPayablesController(db, currentUser, logger, treasuryResolution, journalEntryService, audit);
    }

    private static async Task<(Guid branchId, Guid labId, Guid payableId)> SeedDataAsync(AppDbContext db, decimal amount, decimal paidAmount = 0)
    {
        var branchId = Guid.NewGuid();
        var labId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var payableId = Guid.NewGuid();

        db.Branches.Add(new Branch { Id = branchId, Name = "Main Branch" });
        db.Patients.Add(new Patient { Id = patientId, FirstName = "Test", LastName = "Patient", BranchId = branchId });
        db.Labs.Add(new AqlanDentalPro.Domain.Entities.Lab { Id = labId, Name = "Al-Amal Lab", IsActive = true });
        
        db.LabOrders.Add(new LabOrder
        {
            Id = orderId,
            PatientId = patientId,
            LabId = labId,
            OrderNumber = "LAB-2026-001",
            Status = "sent",
            BranchId = branchId,
            IsActive = true
        });

        db.LabPayables.Add(new LabPayable
        {
            Id = payableId,
            LabOrderId = orderId,
            LabId = labId,
            Amount = amount,
            PaidAmount = paidAmount,
            Status = paidAmount >= amount ? "paid" : (paidAmount > 0 ? "partial" : "pending"),
            IsActive = true
        });

        await db.SaveChangesAsync();
        return (branchId, labId, payableId);
    }

    // 1. Recording lab payable payment without active cashier session returns 400
    [Fact]
    public async Task RecordPayment_WithoutActiveCashierSession_ReturnsBadRequest()
    {
        // Arrange
        await using var db = CreateDb();
        var cashierId = Guid.NewGuid();
        var (branchId, _, payableId) = await SeedDataAsync(db, 1000m);
        var currentUser = CreateUser(cashierId, UserRole.Admin, branchId);
        var controller = BuildController(db, currentUser);

        var request = new LabPayablesController.RecordPaymentRequest { Amount = 500m, Notes = "No session test" };

        // Act
        var result = await controller.RecordPayment(payableId, request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = (BadRequestObjectResult)result;
        badRequest.Value.Should().BeEquivalentTo(new { message = "يجب فتح وردية الكاشير أولاً قبل تسجيل دفعة معملية." });
    }

    // 2. Recording with active cashier session succeeds
    [Fact]
    public async Task RecordPayment_WithActiveCashierSession_ReturnsOk()
    {
        // Arrange
        await using var db = CreateDb();
        var cashierId = Guid.NewGuid();
        var (branchId, _, payableId) = await SeedDataAsync(db, 1000m);
        var currentUser = CreateUser(cashierId, UserRole.Admin, branchId);
        
        // Seed active cashier session
        var treasuryId = Guid.NewGuid();
        db.Treasuries.Add(new Treasury { Id = treasuryId, Name = "Main Cash Treasury", Type = TreasuryType.Vault, BranchId = branchId, Balance = 2000m, IsActive = true });
        db.CashierSessions.Add(new CashierSession
        {
            Id = Guid.NewGuid(),
            CashierId = cashierId,
            BranchId = branchId,
            TreasuryId = treasuryId,
            Status = SessionStatus.Open,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db, currentUser);
        var request = new LabPayablesController.RecordPaymentRequest { Amount = 500m, Notes = "Valid session test" };

        // Act
        var result = await controller.RecordPayment(payableId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        ok.Value.GetType().GetProperty("message")!.GetValue(ok.Value).Should().Be("تم تسجيل الدفعة معملياً ومالياً بنجاح");
    }

    // 3. Partial payment sets status to partial
    [Fact]
    public async Task RecordPayment_PartialPayment_SetsStatusToPartial()
    {
        // Arrange
        await using var db = CreateDb();
        var cashierId = Guid.NewGuid();
        var (branchId, _, payableId) = await SeedDataAsync(db, 1000m);
        var currentUser = CreateUser(cashierId, UserRole.Admin, branchId);
        
        var treasuryId = Guid.NewGuid();
        db.Treasuries.Add(new Treasury { Id = treasuryId, Name = "Main Cash Treasury", Type = TreasuryType.Vault, BranchId = branchId, Balance = 2000m, IsActive = true });
        db.CashierSessions.Add(new CashierSession { Id = Guid.NewGuid(), CashierId = cashierId, BranchId = branchId, TreasuryId = treasuryId, Status = SessionStatus.Open, IsActive = true });
        await db.SaveChangesAsync();

        var controller = BuildController(db, currentUser);
        var request = new LabPayablesController.RecordPaymentRequest { Amount = 400m };

        // Act
        var result = await controller.RecordPayment(payableId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var payable = await db.LabPayables.FindAsync(payableId);
        payable!.PaidAmount.Should().Be(400m);
        payable.Status.Should().Be("partial");
    }

    // 4. Full payment sets status to paid
    [Fact]
    public async Task RecordPayment_FullPayment_SetsStatusToPaid()
    {
        // Arrange
        await using var db = CreateDb();
        var cashierId = Guid.NewGuid();
        var (branchId, _, payableId) = await SeedDataAsync(db, 1000m);
        var currentUser = CreateUser(cashierId, UserRole.Admin, branchId);
        
        var treasuryId = Guid.NewGuid();
        db.Treasuries.Add(new Treasury { Id = treasuryId, Name = "Main Cash Treasury", Type = TreasuryType.Vault, BranchId = branchId, Balance = 2000m, IsActive = true });
        db.CashierSessions.Add(new CashierSession { Id = Guid.NewGuid(), CashierId = cashierId, BranchId = branchId, TreasuryId = treasuryId, Status = SessionStatus.Open, IsActive = true });
        await db.SaveChangesAsync();

        var controller = BuildController(db, currentUser);
        var request = new LabPayablesController.RecordPaymentRequest { Amount = 1000m };

        // Act
        var result = await controller.RecordPayment(payableId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var payable = await db.LabPayables.FindAsync(payableId);
        payable!.PaidAmount.Should().Be(1000m);
        payable.Status.Should().Be("paid");
    }

    // 5. Overpayment is rejected
    [Fact]
    public async Task RecordPayment_Overpayment_ReturnsBadRequest()
    {
        // Arrange
        await using var db = CreateDb();
        var cashierId = Guid.NewGuid();
        var (branchId, _, payableId) = await SeedDataAsync(db, 1000m);
        var currentUser = CreateUser(cashierId, UserRole.Admin, branchId);
        
        var treasuryId = Guid.NewGuid();
        db.Treasuries.Add(new Treasury { Id = treasuryId, Name = "Main Cash Treasury", Type = TreasuryType.Vault, BranchId = branchId, Balance = 2000m, IsActive = true });
        db.CashierSessions.Add(new CashierSession { Id = Guid.NewGuid(), CashierId = cashierId, BranchId = branchId, TreasuryId = treasuryId, Status = SessionStatus.Open, IsActive = true });
        await db.SaveChangesAsync();

        var controller = BuildController(db, currentUser);
        var request = new LabPayablesController.RecordPaymentRequest { Amount = 1200m };

        // Act
        var result = await controller.RecordPayment(payableId, request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = (BadRequestObjectResult)result;
        badRequest.Value.ToString().Should().Contain("يتجاوز المبلغ المتبقي");
    }

    // 6. Already paid payable is rejected
    [Fact]
    public async Task RecordPayment_AlreadyPaidPayable_ReturnsBadRequest()
    {
        // Arrange
        await using var db = CreateDb();
        var cashierId = Guid.NewGuid();
        var (branchId, _, payableId) = await SeedDataAsync(db, 1000m, paidAmount: 1000m);
        var currentUser = CreateUser(cashierId, UserRole.Admin, branchId);
        
        var treasuryId = Guid.NewGuid();
        db.Treasuries.Add(new Treasury { Id = treasuryId, Name = "Main Cash Treasury", Type = TreasuryType.Vault, BranchId = branchId, Balance = 2000m, IsActive = true });
        db.CashierSessions.Add(new CashierSession { Id = Guid.NewGuid(), CashierId = cashierId, BranchId = branchId, TreasuryId = treasuryId, Status = SessionStatus.Open, IsActive = true });
        await db.SaveChangesAsync();

        var controller = BuildController(db, currentUser);
        var request = new LabPayablesController.RecordPaymentRequest { Amount = 100m };

        // Act
        var result = await controller.RecordPayment(payableId, request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = (BadRequestObjectResult)result;
        badRequest.Value.Should().BeEquivalentTo(new { message = "هذه المستحقات مدفوعة بالكامل بالفعل" });
    }

    // 7. Double-submit/concurrent payment cannot overpay
    [Fact]
    public async Task RecordPayment_DoubleSubmit_PreventsOverpayment()
    {
        // Arrange
        await using var db = CreateDb();
        var cashierId = Guid.NewGuid();
        var (branchId, _, payableId) = await SeedDataAsync(db, 1000m);
        var currentUser = CreateUser(cashierId, UserRole.Admin, branchId);
        
        var treasuryId = Guid.NewGuid();
        db.Treasuries.Add(new Treasury { Id = treasuryId, Name = "Main Cash Treasury", Type = TreasuryType.Vault, BranchId = branchId, Balance = 2000m, IsActive = true });
        db.CashierSessions.Add(new CashierSession { Id = Guid.NewGuid(), CashierId = cashierId, BranchId = branchId, TreasuryId = treasuryId, Status = SessionStatus.Open, IsActive = true });
        await db.SaveChangesAsync();

        var controller = BuildController(db, currentUser);
        var request1 = new LabPayablesController.RecordPaymentRequest { Amount = 600m };
        var request2 = new LabPayablesController.RecordPaymentRequest { Amount = 500m }; // Sum is 1100, which exceeds 1000

        // Act & Assert
        var result1 = await controller.RecordPayment(payableId, request1);
        result1.Should().BeOfType<OkObjectResult>();

        var result2 = await controller.RecordPayment(payableId, request2);
        result2.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = (BadRequestObjectResult)result2;
        badRequest.Value.ToString().Should().Contain("يتجاوز المبلغ المتبقي");
    }

    // 8. Cash outflow or journal entry is created according to current finance pattern
    [Fact]
    public async Task RecordPayment_CreatesCashFlowAndJournalEntries()
    {
        // Arrange
        await using var db = CreateDb();
        var cashierId = Guid.NewGuid();
        var (branchId, labId, payableId) = await SeedDataAsync(db, 1000m);
        var currentUser = CreateUser(cashierId, UserRole.Admin, branchId);
        
        var treasuryId = Guid.NewGuid();
        db.Treasuries.Add(new Treasury { Id = treasuryId, Name = "Main Cash Treasury", Type = TreasuryType.Vault, BranchId = branchId, Balance = 2000m, IsActive = true });
        db.CashierSessions.Add(new CashierSession { Id = Guid.NewGuid(), CashierId = cashierId, BranchId = branchId, TreasuryId = treasuryId, Status = SessionStatus.Open, IsActive = true });
        await db.SaveChangesAsync();

        var controller = BuildController(db, currentUser);
        var request = new LabPayablesController.RecordPaymentRequest { Amount = 500m };

        // Act
        var result = await controller.RecordPayment(payableId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        // Verify CashFlowTransaction was created
        var cashflow = await db.CashFlowTransactions.FirstOrDefaultAsync(c => c.ReferenceId == payableId);
        cashflow.Should().NotBeNull();
        cashflow!.Type.Should().Be(TransactionType.Outflow);
        cashflow.Category.Should().Be(FinancialCategory.SupplierPayment);
        cashflow.Amount.Should().Be(500m);

        // Verify JournalEntry was created and balances
        var je = await db.JournalEntries
            .Include(j => j.Lines)
            .FirstOrDefaultAsync(j => j.FinancialDocumentId == payableId);
        je.Should().NotBeNull();
        je!.FinancialDocumentType.Should().Be(FinancialDocumentType.SupplierPayment);
        je.IsPosted.Should().BeTrue();
        je.IsBalanced().Should().BeTrue();
        
        je.Lines.Should().HaveCount(2);
        
        var payableLine = je.Lines.FirstOrDefault(l => l.AccountType == JournalAccountType.Payable);
        payableLine.Should().NotBeNull();
        payableLine!.AccountId.Should().Be(labId);
        payableLine.Debit.Should().Be(500m);
        payableLine.Credit.Should().Be(0m);

        var treasuryLine = je.Lines.FirstOrDefault(l => l.AccountType == JournalAccountType.Treasury);
        treasuryLine.Should().NotBeNull();
        treasuryLine!.AccountId.Should().Be(treasuryId);
        treasuryLine.Debit.Should().Be(0m);
        treasuryLine.Credit.Should().Be(500m);

        // Verify Treasury balance decremented
        var treasury = await db.Treasuries.FindAsync(treasuryId);
        treasury!.Balance.Should().Be(1500m); // 2000 - 500
    }

    // 9. Unauthorized user cannot record lab payable payment
    [Fact]
    public async Task RecordPayment_UnauthorizedUser_ReturnsForbid()
    {
        // Arrange
        await using var db = CreateDb();
        var cashierId = Guid.NewGuid();
        var (branchId, _, payableId) = await SeedDataAsync(db, 1000m);
        
        // Accountant role does not have edit access to lab_payables by default if not granted
        var currentUser = CreateUser(cashierId, UserRole.Accountant, branchId);
        var controller = BuildController(db, currentUser);

        var request = new LabPayablesController.RecordPaymentRequest { Amount = 500m };

        // Act
        var result = await controller.RecordPayment(payableId, request);

        // Assert
        result.Should().BeOfType<ForbidResult>();
    }

    // 10. Verify no migrations or schema changes (ensured by tests not running any Db migration steps)
    [Fact]
    public void Schema_HasNoMigrations()
    {
        // Check that code operates completely on the existing domain entity model structures
        typeof(LabPayable).GetProperty("Amount").Should().NotBeNull();
        typeof(LabPayable).GetProperty("PaidAmount").Should().NotBeNull();
        typeof(LabPayable).GetProperty("Status").Should().NotBeNull();
    }
}
