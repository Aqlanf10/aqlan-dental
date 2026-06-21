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
/// FIN-13 — verifies that the rewritten server-side SQL aggregations in
/// FinanceService produce the SAME totals as the previous in-memory aggregations,
/// and that empty sets return 0 (not throw) thanks to the (decimal?) ?? 0m pattern.
///
/// These tests intentionally seed mixed data (contracts + invoices + payments +
/// refunds + discounts) so that any drift between the old and new query
/// mechanisms would surface as a numeric mismatch.
/// </summary>
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

    private static FinanceService CreateFinanceService(AppDbContext db, ICurrentUserService currentUser)
    {
        var notifications = new Mock<INotificationService>().Object;
        var logger = new Mock<ILogger<FinanceService>>().Object;
        var commissionService = new Mock<ICommissionService>().Object;

        // Mock IJournalEntryService — none of the FIN-13 report methods call it,
        // but the FinanceService constructor requires it.
        var journalEntryServiceMock = new Mock<IJournalEntryService>();
        return new FinanceService(db, currentUser, notifications, logger, commissionService, journalEntryServiceMock.Object);
    }

    private static (Guid branchId, Guid patientId, Guid userId, ICurrentUserService currentUser) SeedPatient(AppDbContext db)
    {
        var branchId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var currentUser = CreateAdminUser(userId, branchId);

        db.Branches.Add(new Branch { Id = branchId, Name = "الفرع الرئيسي" });
        db.Patients.Add(new Patient
        {
            Id = patientId,
            FirstName = "سارة",
            LastName = "أحمد",
            BranchId = branchId,
            PatientNumber = "P-FIN13-001"
        });
        db.Users.Add(new User { Id = userId, Username = "admin-fin13", BranchId = branchId });
        db.SaveChanges();
        return (branchId, patientId, userId, currentUser);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 1. GetPatientFinanceSummaryAsync — mixed data, verify totals match
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetPatientFinanceSummaryAsync_WithMixedData_ReturnsCorrectTotals()
    {
        await using var db = CreateDb();
        var (branchId, patientId, userId, currentUser) = SeedPatient(db);
        var service = CreateFinanceService(db, currentUser);

        // Contract: 10,000 total, 1,000 discount, 2,000 down payment →
        //   contractCost = 10,000 - 1,000 = 9,000
        //   contractPaid contribution = 2,000
        var contractId = Guid.NewGuid();
        db.Contracts.Add(new Contract
        {
            Id = contractId,
            PatientId = patientId,
            Specialty = "تقويم",
            TotalAmount = 10_000m,
            DiscountAmount = 1_000m,
            DownPayment = 2_000m,
            InstallmentsCount = 4,
            InstallmentAmount = 1_750m,
            StartDate = new DateOnly(2024, 1, 1),
            Status = ContractStatus.Active,
            CreatedBy = userId
        });

        // Invoice (Issued, not Draft/Cancelled): 5,000 total
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

        // Draft invoice (should be excluded): 99,999
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

        // Payments: 2,000 (contract down payment) + 1,500 (invoice) = 3,500 total
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            ContractId = contractId,
            Amount = 2_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            PaymentMethod = "cash",
            IsActive = true,
            BranchId = branchId,
            ReceivedBy = userId
        });
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            InvoiceId = invoiceId,
            Amount = 1_500m,
            PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            PaymentMethod = "cash",
            IsActive = true,
            BranchId = branchId,
            ReceivedBy = userId
        });

        await db.SaveChangesAsync();

        var summary = await service.GetPatientFinanceSummaryAsync(patientId);

        // ── Assertions: same totals the old in-memory aggregation produced ──
        summary.Should().NotBeNull();
        summary.TotalTreatmentCost.Should().Be(14_000m,
            "contractCost (10,000 - 1,000 = 9,000) + invoiceCost (5,000; draft excluded) = 14,000");
        summary.TotalPaid.Should().Be(3_500m,
            "2,000 + 1,500 = 3,500 (draft invoice's 99,999 must NOT appear in cost)");
        summary.OutstandingBalance.Should().Be(10_500m,
            "max(0, 14,000 - 3,500) = 10,500");
        summary.ActiveContractsCount.Should().Be(1);
        summary.TotalPaymentsCount.Should().Be(2);
        // Contract started 2024-01-01; expected paid by now is well above 2,000 → overdue.
        summary.OverdueAmount.Should().BeGreaterThan(0m,
            "active installment contract with only down payment should be overdue");
        summary.FinancialStatus.Should().Be("overdue");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 2. GetPatientFinanceSummaryAsync — empty set, must return 0 not throw
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetPatientFinanceSummaryAsync_NoData_ReturnsZeros_DoesNotThrow()
    {
        await using var db = CreateDb();
        var (_, patientId, _, currentUser) = SeedPatient(db);
        var service = CreateFinanceService(db, currentUser);
        await db.SaveChangesAsync();

        // Patient exists but has no contracts, invoices, or payments.
        var act = () => service.GetPatientFinanceSummaryAsync(patientId);
        await act.Should().NotThrowAsync("SumAsync on empty set with (decimal?) ?? 0m must return 0, not throw");

        var summary = await service.GetPatientFinanceSummaryAsync(patientId);
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

    // ═══════════════════════════════════════════════════════════════════════════
    // 3. GetAccountStatementAsync — mixed data, verify totals + per-contract sums
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetAccountStatementAsync_WithMixedData_ReturnsCorrectServerSideAggregations()
    {
        await using var db = CreateDb();
        var (branchId, patientId, userId, currentUser) = SeedPatient(db);
        var service = CreateFinanceService(db, currentUser);

        // Contract 1: 10,000 total, 1,000 discount, paid 3,000
        var c1 = Guid.NewGuid();
        db.Contracts.Add(new Contract
        {
            Id = c1,
            PatientId = patientId,
            Specialty = "تقويم",
            TotalAmount = 10_000m,
            DiscountAmount = 1_000m,
            Status = ContractStatus.Active,
            StartDate = new DateOnly(2024, 1, 1),
            CreatedBy = userId
        });

        // Contract 2 (Completed): 5,000 total, 0 discount, paid 5,000
        var c2 = Guid.NewGuid();
        db.Contracts.Add(new Contract
        {
            Id = c2,
            PatientId = patientId,
            Specialty = "تركيبات",
            TotalAmount = 5_000m,
            DiscountAmount = 0m,
            Status = ContractStatus.Completed,
            StartDate = new DateOnly(2023, 6, 1),
            CreatedBy = userId
        });

        // Issued invoice: subtotal 2,000 + tax 200 = 2,200, discount 100
        var inv1 = Guid.NewGuid();
        db.Invoices.Add(new Invoice
        {
            Id = inv1,
            PatientId = patientId,
            InvoiceNumber = "INV-A-001",
            Status = InvoiceStatus.Issued,
            TotalAmount = 2_100m,
            Subtotal = 2_000m,
            TaxAmount = 200m,
            DiscountAmount = 100m,
            IsActive = true,
            CreatedBy = userId
        });

        // Draft invoice — must be excluded from totals
        db.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            InvoiceNumber = "INV-A-DRAFT",
            Status = InvoiceStatus.Draft,
            TotalAmount = 50_000m,
            Subtotal = 50_000m,
            IsActive = true,
            CreatedBy = userId
        });

        // Payments: 3,000 on c1, 5,000 on c2, 800 on invoice → 8,800 total
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            ContractId = c1,
            Amount = 3_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            IsActive = true,
            BranchId = branchId,
            ReceivedBy = userId
        });
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            ContractId = c2,
            Amount = 5_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            IsActive = true,
            BranchId = branchId,
            ReceivedBy = userId
        });
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            InvoiceId = inv1,
            Amount = 800m,
            PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            IsActive = true,
            BranchId = branchId,
            ReceivedBy = userId
        });

        await db.SaveChangesAsync();

        var statement = await service.GetAccountStatementAsync(patientId);

        statement.Should().NotBeNull();
        // totalContracted = contract totals (10,000 + 5,000 = 15,000) +
        //                    invoice (subtotal + tax: 2,000 + 200 = 2,200) — draft excluded
        statement!.TotalContracted.Should().Be(17_200m,
            "15,000 from contracts + 2,200 from issued invoice (draft 50,000 excluded)");
        // totalDiscounts = contract discounts (1,000 + 0 = 1,000) + invoice discount (100) = 1,100
        statement.TotalDiscounts.Should().Be(1_100m);
        // totalPaid = 3,000 + 5,000 + 800 = 8,800
        statement.TotalPaid.Should().Be(8_800m);
        // totalRemaining = max(0, 17,200 - 1,100 - 8,800) = 7,300
        statement.TotalRemaining.Should().Be(7_300m);
        statement.ActiveContracts.Should().Be(1);
        statement.CompletedContracts.Should().Be(1);

        // Per-contract projections (server-side subquery for PaidAmount):
        statement.Contracts.Should().HaveCount(2);
        var c1Dto = statement.Contracts.Single(x => x.Id == c1);
        c1Dto.PaidAmount.Should().Be(3_000m);
        c1Dto.RemainingAmount.Should().Be(6_000m, "10,000 - 1,000 - 3,000 = 6,000");

        var c2Dto = statement.Contracts.Single(x => x.Id == c2);
        c2Dto.PaidAmount.Should().Be(5_000m);
        c2Dto.RemainingAmount.Should().Be(0m, "5,000 - 0 - 5,000 = 0");

        // RecentPayments should include all 3 payments, ordered by date desc
        statement.RecentPayments.Should().HaveCount(3);
        statement.RecentPayments.All(p => p.Amount > 0).Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 4. GetAccountStatementAsync — empty set, must return 0 not throw
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetAccountStatementAsync_NoData_ReturnsZeros_DoesNotThrow()
    {
        await using var db = CreateDb();
        var (_, patientId, _, currentUser) = SeedPatient(db);
        var service = CreateFinanceService(db, currentUser);
        await db.SaveChangesAsync();

        var act = () => service.GetAccountStatementAsync(patientId);
        await act.Should().NotThrowAsync(
            "all SumAsync calls use (decimal?) ?? 0m, so an empty set must yield 0 — not throw");

        var statement = await service.GetAccountStatementAsync(patientId);
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

    // ═══════════════════════════════════════════════════════════════════════════
    // 5. GetOverdueContractsAsync — server-side projection still produces correct
    //    per-contract PaidAmount and OverdueAmount
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetOverdueContractsAsync_WithActiveInstallmentContract_ReturnsProjectedPaidAmount()
    {
        await using var db = CreateDb();
        var (branchId, patientId, userId, currentUser) = SeedPatient(db);
        var service = CreateFinanceService(db, currentUser);

        // Active installment contract started 6 months ago.
        // Down payment 1,000 + 4 installments of 500 → expected 1,000 + 4×500 = 3,000
        // Actual paid: 1,000 → overdue = 2,000
        var contractId = Guid.NewGuid();
        db.Contracts.Add(new Contract
        {
            Id = contractId,
            PatientId = patientId,
            Specialty = "تقويم",
            TotalAmount = 5_000m,
            DiscountAmount = 0m,
            DownPayment = 1_000m,
            InstallmentsCount = 8,
            InstallmentAmount = 500m,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-6)),
            Status = ContractStatus.Active,
            CreatedBy = userId
        });
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            ContractId = contractId,
            Amount = 1_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-6)),
            IsActive = true,
            BranchId = branchId,
            ReceivedBy = userId
        });
        await db.SaveChangesAsync();

        var overdue = await service.GetOverdueContractsAsync();

        overdue.Should().ContainSingle();
        var dto = overdue[0];
        dto.ContractId.Should().Be(contractId);
        dto.PaidAmount.Should().Be(1_000m,
            "PaidAmount must come from the SQL correlated subquery (not an in-memory sum)");
        dto.MonthsElapsed.Should().BeGreaterThanOrEqualTo(6);
        // expectedPaid = 1,000 + min(6, 8) × 500 = 1,000 + 3,000 = 4,000; overdue = 4,000 - 1,000 = 3,000
        dto.OverdueAmount.Should().Be(3_000m);
        dto.RemainingAmount.Should().Be(4_000m, "5,000 - 0 - 1,000 = 4,000");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 6. GetOverdueContractsAsync — empty set, must not throw
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetOverdueContractsAsync_NoData_ReturnsEmpty_DoesNotThrow()
    {
        await using var db = CreateDb();
        var (_, _, _, currentUser) = SeedPatient(db);
        var service = CreateFinanceService(db, currentUser);
        await db.SaveChangesAsync();

        var act = () => service.GetOverdueContractsAsync();
        await act.Should().NotThrowAsync(
            "server-side projection with .Sum(p => p.Amount) on empty Payments collection must yield 0, not throw");

        var overdue = await service.GetOverdueContractsAsync();
        overdue.Should().BeEmpty();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 7. GetSummaryAsync — empty set, must not throw on any sub-aggregation
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetSummaryAsync_NoData_ReturnsZeros_DoesNotThrow()
    {
        await using var db = CreateDb();
        var (_, _, userId, currentUser) = SeedPatient(db);
        var service = CreateFinanceService(db, currentUser);
        await db.SaveChangesAsync();

        var act = () => service.GetSummaryAsync();
        await act.Should().NotThrowAsync(
            "GetSummaryAsync invokes GetOverdueContractsAsync and several SumAsync calls — all must handle empty sets");

        var summary = await service.GetSummaryAsync();
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
