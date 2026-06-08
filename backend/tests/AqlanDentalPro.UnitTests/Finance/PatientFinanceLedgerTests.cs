using AqlanDentalPro.API.Controllers;
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
/// Sprint Patient-Finance-Ledger: Tests for patient financial visibility —
/// balance consistency, draft invoice exclusion, contract down payment method,
/// and finance data role isolation.
/// </summary>
public class PatientFinanceLedgerTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ICurrentUserService CreateAdminUser(Guid userId, Guid? branchId = null)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(u => u.UserId).Returns(userId);
        mock.Setup(u => u.Role).Returns(UserRole.Admin);
        mock.Setup(u => u.IsAdmin).Returns(true);
        mock.Setup(u => u.IsAuthenticated).Returns(true);
        mock.Setup(u => u.BranchId).Returns(branchId);
        mock.Setup(u => u.IsImpersonating).Returns(false);
        mock.Setup(u => u.OriginalUserId).Returns((Guid?)null);
        return mock.Object;
    }

    private static FinanceService CreateFinanceService(AppDbContext db, ICurrentUserService currentUser)
    {
        var notifications = new Mock<INotificationService>().Object;
        var logger = new Mock<ILogger<FinanceService>>().Object;
        var commissionService = new Mock<ICommissionService>().Object;
        var journalEntryService = new Mock<IJournalEntryService>().Object;
        return new FinanceService(db, currentUser, notifications, logger, commissionService, journalEntryService);
    }

    /// <summary>
    /// Seeds a patient and returns all IDs including the admin userId which
    /// also serves as the CashierSession.CashierId so payment creation works.
    /// </summary>
    private static (Guid branchId, Guid patientId, Guid userId, Guid treasuryId, ICurrentUserService currentUser) SeedPatientWithUser(AppDbContext db)
    {
        var branchId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var treasuryId = Guid.NewGuid();
        var currentUser = CreateAdminUser(userId, branchId);

        db.Branches.Add(new Branch { Id = branchId, Name = "الفرع الرئيسي" });
        db.Patients.Add(new Patient { Id = patientId, FirstName = "أحمد", LastName = "محمد", BranchId = branchId, PatientNumber = "P-001" });
        db.Users.Add(new User { Id = userId, Username = "admin1", BranchId = branchId });
        db.Treasuries.Add(new Treasury { Id = treasuryId, Name = "الخزنة", Type = TreasuryType.Vault, Balance = 0, BranchId = branchId, IsActive = true });
        // CashierSession.CashierId must match currentUser.UserId for payment creation
        db.CashierSessions.Add(new CashierSession
        {
            Id = Guid.NewGuid(),
            CashierId = userId,
            BranchId = branchId,
            TreasuryId = treasuryId,
            OpeningBalance = 0,
            Status = SessionStatus.Open,
            IsActive = true,
            SessionNumber = "CS-001",
            OpeningTime = DateTime.UtcNow
        });
        db.SaveChanges();
        return (branchId, patientId, userId, treasuryId, currentUser);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 1. Payment with PatientId is visible in patient finance summary
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Payment_WithPatientId_IsVisibleInFinanceSummary()
    {
        await using var db = CreateDb();
        var (branchId, patientId, userId, treasuryId, currentUser) = SeedPatientWithUser(db);
        var service = CreateFinanceService(db, currentUser);

        var contractResult = await service.CreateContractAsync(new CreateContractRequest
        {
            PatientId = patientId,
            TotalAmount = 5000,
            DownPayment = 1000,
            InstallmentsCount = 4,
            Specialty = "تقويم"
        });

        var summary = await service.GetPatientFinanceSummaryAsync(patientId);

        summary.Should().NotBeNull();
        summary.TotalPaid.Should().Be(1000m, "down payment of 1000 should be visible in patient finance summary");
        summary.OutstandingBalance.Should().Be(4000m, "5000 - 1000 = 4000 outstanding");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 2. Payment with InvoiceId and PatientId is visible in patient payments
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Payment_WithInvoiceIdAndPatientId_IsVisibleInPatientPayments()
    {
        await using var db = CreateDb();
        var (branchId, patientId, userId, treasuryId, currentUser) = SeedPatientWithUser(db);
        var service = CreateFinanceService(db, currentUser);

        var invoiceId = Guid.NewGuid();
        db.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            PatientId = patientId,
            InvoiceNumber = "INV-001",
            Status = InvoiceStatus.Issued,
            TotalAmount = 3000,
            Subtotal = 3000,
            IsActive = true,
            CreatedBy = userId
        });
        await db.SaveChangesAsync();

        await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patientId,
            InvoiceId = invoiceId,
            Amount = 1500,
            PaymentMethod = "cash",
            ServiceDescription = "دفعة فاتورة"
        });

        var payments = await service.GetPaymentsAsync(1, 50, patientId);
        payments.Should().ContainSingle(p => p.InvoiceId == invoiceId && p.Amount == 1500m,
            "payment linked to invoice should appear in patient payments");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 3. Draft invoice NOT counted in outstanding balance
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DraftInvoice_NotCountedInOutstandingBalance()
    {
        await using var db = CreateDb();
        var (branchId, patientId, userId, treasuryId, currentUser) = SeedPatientWithUser(db);
        var service = CreateFinanceService(db, currentUser);

        db.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            InvoiceNumber = "INV-DRAFT",
            Status = InvoiceStatus.Draft,
            TotalAmount = 5000,
            Subtotal = 5000,
            IsActive = true,
            CreatedBy = userId
        });

        db.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            InvoiceNumber = "INV-ISSUED",
            Status = InvoiceStatus.Issued,
            TotalAmount = 2000,
            Subtotal = 2000,
            IsActive = true,
            CreatedBy = userId
        });
        await db.SaveChangesAsync();

        var summary = await service.GetPatientFinanceSummaryAsync(patientId);

        summary.TotalTreatmentCost.Should().Be(2000m,
            "only issued invoice should count, not draft (5000 draft should be excluded)");
        summary.OutstandingBalance.Should().Be(2000m,
            "outstanding should only reflect issued invoices");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 4. Cancelled invoice NOT counted as outstanding due
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CancelledInvoice_NotCountedAsOutstanding()
    {
        await using var db = CreateDb();
        var (branchId, patientId, userId, treasuryId, currentUser) = SeedPatientWithUser(db);
        var service = CreateFinanceService(db, currentUser);

        db.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            InvoiceNumber = "INV-CANCELLED",
            Status = InvoiceStatus.Cancelled,
            TotalAmount = 10000,
            Subtotal = 10000,
            IsActive = true,
            CreatedBy = userId
        });
        await db.SaveChangesAsync();

        var summary = await service.GetPatientFinanceSummaryAsync(patientId);

        summary.TotalTreatmentCost.Should().Be(0m,
            "cancelled invoice should not contribute to total treatment cost");
        summary.OutstandingBalance.Should().Be(0m,
            "cancelled invoice should not create outstanding balance");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 5. Patient finance summary and account statement agree
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FinanceSummary_AndAccountStatement_Agree()
    {
        await using var db = CreateDb();
        var (branchId, patientId, userId, treasuryId, currentUser) = SeedPatientWithUser(db);
        var service = CreateFinanceService(db, currentUser);

        await service.CreateContractAsync(new CreateContractRequest
        {
            PatientId = patientId,
            TotalAmount = 8000,
            DownPayment = 2000,
            InstallmentsCount = 6,
            Specialty = "تقويم"
        });

        var summary = await service.GetPatientFinanceSummaryAsync(patientId);
        var statement = await service.GetAccountStatementAsync(patientId);

        statement.Should().NotBeNull();
        summary.TotalPaid.Should().Be(statement!.TotalPaid,
            "finance summary and account statement should have same total paid");
        summary.OutstandingBalance.Should().Be(statement.TotalRemaining,
            "finance summary outstanding should match account statement remaining");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 6. Contract down payment uses specified PaymentMethod
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ContractDownPayment_UsesSpecifiedPaymentMethod()
    {
        await using var db = CreateDb();
        var (branchId, patientId, userId, treasuryId, currentUser) = SeedPatientWithUser(db);
        var service = CreateFinanceService(db, currentUser);

        var contract = await service.CreateContractAsync(new CreateContractRequest
        {
            PatientId = patientId,
            TotalAmount = 6000,
            DownPayment = 1500,
            DownPaymentMethod = "bank_transfer",
            InstallmentsCount = 3,
            Specialty = "تركيبات"
        });

        var payments = await service.GetPaymentsAsync(1, 50, patientId);
        var downPayment = payments.FirstOrDefault(p => p.ContractId == contract.Id);

        downPayment.Should().NotBeNull("contract down payment should exist");
        downPayment!.PaymentMethod.Should().Be("bank_transfer",
            "down payment should use the specified payment method, not hardcoded 'cash'");
    }

    [Fact]
    public async Task ContractDownPayment_DefaultsToCash_WhenMethodNotSpecified()
    {
        await using var db = CreateDb();
        var (branchId, patientId, userId, treasuryId, currentUser) = SeedPatientWithUser(db);
        var service = CreateFinanceService(db, currentUser);

        var contract = await service.CreateContractAsync(new CreateContractRequest
        {
            PatientId = patientId,
            TotalAmount = 4000,
            DownPayment = 1000,
            InstallmentsCount = 3,
            Specialty = "علاج جذور"
        });

        var payments = await service.GetPaymentsAsync(1, 50, patientId);
        var downPayment = payments.FirstOrDefault(p => p.ContractId == contract.Id);

        downPayment.Should().NotBeNull("contract down payment should exist");
        downPayment!.PaymentMethod.Should().Be("cash",
            "down payment should default to 'cash' when method not specified");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 7. Deleted inactive payment not counted in active patient balance
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DeletedPayment_NotCountedInPatientBalance()
    {
        await using var db = CreateDb();
        var (branchId, patientId, userId, treasuryId, currentUser) = SeedPatientWithUser(db);
        var service = CreateFinanceService(db, currentUser);

        var contract = await service.CreateContractAsync(new CreateContractRequest
        {
            PatientId = patientId,
            TotalAmount = 5000,
            DownPayment = 2000,
            InstallmentsCount = 3,
            Specialty = "تقويم"
        });

        var summaryBefore = await service.GetPatientFinanceSummaryAsync(patientId);
        summaryBefore.TotalPaid.Should().Be(2000m);

        var payments = await service.GetPaymentsAsync(1, 50, patientId);
        var downPayment = payments.First(p => p.ContractId == contract.Id);
        await service.DeletePaymentAsync(downPayment.Id);

        var summaryAfter = await service.GetPatientFinanceSummaryAsync(patientId);
        summaryAfter.TotalPaid.Should().Be(0m, "deleted payment should not be counted in balance");
        summaryAfter.OutstandingBalance.Should().Be(5000m, "outstanding should revert to full amount");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 8. Receipt PDF endpoint exists for a patient payment
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void PaymentsController_GetPaymentPdf_Exists()
    {
        var method = typeof(PaymentsController).GetMethod("GetPaymentPdf");
        method.Should().NotBeNull("PaymentsController.GetPaymentPdf must exist for receipt generation");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 9. Account statement excludes draft invoices from totalRemaining
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AccountStatement_ExcludesDraftInvoices()
    {
        await using var db = CreateDb();
        var (branchId, patientId, userId, treasuryId, currentUser) = SeedPatientWithUser(db);
        var service = CreateFinanceService(db, currentUser);

        db.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            InvoiceNumber = "INV-DRAFT",
            Status = InvoiceStatus.Draft,
            TotalAmount = 7000,
            Subtotal = 7000,
            IsActive = true,
            CreatedBy = userId
        });
        await db.SaveChangesAsync();

        var statement = await service.GetAccountStatementAsync(patientId);

        statement.Should().NotBeNull();
        statement!.TotalContracted.Should().Be(0m,
            "draft invoice should not inflate totalContracted in account statement");
        statement.TotalRemaining.Should().Be(0m,
            "draft invoice should not create remaining balance in account statement");
    }
}
