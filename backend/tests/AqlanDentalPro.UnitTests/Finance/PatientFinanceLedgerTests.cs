using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.DTOs.Finance;
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

        // Mock IJournalEntryService — DualWritePaymentEntryAsync calls CreateEntryAsync
        var journalEntryServiceMock = new Mock<IJournalEntryService>();
        journalEntryServiceMock
            .Setup(j => j.CreateEntryAsync(
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
            .ReturnsAsync((FinancialDocumentType docType, Guid docId, string desc, DateOnly date,
                Guid branch, Guid performedBy, Guid? sessionId, Guid? treasuryId,
                IEnumerable<(JournalAccountType, Guid, decimal, decimal, string?)> lines,
                bool autoSave, CancellationToken ct) =>
            {
                var entry = new JournalEntry
                {
                    Id = Guid.NewGuid(),
                    EntryNumber = "JE-TEST-001",
                    FinancialDocumentType = docType,
                    FinancialDocumentId = docId,
                    Description = desc,
                    EntryDate = date,
                    BranchId = branch,
                    PerformedBy = performedBy,
                    IsPosted = true,
                    IsReversal = false
                };
                if (!autoSave)
                {
                    db.JournalEntries.Add(entry);
                    // Don't call SaveChanges — caller will do it
                }
                return entry;
            });

        // Mock reversal entry for delete payment
        journalEntryServiceMock
            .Setup(j => j.CreateReversalEntryAsync(
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
                    FinancialDocumentType = FinancialDocumentType.PaymentDeletion,
                    FinancialDocumentId = Guid.NewGuid(),
                    Description = reason,
                    EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    BranchId = Guid.Empty,
                    PerformedBy = performedBy,
                    IsPosted = true,
                    IsReversal = true,
                    ReversalOfEntryId = originalId
                };
            });

        return new FinanceService(db, currentUser, notifications, logger, commissionService, journalEntryServiceMock.Object);
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

    // ═══════════════════════════════════════════════════════════════════════════
    // 10. GET /api/payments/{id}/pdf returns application/pdf and non-empty bytes
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PaymentsController_GetPaymentPdf_ReturnsPdfFileContent()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF
        
        var mockFinanceService = new Mock<IFinanceService>();
        var mockPdfService = new Mock<IPdfService>();
        mockPdfService
            .Setup(p => p.GeneratePaymentReceiptAsync(paymentId))
            .ReturnsAsync(pdfBytes);
        
        var mockAuditService = new Mock<IAuditService>();
        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.SetupGet(c => c.Role).Returns(UserRole.Admin); // Admin bypasses PermissionGuard; these tests assert PDF/statement behavior, not authz
        var mockPush = new Mock<IRealTimePushService>();
        var mockLogger = new Mock<ILogger<PaymentsController>>();
        
        var controller = new PaymentsController(
            mockFinanceService.Object,
            mockPdfService.Object,
            mockAuditService.Object,
            mockCurrentUser.Object,
            CreateDb(),
            mockPush.Object,
            mockLogger.Object);

        // Act
        var result = await controller.GetPaymentPdf(paymentId);

        // Assert
        result.Should().BeOfType<FileContentResult>();
        var fileResult = (FileContentResult)result;
        fileResult.ContentType.Should().Be("application/pdf");
        fileResult.FileDownloadName.Should().Be($"receipt-{paymentId}.pdf");
        fileResult.FileContents.Should().BeEquivalentTo(pdfBytes);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 11. GET /api/invoices/{id}/pdf returns application/pdf and non-empty bytes
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task InvoicesController_GetInvoicePdf_ReturnsPdfFileContent()
    {
        // Arrange
        var invoiceId = Guid.NewGuid();
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF
        
        await using var db = CreateDb();
        var mockPdfService = new Mock<IPdfService>();
        mockPdfService
            .Setup(p => p.GenerateInvoicePdfAsync(invoiceId))
            .ReturnsAsync(pdfBytes);
            
        var mockAuditService = new Mock<IAuditService>();
        var mockLogger = new Mock<ILogger<InvoicesController>>();
        var mockCommissionService = new Mock<ICommissionService>();
        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.SetupGet(c => c.Role).Returns(UserRole.Admin); // Admin bypasses PermissionGuard; these tests assert PDF/statement behavior, not authz
        
        var controller = new InvoicesController(
            db,
            mockPdfService.Object,
            mockAuditService.Object,
            mockLogger.Object,
            mockCommissionService.Object,
            mockCurrentUser.Object,
            new FinanceSettingsReader(db));

        // Act
        var result = await controller.GetInvoicePdf(invoiceId);

        // Assert
        result.Should().BeOfType<FileContentResult>();
        var fileResult = (FileContentResult)result;
        fileResult.ContentType.Should().Be("application/pdf");
        fileResult.FileDownloadName.Should().Be($"invoice-{invoiceId}.pdf");
        fileResult.FileContents.Should().BeEquivalentTo(pdfBytes);
    }

    [Fact]
    public async Task InvoicesController_GetById_ForeignCurrencyPayment_SettlesInAccountCurrency()
    {
        // MULTI-CURRENCY: a 10,000 YER invoice paid with 100 SAR (rate 75 → AppliedAmount 7,500 YER)
        // must show PaidAmount=7,500 and RemainingAmount=2,500 — NOT 100 / 9,900 (raw SAR amount).
        await using var db = CreateDb();
        var (branchId, patientId, userId, _, _) = SeedPatientWithUser(db);

        var invoiceId = Guid.NewGuid();
        db.Invoices.Add(new Invoice
        {
            Id = invoiceId, PatientId = patientId, InvoiceNumber = "INV-FX-001",
            Status = InvoiceStatus.Issued, TotalAmount = 10_000m, Subtotal = 10_000m,
            IsActive = true, CreatedBy = userId,
        });
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(), PatientId = patientId, InvoiceId = invoiceId, BranchId = branchId,
            Amount = 100m, Currency = "SAR", AccountCurrency = "YER",
            ExchangeRateToAccountCurrency = 75m, AppliedAmount = 7_500m,
            PaymentMethod = "cash", PaymentDate = DateOnly.FromDateTime(DateTime.Today), IsActive = true,
        });
        await db.SaveChangesAsync();

        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.SetupGet(c => c.Role).Returns(UserRole.Admin);
        var controller = new InvoicesController(
            db, new Mock<IPdfService>().Object, new Mock<IAuditService>().Object,
            new Mock<ILogger<InvoicesController>>().Object, new Mock<ICommissionService>().Object,
            mockCurrentUser.Object, new FinanceSettingsReader(db));

        var ok = (await controller.GetById(invoiceId)).Should().BeOfType<OkObjectResult>().Subject;
        decimal Get(string n) => (decimal)ok.Value!.GetType().GetProperty(n)!.GetValue(ok.Value)!;
        Get("PaidAmount").Should().Be(7_500m, "the SAR payment settles the YER invoice by its YER-equivalent (AppliedAmount)");
        Get("RemainingAmount").Should().Be(2_500m);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 12. GET /api/patients/{id}/financial-statement/pdf returns PDF and non-empty bytes (and 404 for invalid patient)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PaymentsController_GetPatientFinancialStatementPdf_ReturnsPdfFileContent()
    {
        // Arrange
        var patientId = Guid.NewGuid();
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF
        
        var mockFinanceService = new Mock<IFinanceService>();
        var mockPdfService = new Mock<IPdfService>();
        mockPdfService
            .Setup(p => p.GenerateFinancialStatementAsync(patientId))
            .ReturnsAsync(pdfBytes);
        
        var mockAuditService = new Mock<IAuditService>();
        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.SetupGet(c => c.Role).Returns(UserRole.Admin); // Admin bypasses PermissionGuard; these tests assert PDF/statement behavior, not authz
        var mockPush = new Mock<IRealTimePushService>();
        var mockLogger = new Mock<ILogger<PaymentsController>>();
        
        var controller = new PaymentsController(
            mockFinanceService.Object,
            mockPdfService.Object,
            mockAuditService.Object,
            mockCurrentUser.Object,
            CreateDb(),
            mockPush.Object,
            mockLogger.Object);

        // Act
        var result = await controller.GetPatientFinancialStatementPdf(patientId);

        // Assert
        result.Should().BeOfType<FileContentResult>();
        var fileResult = (FileContentResult)result;
        fileResult.ContentType.Should().Be("application/pdf");
        fileResult.FileDownloadName.Should().Be($"financial-statement-{patientId}.pdf");
        fileResult.FileContents.Should().BeEquivalentTo(pdfBytes);
    }

    [Fact]
    public async Task PaymentsController_GetPatientFinancialStatementPdf_ReturnsNotFound_WhenPatientInvalid()
    {
        // Arrange
        var invalidPatientId = Guid.NewGuid();
        
        var mockFinanceService = new Mock<IFinanceService>();
        var mockPdfService = new Mock<IPdfService>();
        mockPdfService
            .Setup(p => p.GenerateFinancialStatementAsync(invalidPatientId))
            .ThrowsAsync(new ArgumentException("المريض غير موجود"));
        
        var mockAuditService = new Mock<IAuditService>();
        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.SetupGet(c => c.Role).Returns(UserRole.Admin); // Admin bypasses PermissionGuard; these tests assert PDF/statement behavior, not authz
        var mockPush = new Mock<IRealTimePushService>();
        var mockLogger = new Mock<ILogger<PaymentsController>>();
        
        var controller = new PaymentsController(
            mockFinanceService.Object,
            mockPdfService.Object,
            mockAuditService.Object,
            mockCurrentUser.Object,
            CreateDb(),
            mockPush.Object,
            mockLogger.Object);

        // Act
        var result = await controller.GetPatientFinancialStatementPdf(invalidPatientId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = (NotFoundObjectResult)result;
        notFoundResult.Value.Should().BeEquivalentTo(new { message = "المريض غير موجود" });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // QA-594: Unbilled visit + partial payment scenario
    // Owner scenario: root-canal session 50,000 + partial payment 20,000
    // without creating a contract or invoice. Previously the remaining 30,000
    // vanished from every balance view. Now it must show correctly.
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Seed a patient with a Visit (AmountDueReference = sessionCost) and an
    /// unlinked Payment (Amount = paidAmount). No contract, no invoice.
    /// </summary>
    private static (Guid branchId, Guid patientId, ICurrentUserService currentUser) SeedPatientWithUnbilledVisit(
        AppDbContext db, decimal sessionCost, decimal paidAmount)
    {
        var branchId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var currentUser = CreateAdminUser(userId, branchId);

        db.Branches.Add(new Branch { Id = branchId, Name = "الفرع الرئيسي" });
        db.Patients.Add(new Patient { Id = patientId, FirstName = "اختبار", LastName = "النظام", BranchId = branchId, PatientNumber = "P-QA-594" });
        db.Users.Add(new User { Id = userId, Username = "admin-qa", BranchId = branchId });

        if (sessionCost > 0)
        {
            db.Visits.Add(new Visit
            {
                Id = Guid.NewGuid(),
                PatientId = patientId,
                VisitDate = DateOnly.FromDateTime(DateTime.Today),
                VisitType = "عصب",
                AmountDueReference = sessionCost,
                CheckoutStatus = "ReadyForCheckout",
                IsActive = true
            });
        }

        if (paidAmount != 0)
        {
            db.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(),
                PatientId = patientId,
                ContractId = null,
                InvoiceId = null,
                Amount = paidAmount,
                AppliedAmount = paidAmount,
                PaymentDate = DateOnly.FromDateTime(DateTime.Today),
                PaymentMethod = "cash",
                AccountCurrency = "YER",
                ExchangeRateToAccountCurrency = 1m,
                IsActive = true
            });
        }

        db.SaveChanges();
        return (branchId, patientId, currentUser);
    }

    [Fact]
    public async Task QA594_UnbilledVisit_WithPartialPayment_OutstandingReflectsRemaining()
    {
        // Scenario: 50,000 root-canal session + 20,000 partial payment, no
        // contract, no invoice. Expected outstanding = 30,000.
        await using var db = CreateDb();
        var (_, patientId, currentUser) = SeedPatientWithUnbilledVisit(db, sessionCost: 50_000m, paidAmount: 20_000m);
        var service = CreateFinanceService(db, currentUser);

        var summary = await service.GetPatientFinanceSummaryAsync(patientId);

        summary.Should().NotBeNull();
        summary.UnbilledVisitsAmount.Should().Be(50_000m, "the visit has AmountDueReference = 50,000 with no linked invoice");
        summary.TotalTreatmentCost.Should().Be(50_000m, "unbilled visit cost is now included in total");
        summary.TotalPaid.Should().Be(20_000m, "standalone payment is counted");
        summary.OutstandingBalance.Should().Be(30_000m, "50,000 - 20,000 = 30,000 remaining debt");
        summary.FinancialStatus.Should().Be("on_track", "debt exists and is not overdue");
    }

    [Fact]
    public async Task QA594_UnbilledVisit_NoPayment_OutstandingEqualsFullAmount()
    {
        // Scenario: 50,000 session, no payment. Expected outstanding = 50,000.
        await using var db = CreateDb();
        var (_, patientId, currentUser) = SeedPatientWithUnbilledVisit(db, sessionCost: 50_000m, paidAmount: 0m);
        var service = CreateFinanceService(db, currentUser);

        var summary = await service.GetPatientFinanceSummaryAsync(patientId);

        summary.UnbilledVisitsAmount.Should().Be(50_000m);
        summary.TotalTreatmentCost.Should().Be(50_000m);
        summary.TotalPaid.Should().Be(0m);
        summary.OutstandingBalance.Should().Be(50_000m, "no payment → full amount outstanding");
    }

    [Fact]
    public async Task QA594_BilledVisit_NotDoubleCountedInBalance()
    {
        // Scenario: 50,000 session WITH a linked Issued invoice of 50,000 +
        // 20,000 payment linked to that invoice. Expected outstanding = 30,000
        // (NOT 80,000 — the visit must not be double-counted with its invoice).
        await using var db = CreateDb();
        var (branchId, patientId, currentUser) = SeedPatientWithUnbilledVisit(db, sessionCost: 50_000m, paidAmount: 0m);

        var visitId = db.Visits.First().Id;
        var invoiceId = Guid.NewGuid();
        db.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            PatientId = patientId,
            VisitId = visitId, // ← linked to the visit
            InvoiceNumber = "INV-QA-001",
            Status = InvoiceStatus.Issued,
            Subtotal = 50_000m,
            TotalAmount = 50_000m,
            IsActive = true
        });
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            InvoiceId = invoiceId,
            Amount = 20_000m,
            AppliedAmount = 20_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = "cash",
            AccountCurrency = "YER",
            ExchangeRateToAccountCurrency = 1m,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var service = CreateFinanceService(db, currentUser);
        var summary = await service.GetPatientFinanceSummaryAsync(patientId);

        summary.UnbilledVisitsAmount.Should().Be(0m, "visit is billed via the linked invoice → not unbilled");
        summary.TotalTreatmentCost.Should().Be(50_000m, "only the invoice counts, not visit + invoice");
        summary.TotalPaid.Should().Be(20_000m);
        summary.OutstandingBalance.Should().Be(30_000m, "50,000 invoice - 20,000 payment");
    }

    [Fact]
    public async Task QA594_UnlinkedPayment_NoContractOrInvoice_OutstandingNotNegative()
    {
        // Scenario: 20,000 payment, no contract, no invoice, no visit.
        // Expected outstanding = 0 (clamped, NOT -20,000).
        await using var db = CreateDb();
        var (_, patientId, currentUser) = SeedPatientWithUnbilledVisit(db, sessionCost: 0m, paidAmount: 20_000m);
        var service = CreateFinanceService(db, currentUser);

        var summary = await service.GetPatientFinanceSummaryAsync(patientId);

        summary.TotalTreatmentCost.Should().Be(0m);
        summary.TotalPaid.Should().Be(20_000m);
        summary.OutstandingBalance.Should().Be(0m, "must clamp to 0, not go negative");
        summary.FinancialStatus.Should().Be("no_plan", "no obligation tracked → no_plan");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // QA-596: Account statement consistency with GetPatientFinanceSummaryAsync
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task QA596_AccountStatement_IncludesUnbilledVisits()
    {
        // Scenario: 50,000 unbilled visit + 20,000 payment (no contract, no invoice).
        // Account statement's TotalRemaining must be 30,000, NOT 0.
        await using var db = CreateDb();
        var (_, patientId, currentUser) = SeedPatientWithUnbilledVisit(db, sessionCost: 50_000m, paidAmount: 20_000m);
        var service = CreateFinanceService(db, currentUser);

        var statement = await service.GetAccountStatementAsync(patientId);

        statement.Should().NotBeNull();
        statement!.TotalRemaining.Should().Be(30_000m, "QA-596: account statement now includes unbilled visits");
        statement.UnbilledVisitsAmount.Should().Be(50_000m, "unbilled visits amount is surfaced as a dedicated field");
        statement.TotalPaid.Should().Be(20_000m);
        statement.TotalContracted.Should().Be(0m, "no contracts");
    }

    [Fact]
    public async Task QA596_AccountStatement_BilledVisitNotDoubleCounted()
    {
        // Scenario: 50,000 visit WITH a linked Issued invoice of 50,000 + 20,000 payment.
        // Account statement must show 30,000 (NOT 80,000 — visit must not be double-counted).
        await using var db = CreateDb();
        var (branchId, patientId, currentUser) = SeedPatientWithUnbilledVisit(db, sessionCost: 50_000m, paidAmount: 0m);

        var visitId = db.Visits.First().Id;
        db.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            VisitId = visitId,
            InvoiceNumber = "INV-QA596-001",
            Status = InvoiceStatus.Issued,
            Subtotal = 50_000m,
            TotalAmount = 50_000m,
            IsActive = true
        });
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            Amount = 20_000m,
            AppliedAmount = 20_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = "cash",
            AccountCurrency = "YER",
            ExchangeRateToAccountCurrency = 1m,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var service = CreateFinanceService(db, currentUser);
        var statement = await service.GetAccountStatementAsync(patientId);

        statement!.TotalRemaining.Should().Be(30_000m, "50,000 invoice - 20,000 payment (visit not double-counted)");
        statement.UnbilledVisitsAmount.Should().Be(0m, "visit has a linked invoice → not unbilled");
    }

    [Fact]
    public async Task QA596_FinanceSummary_IncludesUnbilledVisitsAcrossBranch()
    {
        // Scenario: two patients, each with a 25,000 unbilled visit. TotalOutstanding
        // on the finance dashboard summary should be 50,000 (sum of unbilled visits).
        await using var db = CreateDb();
        var (_, patientA, currentUserA) = SeedPatientWithUnbilledVisit(db, sessionCost: 25_000m, paidAmount: 0m);

        // Seed second patient in same branch
        var branchId = db.Patients.First().BranchId!.Value;
        var patientB = Guid.NewGuid();
        db.Patients.Add(new Patient { Id = patientB, FirstName = "مريض", LastName = "ب", BranchId = branchId, PatientNumber = "P-B-QA596" });
        db.Visits.Add(new Visit
        {
            Id = Guid.NewGuid(),
            PatientId = patientB,
            VisitDate = DateOnly.FromDateTime(DateTime.Today),
            VisitType = "عصب",
            AmountDueReference = 25_000m,
            CheckoutStatus = "ReadyForCheckout",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var service = CreateFinanceService(db, currentUserA);
        var summary = await service.GetSummaryAsync();

        summary.TotalOutstanding.Should().Be(50_000m, "QA-596: dashboard now includes unbilled visits across patients (25k × 2)");
    }
}
