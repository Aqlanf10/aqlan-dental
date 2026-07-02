using System.Reflection;
using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.API.Middleware;
using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Xunit;

namespace AqlanDentalPro.UnitTests;

/// <summary>
/// Tests for the chore/system-technical-debt-cleanup branch changes:
///   1. ErrorHandlingMiddleware: SanitizeArgumentMessage strips parameter names
///   2. FinanceV3Controller: ResolveBranchIdAsync throws for non-admin without branch
///   3. TreasuriesController: Branch guard on Create and GetAll
///   4. CashierSessionsController: Proper branch filtering for non-admin without branch
///   5. FinanceV3SuppliersController: Branch guard on CreateCreditNote; FinanceWrite on write endpoints
///   6. Unique constraint violation detection uses Npgsql.PostgresException.SqlState
/// </summary>
public class TechnicalDebtCleanupTests
{
    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ICurrentUserService CreateNonAdminUserWithoutBranch()
    {
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(u => u.UserId).Returns(Guid.NewGuid());
        mock.Setup(u => u.Role).Returns(UserRole.Accountant);
        mock.Setup(u => u.IsAdmin).Returns(false);
        mock.Setup(u => u.IsAuthenticated).Returns(true);
        mock.Setup(u => u.BranchId).Returns((Guid?)null);
        mock.Setup(u => u.IsImpersonating).Returns(false);
        mock.Setup(u => u.OriginalUserId).Returns((Guid?)null);
        return mock.Object;
    }

    private static ICurrentUserService CreateNonAdminUserWithEmptyBranch()
    {
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(u => u.UserId).Returns(Guid.NewGuid());
        mock.Setup(u => u.Role).Returns(UserRole.Accountant);
        mock.Setup(u => u.IsAdmin).Returns(false);
        mock.Setup(u => u.IsAuthenticated).Returns(true);
        mock.Setup(u => u.BranchId).Returns(Guid.Empty);
        mock.Setup(u => u.IsImpersonating).Returns(false);
        mock.Setup(u => u.OriginalUserId).Returns((Guid?)null);
        return mock.Object;
    }

    private static ICurrentUserService CreateAdminUserWithoutBranch()
    {
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(u => u.UserId).Returns(Guid.NewGuid());
        mock.Setup(u => u.Role).Returns(UserRole.Admin);
        mock.Setup(u => u.IsAdmin).Returns(true);
        mock.Setup(u => u.IsAuthenticated).Returns(true);
        mock.Setup(u => u.BranchId).Returns((Guid?)null);
        mock.Setup(u => u.IsImpersonating).Returns(false);
        mock.Setup(u => u.OriginalUserId).Returns((Guid?)null);
        return mock.Object;
    }

    private static (ErrorHandlingMiddleware middleware, DefaultHttpContext context) CreateMiddleware(
        RequestDelegate next, bool isDevelopment = true)
    {
        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.EnvironmentName).Returns(isDevelopment ? "Development" : "Production");

        var loggerMock = new Mock<ILogger<ErrorHandlingMiddleware>>();

        var services = new ServiceCollection();
        services.AddSingleton(envMock.Object);
        var sp = services.BuildServiceProvider();

        var context = new DefaultHttpContext
        {
            RequestServices = sp,
            Response = { Body = new MemoryStream() }
        };
        context.Request.Path = "/api/test";

        var middleware = new ErrorHandlingMiddleware(next, loggerMock.Object);
        return (middleware, context);
    }

    private static async Task<ProblemDetails?> ReadResponse(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();
        return System.Text.Json.JsonSerializer.Deserialize<ProblemDetails>(json, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Test 1: ErrorHandlingMiddleware — SanitizeArgumentMessage strips parameter names
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ErrorHandlingMiddleware_SanitizeArgumentMessage_StripsParameterName()
    {
        // Arrange — ArgumentException with "(Parameter 'source')" suffix
        var (middleware, context) = CreateMiddleware(_ =>
            throw new ArgumentException("Value cannot be null. (Parameter 'source')"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert — the "(Parameter 'source')" part must be stripped
        var problem = await ReadResponse(context);
        problem.Should().NotBeNull();
        problem!.Detail.Should().Be("Value cannot be null.",
            "SanitizeArgumentMessage should strip '(Parameter ...)' suffix from ArgumentException messages");
    }

    [Fact]
    public async Task ErrorHandlingMiddleware_SanitizeArgumentMessage_NoParameterName_NoChange()
    {
        // Arrange — ArgumentException without parameter hint
        var (middleware, context) = CreateMiddleware(_ =>
            throw new ArgumentException("Simple message"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert — message remains unchanged
        var problem = await ReadResponse(context);
        problem.Should().NotBeNull();
        problem!.Detail.Should().Be("Simple message",
            "SanitizeArgumentMessage should not alter messages without '(Parameter ...)' suffix");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Test 2: ResolveBranchIdAsync — throws for non-admin without branch
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ResolveBranchIdAsync_NonAdminWithoutBranch_Throws()
    {
        // Arrange — Use reflection to call the private ResolveBranchIdAsync method
        await using var db = CreateDb();
        var nonAdminUser = CreateNonAdminUserWithoutBranch();
        var notifications = new Mock<INotificationService>().Object;
        var commissionService = new Mock<ICommissionService>().Object;
        var journalEntryService = new JournalEntryService(db, new Mock<ILogger<JournalEntryService>>().Object);
        var financeService = new FinanceService(db, nonAdminUser, notifications, new Mock<ILogger<FinanceService>>().Object, commissionService, journalEntryService, new ContractService(db, nonAdminUser));
        var audit = new Mock<IAuditService>().Object;
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        var logger = new Mock<ILogger<FinanceV3Controller>>().Object;

        var controller = new FinanceV3Controller(db, nonAdminUser, financeService, new Mock<IInvoiceLedgerService>().Object, audit, journalEntryService, treasuryResolution, logger);

        // Use reflection to invoke the private method
        var method = typeof(FinanceV3Controller).GetMethod("ResolveBranchIdAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull("ResolveBranchIdAsync must exist on FinanceV3Controller");

        // Act
        var act = () => (Task<Guid>)method!.Invoke(controller, null)!;

        // Assert — should throw InvalidOperationException for non-admin without branch
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*فرع*");
    }

    [Fact]
    public async Task ResolveBranchIdAsync_NonAdminWithEmptyBranch_Throws()
    {
        // Arrange
        await using var db = CreateDb();
        var nonAdminUser = CreateNonAdminUserWithEmptyBranch();
        var notifications = new Mock<INotificationService>().Object;
        var commissionService = new Mock<ICommissionService>().Object;
        var journalEntryService = new JournalEntryService(db, new Mock<ILogger<JournalEntryService>>().Object);
        var financeService = new FinanceService(db, nonAdminUser, notifications, new Mock<ILogger<FinanceService>>().Object, commissionService, journalEntryService, new ContractService(db, nonAdminUser));
        var audit = new Mock<IAuditService>().Object;
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        var logger = new Mock<ILogger<FinanceV3Controller>>().Object;

        var controller = new FinanceV3Controller(db, nonAdminUser, financeService, new Mock<IInvoiceLedgerService>().Object, audit, journalEntryService, treasuryResolution, logger);

        var method = typeof(FinanceV3Controller).GetMethod("ResolveBranchIdAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Act
        var act = () => (Task<Guid>)method!.Invoke(controller, null)!;

        // Assert — should throw InvalidOperationException for non-admin with Guid.Empty branch
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*فرع*");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Test 3: TreasuriesController — Create requires branch
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TreasuriesController_Create_RequiresBranch()
    {
        // Arrange — Admin user with no branch assigned
        await using var db = CreateDb();
        var adminNoBranch = CreateAdminUserWithoutBranch();
        var audit = new Mock<IAuditService>().Object;
        var logger = new Mock<ILogger<TreasuriesController>>().Object;

        var controller = new TreasuriesController(db, adminNoBranch, audit, logger);

        var request = new CreateTreasuryRequest
        {
            Name = "خزنة اختبار",
            Type = "Vault",
            OpeningBalance = 10_000m
        };

        // Act
        var result = await controller.Create(request);

        // Assert — should return BadRequest with Arabic message about branch
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = (BadRequestObjectResult)result;
        var messageProp = badRequest.Value?.GetType().GetProperty("message");
        messageProp.Should().NotBeNull();
        var message = messageProp!.GetValue(badRequest.Value)?.ToString();
        message.Should().Contain("الفرع", "should reject with Arabic message about branch requirement");
    }

    [Fact]
    public async Task TreasuriesController_Create_EmptyBranchId_ReturnsBadRequest()
    {
        // Arrange — Admin user with Guid.Empty branch
        await using var db = CreateDb();
        var mockUser = new Mock<ICurrentUserService>();
        mockUser.Setup(u => u.UserId).Returns(Guid.NewGuid());
        mockUser.Setup(u => u.Role).Returns(UserRole.Admin);
        mockUser.Setup(u => u.IsAdmin).Returns(true);
        mockUser.Setup(u => u.IsAuthenticated).Returns(true);
        mockUser.Setup(u => u.BranchId).Returns(Guid.Empty);
        mockUser.Setup(u => u.IsImpersonating).Returns(false);
        mockUser.Setup(u => u.OriginalUserId).Returns((Guid?)null);

        var audit = new Mock<IAuditService>().Object;
        var logger = new Mock<ILogger<TreasuriesController>>().Object;

        var controller = new TreasuriesController(db, mockUser.Object, audit, logger);

        var request = new CreateTreasuryRequest
        {
            Name = "خزنة اختبار",
            Type = "Bank",
            OpeningBalance = 50_000m
        };

        // Act
        var result = await controller.Create(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Test 4: TreasuriesController — GetAll non-admin without branch returns Forbid
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TreasuriesController_GetAll_NonAdminWithoutBranch_Forbid()
    {
        // Arrange
        await using var db = CreateDb();
        var nonAdminNoBranch = CreateNonAdminUserWithoutBranch();
        var audit = new Mock<IAuditService>().Object;
        var logger = new Mock<ILogger<TreasuriesController>>().Object;

        // FIN-PERM: grant Accountant finance.treasuries.view so the 403 under test comes
        // from the branch guard (no branch assigned), not the granular permission gate.
        db.RolePermissions.Add(new RolePermission { Role = "Accountant", Resource = "finance.treasuries", CanView = true });
        await db.SaveChangesAsync();

        var controller = new TreasuriesController(db, nonAdminNoBranch, audit, logger);

        // Act
        var result = await controller.GetAll();

        // Assert
        result.Should().BeOfType<ObjectResult>("non-admin without branch must be forbidden from listing treasuries").Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task TreasuriesController_GetAll_NonAdminWithEmptyBranch_Forbid()
    {
        // Arrange
        await using var db = CreateDb();
        var nonAdminEmptyBranch = CreateNonAdminUserWithEmptyBranch();
        var audit = new Mock<IAuditService>().Object;
        var logger = new Mock<ILogger<TreasuriesController>>().Object;

        // FIN-PERM: grant Accountant finance.treasuries.view so the 403 under test comes
        // from the branch guard (Guid.Empty branch), not the granular permission gate.
        db.RolePermissions.Add(new RolePermission { Role = "Accountant", Resource = "finance.treasuries", CanView = true });
        await db.SaveChangesAsync();

        var controller = new TreasuriesController(db, nonAdminEmptyBranch, audit, logger);

        // Act
        var result = await controller.GetAll();

        // Assert
        result.Should().BeOfType<ObjectResult>("non-admin with Guid.Empty branch must be forbidden").Which.StatusCode.Should().Be(403);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Test 5: CashierSessionsController — GetAll non-admin without branch Forbid
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CashierSessionsController_GetAll_NonAdminWithoutBranch_Forbid()
    {
        // Arrange
        await using var db = CreateDb();
        var nonAdminNoBranch = CreateNonAdminUserWithoutBranch();
        // FIN-PERM: grant cashier-session view so the 403 under test comes from the
        // branch guard (no branch assigned), not the granular permission gate.
        db.RolePermissions.Add(new RolePermission { Role = "Accountant", Resource = "finance.cashier_session", CanView = true });
        await db.SaveChangesAsync();
        var audit = new Mock<IAuditService>().Object;
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        var logger = new Mock<ILogger<CashierSessionsController>>().Object;

        var controller = new CashierSessionsController(db, nonAdminNoBranch, audit, treasuryResolution, logger);

        // Act
        var result = await controller.GetAll(page: 1, pageSize: 20);

        // Assert
        result.Should().BeOfType<ObjectResult>("non-admin without branch must be forbidden from listing cashier sessions").Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task CashierSessionsController_GetAll_NonAdminWithEmptyBranch_Forbid()
    {
        // Arrange
        await using var db = CreateDb();
        var nonAdminEmptyBranch = CreateNonAdminUserWithEmptyBranch();
        // FIN-PERM: grant cashier-session view so the 403 under test comes from the
        // branch guard (Guid.Empty branch), not the granular permission gate.
        db.RolePermissions.Add(new RolePermission { Role = "Accountant", Resource = "finance.cashier_session", CanView = true });
        await db.SaveChangesAsync();
        var audit = new Mock<IAuditService>().Object;
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        var logger = new Mock<ILogger<CashierSessionsController>>().Object;

        var controller = new CashierSessionsController(db, nonAdminEmptyBranch, audit, treasuryResolution, logger);

        // Act
        var result = await controller.GetAll(page: 1, pageSize: 20);

        // Assert
        result.Should().BeOfType<ObjectResult>("non-admin with Guid.Empty branch must be forbidden from listing cashier sessions").Which.StatusCode.Should().Be(403);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Test 6: FinanceV3SuppliersController — CreateCreditNote requires branch
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FinanceV3SuppliersController_CreateCreditNote_RequiresBranch()
    {
        // Arrange — user without branch
        await using var db = CreateDb();
        var branchId = Guid.NewGuid();
        db.Branches.Add(new Branch { Id = branchId, Name = "الفرع الرئيسي" });
        var userId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, Username = "accountant1", BranchId = branchId });

        var patientId = Guid.NewGuid();
        db.Patients.Add(new Patient
        {
            Id = patientId, FirstName = "أحمد", LastName = "علي",
            PatientNumber = "P-001", BranchId = branchId
        });

        var invoiceId = Guid.NewGuid();
        db.Invoices.Add(new Invoice
        {
            Id = invoiceId, PatientId = patientId, InvoiceNumber = "INV-001",
            Status = InvoiceStatus.Issued, TotalAmount = 1000m, Subtotal = 1000m,
            CreatedBy = userId
        });
        await db.SaveChangesAsync();

        // Use non-admin user with null branch
        var nonAdminNoBranch = CreateNonAdminUserWithoutBranch();
        var financeService = new Mock<IFinanceService>().Object;
        var audit = new Mock<IAuditService>().Object;

        // FIN-PERM (Sprint 3): grant finance.expenses so the 403 under test comes from the
        // branch guard (no/empty branch), not the new granular permission gate.
        db.RolePermissions.Add(new RolePermission { Role = "Accountant", Resource = "finance.expenses", CanView = true, CanCreate = true, CanEdit = true, CanApprove = true, CanDelete = true });
        await db.SaveChangesAsync();
        var controller = new FinanceV3SuppliersController(db, financeService, nonAdminNoBranch, audit);

        var request = new CreateCreditNoteRequest
        {
            InvoiceId = invoiceId,
            PatientId = patientId,
            Amount = 100m,
            Reason = "إرجاع"
        };

        // Act
        var result = await controller.CreateCreditNote(request);

        // Assert — should return BadRequest with Arabic message about branch
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = (BadRequestObjectResult)result;
        var messageProp = badRequest.Value?.GetType().GetProperty("message");
        messageProp.Should().NotBeNull();
        var message = messageProp!.GetValue(badRequest.Value)?.ToString();
        message.Should().Contain("الفرع", "should reject with Arabic message about branch requirement");
    }

    [Fact]
    public async Task FinanceV3SuppliersController_CreateCreditNote_EmptyBranch_ReturnsBadRequest()
    {
        // Arrange — user with Guid.Empty branch
        await using var db = CreateDb();
        var branchId = Guid.NewGuid();
        db.Branches.Add(new Branch { Id = branchId, Name = "الفرع الرئيسي" });
        var userId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, Username = "accountant1", BranchId = branchId });

        var patientId = Guid.NewGuid();
        db.Patients.Add(new Patient
        {
            Id = patientId, FirstName = "سارة", LastName = "محمد",
            PatientNumber = "P-002", BranchId = branchId
        });

        var invoiceId = Guid.NewGuid();
        db.Invoices.Add(new Invoice
        {
            Id = invoiceId, PatientId = patientId, InvoiceNumber = "INV-002",
            Status = InvoiceStatus.Issued, TotalAmount = 500m, Subtotal = 500m,
            CreatedBy = userId
        });
        await db.SaveChangesAsync();

        var nonAdminEmptyBranch = CreateNonAdminUserWithEmptyBranch();
        var financeService = new Mock<IFinanceService>().Object;
        var audit = new Mock<IAuditService>().Object;

        // FIN-PERM (Sprint 3): grant finance.expenses so the 403 under test comes from the
        // branch guard (no/empty branch), not the new granular permission gate.
        db.RolePermissions.Add(new RolePermission { Role = "Accountant", Resource = "finance.expenses", CanView = true, CanCreate = true, CanEdit = true, CanApprove = true, CanDelete = true });
        await db.SaveChangesAsync();
        var controller = new FinanceV3SuppliersController(db, financeService, nonAdminEmptyBranch, audit);

        var request = new CreateCreditNoteRequest
        {
            InvoiceId = invoiceId,
            PatientId = patientId,
            Amount = 50m,
            Reason = "خصم"
        };

        // Act
        var result = await controller.CreateCreditNote(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void FinanceV3SuppliersController_CreateCreditNote_HasFinanceWritePolicy()
    {
        // Verify that CreateCreditNote is protected with FinanceWrite authorization policy
        var method = typeof(FinanceV3SuppliersController).GetMethod("CreateCreditNote");
        method.Should().NotBeNull();

        var authorizeAttrs = method!.GetCustomAttributes<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>();
        authorizeAttrs.Should().ContainSingle(a => a.Policy == "FinanceWrite",
            "CreateCreditNote must require FinanceWrite policy");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Test 7: IsUniqueViolation uses Npgsql.PostgresException.SqlState
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void IsUniqueViolation_UsesNpgsqlSqlState_Detects23505()
    {
        // Arrange — Find the IsUniqueViolation method on PurchaseOrdersController
        var method = typeof(PurchaseOrdersController).GetMethod("IsUniqueViolation",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull("IsUniqueViolation must exist on PurchaseOrdersController");

        // Create a DbUpdateException with a PostgresException (SqlState=23505) as inner exception
        var pgException = CreatePostgresException("23505");
        var dbUpdateException = new DbUpdateException("unique constraint violation", pgException);

        // Act
        var result = (bool)method!.Invoke(null, new object[] { dbUpdateException })!;

        // Assert
        result.Should().BeTrue("IsUniqueViolation should detect SQL state 23505 from PostgresException");
    }

    [Fact]
    public void IsUniqueViolation_UsesNpgsqlSqlState_RejectsNon23505()
    {
        // Arrange
        var method = typeof(PurchaseOrdersController).GetMethod("IsUniqueViolation",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        // Create a DbUpdateException with a PostgresException with a different SQL state
        var pgException = CreatePostgresException("23503"); // foreign key violation, not unique
        var dbUpdateException = new DbUpdateException("fk violation", pgException);

        // Act
        var result = (bool)method!.Invoke(null, new object[] { dbUpdateException })!;

        // Assert
        result.Should().BeFalse("IsUniqueViolation should NOT detect non-23505 SQL states");
    }

    [Fact]
    public void IsUniqueViolation_UsesNpgsqlSqlState_NonPostgresException_ReturnsFalse()
    {
        // Arrange
        var method = typeof(PurchaseOrdersController).GetMethod("IsUniqueViolation",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        // Create a DbUpdateException with a generic inner exception (not Npgsql)
        var dbUpdateException = new DbUpdateException("some error", new InvalidOperationException("not postgres"));

        // Act
        var result = (bool)method!.Invoke(null, new object[] { dbUpdateException })!;

        // Assert
        result.Should().BeFalse("IsUniqueViolation should return false for non-PostgresException inner exceptions");
    }

    [Fact]
    public void IsUniqueViolation_UsesNpgsqlSqlState_NoInnerException_ReturnsFalse()
    {
        // Arrange
        var method = typeof(PurchaseOrdersController).GetMethod("IsUniqueViolation",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        // Create a DbUpdateException with no inner exception
        var dbUpdateException = new DbUpdateException("some error");

        // Act
        var result = (bool)method!.Invoke(null, new object[] { dbUpdateException })!;

        // Assert
        result.Should().BeFalse("IsUniqueViolation should return false when there is no inner exception");
    }

    [Fact]
    public void IsUniqueViolation_UsesNpgsqlSqlState_NestedPostgresException_Detects23505()
    {
        // Arrange — PostgresException is nested two levels deep
        var method = typeof(PurchaseOrdersController).GetMethod("IsUniqueViolation",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        var pgException = CreatePostgresException("23505");
        var middleException = new Exception("wrapper", pgException);
        var dbUpdateException = new DbUpdateException("unique constraint violation", middleException);

        // Act
        var result = (bool)method!.Invoke(null, new object[] { dbUpdateException })!;

        // Assert — should traverse the inner exception chain
        result.Should().BeTrue("IsUniqueViolation should detect SQL state 23505 even when PostgresException is nested");
    }

    /// <summary>
    /// Creates a Npgsql.PostgresException with the specified SqlState using reflection.
    /// PostgresException has no public parameterless constructor, so we use
    /// FormatterServices.GetUninitializedObject to create an instance without
    /// calling a constructor, then set the SqlState via its backing field.
    /// </summary>
#pragma warning disable SYSLIB0050 // FormatterServices is obsolete but needed for test object construction
    private static PostgresException CreatePostgresException(string sqlState)
    {
        // Create an uninitialized instance (bypasses constructor entirely)
        var exception = (PostgresException)System.Runtime.Serialization.FormatterServices
            .GetUninitializedObject(typeof(PostgresException));

        // Set the SqlState property via its backing field (the property has no public setter)
        var sqlStateField = typeof(PostgresException)
            .GetField("<SqlState>k__BackingField",
                BindingFlags.NonPublic | BindingFlags.Instance)
            ?? typeof(PostgresException)
                .GetField("_sqlState",
                    BindingFlags.NonPublic | BindingFlags.Instance)
            // Fall back to scanning all fields for one containing "sqlState"
            ?? typeof(PostgresException)
                .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(f => f.Name.Contains("sqlState", StringComparison.OrdinalIgnoreCase));

        sqlStateField.Should().NotBeNull("PostgresException must have a SqlState backing field that can be set via reflection");
        sqlStateField!.SetValue(exception, sqlState);

        return exception;
    }
#pragma warning restore SYSLIB0050
}
