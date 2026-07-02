using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Finance;

/// <summary>
/// QA-595: Branch-isolation tests for ReportsController + AdvancePaymentController.
///
/// Before QA-595, 18 of 20 ReportsController endpoints and the
/// AdvancePaymentController.GetAll endpoint had no branch scoping for non-admin
/// users — an Accountant from branch A could read/CSV-export data from branch B.
/// These tests seed data in two branches and verify that a non-admin caller
/// only sees their own branch's records.
/// </summary>
public class ReportsBranchIsolationTests
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

    private static ICurrentUserService CreateBranchAccountant(Guid userId, Guid branchId)
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

    private static ICurrentUserService CreateBranchlessAccountant(Guid userId)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(u => u.UserId).Returns(userId);
        mock.Setup(u => u.Role).Returns(UserRole.Accountant);
        mock.Setup(u => u.IsAdmin).Returns(false);
        mock.Setup(u => u.IsAuthenticated).Returns(true);
        mock.Setup(u => u.BranchId).Returns((Guid?)null);
        mock.Setup(u => u.IsImpersonating).Returns(false);
        mock.Setup(u => u.OriginalUserId).Returns((Guid?)null);
        return mock.Object;
    }

    private static ReportsController BuildReportsController(AppDbContext db, ICurrentUserService currentUser)
    {
        var pdfService = new Mock<IPdfService>().Object;
        var logger = new Mock<ILogger<ReportsController>>().Object;
        return new ReportsController(db, pdfService, logger, currentUser);
    }

    /// <summary>
    /// Seed two branches (A and B), each with one patient and one payment.
    /// Returns (branchAId, branchBId, patientAId, patientBId).
    /// </summary>
    private static (Guid branchA, Guid branchB, Guid patientA, Guid patientB) SeedTwoBranchesWithPatientsAndPayments(AppDbContext db)
    {
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        var patientA = Guid.NewGuid();
        var patientB = Guid.NewGuid();

        db.Branches.Add(new Branch { Id = branchA, Name = "فرع أ" });
        db.Branches.Add(new Branch { Id = branchB, Name = "فرع ب" });

        db.Patients.Add(new Patient
        {
            Id = patientA,
            FirstName = "مريض",
            LastName = "أ",
            PatientNumber = "P-A-001",
            BranchId = branchA,
            CreatedAt = DateTime.UtcNow
        });
        db.Patients.Add(new Patient
        {
            Id = patientB,
            FirstName = "مريض",
            LastName = "ب",
            PatientNumber = "P-B-001",
            BranchId = branchB,
            CreatedAt = DateTime.UtcNow
        });

        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            PatientId = patientA,
            Amount = 10_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = "cash",
            BranchId = branchA,
            IsActive = true,
            ReceiptNumber = "R-A-001"
        });
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            PatientId = patientB,
            Amount = 20_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            PaymentMethod = "cash",
            BranchId = branchB,
            IsActive = true,
            ReceiptNumber = "R-B-001"
        });

        db.SaveChanges();
        return (branchA, branchB, patientA, patientB);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // GetCenterSummary — non-admin sees only their branch
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CenterSummary_Admin_ReturnsConsolidatedAcrossBranches()
    {
        await using var db = CreateDb();
        var (branchA, branchB, _, _) = SeedTwoBranchesWithPatientsAndPayments(db);
        var controller = BuildReportsController(db, CreateAdminUser());

        var result = await controller.GetCenterSummary(null, null);

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var totalRevenue = (decimal)ok.Value!.GetType().GetProperty("totalRevenue")!.GetValue(ok.Value)!;
        totalRevenue.Should().Be(30_000m, "admin sees both branches: 10,000 + 20,000");
    }

    [Fact]
    public async Task CenterSummary_NonAdmin_ReturnsOnlyOwnBranch()
    {
        await using var db = CreateDb();
        var (branchA, branchB, _, _) = SeedTwoBranchesWithPatientsAndPayments(db);
        var accountantA = CreateBranchAccountant(Guid.NewGuid(), branchA);
        var controller = BuildReportsController(db, accountantA);

        var result = await controller.GetCenterSummary(null, null);

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var totalRevenue = (decimal)ok.Value!.GetType().GetProperty("totalRevenue")!.GetValue(ok.Value)!;
        totalRevenue.Should().Be(10_000m, "accountant from branch A sees only branch A's payment (10,000), NOT branch B's 20,000");

        var totalPatients = (int)ok.Value!.GetType().GetProperty("totalPatients")!.GetValue(ok.Value)!;
        totalPatients.Should().Be(1, "only one patient exists in branch A");
    }

    [Fact]
    public async Task CenterSummary_NonAdmin_WithoutBranch_Returns403()
    {
        await using var db = CreateDb();
        SeedTwoBranchesWithPatientsAndPayments(db);
        var branchlessAccountant = CreateBranchlessAccountant(Guid.NewGuid());
        var controller = BuildReportsController(db, branchlessAccountant);

        var result = await controller.GetCenterSummary(null, null);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(403);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // GetFinancialReport — non-admin filtered; dead-code branchId now applied
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FinancialReport_NonAdmin_FiltersByBranch_IncludingCashFlowTransactions()
    {
        await using var db = CreateDb();
        var (branchA, branchB, patientA, patientB) = SeedTwoBranchesWithPatientsAndPayments(db);

        // Add CashFlowTransaction expenses in both branches
        db.CashFlowTransactions.Add(new CashFlowTransaction
        {
            Id = Guid.NewGuid(),
            Type = TransactionType.Outflow,
            IsActive = true,
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            Category = FinancialCategory.OperationalExpense,
            Amount = 1_000m,
            BranchId = branchA,
            Description = "مصروف فرع أ"
        });
        db.CashFlowTransactions.Add(new CashFlowTransaction
        {
            Id = Guid.NewGuid(),
            Type = TransactionType.Outflow,
            IsActive = true,
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            Category = FinancialCategory.OperationalExpense,
            Amount = 5_000m,
            BranchId = branchB,
            Description = "مصروف فرع ب"
        });
        await db.SaveChangesAsync();

        var accountantA = CreateBranchAccountant(Guid.NewGuid(), branchA);
        var controller = BuildReportsController(db, accountantA);

        var result = await controller.GetFinancialReport(null, null);

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var totalCollected = (decimal)ok.Value!.GetType().GetProperty("totalCollected")!.GetValue(ok.Value)!;
        var totalExpenses = (decimal)ok.Value!.GetType().GetProperty("totalExpenses")!.GetValue(ok.Value)!;

        totalCollected.Should().Be(10_000m, "only branch A's payment");
        totalExpenses.Should().Be(1_000m, "QA-595: branchId is now applied to CashFlowTransaction queries (was dead code). Branch B's 5,000 expense must NOT appear.");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ExportPatients — CRITICAL PII leak fixed
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExportPatients_NonAdmin_ReturnsOnlyOwnBranchPatients()
    {
        await using var db = CreateDb();
        var (branchA, branchB, patientA, patientB) = SeedTwoBranchesWithPatientsAndPayments(db);
        var accountantA = CreateBranchAccountant(Guid.NewGuid(), branchA);
        var controller = BuildReportsController(db, accountantA);

        var result = await controller.ExportPatients();

        result.Should().BeOfType<FileContentResult>();
        var fileResult = (FileContentResult)result;
        var csv = System.Text.Encoding.UTF8.GetString(fileResult.FileContents);
        csv.Should().Contain("P-A-001", "branch A patient must be in the CSV");
        csv.Should().NotContain("P-B-001", "QA-595: branch B patient must NOT leak into branch A accountant's CSV");
    }

    [Fact]
    public async Task ExportPatients_Admin_ReturnsAllBranches()
    {
        await using var db = CreateDb();
        var (branchA, branchB, patientA, patientB) = SeedTwoBranchesWithPatientsAndPayments(db);
        var controller = BuildReportsController(db, CreateAdminUser());

        var result = await controller.ExportPatients();

        result.Should().BeOfType<FileContentResult>();
        var fileResult = (FileContentResult)result;
        var csv = System.Text.Encoding.UTF8.GetString(fileResult.FileContents);
        csv.Should().Contain("P-A-001");
        csv.Should().Contain("P-B-001", "admin sees both branches");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ExportPayments — CRITICAL financial leak fixed
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExportPayments_NonAdmin_ReturnsOnlyOwnBranchPayments()
    {
        await using var db = CreateDb();
        var (branchA, branchB, _, _) = SeedTwoBranchesWithPatientsAndPayments(db);
        var accountantA = CreateBranchAccountant(Guid.NewGuid(), branchA);
        var controller = BuildReportsController(db, accountantA);

        var today = DateTime.Today.ToString("yyyy-MM-dd");
        var result = await controller.ExportPayments(today, today);

        result.Should().BeOfType<FileContentResult>();
        var csv = System.Text.Encoding.UTF8.GetString(((FileContentResult)result).FileContents);
        csv.Should().Contain("R-A-001", "branch A receipt must be in the CSV");
        csv.Should().NotContain("R-B-001", "QA-595: branch B receipt must NOT leak into branch A accountant's CSV");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // GetFinancialStatementPdf — cross-branch patient access blocked
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FinancialStatementPdf_NonAdmin_CrossBranchPatient_Returns403()
    {
        await using var db = CreateDb();
        var (branchA, branchB, patientA, patientB) = SeedTwoBranchesWithPatientsAndPayments(db);
        var accountantA = CreateBranchAccountant(Guid.NewGuid(), branchA);
        var controller = BuildReportsController(db, accountantA);

        // accountantA tries to access patientB's financial statement (branch B)
        var result = await controller.GetFinancialStatementPdf(patientB);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task FinancialStatementPdf_NonAdmin_SameBranchPatient_ProceedsToPdfService()
    {
        await using var db = CreateDb();
        var (branchA, branchB, patientA, patientB) = SeedTwoBranchesWithPatientsAndPayments(db);
        var accountantA = CreateBranchAccountant(Guid.NewGuid(), branchA);
        var controller = BuildReportsController(db, accountantA);

        // accountantA accesses patientA (same branch) — should pass the branch check
        // and proceed to pdfService (which is a mock, so it returns null → 500 is fine,
        // but the key assertion is that we did NOT get 403)
        var result = await controller.GetFinancialStatementPdf(patientA);

        // The mock PdfService returns null, which will throw inside the try block → 500.
        // What matters: NOT 403 (the branch check passed).
        if (result is ObjectResult objResult)
            objResult.StatusCode.Should().NotBe(403, "same-branch patient must pass the branch check");
    }

    [Fact]
    public async Task FinancialStatementPdf_Admin_AnyBranchPatient_ProceedsWithout403()
    {
        await using var db = CreateDb();
        var (branchA, branchB, patientA, patientB) = SeedTwoBranchesWithPatientsAndPayments(db);
        var controller = BuildReportsController(db, CreateAdminUser());

        var result = await controller.GetFinancialStatementPdf(patientB);

        // Admin bypasses the branch check entirely. Mock PdfService returns null
        // → exception → 500. Key: NOT 403.
        if (result is ObjectResult objResult)
            objResult.StatusCode.Should().NotBe(403, "admin must bypass the branch check for any branch's patient");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // AdvancePaymentController.GetAll — non-admin force-scoped, ignores ?branchId=
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Advance_GetAll_NonAdmin_ReturnsOnlyOwnBranch()
    {
        // We need the branchId to match what we seed — re-create with the right branch
        await using var db = CreateDb();
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        var employeeA = Guid.NewGuid();
        var employeeB = Guid.NewGuid();

        db.Branches.Add(new Branch { Id = branchA, Name = "فرع أ" });
        db.Branches.Add(new Branch { Id = branchB, Name = "فرع ب" });
        db.Employees.Add(new Employee { Id = employeeA, UserId = Guid.NewGuid(), FullName = "موظف أ", BranchId = branchA });
        db.Employees.Add(new Employee { Id = employeeB, UserId = Guid.NewGuid(), FullName = "موظف ب", BranchId = branchB });
        db.AdvancePayments.Add(new AdvancePayment
        {
            Id = Guid.NewGuid(), EmployeeId = employeeA, Amount = 5_000m, Reason = "سلفة أ",
            RequestDate = DateTime.UtcNow, Status = RequestStatus.Pending
        });
        db.AdvancePayments.Add(new AdvancePayment
        {
            Id = Guid.NewGuid(), EmployeeId = employeeB, Amount = 15_000m, Reason = "سلفة ب",
            RequestDate = DateTime.UtcNow, Status = RequestStatus.Pending
        });
        db.RolePermissions.Add(new RolePermission
        {
            Role = UserRole.Accountant.ToString(), Resource = "finance.expenses",
            CanView = true, CanCreate = false, CanEdit = false, CanDelete = false, CanApprove = false
        });
        await db.SaveChangesAsync();

        var accountantABuilder = CreateBranchAccountant(Guid.NewGuid(), branchA);
        var audit = new Mock<IAuditService>().Object;
        var journalEntry = new Mock<IJournalEntryService>().Object;
        var treasuryResolution = new Mock<ITreasuryResolutionService>().Object;
        var logger = new Mock<ILogger<AdvancePaymentController>>().Object;
        var controller = new AdvancePaymentController(db, audit, journalEntry, treasuryResolution, accountantABuilder, logger);

        var result = await controller.GetAll(null, null, null, 1, 20);

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var total = (int)ok.Value!.GetType().GetProperty("total")!.GetValue(ok.Value)!;
        total.Should().Be(1, "accountant from branch A sees only their branch's advance (1), NOT branch B's");
    }

    [Fact]
    public async Task Advance_GetAll_NonAdmin_IgnoresCallerSuppliedBranchId()
    {
        // QA-595: A non-admin caller cannot bypass branch scoping by passing a
        // different ?branchId= query param. Their own branch is always forced.
        await using var db = CreateDb();
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        var employeeA = Guid.NewGuid();
        var employeeB = Guid.NewGuid();

        db.Branches.Add(new Branch { Id = branchA, Name = "فرع أ" });
        db.Branches.Add(new Branch { Id = branchB, Name = "فرع ب" });
        db.Employees.Add(new Employee { Id = employeeA, UserId = Guid.NewGuid(), FullName = "موظف أ", BranchId = branchA });
        db.Employees.Add(new Employee { Id = employeeB, UserId = Guid.NewGuid(), FullName = "موظف ب", BranchId = branchB });
        db.AdvancePayments.Add(new AdvancePayment
        {
            Id = Guid.NewGuid(), EmployeeId = employeeA, Amount = 5_000m, Reason = "سلفة أ",
            RequestDate = DateTime.UtcNow, Status = RequestStatus.Pending
        });
        db.AdvancePayments.Add(new AdvancePayment
        {
            Id = Guid.NewGuid(), EmployeeId = employeeB, Amount = 15_000m, Reason = "سلفة ب",
            RequestDate = DateTime.UtcNow, Status = RequestStatus.Pending
        });
        db.RolePermissions.Add(new RolePermission
        {
            Role = UserRole.Accountant.ToString(), Resource = "finance.expenses",
            CanView = true, CanCreate = false, CanEdit = false, CanDelete = false, CanApprove = false
        });
        await db.SaveChangesAsync();

        var accountantA = CreateBranchAccountant(Guid.NewGuid(), branchA);
        var audit = new Mock<IAuditService>().Object;
        var journalEntry = new Mock<IJournalEntryService>().Object;
        var treasuryResolution = new Mock<ITreasuryResolutionService>().Object;
        var logger = new Mock<ILogger<AdvancePaymentController>>().Object;
        var controller = new AdvancePaymentController(db, audit, journalEntry, treasuryResolution, accountantA, logger);

        // accountant from branch A tries to pass branchB in the query — must be IGNORED
        var result = await controller.GetAll(null, null, branchB, 1, 20);

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var total = (int)ok.Value!.GetType().GetProperty("total")!.GetValue(ok.Value)!;
        total.Should().Be(1, "QA-595: non-admin's caller-supplied branchId must be ignored — still sees only their own branch (1 record)");
    }

    [Fact]
    public async Task Advance_GetAll_NonAdmin_WithoutBranch_Returns403()
    {
        await using var db = CreateDb();
        var branchA = Guid.NewGuid();
        db.Branches.Add(new Branch { Id = branchA, Name = "فرع أ" });
        db.RolePermissions.Add(new RolePermission
        {
            Role = UserRole.Accountant.ToString(), Resource = "finance.expenses",
            CanView = true, CanCreate = false, CanEdit = false, CanDelete = false, CanApprove = false
        });
        await db.SaveChangesAsync();

        var branchlessAccountant = CreateBranchlessAccountant(Guid.NewGuid());
        var audit = new Mock<IAuditService>().Object;
        var journalEntry = new Mock<IJournalEntryService>().Object;
        var treasuryResolution = new Mock<ITreasuryResolutionService>().Object;
        var logger = new Mock<ILogger<AdvancePaymentController>>().Object;
        var controller = new AdvancePaymentController(db, audit, journalEntry, treasuryResolution, branchlessAccountant, logger);

        var result = await controller.GetAll(null, null, null, 1, 20);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Advance_GetAll_Admin_CanRequestSpecificBranch()
    {
        await using var db = CreateDb();
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        var employeeA = Guid.NewGuid();
        var employeeB = Guid.NewGuid();

        db.Branches.Add(new Branch { Id = branchA, Name = "فرع أ" });
        db.Branches.Add(new Branch { Id = branchB, Name = "فرع ب" });
        db.Employees.Add(new Employee { Id = employeeA, UserId = Guid.NewGuid(), FullName = "موظف أ", BranchId = branchA });
        db.Employees.Add(new Employee { Id = employeeB, UserId = Guid.NewGuid(), FullName = "موظف ب", BranchId = branchB });
        db.AdvancePayments.Add(new AdvancePayment
        {
            Id = Guid.NewGuid(), EmployeeId = employeeA, Amount = 5_000m, Reason = "سلفة أ",
            RequestDate = DateTime.UtcNow, Status = RequestStatus.Pending
        });
        db.AdvancePayments.Add(new AdvancePayment
        {
            Id = Guid.NewGuid(), EmployeeId = employeeB, Amount = 15_000m, Reason = "سلفة ب",
            RequestDate = DateTime.UtcNow, Status = RequestStatus.Pending
        });
        await db.SaveChangesAsync();

        var admin = CreateAdminUser();
        var audit = new Mock<IAuditService>().Object;
        var journalEntry = new Mock<IJournalEntryService>().Object;
        var treasuryResolution = new Mock<ITreasuryResolutionService>().Object;
        var logger = new Mock<ILogger<AdvancePaymentController>>().Object;
        var controller = new AdvancePaymentController(db, audit, journalEntry, treasuryResolution, admin, logger);

        // Admin requests branch B specifically
        var result = await controller.GetAll(null, null, branchB, 1, 20);

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var total = (int)ok.Value!.GetType().GetProperty("total")!.GetValue(ok.Value)!;
        total.Should().Be(1, "admin requests branch B → only branch B's advance (1 record)");
    }
}
