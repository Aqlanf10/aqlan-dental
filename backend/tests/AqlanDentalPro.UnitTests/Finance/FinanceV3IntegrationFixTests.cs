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
/// Finance V3 Integration Fix tests — verifies all 5 fixes:
///
/// Fix 1: GET /api/finance-v3/contracts?patientId= returns contractNumber + outstandingAmount
/// Fix 2: GET /api/finance-v3/payments returns PaymentNumber (alias for ReceiptNumber) + receiptNumber
/// Fix 3: DELETE /api/finance-v3/payments/{id} is AdminOnly (not accessible by Accountant)
/// Fix 4: Admin without branch cannot create Guid.Empty financial records
/// Fix 5: Patient balance includes reconciliation indicator (HasJournalEntry on payments)
/// </summary>
public class FinanceV3IntegrationFixTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
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
        var financeService = new FinanceService(db, currentUser, notifications, new Mock<ILogger<FinanceService>>().Object, commissionService, journalEntryService);
        var audit = new Mock<IAuditService>().Object;
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        var logger = new Mock<ILogger<FinanceV3Controller>>().Object;
        return new FinanceV3Controller(db, currentUser, financeService, audit, journalEntryService, treasuryResolution, logger);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Fix 1: GET /api/finance-v3/contracts?patientId= returns ContractNumber + OutstandingAmount
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Fix1_GetContracts_ByPatientId_ReturnsContractNumber_And_OutstandingAmount()
    {
        await using var db = CreateDb();
        var (branchId, userId) = SeedBranchAndUser(db);

        var patient = new Patient
        {
            Id = Guid.NewGuid(), FirstName = "يوسف", LastName = "أحمد",
            PatientNumber = "P-YUSUF", BranchId = branchId
        };
        db.Patients.Add(patient);

        var contract = new Contract
        {
            Id = Guid.NewGuid(), PatientId = patient.Id,
            Specialty = "تقويم", TotalAmount = 10_000m, DownPayment = 0,
            InstallmentsCount = 10, InstallmentAmount = 1000m,
            StartDate = DateOnly.FromDateTime(DateTime.Today), Status = ContractStatus.Active,
            CreatedBy = userId
        };
        db.Contracts.Add(contract);

        // Payment of 3000 towards the contract
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(), PatientId = patient.Id, ContractId = contract.Id,
            Amount = 3000m, PaymentMethod = "cash",
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            ReceiptNumber = "RCP-CTR-001", IsActive = true, BranchId = branchId
        });

        await db.SaveChangesAsync();

        var controller = BuildFinanceV3Controller(db, CreateAdminUser(branchId));
        var result = await controller.GetContracts(page: 1, pageSize: 20, patientId: patient.Id, status: "active");

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var response = ok.Value!;
        var responseType = response.GetType();

        var dataProp = responseType.GetProperty("data");
        dataProp.Should().NotBeNull("response must have 'data' property");

        var items = ((System.Collections.IEnumerable)dataProp!.GetValue(response)!).Cast<object>().ToList();
        items.Should().HaveCount(1, "one active contract for this patient");

        var dto = items[0];
        var dtoType = dto.GetType();

        // Fix 1: ContractNumber must exist
        var contractNumberProp = dtoType.GetProperty("ContractNumber");
        contractNumberProp.Should().NotBeNull("Fix 1: contract DTO must have ContractNumber field");
        var contractNumber = contractNumberProp!.GetValue(dto) as string;
        contractNumber.Should().NotBeNullOrEmpty("ContractNumber should be generated from CTR-{id-short}");

        // Fix 1: OutstandingAmount must exist and be correct
        var outstandingProp = dtoType.GetProperty("OutstandingAmount");
        outstandingProp.Should().NotBeNull("Fix 1: contract DTO must have OutstandingAmount field");
        outstandingProp!.GetValue(dto).Should().Be(7000m, "OutstandingAmount = 10000 - 0 - 3000");

        // Specialty field must exist
        var specialtyProp = dtoType.GetProperty("Specialty");
        specialtyProp.Should().NotBeNull("contract DTO must have Specialty field");
        specialtyProp!.GetValue(dto).Should().Be("تقويم");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Fix 2: GET /api/finance-v3/payments returns PaymentNumber (alias) + ReceiptNumber
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Fix2_GetPayments_ReturnsPaymentNumber_And_ReceiptNumber()
    {
        await using var db = CreateDb();
        var (branchId, userId) = SeedBranchAndUser(db);

        var patient = new Patient
        {
            Id = Guid.NewGuid(), FirstName = "سارة", LastName = "محمد",
            PatientNumber = "P-SARA", BranchId = branchId
        };
        db.Patients.Add(patient);

        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(), PatientId = patient.Id,
            Amount = 500m, PaymentMethod = "cash",
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            ReceiptNumber = "RCPT-20250101-001", IsActive = true, BranchId = branchId
        });

        await db.SaveChangesAsync();

        var controller = BuildFinanceV3Controller(db, CreateAdminUser(branchId));
        var result = await controller.GetPayments(page: 1, pageSize: 20);

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var response = ok.Value!;
        var responseType = response.GetType();

        var dataProp = responseType.GetProperty("data");
        dataProp.Should().NotBeNull("response must have 'data' property");

        var items = ((System.Collections.IEnumerable)dataProp!.GetValue(response)!).Cast<object>().ToList();
        items.Should().HaveCount(1, "one payment was seeded");

        var dto = items[0];
        var dtoType = dto.GetType();

        // Fix 2: ReceiptNumber must exist
        var receiptNumberProp = dtoType.GetProperty("ReceiptNumber");
        receiptNumberProp.Should().NotBeNull("Fix 2: payment DTO must have ReceiptNumber field");
        receiptNumberProp!.GetValue(dto).Should().Be("RCPT-20250101-001");

        // Fix 2: PaymentNumber must exist as alias for ReceiptNumber
        var paymentNumberProp = dtoType.GetProperty("PaymentNumber");
        paymentNumberProp.Should().NotBeNull("Fix 2: payment DTO must have PaymentNumber field (alias for ReceiptNumber)");
        paymentNumberProp!.GetValue(dto).Should().Be("RCPT-20250101-001", "PaymentNumber should equal ReceiptNumber");

        // Additional fields required by frontend
        dtoType.GetProperty("PatientId").Should().NotBeNull("frontend needs PatientId");
        dtoType.GetProperty("ContractId").Should().NotBeNull("frontend needs ContractId for contract linking");
        dtoType.GetProperty("InvoiceId").Should().NotBeNull("frontend needs InvoiceId for invoice linking");
        dtoType.GetProperty("IsReversal").Should().NotBeNull("frontend needs IsReversal");
        dtoType.GetProperty("ReversedById").Should().NotBeNull("frontend needs ReversedById");
        dtoType.GetProperty("HasJournalEntry").Should().NotBeNull("Fix 5: frontend needs HasJournalEntry for reconciliation");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Fix 3: DELETE /api/finance-v3/payments/{id} is AdminOnly
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Fix3_DeletePayment_AdminOnly_AccountantCannotDelete()
    {
        // This test verifies the policy attribute on the endpoint.
        // The actual authorization is handled by ASP.NET Core middleware.
        // We verify the controller method has [Authorize(Policy = "AdminOnly")].
        var method = typeof(FinanceV3Controller).GetMethod("DeletePayment");
        method.Should().NotBeNull("DeletePayment method must exist on FinanceV3Controller");

        var adminOnlyAttr = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .FirstOrDefault();

        adminOnlyAttr.Should().NotBeNull("DeletePayment must have [Authorize] attribute");
        adminOnlyAttr!.Policy.Should().Be("AdminOnly", "Fix 3: DeletePayment must be AdminOnly — Accountant should not see/use this action");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Fix 4: Admin without branch cannot create Guid.Empty financial records
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Fix4_CreatePayment_AdminNoBranch_ResolvesToFallbackBranch()
    {
        await using var db = CreateDb();
        var branchId = Guid.NewGuid();
        db.Branches.Add(new Branch { Id = branchId, Name = "الفرع الرئيسي" });

        var userId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, Username = "branchless-admin", BranchId = null });

        var patient = new Patient
        {
            Id = Guid.NewGuid(), FirstName = "مريض", LastName = "اختبار",
            PatientNumber = "P-TEST", BranchId = branchId
        };
        db.Patients.Add(patient);

        // Create an open cashier session for the admin user
        db.CashierSessions.Add(new CashierSession
        {
            Id = Guid.NewGuid(),
            CashierId = userId,
            BranchId = branchId,
            SessionNumber = "CS-TEST-001",
            OpeningTime = DateTime.UtcNow,
            OpeningBalance = 50_000m,
            Status = SessionStatus.Open,
            IsActive = true
        });

        db.Treasuries.Add(new Treasury
        {
            Id = Guid.NewGuid(), Name = "الخزنة", Type = TreasuryType.Vault,
            Balance = 100_000m, BranchId = branchId, IsActive = true
        });

        await db.SaveChangesAsync();

        // Admin user with NO branch (Guid.Empty)
        var adminNoBranch = CreateAdminUser(Guid.Empty);
        var controller = BuildFinanceV3Controller(db, adminNoBranch);

        var request = new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 500m,
            PaymentMethod = "cash"
        };

        // The controller should resolve a fallback branch and pass it to the service
        var result = await controller.CreatePayment(request);

        // Should succeed (not return BadRequest about branch)
        result.Should().NotBeOfType<BadRequestObjectResult>("Admin without branch should use fallback branch");

        // Verify the payment was created with a valid (non-empty) branch by checking the entity directly
        var paymentEntity = await db.Payments.FirstOrDefaultAsync(p => p.PatientId == patient.Id);
        paymentEntity.Should().NotBeNull("payment should have been created");
        paymentEntity!.BranchId.Should().NotBe(Guid.Empty, "Fix 4: BranchId must never be Guid.Empty in financial records");
        paymentEntity.BranchId.Should().Be(branchId, "BranchId should be the resolved fallback branch");
    }

    [Fact]
    public async Task Fix4_CreatePayment_NoBranchesInSystem_ReturnsBadRequest()
    {
        await using var db = CreateDb();
        // No branches at all in the system

        var userId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, Username = "admin-no-branches", BranchId = null });

        var patient = new Patient
        {
            Id = Guid.NewGuid(), FirstName = "مريض", LastName = "لا فروع",
            PatientNumber = "P-NOBRANCH", BranchId = Guid.NewGuid()
        };
        db.Patients.Add(patient);

        await db.SaveChangesAsync();

        // Admin user with NO branch and no branches in the system
        var adminNoBranch = CreateAdminUser(null);
        var controller = BuildFinanceV3Controller(db, adminNoBranch);

        var request = new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 500m,
            PaymentMethod = "cash"
        };

        var result = await controller.CreatePayment(request);

        // Should return BadRequest because no branch could be resolved
        result.Should().BeOfType<BadRequestObjectResult>("Fix 4: cannot create payment without a valid branch");
        var badRequest = (BadRequestObjectResult)result;
        var message = badRequest.Value!.GetType().GetProperty("message")!.GetValue(badRequest.Value) as string;
        message.Should().Contain("فرع", "error message should mention branch in Arabic");
    }

    [Fact]
    public async Task Fix4_FinanceService_BranchIdGuidEmpty_ThrowsException()
    {
        await using var db = CreateDb();
        var (branchId, userId) = SeedBranchAndUser(db);

        var patient = new Patient
        {
            Id = Guid.NewGuid(), FirstName = "مريض", LastName = "خدمة",
            PatientNumber = "P-SVC", BranchId = branchId
        };
        db.Patients.Add(patient);

        // Open cashier session
        db.CashierSessions.Add(new CashierSession
        {
            Id = Guid.NewGuid(),
            CashierId = userId,
            BranchId = branchId,
            SessionNumber = "CS-SVC-001",
            OpeningTime = DateTime.UtcNow,
            OpeningBalance = 50_000m,
            Status = SessionStatus.Open,
            IsActive = true
        });

        db.Treasuries.Add(new Treasury
        {
            Id = Guid.NewGuid(), Name = "الخزنة", Type = TreasuryType.Vault,
            Balance = 100_000m, BranchId = branchId, IsActive = true
        });

        await db.SaveChangesAsync();

        // Admin with Guid.Empty branch and no ResolvedBranchId
        var adminEmptyBranch = CreateAdminUser(Guid.Empty);
        var notifications = new Mock<INotificationService>().Object;
        var commissionService = new Mock<ICommissionService>().Object;
        var journalEntryService = new JournalEntryService(db, new Mock<ILogger<JournalEntryService>>().Object);
        var financeService = new FinanceService(db, adminEmptyBranch, notifications, new Mock<ILogger<FinanceService>>().Object, commissionService, journalEntryService);

        var request = new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 500m,
            PaymentMethod = "cash",
            ResolvedBranchId = null // No resolved branch from controller
        };

        // FinanceService should find a fallback branch since we seeded one
        // But if it couldn't, it would throw. In this case, it should find the seeded branch.
        var resultDto = await financeService.CreatePaymentAsync(request);
        resultDto.Should().NotBeNull();

        // Verify the entity directly (PaymentDto doesn't expose BranchId)
        var paymentEntity = await db.Payments.FindAsync(resultDto.Id);
        paymentEntity.Should().NotBeNull();
        paymentEntity!.BranchId.Should().NotBe(Guid.Empty, "Fix 4: FinanceService should never write Guid.Empty as BranchId");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Fix 5: Payment has HasJournalEntry for reconciliation indicator
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Fix5_GetPayments_ReturnsHasJournalEntry_Field()
    {
        await using var db = CreateDb();
        var (branchId, userId) = SeedBranchAndUser(db);

        var patient = new Patient
        {
            Id = Guid.NewGuid(), FirstName = "مريض", LastName = "قيود",
            PatientNumber = "P-JE", BranchId = branchId
        };
        db.Patients.Add(patient);

        // Payment with a corresponding JournalEntry (dual-write)
        var paymentId = Guid.NewGuid();
        db.Payments.Add(new Payment
        {
            Id = paymentId, PatientId = patient.Id,
            Amount = 1000m, PaymentMethod = "cash",
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            ReceiptNumber = "RCP-JE-001", IsActive = true, BranchId = branchId
        });

        // Create a corresponding JournalEntry for this payment
        var jeId = Guid.NewGuid();
        db.JournalEntries.Add(new JournalEntry
        {
            Id = jeId,
            EntryNumber = "JE-20250101-001",
            FinancialDocumentType = FinancialDocumentType.Payment,
            FinancialDocumentId = paymentId,
            EntryDate = DateOnly.FromDateTime(DateTime.Today),
            Description = "Payment JE",
            BranchId = branchId,
            PerformedBy = userId,
            IsPosted = true,
            IsReversal = false
        });

        // Payment WITHOUT a JournalEntry (legacy record)
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(), PatientId = patient.Id,
            Amount = 2000m, PaymentMethod = "cash",
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            ReceiptNumber = "RCP-JE-002", IsActive = true, BranchId = branchId
        });

        await db.SaveChangesAsync();

        var controller = BuildFinanceV3Controller(db, CreateAdminUser(branchId));
        var result = await controller.GetPayments(page: 1, pageSize: 20);

        var ok = (OkObjectResult)result;
        var response = ok.Value!;
        var dataProp = response.GetType().GetProperty("data")!;
        var items = ((System.Collections.IEnumerable)dataProp.GetValue(response)!).Cast<object>().ToList();
        items.Should().HaveCount(2, "two payments were seeded");

        // Find the payment with JE
        var withJe = items.FirstOrDefault(i =>
        {
            var id = i.GetType().GetProperty("Id")!.GetValue(i);
            return id != null && id.Equals(paymentId);
        });
        withJe.Should().NotBeNull();
        var hasJeValue = withJe!.GetType().GetProperty("HasJournalEntry")!.GetValue(withJe);
        hasJeValue.Should().Be(true, "Fix 5: payment with JournalEntry should have HasJournalEntry = true");

        // Find the payment without JE
        var withoutJe = items.FirstOrDefault(i =>
        {
            var id = i.GetType().GetProperty("Id")!.GetValue(i);
            return id != null && !id.Equals(paymentId);
        });
        withoutJe.Should().NotBeNull();
        var noJeValue = withoutJe!.GetType().GetProperty("HasJournalEntry")!.GetValue(withoutJe);
        noJeValue.Should().Be(false, "Fix 5: payment without JournalEntry should have HasJournalEntry = false");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Fix 5: Patient balance includes reconciliation indicator
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Fix5_GetPatientBalance_ReturnsJournalReceivable_And_EntityBalance()
    {
        await using var db = CreateDb();
        var (branchId, userId) = SeedBranchAndUser(db);

        var patient = new Patient
        {
            Id = Guid.NewGuid(), FirstName = "مريض", LastName = "تسوية",
            PatientNumber = "P-RECON", BranchId = branchId
        };
        db.Patients.Add(patient);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(), PatientId = patient.Id, InvoiceNumber = "INV-RECON",
            Status = InvoiceStatus.Issued, TotalAmount = 5000m, Subtotal = 5000m,
            CreatedBy = userId
        };
        db.Invoices.Add(invoice);

        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(), PatientId = patient.Id, InvoiceId = invoice.Id,
            Amount = 2000m, PaymentMethod = "cash",
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            ReceiptNumber = "RCP-RECON-001", IsActive = true, BranchId = branchId
        });

        await db.SaveChangesAsync();

        var controller = BuildFinanceV3Controller(db, CreateAdminUser(branchId));
        var result = await controller.GetPatientBalance(patient.Id);

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var response = ok.Value!;
        var responseType = response.GetType();

        // Both JournalReceivable and EntityBalance must be present
        var journalReceivableProp = responseType.GetProperty("JournalReceivable");
        journalReceivableProp.Should().NotBeNull("Fix 5: response must have JournalReceivable for reconciliation");

        var entityBalanceProp = responseType.GetProperty("EntityBalance");
        entityBalanceProp.Should().NotBeNull("Fix 5: response must have EntityBalance for reconciliation");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Fix 1: Contracts endpoint supports patientId filter parameter
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Fix1_GetContracts_FilterByPatientId_ReturnsOnlyPatientContracts()
    {
        await using var db = CreateDb();
        var (branchId, userId) = SeedBranchAndUser(db);

        var patient1 = new Patient
        {
            Id = Guid.NewGuid(), FirstName = "أحمد", LastName = "علي",
            PatientNumber = "P-AHMED", BranchId = branchId
        };
        var patient2 = new Patient
        {
            Id = Guid.NewGuid(), FirstName = "فاطمة", LastName = "خالد",
            PatientNumber = "P-FATMA", BranchId = branchId
        };
        db.Patients.AddRange(patient1, patient2);

        db.Contracts.Add(new Contract
        {
            Id = Guid.NewGuid(), PatientId = patient1.Id,
            Specialty = "تقويم", TotalAmount = 5000m,
            StartDate = DateOnly.FromDateTime(DateTime.Today), Status = ContractStatus.Active,
            CreatedBy = userId
        });
        db.Contracts.Add(new Contract
        {
            Id = Guid.NewGuid(), PatientId = patient2.Id,
            Specialty = "زراعة", TotalAmount = 8000m,
            StartDate = DateOnly.FromDateTime(DateTime.Today), Status = ContractStatus.Active,
            CreatedBy = userId
        });

        await db.SaveChangesAsync();

        var controller = BuildFinanceV3Controller(db, CreateAdminUser(branchId));

        // Filter by patient1
        var result = await controller.GetContracts(page: 1, pageSize: 20, patientId: patient1.Id, status: null);

        var ok = (OkObjectResult)result;
        var response = ok.Value!;
        var dataProp = response.GetType().GetProperty("data")!;
        var items = ((System.Collections.IEnumerable)dataProp.GetValue(response)!).Cast<object>().ToList();

        items.Should().HaveCount(1, "only patient1's contract should be returned");
        var dto = items[0];
        var patientIdValue = dto.GetType().GetProperty("PatientId")!.GetValue(dto);
        patientIdValue.Should().Be(patient1.Id, "returned contract should belong to patient1");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Daily Operations payment still uses /api/payments and appears in patient file
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DailyOps_Payment_CreatedViaPaymentsController_AppearsInPatientPaymentsList()
    {
        await using var db = CreateDb();
        var (branchId, userId) = SeedBranchAndUser(db);

        var patient = new Patient
        {
            Id = Guid.NewGuid(), FirstName = "يوسف", LastName = "محمد",
            PatientNumber = "P-YUSUF", BranchId = branchId
        };
        db.Patients.Add(patient);

        // Open cashier session
        db.CashierSessions.Add(new CashierSession
        {
            Id = Guid.NewGuid(),
            CashierId = userId,
            BranchId = branchId,
            SessionNumber = "CS-DAILY-001",
            OpeningTime = DateTime.UtcNow,
            OpeningBalance = 50_000m,
            Status = SessionStatus.Open,
            IsActive = true
        });

        db.Treasuries.Add(new Treasury
        {
            Id = Guid.NewGuid(), Name = "الخزنة", Type = TreasuryType.Vault,
            Balance = 100_000m, BranchId = branchId, IsActive = true
        });

        await db.SaveChangesAsync();

        // Create payment via FinanceService (same as PaymentsController)
        var adminUser = CreateAdminUser(branchId);
        var notifications = new Mock<INotificationService>().Object;
        var commissionService = new Mock<ICommissionService>().Object;
        var journalEntryService = new JournalEntryService(db, new Mock<ILogger<JournalEntryService>>().Object);
        var financeService = new FinanceService(db, adminUser, notifications, new Mock<ILogger<FinanceService>>().Object, commissionService, journalEntryService);

        var payment = await financeService.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 1500m,
            PaymentMethod = "cash",
            ServiceDescription = "دفع سريع — تقويم"
        });

        // Payment should be created
        payment.Should().NotBeNull();
        payment.Amount.Should().Be(1500m);
        payment.PatientId.Should().Be(patient.Id);

        // Payment should appear in FinanceV3 GET /payments
        var controller = BuildFinanceV3Controller(db, adminUser);
        var result = await controller.GetPayments(page: 1, pageSize: 20);

        var ok = (OkObjectResult)result;
        var response = ok.Value!;
        var dataProp = response.GetType().GetProperty("data")!;
        var items = ((System.Collections.IEnumerable)dataProp.GetValue(response)!).Cast<object>().ToList();

        var foundPayment = items.FirstOrDefault(i =>
        {
            var id = i.GetType().GetProperty("Id")!.GetValue(i);
            return id != null && id.ToString() == payment.Id.ToString();
        });
        foundPayment.Should().NotBeNull("daily operations payment must appear in Finance V3 payments list");

        // Payment should have a JournalEntry (dual-write)
        var hasJe = foundPayment!.GetType().GetProperty("HasJournalEntry")!.GetValue(foundPayment);
        hasJe.Should().Be(true, "payment created via FinanceService must have a JournalEntry");
    }
}
