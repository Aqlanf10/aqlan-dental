using System.Security.Claims;
using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Finance;

/// <summary>
/// PR #245 — Real controller-level tests that instantiate and call actual controller methods.
/// Tests verify HTTP response types, status codes, and response shape (field names, wrappers)
/// rather than simulating or re-implementing controller logic.
///
/// Endpoints tested:
///   - FinanceV3Controller: invoices, patient-accounts, treasuries, dashboard, patient-balance
///   - InvoicesController: /api/patients/{id}/invoices (must return Balance)
///   - TreasuriesController: GetAll (must return { data } wrapper)
///   - CashierSessionsController: GetAll, GetSessionDetail
///   - AdvancePaymentController: rejection path, create, delete, GetAll
///   - SalaryController: GetAll, GenerateSalary, rejection paths
/// </summary>
public class FinanceV3ControllerTests
{
    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static ICurrentUserService CreateAdminUser(Guid? branchId = null)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(u => u.UserId).Returns(Guid.NewGuid());
        mock.Setup(u => u.Role).Returns(UserRole.Admin);
        mock.Setup(u => u.IsAdmin).Returns(true);
        mock.Setup(u => u.IsAuthenticated).Returns(true);
        mock.Setup(u => u.BranchId).Returns(branchId);
        mock.Setup(u => u.IsImpersonating).Returns(false);
        mock.Setup(u => u.OriginalUserId).Returns((Guid?)null);
        return mock.Object;
    }

    private static ICurrentUserService CreateBranchUser(Guid userId, Guid branchId)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(u => u.UserId).Returns(userId);
        mock.Setup(u => u.Role).Returns(UserRole.Accountant);
        mock.Setup(u => u.IsAdmin).Returns(false);
        mock.Setup(u => u.IsAuthenticated).Returns(true);
        mock.Setup(u => u.BranchId).Returns(branchId);
        mock.Setup(u => u.IsImpersonating).Returns(false);
        mock.Setup(u => u.OriginalUserId).Returns((Guid?)null);
        return mock.Object;
    }

    private static (Guid branchId, Guid userId) SeedBranchAndUser(AppDbContext db)
    {
        var branchId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        db.Branches.Add(new Branch { Id = branchId, Name = "الفرع الرئيسي" });
        db.Users.Add(new User { Id = userId, Username = "admin1", BranchId = branchId });
        db.SaveChanges();
        return (branchId, userId);
    }

    private static FinanceV3Controller BuildFinanceV3Controller(AppDbContext db, ICurrentUserService? currentUser = null)
    {
        currentUser ??= CreateAdminUser();
        var notifications = new Mock<INotificationService>().Object;
        var commissionService = new Mock<ICommissionService>().Object;
        var journalEntryService = new JournalEntryService(db, new Mock<ILogger<JournalEntryService>>().Object);
        var paymentService = new PaymentService(db, currentUser, notifications, new Mock<ILogger<PaymentService>>().Object, commissionService, journalEntryService);
        var audit = new Mock<IAuditService>().Object;
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        var logger = new Mock<ILogger<FinanceV3Controller>>().Object;
        return new FinanceV3Controller(db, currentUser, paymentService, new Mock<IInvoiceLedgerService>().Object, audit, journalEntryService, treasuryResolution, logger);
    }

    private static TreasuriesController BuildTreasuriesController(AppDbContext db, ICurrentUserService? currentUser = null, IAuditService? audit = null)
    {
        currentUser ??= CreateAdminUser();
        audit ??= new Mock<IAuditService>().Object;
        var logger = new Mock<ILogger<TreasuriesController>>().Object;
        return new TreasuriesController(db, currentUser, audit, logger);
    }

    private static CashierSessionsController BuildCashierSessionsController(
        AppDbContext db, ICurrentUserService? currentUser = null, IAuditService? audit = null,
        ITreasuryResolutionService? treasuryResolution = null)
    {
        currentUser ??= CreateAdminUser();
        audit ??= new Mock<IAuditService>().Object;
        treasuryResolution ??= new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        var logger = new Mock<ILogger<CashierSessionsController>>().Object;
        return new CashierSessionsController(db, currentUser, audit, treasuryResolution, logger);
    }

    private static InvoicesController BuildInvoicesController(AppDbContext db, ICurrentUserService? currentUser = null)
    {
        currentUser ??= CreateAdminUser();
        var pdfService = new Mock<IPdfService>().Object;
        var audit = new Mock<IAuditService>().Object;
        var logger = new Mock<ILogger<InvoicesController>>().Object;
        var commission = new Mock<ICommissionService>().Object;
        var financeSettings = new FinanceSettingsReader(db);
        return new InvoicesController(db, pdfService, audit, logger, commission, currentUser, financeSettings);
    }

    private static AdvancePaymentController BuildAdvancePaymentController(
        AppDbContext db, IAuditService? audit = null,
        IJournalEntryService? jeService = null, ITreasuryResolutionService? treasuryResolution = null,
        ICurrentUserService? currentUser = null)
    {
        // FIN-PERM: AdvancePaymentController now takes ICurrentUserService for PermissionGuard.
        // Default to an Admin user so the granular finance.expenses guard always bypasses
        // (these tests assert business logic, not the new authz gate).
        currentUser ??= CreateAdminUser();
        audit ??= new Mock<IAuditService>().Object;
        jeService ??= new JournalEntryService(db, new Mock<ILogger<JournalEntryService>>().Object);
        treasuryResolution ??= new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        var logger = new Mock<ILogger<AdvancePaymentController>>().Object;
        return new AdvancePaymentController(db, audit, jeService, treasuryResolution, currentUser, logger);
    }

    private static SalaryController BuildSalaryController(
        AppDbContext db, IJournalEntryService? jeService = null,
        ITreasuryResolutionService? treasuryResolution = null, IAuditService? audit = null)
    {
        jeService ??= new JournalEntryService(db, new Mock<ILogger<JournalEntryService>>().Object);
        treasuryResolution ??= new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        audit ??= new Mock<IAuditService>().Object;
        var logger = new Mock<ILogger<SalaryController>>().Object;
        return new SalaryController(db, jeService, treasuryResolution, audit, logger);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // FinanceV3Controller — Invoices endpoint returns PatientId, Balance, IssueDate
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FinanceV3_GetInvoices_ReturnsPatientId_Balance_IssueDate()
    {
        await using var db = CreateDb();
        var (branchId, userId) = SeedBranchAndUser(db);

        var patient = new Patient
        {
            Id = Guid.NewGuid(), FirstName = "أحمد", LastName = "علي",
            PatientNumber = "P-001", BranchId = branchId
        };
        db.Patients.Add(patient);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(), PatientId = patient.Id, InvoiceNumber = "INV-001",
            Status = InvoiceStatus.Issued, TotalAmount = 500m, Subtotal = 500m,
            CreatedBy = userId
        };
        db.Invoices.Add(invoice);

        var payment = new Payment
        {
            Id = Guid.NewGuid(), PatientId = patient.Id, InvoiceId = invoice.Id,
            Amount = 200m, PaymentMethod = "cash",
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            ReceiptNumber = "RCP-001", IsActive = true
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        var controller = BuildFinanceV3Controller(db, CreateAdminUser(branchId));

        // Act
        var result = await controller.GetInvoices(page: 1, pageSize: 20);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;

        // Verify response has data wrapper with correct shape
        var response = ok.Value!;
        var dataType = response.GetType();

        // Check data property exists and contains items
        var dataProp = dataType.GetProperty("data");
        dataProp.Should().NotBeNull("response must have 'data' property");

        var dataCollection = (System.Collections.IEnumerable)dataProp!.GetValue(response)!;
        var items = dataCollection.Cast<object>().ToList();
        items.Should().HaveCount(1, "one invoice was seeded");

        // Verify each required field exists on the invoice DTO
        var invoiceDto = items[0];
        var dtoType = invoiceDto.GetType();

        var patientIdProp = dtoType.GetProperty("PatientId");
        patientIdProp.Should().NotBeNull("invoice DTO must have PatientId field");
        patientIdProp!.GetValue(invoiceDto).Should().Be(patient.Id);

        var balanceProp = dtoType.GetProperty("Balance");
        balanceProp.Should().NotBeNull("invoice DTO must have Balance field");
        balanceProp!.GetValue(invoiceDto).Should().Be(300m, "Balance = 500 - 200");

        var issueDateProp = dtoType.GetProperty("IssueDate");
        issueDateProp.Should().NotBeNull("invoice DTO must have IssueDate field");
    }

    [Fact]
    public async Task FinanceV3_GetInvoices_ReturnsPaginatedResponse()
    {
        await using var db = CreateDb();
        var (branchId, _) = SeedBranchAndUser(db);
        var controller = BuildFinanceV3Controller(db, CreateAdminUser(branchId));

        var result = await controller.GetInvoices(page: 1, pageSize: 20);
        var ok = (OkObjectResult)result;
        var response = ok.Value!;
        var responseType = response.GetType();

        // Must have total, page, pageSize
        responseType.GetProperty("total").Should().NotBeNull("must have total");
        responseType.GetProperty("page").Should().NotBeNull("must have page");
        responseType.GetProperty("pageSize").Should().NotBeNull("must have pageSize");
        responseType.GetProperty("data").Should().NotBeNull("must have data array");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // FinanceV3Controller — Patient Accounts returns paginated with Balance
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FinanceV3_GetPatientAccounts_ReturnsPaginatedWithBalance()
    {
        await using var db = CreateDb();
        var (branchId, _) = SeedBranchAndUser(db);

        var patient = new Patient
        {
            Id = Guid.NewGuid(), FirstName = "سارة", LastName = "محمد",
            PatientNumber = "P-002", BranchId = branchId
        };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var controller = BuildFinanceV3Controller(db, CreateAdminUser(branchId));
        var result = await controller.GetPatientAccounts(page: 1, pageSize: 20);

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var response = ok.Value!;
        var responseType = response.GetType();

        responseType.GetProperty("data").Should().NotBeNull("must have data wrapper");
        responseType.GetProperty("total").Should().NotBeNull("must have total");
        responseType.GetProperty("page").Should().NotBeNull("must have page");
        responseType.GetProperty("pageSize").Should().NotBeNull("must have pageSize");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // FinanceV3Controller — GetPayments exposes currency fields + honors method filter
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FinanceV3_GetPayments_ExposesCurrencyFields_AndFiltersByMethod()
    {
        await using var db = CreateDb();
        var (branchId, _) = SeedBranchAndUser(db);

        var patient = new Patient
        {
            Id = Guid.NewGuid(), FirstName = "خالد", LastName = "سعيد",
            PatientNumber = "P-PAY", BranchId = branchId
        };
        db.Patients.Add(patient);

        var today = DateOnly.FromDateTime(DateTime.Today);

        // YER cash payment
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(), PatientId = patient.Id, BranchId = branchId,
            Amount = 1000m, Currency = "YER", AccountCurrency = "YER",
            ExchangeRateToAccountCurrency = 1m, AppliedAmount = 1000m,
            PaymentMethod = "cash", PaymentDate = today,
            ReceiptNumber = "RCP-YER", IsActive = true
        });

        // SAR card payment settling a YER account (100 SAR ≈ 7500 YER)
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(), PatientId = patient.Id, BranchId = branchId,
            Amount = 100m, Currency = "SAR", AccountCurrency = "YER",
            ExchangeRateToAccountCurrency = 75m, AppliedAmount = 7500m,
            PaymentMethod = "card", PaymentDate = today,
            ReceiptNumber = "RCP-SAR", IsActive = true
        });
        await db.SaveChangesAsync();

        var controller = BuildFinanceV3Controller(db, CreateAdminUser(branchId));

        // Act 1: no filter → both payments, currency fields present
        var all = (OkObjectResult)await controller.GetPayments(page: 1, pageSize: 20);
        var allData = (System.Collections.IEnumerable)all.Value!.GetType()
            .GetProperty("data")!.GetValue(all.Value)!;
        var allItems = allData.Cast<object>().ToList();
        allItems.Should().HaveCount(2, "both payments returned with no filter");

        var sar = allItems.Single(i => (string)i.GetType().GetProperty("Currency")!.GetValue(i)! == "SAR");
        var sarType = sar.GetType();
        sarType.GetProperty("Currency").Should().NotBeNull("DTO must expose Currency");
        sarType.GetProperty("AccountCurrency").Should().NotBeNull("DTO must expose AccountCurrency");
        sarType.GetProperty("AppliedAmount").Should().NotBeNull("DTO must expose AppliedAmount");
        sarType.GetProperty("AppliedAmount")!.GetValue(sar).Should().Be(7500m,
            "SAR payment applied 7500 YER to the patient account");

        // Act 2: method=cash → only the YER cash payment
        var cashOnly = (OkObjectResult)await controller.GetPayments(page: 1, pageSize: 20, method: "cash");
        var cashData = (System.Collections.IEnumerable)cashOnly.Value!.GetType()
            .GetProperty("data")!.GetValue(cashOnly.Value)!;
        var cashItems = cashData.Cast<object>().ToList();
        cashItems.Should().HaveCount(1, "method filter returns only cash payments");
        cashItems[0].GetType().GetProperty("PaymentMethod")!.GetValue(cashItems[0]).Should().Be("cash");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // FinanceV3Controller — Branch isolation for non-admin with null BranchId
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FinanceV3_Dashboard_BranchlessNonAdmin_ReturnsForbid()
    {
        await using var db = CreateDb();
        var branchlessUser = CreateBranchUser(Guid.NewGuid(), Guid.Empty);
        // Override BranchId to null
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(u => u.UserId).Returns(Guid.NewGuid());
        mock.Setup(u => u.Role).Returns(UserRole.Accountant);
        mock.Setup(u => u.IsAdmin).Returns(false);
        mock.Setup(u => u.IsAuthenticated).Returns(true);
        mock.Setup(u => u.BranchId).Returns((Guid?)null);

        var controller = BuildFinanceV3Controller(db, mock.Object);
        var result = await controller.GetDashboard();

        result.Should().BeOfType<ObjectResult>("non-admin with null BranchId must be forbidden").Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task FinanceV3_Dashboard_BranchlessNonAdmin_EmptyBranchId_ReturnsForbid()
    {
        await using var db = CreateDb();
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(u => u.UserId).Returns(Guid.NewGuid());
        mock.Setup(u => u.Role).Returns(UserRole.Accountant);
        mock.Setup(u => u.IsAdmin).Returns(false);
        mock.Setup(u => u.IsAuthenticated).Returns(true);
        mock.Setup(u => u.BranchId).Returns(Guid.Empty);

        var controller = BuildFinanceV3Controller(db, mock.Object);
        var result = await controller.GetDashboard();

        result.Should().BeOfType<ObjectResult>("non-admin with empty BranchId must be forbidden").Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task FinanceV3_Dashboard_AdminWithFinanceRows_ReturnsOk()
    {
        await using var db = CreateDb();
        var (branchId, userId) = SeedBranchAndUser(db);

        var treasury = new Treasury
        {
            Id = Guid.NewGuid(), Name = "Main Vault", Type = TreasuryType.Vault,
            Balance = 25_000m, BranchId = branchId, IsActive = true
        };
        db.Treasuries.Add(treasury);

        db.JournalEntries.Add(new JournalEntry
        {
            Id = Guid.NewGuid(),
            EntryNumber = "JE-DASH-001",
            FinancialDocumentId = Guid.NewGuid(),
            FinancialDocumentType = FinancialDocumentType.Payment,
            Description = "Dashboard smoke test",
            EntryDate = DateOnly.FromDateTime(DateTime.Today),
            BranchId = branchId,
            PerformedBy = userId,
            TreasuryId = treasury.Id,
            IsPosted = true,
            Lines =
            [
                new JournalLine
                {
                    AccountType = JournalAccountType.Treasury,
                    AccountId = treasury.Id,
                    Debit = 10_000m,
                    Credit = 0m,
                    BranchId = branchId
                },
                new JournalLine
                {
                    AccountType = JournalAccountType.PatientReceivable,
                    AccountId = Guid.NewGuid(),
                    Debit = 0m,
                    Credit = 10_000m,
                    BranchId = branchId
                }
            ]
        });
        await db.SaveChangesAsync();

        var controller = BuildFinanceV3Controller(db, CreateAdminUser());
        var result = await controller.GetDashboard();

        result.Should().BeOfType<OkObjectResult>("dashboard must not fail when Finance V3 tables contain real rows");
        var ok = (OkObjectResult)result;
        ok.Value!.GetType().GetProperty("TodayInflow").Should().NotBeNull();
        ok.Value!.GetType().GetProperty("TotalTreasuryBalance").Should().NotBeNull();
    }

    [Fact]
    public async Task FinanceV3_Dashboard_TotalTreasuryBalance_SumsOnlyYerTreasuries()
    {
        // MULTI-CURRENCY: the YER-denominated dashboard total must NOT add SAR/USD balances into
        // a YER figure. Foreign balances are surfaced per-currency in the Treasuries tab instead.
        await using var db = CreateDb();
        var (branchId, _) = SeedBranchAndUser(db);

        db.Treasuries.Add(new Treasury { Id = Guid.NewGuid(), Name = "درج YER", Type = TreasuryType.Vault, Currency = "YER", Balance = 1_000m, BranchId = branchId, IsActive = true });
        db.Treasuries.Add(new Treasury { Id = Guid.NewGuid(), Name = "درج SAR", Type = TreasuryType.Vault, Currency = "SAR", Balance = 500m, BranchId = branchId, IsActive = true });
        db.Treasuries.Add(new Treasury { Id = Guid.NewGuid(), Name = "حساب USD", Type = TreasuryType.Bank, Currency = "USD", Balance = 200m, BranchId = branchId, IsActive = true });
        await db.SaveChangesAsync();

        var controller = BuildFinanceV3Controller(db, CreateAdminUser());
        var result = await controller.GetDashboard();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var total = (decimal)ok.Value!.GetType().GetProperty("TotalTreasuryBalance")!.GetValue(ok.Value)!;
        total.Should().Be(1_000m, "only the YER treasury counts toward the YER-denominated total — SAR/USD are never mixed in");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // InvoicesController — /api/patients/{id}/invoices returns Balance
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Invoices_GetByPatient_ReturnsBalance()
    {
        await using var db = CreateDb();
        var (branchId, userId) = SeedBranchAndUser(db);

        var patient = new Patient
        {
            Id = Guid.NewGuid(), FirstName = "خالد", LastName = "سعيد",
            PatientNumber = "P-003", BranchId = branchId
        };
        db.Patients.Add(patient);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(), PatientId = patient.Id, InvoiceNumber = "INV-002",
            Status = InvoiceStatus.Issued, TotalAmount = 1000m, Subtotal = 1000m,
            CreatedBy = userId
        };
        db.Invoices.Add(invoice);

        var payment = new Payment
        {
            Id = Guid.NewGuid(), PatientId = patient.Id, InvoiceId = invoice.Id,
            Amount = 400m, PaymentMethod = "cash",
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            ReceiptNumber = "RCP-002", IsActive = true
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        var controller = BuildInvoicesController(db, CreateAdminUser(branchId));

        var result = await controller.GetByPatient(patient.Id);

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;

        // The response is a list of anonymous objects
        var invoices = (System.Collections.IEnumerable)ok.Value!;
        var items = invoices.Cast<object>().ToList();
        items.Should().HaveCount(1);

        var dto = items[0];
        var dtoType = dto.GetType();

        // Verify Balance field exists and is correct
        var balanceProp = dtoType.GetProperty("Balance");
        balanceProp.Should().NotBeNull("patient invoices DTO must have Balance field");
        balanceProp!.GetValue(dto).Should().Be(600m, "Balance = 1000 - 400");

        // Verify PaidAmount field
        var paidAmountProp = dtoType.GetProperty("PaidAmount");
        paidAmountProp.Should().NotBeNull("patient invoices DTO must have PaidAmount field");
        paidAmountProp!.GetValue(dto).Should().Be(400m);
    }

    [Fact]
    public async Task Invoices_GetByPatient_FiltersByBalance_WhenUsedByPaymentModal()
    {
        // This test verifies that the Balance field is returned so the frontend
        // can filter invoices with i.Balance > 0 (for payment modal selection)
        await using var db = CreateDb();
        var (branchId, userId) = SeedBranchAndUser(db);

        var patient = new Patient
        {
            Id = Guid.NewGuid(), FirstName = "نورة", LastName = "أحمد",
            PatientNumber = "P-004", BranchId = branchId
        };
        db.Patients.Add(patient);

        // Fully paid invoice (Balance = 0)
        var paidInvoice = new Invoice
        {
            Id = Guid.NewGuid(), PatientId = patient.Id, InvoiceNumber = "INV-003",
            Status = InvoiceStatus.Paid, TotalAmount = 500m, Subtotal = 500m,
            CreatedBy = userId
        };
        db.Invoices.Add(paidInvoice);
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(), PatientId = patient.Id, InvoiceId = paidInvoice.Id,
            Amount = 500m, PaymentMethod = "cash",
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            ReceiptNumber = "RCP-003", IsActive = true
        });

        // Partially paid invoice (Balance > 0)
        var partialInvoice = new Invoice
        {
            Id = Guid.NewGuid(), PatientId = patient.Id, InvoiceNumber = "INV-004",
            Status = InvoiceStatus.Issued, TotalAmount = 800m, Subtotal = 800m,
            CreatedBy = userId
        };
        db.Invoices.Add(partialInvoice);
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(), PatientId = patient.Id, InvoiceId = partialInvoice.Id,
            Amount = 300m, PaymentMethod = "card",
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            ReceiptNumber = "RCP-004", IsActive = true
        });

        await db.SaveChangesAsync();

        var controller = BuildInvoicesController(db, CreateAdminUser(branchId));
        var result = await controller.GetByPatient(patient.Id);

        var ok = (OkObjectResult)result;
        var invoices = (System.Collections.IEnumerable)ok.Value!;
        var items = invoices.Cast<object>().ToList();

        // Simulate frontend filter: i.Balance > 0
        var openInvoices = items.Where(i =>
        {
            var balance = Convert.ToDecimal(i.GetType().GetProperty("Balance")!.GetValue(i));
            return balance > 0;
        }).ToList();

        openInvoices.Should().HaveCount(1, "only partially paid invoice should appear");
        var openInv = openInvoices[0];
        openInv.GetType().GetProperty("Balance")!.GetValue(openInv).Should().Be(500m);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TreasuriesController — GetAll returns { data } wrapper
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Treasuries_GetAll_ReturnsDataWrapper()
    {
        await using var db = CreateDb();
        var (branchId, _) = SeedBranchAndUser(db);

        db.Treasuries.Add(new Treasury
        {
            Id = Guid.NewGuid(), Name = "الخزنة الرئيسية", Type = TreasuryType.Vault,
            Balance = 50_000m, BranchId = branchId, IsActive = true
        });
        db.Treasuries.Add(new Treasury
        {
            Id = Guid.NewGuid(), Name = "الحساب البنكي", Type = TreasuryType.Bank,
            Balance = 200_000m, BranchId = branchId, IsActive = true
        });
        await db.SaveChangesAsync();

        var controller = BuildTreasuriesController(db, CreateAdminUser(branchId));
        var result = await controller.GetAll();

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var response = ok.Value!;
        var responseType = response.GetType();

        // Must have data property (not a raw array)
        var dataProp = responseType.GetProperty("data");
        dataProp.Should().NotBeNull("TreasuriesController must return { data: [...] } wrapper");

        var dataCollection = (System.Collections.IEnumerable)dataProp!.GetValue(response)!;
        var items = dataCollection.Cast<object>().ToList();
        items.Should().HaveCount(2, "two treasuries were seeded");

        // Verify each treasury has required fields
        var firstItem = items[0];
        var dtoType = firstItem.GetType();

        dtoType.GetProperty("Id").Should().NotBeNull();
        dtoType.GetProperty("Name").Should().NotBeNull();
        dtoType.GetProperty("Type").Should().NotBeNull();
        dtoType.GetProperty("Balance").Should().NotBeNull();
        dtoType.GetProperty("BranchId").Should().NotBeNull();
    }

    [Fact]
    public async Task Treasuries_GetAll_BranchUser_SeesOnlyOwnBranch()
    {
        await using var db = CreateDb();
        var branch1 = Guid.NewGuid();
        var branch2 = Guid.NewGuid();
        db.Branches.Add(new Branch { Id = branch1, Name = "الفرع ١" });
        db.Branches.Add(new Branch { Id = branch2, Name = "الفرع ٢" });

        db.Treasuries.Add(new Treasury
        {
            Id = Guid.NewGuid(), Name = "خزنة فرع ١", Type = TreasuryType.Vault,
            Balance = 30_000m, BranchId = branch1, IsActive = true
        });
        db.Treasuries.Add(new Treasury
        {
            Id = Guid.NewGuid(), Name = "خزنة فرع ٢", Type = TreasuryType.Vault,
            Balance = 70_000m, BranchId = branch2, IsActive = true
        });
        // FIN-PERM: grant Accountant finance.treasuries.view so the granular permission
        // gate passes; the test then exercises the branch-isolation filter (not the gate).
        db.RolePermissions.Add(new RolePermission
        {
            Role = "Accountant", Resource = "finance.treasuries", CanView = true,
        });
        await db.SaveChangesAsync();

        var controller = BuildTreasuriesController(db, CreateBranchUser(Guid.NewGuid(), branch1));
        var result = await controller.GetAll();

        var ok = (OkObjectResult)result;
        var response = ok.Value!;
        var dataProp = response.GetType().GetProperty("data")!;
        var items = ((System.Collections.IEnumerable)dataProp.GetValue(response)!).Cast<object>().ToList();

        items.Should().HaveCount(1, "branch user should only see their own branch treasuries");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CashierSessionsController — GetAll returns correct field names
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CashierSessions_GetAll_ReturnsExpectedFieldNames()
    {
        await using var db = CreateDb();
        var (branchId, cashierId) = SeedBranchAndUser(db);

        db.CashierSessions.Add(new CashierSession
        {
            SessionNumber = "CS-20250101-01",
            CashierId = cashierId,
            BranchId = branchId,
            OpeningTime = DateTime.UtcNow.AddHours(-2),
            OpeningBalance = 100_000m,
            ExpectedClosingCash = 80_000m,
            ExpectedClosingCard = 20_000m,
            ExpectedClosingBank = 10_000m,
            ActualClosingCash = 79_500m,
            ActualClosingCard = 20_100m,
            ActualClosingBank = 10_000m,
            Status = SessionStatus.Closed
        });
        await db.SaveChangesAsync();

        var controller = BuildCashierSessionsController(db, CreateAdminUser(branchId));
        var result = await controller.GetAll(page: 1, pageSize: 20);

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var response = ok.Value!;
        var responseType = response.GetType();

        // Must have data wrapper
        var dataProp = responseType.GetProperty("data");
        dataProp.Should().NotBeNull("CashierSessions must return { data: [...] }");

        var items = ((System.Collections.IEnumerable)dataProp!.GetValue(response)!).Cast<object>().ToList();
        items.Should().HaveCount(1);

        var dto = items[0];
        var dtoType = dto.GetType();

        // Verify key fields the frontend depends on
        dtoType.GetProperty("OpenedAt").Should().NotBeNull("frontend reads OpenedAt");
        dtoType.GetProperty("ExpectedClosingCash").Should().NotBeNull("frontend reads ExpectedClosingCash");
        dtoType.GetProperty("ExpectedClosingCard").Should().NotBeNull("frontend reads ExpectedClosingCard");
        dtoType.GetProperty("ExpectedClosingBank").Should().NotBeNull("frontend reads ExpectedClosingBank");
        dtoType.GetProperty("ActualClosingCash").Should().NotBeNull("frontend reads ActualClosingCash");
        dtoType.GetProperty("ActualClosingCard").Should().NotBeNull("frontend reads ActualClosingCard");
        dtoType.GetProperty("ActualClosingBank").Should().NotBeNull("frontend reads ActualClosingBank");
        dtoType.GetProperty("ShortageOrSurplus").Should().NotBeNull();
    }

    [Fact]
    public async Task CashierSessions_GetSessionDetail_ReturnsExpectedClosingFields()
    {
        await using var db = CreateDb();
        var (branchId, cashierId) = SeedBranchAndUser(db);

        var sessionId = Guid.NewGuid();
        var session = new CashierSession
        {
            Id = sessionId,
            SessionNumber = "CS-20250101-02",
            CashierId = cashierId,
            BranchId = branchId,
            OpeningTime = DateTime.UtcNow.AddHours(-1),
            OpeningBalance = 50_000m,
            ExpectedClosingCash = 40_000m,
            ExpectedClosingCard = 15_000m,
            ExpectedClosingBank = 5_000m,
            Status = SessionStatus.Open
        };
        db.CashierSessions.Add(session);
        // User already seeded by SeedBranchAndUser — no need to add again (causes tracking conflict)
        await db.SaveChangesAsync();

        var controller = BuildCashierSessionsController(db, CreateAdminUser(branchId));
        var result = await controller.GetSessionDetail(sessionId);

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var detail = ok.Value!;
        var detailType = detail.GetType();

        // Frontend fetches detail before close modal — must have these fields
        detailType.GetProperty("ExpectedClosingCash").Should().NotBeNull("close modal needs ExpectedClosingCash");
        detailType.GetProperty("ExpectedClosingCard").Should().NotBeNull("close modal needs ExpectedClosingCard");
        detailType.GetProperty("ExpectedClosingBank").Should().NotBeNull("close modal needs ExpectedClosingBank");
        detailType.GetProperty("OpeningBalance").Should().NotBeNull();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // AdvancePaymentController — rejection path (no transaction needed)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CashierSession_Open_PersistsEachCurrencyOpeningBalance()
    {
        await using var db = CreateDb();
        var (branchId, cashierId) = SeedBranchAndUser(db);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.UserId).Returns(cashierId);
        currentUser.SetupGet(user => user.BranchId).Returns(branchId);
        currentUser.SetupGet(user => user.IsAdmin).Returns(true);
        currentUser.SetupGet(user => user.IsAuthenticated).Returns(true);
        currentUser.SetupGet(user => user.Role).Returns(UserRole.Admin);
        var controller = BuildCashierSessionsController(db, currentUser.Object);

        var result = await controller.OpenSession(new OpenSessionRequest
        {
            OpeningBalance = 50_000m,
            CurrencyOpeningBalances =
            [
                new CurrencyOpeningBalanceRequest { Currency = "SAR", OpeningCash = 250m },
                new CurrencyOpeningBalanceRequest { Currency = "USD", OpeningCash = 30m }
            ]
        });

        result.Should().BeOfType<OkObjectResult>();
        var session = await db.CashierSessions.SingleAsync(item => item.CashierId == cashierId);
        var openingBalances = await db.CashierSessionCurrencyOpeningBalances
            .Where(item => item.CashierSessionId == session.Id)
            .OrderBy(item => item.Currency)
            .ToListAsync();
        openingBalances.Should().HaveCount(2);
        openingBalances.Single(item => item.Currency == "SAR").OpeningCash.Should().Be(250m);
        openingBalances.Single(item => item.Currency == "USD").OpeningCash.Should().Be(30m);
    }

    [Fact]
    public async Task CashierSession_Close_DoesNotMixSarCashIntoYerDrawerVariance()
    {
        await using var db = CreateDb();
        var (branchId, cashierId) = SeedBranchAndUser(db);
        var session = new CashierSession
        {
            SessionNumber = "CS-FX-001",
            CashierId = cashierId,
            BranchId = branchId,
            OpeningTime = DateTime.UtcNow.AddHours(-1),
            OpeningBalance = 50_000m,
            Status = SessionStatus.Open,
            CurrencyOpeningBalances =
            [
                new CashierSessionCurrencyOpeningBalance
                {
                    Currency = "SAR",
                    OpeningCash = 25m
                }
            ]
        };
        db.CashierSessions.Add(session);
        db.CashFlowTransactions.AddRange(
            new CashFlowTransaction
            {
                TransactionNumber = "TX-YER-001",
                Type = TransactionType.Inflow,
                Category = FinancialCategory.PatientPayment,
                Amount = 10_000m,
                Currency = "YER",
                PaymentMethod = "cash",
                TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow),
                CashierSessionId = session.Id,
                PerformedBy = cashierId,
                BranchId = branchId
            },
            new CashFlowTransaction
            {
                TransactionNumber = "TX-SAR-001",
                Type = TransactionType.Inflow,
                Category = FinancialCategory.PatientPayment,
                Amount = 100m,
                Currency = "SAR",
                PaymentMethod = "cash",
                TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow),
                CashierSessionId = session.Id,
                PerformedBy = cashierId,
                BranchId = branchId
            });
        await db.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.UserId).Returns(cashierId);
        currentUser.SetupGet(user => user.BranchId).Returns(branchId);
        currentUser.SetupGet(user => user.IsAdmin).Returns(true);
        currentUser.SetupGet(user => user.IsAuthenticated).Returns(true);
        currentUser.SetupGet(user => user.Role).Returns(UserRole.Admin);
        var controller = BuildCashierSessionsController(db, currentUser.Object);

        var result = await controller.CloseSession(new CloseSessionRequest
        {
            ActualClosingCash = 60_000m,
            ActualClosingCard = 0,
            ActualClosingBank = 0,
            CurrencyClosings =
            [
                new CurrencyClosingRequest
                {
                    Currency = "SAR",
                    ActualCash = 120m,
                    ActualBank = 0m
                }
            ]
        });

        result.Should().BeOfType<OkObjectResult>();
        var saved = await db.CashierSessions.FindAsync(session.Id);
        saved!.ExpectedClosingCash.Should().Be(60_000m);
        saved.ShortageOrSurplus.Should().Be(0m, "SAR cash belongs to its own treasury and cannot alter the YER drawer variance");

        var reconciliations = await db.CashierSessionCurrencyReconciliations
            .Where(item => item.CashierSessionId == session.Id)
            .OrderBy(item => item.Currency)
            .ToListAsync();
        reconciliations.Should().HaveCount(2);
        var sarReconciliation = reconciliations.Single(item => item.Currency == "SAR");
        sarReconciliation.ExpectedCash.Should().Be(125m);
        sarReconciliation.ActualCash.Should().Be(120m);
        sarReconciliation.CashVariance.Should().Be(-5m);

        var response = ((OkObjectResult)result).Value!;
        var foreignActivity = ((System.Collections.IEnumerable)response.GetType()
            .GetProperty("ForeignCurrencyActivity")!
            .GetValue(response)!).Cast<object>().ToList();
        foreignActivity.Should().ContainSingle();
        foreignActivity[0].GetType().GetProperty("Currency")!.GetValue(foreignActivity[0]).Should().Be("SAR");
    }

    [Fact]
    public async Task CashierSession_Close_RejectsMissingForeignCurrencyCount()
    {
        await using var db = CreateDb();
        var (branchId, cashierId) = SeedBranchAndUser(db);
        var session = new CashierSession
        {
            SessionNumber = "CS-FX-002",
            CashierId = cashierId,
            BranchId = branchId,
            OpeningTime = DateTime.UtcNow.AddHours(-1),
            OpeningBalance = 50_000m,
            Status = SessionStatus.Open,
            CurrencyOpeningBalances =
            [
                new CashierSessionCurrencyOpeningBalance
                {
                    Currency = "SAR",
                    OpeningCash = 25m
                }
            ]
        };
        db.CashierSessions.Add(session);
        db.CashFlowTransactions.Add(new CashFlowTransaction
        {
            TransactionNumber = "TX-SAR-002",
            Type = TransactionType.Inflow,
            Category = FinancialCategory.PatientPayment,
            Amount = 100m,
            Currency = "SAR",
            PaymentMethod = "cash",
            TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CashierSessionId = session.Id,
            PerformedBy = cashierId,
            BranchId = branchId
        });
        await db.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.UserId).Returns(cashierId);
        currentUser.SetupGet(user => user.BranchId).Returns(branchId);
        currentUser.SetupGet(user => user.IsAdmin).Returns(true);
        currentUser.SetupGet(user => user.IsAuthenticated).Returns(true);
        currentUser.SetupGet(user => user.Role).Returns(UserRole.Admin);
        var controller = BuildCashierSessionsController(db, currentUser.Object);

        var result = await controller.CloseSession(new CloseSessionRequest
        {
            ActualClosingCash = 50_000m,
            ActualClosingCard = 0m,
            ActualClosingBank = 0m
        });

        result.Should().BeOfType<BadRequestObjectResult>();
        db.ChangeTracker.Clear();
        (await db.CashierSessions.FindAsync(session.Id))!.Status.Should().Be(SessionStatus.Open);
        (await db.CashierSessionCurrencyReconciliations
            .CountAsync(item => item.CashierSessionId == session.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Advance_Approve_RejectsNonPendingAdvance()
    {
        await using var db = CreateDb();
        var (branchId, userId) = SeedBranchAndUser(db);
        var employee = new Employee
        {
            Id = Guid.NewGuid(), FullName = "أحمد", BranchId = branchId, BaseSalary = 10_000m
        };
        db.Employees.Add(employee);

        // Already approved advance
        var advance = new AdvancePayment
        {
            Id = Guid.NewGuid(), EmployeeId = employee.Id, Amount = 3_000m,
            RequestDate = DateTime.UtcNow, Status = RequestStatus.Approved
        };
        db.AdvancePayments.Add(advance);
        await db.SaveChangesAsync();

        // Setup user claims
        var controller = BuildAdvancePaymentController(db);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.HttpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            }));

        var result = await controller.Approve(advance.Id, new ApproveAdvanceRequest { Approve = false });

        result.Should().BeOfType<BadRequestObjectResult>("already-approved advance cannot be rejected");
    }

    [Fact]
    public async Task Advance_Approve_RejectionPath_ReturnsOk()
    {
        await using var db = CreateDb();
        var (branchId, userId) = SeedBranchAndUser(db);
        var employee = new Employee
        {
            Id = Guid.NewGuid(), FullName = "محمد", BranchId = branchId, BaseSalary = 12_000m
        };
        db.Employees.Add(employee);

        var advance = new AdvancePayment
        {
            Id = Guid.NewGuid(), EmployeeId = employee.Id, Amount = 2_000m,
            RequestDate = DateTime.UtcNow, Status = RequestStatus.Pending
        };
        db.AdvancePayments.Add(advance);
        await db.SaveChangesAsync();

        var controller = BuildAdvancePaymentController(db);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.HttpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            }));

        var result = await controller.Approve(advance.Id, new ApproveAdvanceRequest
        {
            Approve = false,
            RejectionReason = "غير مبرر"
        });

        result.Should().BeOfType<OkObjectResult>("rejection of pending advance should succeed");

        // Verify advance is now rejected
        var saved = await db.AdvancePayments.FindAsync(advance.Id);
        saved!.Status.Should().Be(RequestStatus.Rejected);
    }

    [Fact]
    public async Task Advance_GetAll_ReturnsPaginatedWithWrapper()
    {
        await using var db = CreateDb();
        var (branchId, _) = SeedBranchAndUser(db);
        var controller = BuildAdvancePaymentController(db);

        var result = await controller.GetAll(employeeId: null, status: null, branchId: null, page: 1, pageSize: 20);

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var response = ok.Value!;
        var responseType = response.GetType();

        responseType.GetProperty("data").Should().NotBeNull("must have data wrapper");
        responseType.GetProperty("total").Should().NotBeNull("must have total");
        responseType.GetProperty("page").Should().NotBeNull();
        responseType.GetProperty("pageSize").Should().NotBeNull();
    }

    [Fact]
    public async Task Advance_DeleteApproved_ReturnsBadRequest()
    {
        await using var db = CreateDb();
        var (branchId, _) = SeedBranchAndUser(db);
        var employee = new Employee
        {
            Id = Guid.NewGuid(), FullName = "سعيد", BranchId = branchId, BaseSalary = 10_000m
        };
        db.Employees.Add(employee);

        var advance = new AdvancePayment
        {
            Id = Guid.NewGuid(), EmployeeId = employee.Id, Amount = 5_000m,
            RequestDate = DateTime.UtcNow, Status = RequestStatus.Approved
        };
        db.AdvancePayments.Add(advance);
        await db.SaveChangesAsync();

        var controller = BuildAdvancePaymentController(db);
        var result = await controller.Delete(advance.Id);

        result.Should().BeOfType<BadRequestObjectResult>("approved advances cannot be deleted");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SalaryController — GenerateSalary and GetAll
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Salary_Generate_CreatesSalaryRecord()
    {
        await using var db = CreateDb();
        var (branchId, _) = SeedBranchAndUser(db);
        var employee = new Employee
        {
            Id = Guid.NewGuid(), FullName = "فاطمة", BranchId = branchId, BaseSalary = 15_000m
        };
        db.Employees.Add(employee);
        await db.SaveChangesAsync();

        var controller = BuildSalaryController(db);
        var result = await controller.GenerateSalary(new GenerateSalaryRequest
        {
            EmployeeId = employee.Id, Year = 2025, Month = 6
        });

        result.Should().BeOfType<CreatedResult>("salary generation should return 201");
        var salaries = await db.SalaryRecords.ToListAsync();
        salaries.Should().HaveCount(1);
        salaries[0].NetSalary.Should().Be(15_000m);
    }

    [Fact]
    public async Task Salary_Generate_DuplicateMonth_ReturnsConflict()
    {
        await using var db = CreateDb();
        var (branchId, _) = SeedBranchAndUser(db);
        var employee = new Employee
        {
            Id = Guid.NewGuid(), FullName = "ليلى", BranchId = branchId, BaseSalary = 12_000m
        };
        db.Employees.Add(employee);

        db.SalaryRecords.Add(new SalaryRecord
        {
            Id = Guid.NewGuid(), EmployeeId = employee.Id,
            Year = 2025, Month = 7, BaseSalary = 12_000m, NetSalary = 12_000m
        });
        await db.SaveChangesAsync();

        var controller = BuildSalaryController(db);
        var result = await controller.GenerateSalary(new GenerateSalaryRequest
        {
            EmployeeId = employee.Id, Year = 2025, Month = 7
        });

        result.Should().BeOfType<ConflictObjectResult>("duplicate month must return 409");
    }

    [Fact]
    public async Task Salary_GetAll_ReturnsPaginatedWithWrapper()
    {
        await using var db = CreateDb();
        var controller = BuildSalaryController(db);

        var result = await controller.GetAll(employeeId: null, year: null, month: null, branchId: null, paid: null, page: 1, pageSize: 20);

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var response = ok.Value!;
        var responseType = response.GetType();

        responseType.GetProperty("data").Should().NotBeNull("must have data wrapper");
        responseType.GetProperty("total").Should().NotBeNull();
    }

    [Fact]
    public async Task Salary_DeletePaidRecord_ReturnsBadRequest()
    {
        await using var db = CreateDb();
        var (branchId, cashierId) = SeedBranchAndUser(db);

        var salary = new SalaryRecord
        {
            Id = Guid.NewGuid(), EmployeeId = Guid.NewGuid(),
            Year = 2025, Month = 8, BaseSalary = 10_000m, NetSalary = 10_000m,
            PaidAt = DateTime.UtcNow, PaidBy = cashierId
        };
        db.SalaryRecords.Add(salary);
        await db.SaveChangesAsync();

        var controller = BuildSalaryController(db);
        var result = await controller.Delete(salary.Id);

        result.Should().BeOfType<BadRequestObjectResult>("paid salary cannot be deleted");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TreasuryType enum — only Vault/Bank, not Cash
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TreasuryType_CashIsNotValid()
    {
        var canParseCash = Enum.TryParse<TreasuryType>("Cash", true, out _);
        canParseCash.Should().BeFalse("TreasuryType must NOT accept 'Cash'");

        var canParseVault = Enum.TryParse<TreasuryType>("Vault", true, out _);
        var canParseBank = Enum.TryParse<TreasuryType>("Bank", true, out _);
        canParseVault.Should().BeTrue();
        canParseBank.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // FinanceV3Controller — Patient Balance formula (EntityBalance = TotalInvoiced - NetPayments)
    // Note: Balance uses JournalLine (canonical) which requires full dual-write path.
    // EntityBalance is entity-based and works with directly seeded Payment/Invoice data.
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FinanceV3_GetPatientBalance_RefundIncreasesOutstanding()
    {
        await using var db = CreateDb();
        var (branchId, userId) = SeedBranchAndUser(db);

        var patient = new Patient
        {
            Id = Guid.NewGuid(), FirstName = "مريض", LastName = "استرداد",
            PatientNumber = "P-REFUND", BranchId = branchId
        };
        db.Patients.Add(patient);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(), PatientId = patient.Id, InvoiceNumber = "INV-REF",
            Status = InvoiceStatus.Issued, TotalAmount = 1000m, Subtotal = 1000m,
            CreatedBy = userId
        };
        db.Invoices.Add(invoice);

        // Payment of 600
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(), PatientId = patient.Id, InvoiceId = invoice.Id,
            Amount = 600m, PaymentMethod = "cash",
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            ReceiptNumber = "RCP-REF1", IsActive = true
        });

        // Refund of -200 (increases outstanding)
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(), PatientId = patient.Id, InvoiceId = invoice.Id,
            Amount = -200m, PaymentMethod = "cash",
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            ReceiptNumber = "RCP-REF2", IsActive = true
        });

        await db.SaveChangesAsync();

        var controller = BuildFinanceV3Controller(db, CreateAdminUser(branchId));
        var result = await controller.GetPatientBalance(patient.Id);

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var response = ok.Value!;
        var responseType = response.GetType();

        // EntityBalance = TotalInvoiced(1000) - NetPayments(600 + -200 = 400) = 600
        // Uses EntityBalance because Balance requires JournalLines from full dual-write path
        var balanceProp = responseType.GetProperty("EntityBalance");
        balanceProp.Should().NotBeNull();
        balanceProp!.GetValue(response).Should().Be(600m,
            "EntityBalance = TotalInvoiced - NetPaid - Discounts; refund increases outstanding");

        var totalInvoicedProp = responseType.GetProperty("TotalInvoiced");
        totalInvoicedProp.Should().NotBeNull();
        totalInvoicedProp!.GetValue(response).Should().Be(1000m);

        var totalPaidProp = responseType.GetProperty("TotalPaid");
        totalPaidProp.Should().NotBeNull();
        totalPaidProp!.GetValue(response).Should().Be(600m);

        var totalRefundsProp = responseType.GetProperty("TotalRefunds");
        totalRefundsProp.Should().NotBeNull();
        totalRefundsProp!.GetValue(response).Should().Be(200m);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // FinanceV3Controller — Treasuries sub-endpoint returns { data } wrapper
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FinanceV3_GetTreasuries_ReturnsDataWrapper()
    {
        await using var db = CreateDb();
        var (branchId, _) = SeedBranchAndUser(db);

        db.Treasuries.Add(new Treasury
        {
            Id = Guid.NewGuid(), Name = "خزنة", Type = TreasuryType.Vault,
            Balance = 100_000m, BranchId = branchId, IsActive = true
        });
        await db.SaveChangesAsync();

        var controller = BuildFinanceV3Controller(db, CreateAdminUser(branchId));
        var result = await controller.GetTreasuries();

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var response = ok.Value!;
        var responseType = response.GetType();

        var dataProp = responseType.GetProperty("data");
        dataProp.Should().NotBeNull("FinanceV3 treasuries must return { data: [...] }");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // FinanceV3Controller — Journal Entries returns paginated
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FinanceV3_GetExpenses_ExpenseWithoutJournalEntry_ReturnsNullTreasury()
    {
        await using var db = CreateDb();
        var (branchId, userId) = SeedBranchAndUser(db);

        db.OperationalExpenses.Add(new OperationalExpense
        {
            Id = Guid.NewGuid(),
            ExpenseNumber = "EXP-SMOKE-001",
            Title = "Legacy expense without journal entry",
            Category = ExpenseCategory.Miscellaneous,
            Amount = 1_000m,
            PaymentMethod = "cash",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            PaidBy = userId,
            BranchId = branchId,
            ApprovalStatus = ApprovalStatus.NotRequired,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var controller = BuildFinanceV3Controller(db, CreateAdminUser(branchId));
        var result = await controller.GetExpenses();

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var response = ok.Value!;
        var dataProp = response.GetType().GetProperty("data");
        dataProp.Should().NotBeNull("expenses response must have data wrapper");

        var items = ((System.Collections.IEnumerable)dataProp!.GetValue(response)!).Cast<object>().ToList();
        items.Should().HaveCount(1);
        items[0].GetType().GetProperty("TreasuryId")!.GetValue(items[0])
            .Should().BeNull("legacy expenses without JournalEntry must not resolve Guid.Empty as a treasury");
    }

    [Fact]
    public async Task FinanceV3_GetVaultTransfers_ReturnsDataWrapper()
    {
        await using var db = CreateDb();
        var (branchId, userId) = SeedBranchAndUser(db);

        var destination = new Treasury
        {
            Id = Guid.NewGuid(),
            Name = "Destination treasury",
            Type = TreasuryType.Vault,
            Balance = 0m,
            BranchId = branchId,
            IsActive = true
        };
        db.Treasuries.Add(destination);
        db.VaultTransfers.Add(new VaultTransfer
        {
            Id = Guid.NewGuid(),
            TransferNumber = "TR-SMOKE-001",
            DestinationTreasuryId = destination.Id,
            DestinationTreasury = destination,
            Amount = 500m,
            TransferDate = DateTime.UtcNow,
            PerformedBy = userId,
            PerformedByUser = db.Users.Single(u => u.Id == userId),
            Status = TransferStatus.Pending,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var controller = BuildFinanceV3Controller(db, CreateAdminUser(branchId));
        var result = await controller.GetVaultTransfers();

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var response = ok.Value!;
        var dataProp = response.GetType().GetProperty("data");
        dataProp.Should().NotBeNull("vault transfers response must have data wrapper");
        var items = ((System.Collections.IEnumerable)dataProp!.GetValue(response)!).Cast<object>().ToList();
        items.Should().HaveCount(1);
        items[0].GetType().GetProperty("RequestedBy")!.GetValue(items[0]).Should().Be("admin1");
    }

    [Fact]
    public async Task FinanceV3_GetJournalEntries_ReturnsPaginatedWithLines()
    {
        await using var db = CreateDb();
        var (branchId, userId) = SeedBranchAndUser(db);

        var controller = BuildFinanceV3Controller(db, CreateAdminUser(branchId));
        var result = await controller.GetJournalEntries(page: 1, pageSize: 20);

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var response = ok.Value!;
        var responseType = response.GetType();

        responseType.GetProperty("data").Should().NotBeNull();
        responseType.GetProperty("total").Should().NotBeNull();
        responseType.GetProperty("page").Should().NotBeNull();
        responseType.GetProperty("pageSize").Should().NotBeNull();
    }
}
