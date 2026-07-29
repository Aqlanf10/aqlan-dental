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

public class FIN13SqlAggregationTests
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

    /// <summary>
    /// TD-021 PR A2: read-side finance service extracted from FinanceService.
    /// Used by tests that call GetPatientFinanceSummaryAsync, GetAccountStatementAsync,
    /// GetSummaryAsync, or GetOverdueContractsAsync.
    /// </summary>
    private static FinanceReadService CreateFinanceReadService(AppDbContext db, ICurrentUserService currentUser)
        => new(db, currentUser);

    private static (Guid branchId, Guid patientId, Guid userId, ICurrentUserService currentUser) SeedPatient(AppDbContext db)
    {
        var branchId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var currentUser = CreateAdminUser(userId, branchId);

        db.Branches.Add(new Branch { Id = branchId, Name = "Main Branch" });
        db.Patients.Add(new Patient
        {
            Id = patientId,
            FirstName = "Test",
            LastName = "Patient",
            BranchId = branchId,
            PatientNumber = "P-FIN13-001"
        });
        db.Users.Add(new User { Id = userId, Username = "admin-fin13", BranchId = branchId });
        db.SaveChanges();
        return (branchId, patientId, userId, currentUser);
    }

    [Fact]
    public async Task GetPatientFinanceSummaryAsync_WithMixedData_ReturnsCorrectTotals()
    {
        await using var db = CreateDb();
        var (branchId, patientId, userId, currentUser) = SeedPatient(db);

        var readService = CreateFinanceReadService(db, currentUser);

        var contractId = Guid.NewGuid();
        db.Contracts.Add(new Contract
        {
            Id = contractId,
            PatientId = patientId,
            Specialty = "Ortho",
            TotalAmount = 10_000m,
            DiscountAmount = 1_000m,
            DownPayment = 2_000m,
            InstallmentsCount = 4,
            InstallmentAmount = 1_750m,
            StartDate = new DateOnly(2024, 1, 1),
            Status = ContractStatus.Active,
            CreatedBy = userId
        });

        var invoiceId = Guid.NewGuid();
        db.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            PatientId = patientId,
            InvoiceNumber = "INV-FIN13-001",
            Status = InvoiceStatus.Issued,
            TotalAmount = 5_000m,
            Subtotal = 5_000m,
            IsActive = true,
            CreatedBy = userId
        });

        db.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            InvoiceNumber = "INV-FIN13-DRAFT",
            Status = InvoiceStatus.Draft,
            TotalAmount = 99_999m,
            Subtotal = 99_999m,
            IsActive = true,
            CreatedBy = userId
        });

        db.Payments.Add(new Payment { Id = Guid.NewGuid(), PatientId = patientId, ContractId = contractId, Amount = 2_000m, PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow), PaymentMethod = "cash", IsActive = true, BranchId = branchId, ReceivedBy = userId });
        db.Payments.Add(new Payment { Id = Guid.NewGuid(), PatientId = patientId, InvoiceId = invoiceId, Amount = 1_500m, PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow), PaymentMethod = "cash", IsActive = true, BranchId = branchId, ReceivedBy = userId });

        await db.SaveChangesAsync();

        var summary = await readService.GetPatientFinanceSummaryAsync(patientId);

        summary.Should().NotBeNull();
        summary.TotalTreatmentCost.Should().Be(14_000m);
        summary.TotalPaid.Should().Be(3_500m);
        summary.OutstandingBalance.Should().Be(10_500m);
        summary.ActiveContractsCount.Should().Be(1);
        summary.TotalPaymentsCount.Should().Be(2);
        summary.OverdueAmount.Should().BeGreaterThan(0m);
        summary.FinancialStatus.Should().Be("overdue");
    }

    [Fact]
    public async Task GetPatientFinanceSummaryAsync_NoData_ReturnsZeros_DoesNotThrow()
    {
        await using var db = CreateDb();
        var (_, patientId, _, currentUser) = SeedPatient(db);

        var readService = CreateFinanceReadService(db, currentUser);
        await db.SaveChangesAsync();

        var act = () => readService.GetPatientFinanceSummaryAsync(patientId);
        await act.Should().NotThrowAsync();

        var summary = await readService.GetPatientFinanceSummaryAsync(patientId);
        summary.Should().NotBeNull();
        summary.TotalTreatmentCost.Should().Be(0m);
        summary.TotalPaid.Should().Be(0m);
        summary.OutstandingBalance.Should().Be(0m);
        summary.OverdueAmount.Should().Be(0m);
        summary.ActiveContractsCount.Should().Be(0);
        summary.TotalPaymentsCount.Should().Be(0);
        summary.LatestPayment.Should().BeNull();
        summary.FinancialStatus.Should().Be("no_plan");
    }

    [Fact]
    public async Task GetAccountStatementAsync_WithMixedData_ReturnsCorrectServerSideAggregations()
    {
        await using var db = CreateDb();
        var (branchId, patientId, userId, currentUser) = SeedPatient(db);

        var readService = CreateFinanceReadService(db, currentUser);

        var c1 = Guid.NewGuid();
        var c2 = Guid.NewGuid();
        db.Contracts.Add(new Contract { Id = c1, PatientId = patientId, Specialty = "Ortho", TotalAmount = 10_000m, DiscountAmount = 1_000m, Status = ContractStatus.Active, StartDate = new DateOnly(2024, 1, 1), CreatedBy = userId });
        db.Contracts.Add(new Contract { Id = c2, PatientId = patientId, Specialty = "Restorative", TotalAmount = 5_000m, DiscountAmount = 0m, Status = ContractStatus.Completed, StartDate = new DateOnly(2023, 6, 1), CreatedBy = userId });

        var inv1 = Guid.NewGuid();
        db.Invoices.Add(new Invoice { Id = inv1, PatientId = patientId, InvoiceNumber = "INV-A-001", Status = InvoiceStatus.Issued, TotalAmount = 2_100m, Subtotal = 2_000m, TaxAmount = 200m, DiscountAmount = 100m, IsActive = true, CreatedBy = userId });
        db.Invoices.Add(new Invoice { Id = Guid.NewGuid(), PatientId = patientId, InvoiceNumber = "INV-A-DRAFT", Status = InvoiceStatus.Draft, TotalAmount = 50_000m, Subtotal = 50_000m, IsActive = true, CreatedBy = userId });

        db.Payments.Add(new Payment { Id = Guid.NewGuid(), PatientId = patientId, ContractId = c1, Amount = 3_000m, PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow), IsActive = true, BranchId = branchId, ReceivedBy = userId });
        db.Payments.Add(new Payment { Id = Guid.NewGuid(), PatientId = patientId, ContractId = c2, Amount = 5_000m, PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow), IsActive = true, BranchId = branchId, ReceivedBy = userId });
        db.Payments.Add(new Payment { Id = Guid.NewGuid(), PatientId = patientId, InvoiceId = inv1, Amount = 800m, PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow), IsActive = true, BranchId = branchId, ReceivedBy = userId });

        await db.SaveChangesAsync();

        var statement = await readService.GetAccountStatementAsync(patientId);

        statement.Should().NotBeNull();
        statement!.TotalContracted.Should().Be(17_200m);
        statement.TotalDiscounts.Should().Be(1_100m);
        statement.TotalPaid.Should().Be(8_800m);
        statement.TotalRemaining.Should().Be(7_300m);
        statement.ActiveContracts.Should().Be(1);
        statement.CompletedContracts.Should().Be(1);
        statement.Contracts.Should().HaveCount(2);
        statement.Contracts.Single(x => x.Id == c1).PaidAmount.Should().Be(3_000m);
        statement.Contracts.Single(x => x.Id == c1).RemainingAmount.Should().Be(6_000m);
        statement.Contracts.Single(x => x.Id == c2).PaidAmount.Should().Be(5_000m);
        statement.Contracts.Single(x => x.Id == c2).RemainingAmount.Should().Be(0m);
        statement.RecentPayments.Should().HaveCount(3);
        statement.RecentPayments.All(p => p.Amount > 0).Should().BeTrue();
    }

    [Fact]
    public async Task GetAccountStatementAsync_NoData_ReturnsZeros_DoesNotThrow()
    {
        await using var db = CreateDb();
        var (_, patientId, _, currentUser) = SeedPatient(db);

        var readService = CreateFinanceReadService(db, currentUser);
        await db.SaveChangesAsync();

        var act = () => readService.GetAccountStatementAsync(patientId);
        await act.Should().NotThrowAsync();

        var statement = await readService.GetAccountStatementAsync(patientId);
        statement.Should().NotBeNull();
        statement!.TotalContracted.Should().Be(0m);
        statement.TotalDiscounts.Should().Be(0m);
        statement.TotalPaid.Should().Be(0m);
        statement.TotalRemaining.Should().Be(0m);
        statement.ActiveContracts.Should().Be(0);
        statement.CompletedContracts.Should().Be(0);
        statement.Contracts.Should().BeEmpty();
        statement.RecentPayments.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOverdueContractsAsync_WithActiveInstallmentContract_ReturnsProjectedPaidAmount()
    {
        await using var db = CreateDb();
        var (branchId, patientId, userId, currentUser) = SeedPatient(db);

        var readService = CreateFinanceReadService(db, currentUser);

        var contractStartDate = ClinicTimeProvider.ClinicToday().AddMonths(-6);
        var contractId = Guid.NewGuid();
        db.Contracts.Add(new Contract
        {
            Id = contractId,
            PatientId = patientId,
            Specialty = "Ortho",
            TotalAmount = 5_000m,
            DiscountAmount = 0m,
            DownPayment = 1_000m,
            InstallmentsCount = 8,
            InstallmentAmount = 500m,
            StartDate = contractStartDate,
            Status = ContractStatus.Active,
            CreatedBy = userId
        });
        db.Payments.Add(new Payment { Id = Guid.NewGuid(), PatientId = patientId, ContractId = contractId, Amount = 1_000m, PaymentDate = contractStartDate, IsActive = true, BranchId = branchId, ReceivedBy = userId });
        await db.SaveChangesAsync();

        var overdue = await readService.GetOverdueContractsAsync();

        overdue.Should().ContainSingle();
        var dto = overdue[0];
        dto.ContractId.Should().Be(contractId);
        dto.PaidAmount.Should().Be(1_000m);
        dto.MonthsElapsed.Should().Be(6);
        dto.OverdueAmount.Should().Be(3_000m);
        dto.RemainingAmount.Should().Be(4_000m);
    }

    [Fact]
    public async Task GetOverdueContractsAsync_NoData_ReturnsEmpty_DoesNotThrow()
    {
        await using var db = CreateDb();
        var (_, _, _, currentUser) = SeedPatient(db);

        var readService = CreateFinanceReadService(db, currentUser);
        await db.SaveChangesAsync();

        var act = () => readService.GetOverdueContractsAsync();
        await act.Should().NotThrowAsync();

        var overdue = await readService.GetOverdueContractsAsync();
        overdue.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSummaryAsync_NoData_ReturnsZeros_DoesNotThrow()
    {
        await using var db = CreateDb();
        var (_, _, _, currentUser) = SeedPatient(db);

        var readService = CreateFinanceReadService(db, currentUser);
        await db.SaveChangesAsync();

        var act = () => readService.GetSummaryAsync();
        await act.Should().NotThrowAsync();

        var summary = await readService.GetSummaryAsync();
        summary.Should().NotBeNull();
        summary.TodayCollected.Should().Be(0m);
        summary.MonthCollected.Should().Be(0m);
        summary.TotalOutstanding.Should().Be(0m);
        summary.OverdueAmount.Should().Be(0m);
        summary.PendingCommissionsAmount.Should().Be(0m);
        summary.ActiveContracts.Should().Be(0);
        summary.UnpaidInvoicesCount.Should().Be(0);
        summary.DraftInvoicesCount.Should().Be(0);
        summary.RecentPayments.Should().BeEmpty();
        summary.RecentInvoices.Should().BeEmpty();
    }
}
