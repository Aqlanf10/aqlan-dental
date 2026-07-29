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

namespace AqlanDentalPro.UnitTests.Finance;

/// <summary>
/// Task #6 follow-up (test gap) — verifies CloseSession reconciliation behavior once the
/// Phase 0B heuristic backward-compat pass (PerformedBy + timestamp matching) was removed.
///
/// Every cashier-scoped CashFlowTransaction is expected to be linked to its
/// CashierSessionId at creation time. If one somehow slips through unlinked, CloseSession
/// must NOT heuristically re-attribute it to whichever session happens to be closing —
/// doing so risks misattributing a transaction from a different/overlapping session to the
/// wrong shift's reconciliation totals. Instead, the orphan should be excluded from the
/// closing totals and only surfaced via a warning log for investigation.
/// </summary>
public class CashierSessionOrphanedTransactionTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static (Guid branchId, Guid cashierId) SeedBranchAndCashier(AppDbContext db)
    {
        var branchId = Guid.NewGuid();
        var cashierId = Guid.NewGuid();

        db.Branches.Add(new Branch { Id = branchId, Name = "الفرع الرئيسي" });
        db.Users.Add(new User { Id = cashierId, Username = "cashier1", BranchId = branchId, Role = UserRole.Admin });
        db.SaveChanges();

        return (branchId, cashierId);
    }

    private static Treasury SeedVault(AppDbContext db, Guid branchId, decimal balance = 500_000m)
    {
        var vault = new Treasury
        {
            Id = Guid.NewGuid(),
            Name = "درج كاشير",
            Type = TreasuryType.Vault,
            Balance = balance,
            BranchId = branchId,
            IsActive = true
        };
        db.Treasuries.Add(vault);
        db.SaveChanges();
        return vault;
    }

    private static CashierSession CreateOpenSession(AppDbContext db, Guid cashierId, Guid branchId, Guid treasuryId, decimal openingBalance)
    {
        var session = new CashierSession
        {
            SessionNumber = $"CS-{DateTime.UtcNow:yyyyMMdd}-01",
            CashierId = cashierId,
            BranchId = branchId,
            OpeningTime = DateTime.UtcNow.AddHours(-2),
            OpeningBalance = openingBalance,
            ExpectedClosingCash = openingBalance,
            ExpectedClosingCard = 0,
            ExpectedClosingBank = 0,
            Status = SessionStatus.Open,
            TreasuryId = treasuryId
        };
        db.CashierSessions.Add(session);
        db.SaveChanges();
        return session;
    }

    private static ICurrentUserService CreateCashierCurrentUser(Guid userId, Guid branchId)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.SetupGet(c => c.UserId).Returns(userId);
        mock.SetupGet(c => c.BranchId).Returns(branchId);
        mock.SetupGet(c => c.IsAdmin).Returns(true);
        mock.SetupGet(c => c.IsAuthenticated).Returns(true);
        mock.SetupGet(c => c.Role).Returns(UserRole.Admin);
        return mock.Object;
    }

    private static CashFlowTransaction AddCashInflow(AppDbContext db, Guid branchId, Guid performedBy, Guid? cashierSessionId, decimal amount, Guid? treasuryId = null)
    {
        var tx = new CashFlowTransaction
        {
            TransactionNumber = $"TX-TEST-{Guid.NewGuid().ToString()[..8]}",
            Type = TransactionType.Inflow,
            Category = FinancialCategory.PatientPayment,
            Amount = amount,
            Currency = "YER",
            PaymentMethod = "cash",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            Description = "دفعة نقدية تجريبية",
            PerformedBy = performedBy,
            BranchId = branchId,
            CashierSessionId = cashierSessionId,
            TreasuryId = treasuryId,
            IsActive = true
        };
        db.CashFlowTransactions.Add(tx);
        db.SaveChanges();
        return tx;
    }

    [Fact]
    public async Task CloseSession_OrphanedTransaction_IsExcludedFromExpectedTotals_AndNotReattributed()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndCashier(db);
        var vault = SeedVault(db, branchId);
        var session = CreateOpenSession(db, cashierId, branchId, vault.Id, openingBalance: 50_000m);

        // A transaction correctly linked to the session at creation time.
        var linkedTx = AddCashInflow(db, branchId, cashierId, session.Id, 20_000m, vault.Id);

        // An orphaned transaction: same cashier, created within the session window, but never
        // linked to a session at creation time (simulates a gap in a creation call site).
        var orphanTx = AddCashInflow(db, branchId, cashierId, cashierSessionId: null, amount: 100_000m);

        var currentUser = CreateCashierCurrentUser(cashierId, branchId);
        var audit = new Mock<IAuditService>().Object;
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        var logger = new Mock<ILogger<CashierSessionsController>>();

        var controller = new CashierSessionsController(db, currentUser, audit, treasuryResolution, logger.Object);

        // Actual cash counted only matches the opening balance + the correctly linked transaction.
        var expectedCash = session.OpeningBalance + linkedTx.Amount;

        var result = await controller.CloseSession(new CloseSessionRequest
        {
            ActualClosingCash = expectedCash,
            ActualClosingCard = 0,
            ActualClosingBank = 0
        });

        result.Should().BeOfType<OkObjectResult>("closing must succeed even when an orphaned transaction exists");

        var closedSession = await db.CashierSessions.FirstAsync(s => s.Id == session.Id);

        closedSession.ExpectedClosingCash.Should().Be(expectedCash,
            "expected closing cash must reflect only the correctly linked transaction, not the orphan");
        closedSession.ShortageOrSurplus.Should().Be(0m,
            "no shortage/surplus should be reported when the actual count matches the correctly linked total");

        // The orphaned transaction must NOT have been heuristically re-attributed to this session.
        var reloadedOrphan = await db.CashFlowTransactions.FirstAsync(t => t.Id == orphanTx.Id);
        reloadedOrphan.CashierSessionId.Should().BeNull(
            "an orphaned transaction must never be silently re-attributed to whichever session happens to close next");

        // The linked transaction remains correctly attributed.
        var reloadedLinked = await db.CashFlowTransactions.FirstAsync(t => t.Id == linkedTx.Id);
        reloadedLinked.CashierSessionId.Should().Be(session.Id);
    }

    [Fact]
    public async Task CloseSession_NoOrphans_ReconciliationUnaffected()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndCashier(db);
        var vault = SeedVault(db, branchId);
        var session = CreateOpenSession(db, cashierId, branchId, vault.Id, openingBalance: 30_000m);

        var linkedTx = AddCashInflow(db, branchId, cashierId, session.Id, 5_000m, vault.Id);

        var currentUser = CreateCashierCurrentUser(cashierId, branchId);
        var audit = new Mock<IAuditService>().Object;
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        var logger = new Mock<ILogger<CashierSessionsController>>();

        var controller = new CashierSessionsController(db, currentUser, audit, treasuryResolution, logger.Object);

        var expectedCash = session.OpeningBalance + linkedTx.Amount;

        var result = await controller.CloseSession(new CloseSessionRequest
        {
            ActualClosingCash = expectedCash,
            ActualClosingCard = 0,
            ActualClosingBank = 0
        });

        result.Should().BeOfType<OkObjectResult>();

        var closedSession = await db.CashierSessions.FirstAsync(s => s.Id == session.Id);
        closedSession.ExpectedClosingCash.Should().Be(expectedCash);
        closedSession.ShortageOrSurplus.Should().Be(0m);
    }
}
