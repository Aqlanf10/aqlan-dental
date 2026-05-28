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
/// Hotfix tests: Payment 400 — Branch resolution from cashier session.
/// Admin users without a BranchId claim can create payments if they have
/// an active cashier session with a valid BranchId.
/// The cashier session's BranchId is the authoritative source.
/// </summary>
public class PaymentBranchSessionResolutionTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static (Guid branchId, Guid cashierId, Guid patientId) SeedBranchAndUser(
        AppDbContext db, bool userHasBranch = true)
    {
        var branchId = Guid.NewGuid();
        var cashierId = Guid.NewGuid();
        var patientId = Guid.NewGuid();

        db.Branches.Add(new Branch
        {
            Id = branchId,
            Name = "الفرع الرئيسي"
        });

        db.Users.Add(new User
        {
            Id = cashierId,
            Username = "cashier1",
            BranchId = userHasBranch ? branchId : (Guid?)null
        });

        db.Patients.Add(new Patient
        {
            Id = patientId,
            FirstName = "مريض",
            LastName = "تجريبي",
            PatientNumber = $"P-{Guid.NewGuid().ToString()[..8]}",
            BranchId = branchId
        });

        db.SaveChanges();
        return (branchId, cashierId, patientId);
    }

    private static CashierSession CreateOpenSession(AppDbContext db, Guid cashierId, Guid branchId,
        decimal openingBalance = 100_000m)
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
            Status = SessionStatus.Open
        };
        db.CashierSessions.Add(session);
        db.SaveChanges();
        return session;
    }

    private static (FinanceService service, Mock<ICurrentUserService> currentUserMock) CreateService(
        AppDbContext db, Guid userId, Guid? userBranchId = null, bool isAdmin = false)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(userId);
        currentUser.SetupGet(c => c.BranchId).Returns(userBranchId);
        currentUser.SetupGet(c => c.IsAdmin).Returns(isAdmin);

        var notifications = new Mock<INotificationService>();
        var logger = new Mock<ILogger<FinanceService>>();
        var commissionService = new Mock<ICommissionService>();

        var journalEntryService = new Mock<IJournalEntryService>();
        journalEntryService.Setup(s => s.CreateEntryAsync(
            It.IsAny<FinancialDocumentType>(),
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<DateOnly>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid?>(),
            It.IsAny<Guid?>(),
            It.IsAny<IEnumerable<(JournalAccountType, Guid, decimal, decimal, string?)>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((FinancialDocumentType docType, Guid docId, string desc, DateOnly date, Guid branch, Guid performedBy, Guid? sessionId, Guid? treasuryId, IEnumerable<(JournalAccountType, Guid, decimal, decimal, string?)> lines, CancellationToken ct) =>
            {
                var entry = new JournalEntry
                {
                    Id = Guid.NewGuid(),
                    EntryNumber = "JE-TEST-001",
                    FinancialDocumentId = docId,
                    FinancialDocumentType = docType,
                    Description = desc,
                    EntryDate = date,
                    BranchId = branch,
                    PerformedBy = performedBy,
                    CashierSessionId = sessionId,
                    TreasuryId = treasuryId,
                    IsPosted = false,
                    IsReversal = false,
                };
                foreach (var (accountType, accountId, debit, credit, lineDesc) in lines)
                {
                    entry.Lines.Add(new JournalLine
                    {
                        Id = Guid.NewGuid(),
                        AccountType = accountType,
                        AccountId = accountId,
                        Debit = debit,
                        Credit = credit,
                        Description = lineDesc,
                        BranchId = branch,
                    });
                }
                return entry;
            });
        journalEntryService.Setup(s => s.CreateReversalEntryAsync(
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
                    FinancialDocumentId = Guid.NewGuid(),
                    FinancialDocumentType = FinancialDocumentType.PaymentDeletion,
                    Description = $"Reversal: {reason}",
                    EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    BranchId = Guid.Empty,
                    PerformedBy = performedBy,
                    IsPosted = false,
                    IsReversal = true,
                    ReversalOfEntryId = originalId,
                };
            });

        var service = new FinanceService(db, currentUser.Object, notifications.Object, logger.Object, commissionService.Object, journalEntryService.Object);
        return (service, currentUser);
    }

    // ─── Test 1: Admin without currentUser.BranchId but with active session.BranchId can create payment ───

    [Fact]
    public async Task CreatePaymentAsync_AdminWithoutBranchClaim_ButSessionHasBranch_Succeeds()
    {
        await using var db = CreateContext();
        var (branchId, cashierId, patientId) = SeedBranchAndUser(db, userHasBranch: false);

        // Admin has no branch in token, but opens session with branch selected
        var session = CreateOpenSession(db, cashierId, branchId);
        var (service, _) = CreateService(db, cashierId, userBranchId: null, isAdmin: true);

        var result = await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patientId,
            Amount = 5_000m,
            PaymentMethod = "cash"
        });

        result.Should().NotBeNull();
        result.Amount.Should().Be(5_000m);

        // Verify Payment.BranchId is set from the session, not the user claim
        var payment = await db.Payments.FirstAsync(p => p.Id == result.Id);
        payment.BranchId.Should().Be(branchId);

        // Verify CashFlowTransaction.BranchId is also set from session
        var cashflow = await db.CashFlowTransactions.FirstAsync(c => c.ReferenceId == payment.Id);
        cashflow.BranchId.Should().Be(branchId);
    }

    // ─── Test 2: User without BranchId and without active session cannot create payment ───

    [Fact]
    public async Task CreatePaymentAsync_NoBranchIdAndNoActiveSession_ThrowsWithArabicMessage()
    {
        await using var db = CreateContext();
        var (branchId, cashierId, patientId) = SeedBranchAndUser(db, userHasBranch: false);

        // No session opened
        var (service, _) = CreateService(db, cashierId, userBranchId: null, isAdmin: false);

        var act = () => service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patientId,
            Amount = 5_000m,
            PaymentMethod = "cash"
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*وردية*");
    }

    // ─── Test 3: Active session with empty BranchId fails with clear Arabic message ───

    [Fact]
    public async Task CreatePaymentAsync_SessionWithEmptyBranchId_ThrowsWithArabicMessage()
    {
        await using var db = CreateContext();
        var (branchId, cashierId, patientId) = SeedBranchAndUser(db, userHasBranch: false);

        // Open session with empty branch (corrupted data)
        var session = CreateOpenSession(db, cashierId, Guid.Empty);
        var (service, _) = CreateService(db, cashierId, userBranchId: null, isAdmin: true);

        var act = () => service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patientId,
            Amount = 5_000m,
            PaymentMethod = "cash"
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*فرع الوردية*");
    }

    // ─── Test 4: Payment uses activeSession.BranchId for Payment and CashFlow ───

    [Fact]
    public async Task CreatePaymentAsync_SessionBranchId_UsedForPaymentAndCashFlow()
    {
        await using var db = CreateContext();
        var (branchId, cashierId, patientId) = SeedBranchAndUser(db, userHasBranch: true);

        var session = CreateOpenSession(db, cashierId, branchId);
        var (service, _) = CreateService(db, cashierId, userBranchId: branchId, isAdmin: false);

        var result = await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patientId,
            Amount = 10_000m,
            PaymentMethod = "cash"
        });

        var payment = await db.Payments.FirstAsync(p => p.Id == result.Id);
        payment.BranchId.Should().Be(branchId);

        var cashflow = await db.CashFlowTransactions.FirstAsync(c => c.ReferenceId == payment.Id);
        cashflow.BranchId.Should().Be(branchId);
        cashflow.CashierSessionId.Should().Be(session.Id);
    }

    // ─── Test 5: Existing Reception user with BranchId still creates payment successfully ───

    [Fact]
    public async Task CreatePaymentAsync_ReceptionUserWithBranchId_SucceedsAsBefore()
    {
        await using var db = CreateContext();
        var (branchId, cashierId, patientId) = SeedBranchAndUser(db, userHasBranch: true);

        var session = CreateOpenSession(db, cashierId, branchId);
        var (service, _) = CreateService(db, cashierId, userBranchId: branchId, isAdmin: false);

        var result = await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patientId,
            Amount = 3_000m,
            PaymentMethod = "card"
        });

        result.Should().NotBeNull();
        result.Amount.Should().Be(3_000m);
        result.PaymentMethod.Should().Be("card");
    }

    // ─── Test 6: Amount <= 0 still fails ───

    [Fact]
    public async Task CreatePaymentAsync_ZeroAmount_ThrowsWithArabicMessage()
    {
        await using var db = CreateContext();
        var (branchId, cashierId, patientId) = SeedBranchAndUser(db, userHasBranch: true);

        var session = CreateOpenSession(db, cashierId, branchId);
        var (service, _) = CreateService(db, cashierId, userBranchId: branchId, isAdmin: false);

        var act = () => service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patientId,
            Amount = 0m,
            PaymentMethod = "cash"
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*أكبر من الصفر*");
    }

    [Fact]
    public async Task CreatePaymentAsync_NegativeAmount_ThrowsWithArabicMessage()
    {
        await using var db = CreateContext();
        var (branchId, cashierId, patientId) = SeedBranchAndUser(db, userHasBranch: true);

        var session = CreateOpenSession(db, cashierId, branchId);
        var (service, _) = CreateService(db, cashierId, userBranchId: branchId, isAdmin: false);

        var act = () => service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patientId,
            Amount = -500m,
            PaymentMethod = "cash"
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*أكبر من الصفر*");
    }

    // ─── Test 7: No active shift still fails ───

    [Fact]
    public async Task CreatePaymentAsync_NoActiveShift_ThrowsWithArabicMessage()
    {
        await using var db = CreateContext();
        var (branchId, cashierId, patientId) = SeedBranchAndUser(db, userHasBranch: true);

        // No session opened
        var (service, _) = CreateService(db, cashierId, userBranchId: branchId, isAdmin: false);

        var act = () => service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patientId,
            Amount = 5_000m,
            PaymentMethod = "cash"
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*وردية*");
    }

    // ─── Test 8: Admin with BranchId claim + session with same branch — session branch wins ───

    [Fact]
    public async Task CreatePaymentAsync_AdminWithBranchId_SessionBranchTakesPriority()
    {
        await using var db = CreateContext();
        var branchId1 = Guid.NewGuid();
        var branchId2 = Guid.NewGuid();
        var cashierId = Guid.NewGuid();
        var patientId = Guid.NewGuid();

        // Two branches
        db.Branches.AddRange(
            new Branch { Id = branchId1, Name = "الفرع ١" },
            new Branch { Id = branchId2, Name = "الفرع ٢" }
        );

        // User's token has branchId1, but session opened with branchId2
        db.Users.Add(new User { Id = cashierId, Username = "admin1", BranchId = branchId1 });
        db.Patients.Add(new Patient
        {
            Id = patientId,
            FirstName = "مريض",
            LastName = "تجريبي",
            PatientNumber = $"P-{Guid.NewGuid().ToString()[..8]}",
            BranchId = branchId2
        });
        db.SaveChanges();

        var session = CreateOpenSession(db, cashierId, branchId2);
        var (service, _) = CreateService(db, cashierId, userBranchId: branchId1, isAdmin: true);

        var result = await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patientId,
            Amount = 7_000m,
            PaymentMethod = "cash"
        });

        // Payment.BranchId should match the session's branch (branchId2), not the user's claim
        var payment = await db.Payments.FirstAsync(p => p.Id == result.Id);
        payment.BranchId.Should().Be(branchId2);

        var cashflow = await db.CashFlowTransactions.FirstAsync(c => c.ReferenceId == payment.Id);
        cashflow.BranchId.Should().Be(branchId2);
    }
}
