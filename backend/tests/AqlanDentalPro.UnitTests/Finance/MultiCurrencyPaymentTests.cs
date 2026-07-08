using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Validators;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Finance;

/// <summary>
/// MULTI-CURRENCY (Task ID: MULTI-CURRENCY) — tests for YER/SAR/USD payment support.
///
/// Covers:
///   1. CreatePayment_WithSAR_PersistsCurrency — SAR payment is recorded with Currency="SAR".
///   2. CreatePayment_WithoutCurrency_DefaultsToYER — null Currency defaults to "YER".
///   3. CreatePayment_InvalidCurrency_Rejected — "EUR" is rejected by the FluentValidation validator with an Arabic error.
///   4. TreasurySum_ExcludesForeignCurrency — YER 5000 + SAR 3000 leaves treasury balance at 5000 (not 8000).
///   5. DashboardSum_ExcludesForeignCurrency — todayCollected = YER payments only.
///
/// Strategy: uses the in-memory provider + the same FinanceService wiring as
/// FinanceV2Phase0BTests. The validator test (#3) instantiates
/// CreatePaymentRequestValidator directly because FluentValidation auto-validation
/// is wired at the API pipeline level (not visible to unit tests).
/// </summary>
public class MultiCurrencyPaymentTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static (Guid branchId, Guid cashierId) SeedBranchAndUser(AppDbContext db)
    {
        var branchId = Guid.NewGuid();
        var cashierId = Guid.NewGuid();

        db.Branches.Add(new Branch { Id = branchId, Name = "الفرع الرئيسي" });
        db.Users.Add(new User { Id = cashierId, Username = "cashier1", BranchId = branchId });
        db.SaveChanges();
        return (branchId, cashierId);
    }

    private static CashierSession CreateOpenSession(AppDbContext db, Guid cashierId, Guid branchId, decimal openingBalance = 100_000m)
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

    private static (PaymentService service, FinanceReadService readService, Guid branchId, Guid cashierId) CreateService(AppDbContext db)
    {
        var (branchId, cashierId) = SeedBranchAndUser(db);

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(cashierId);
        currentUser.SetupGet(c => c.BranchId).Returns(branchId);
        currentUser.SetupGet(c => c.IsAdmin).Returns(true);

        var notifications = new Mock<INotificationService>();
        var logger = new Mock<ILogger<PaymentService>>();
        var commissionService = new Mock<ICommissionService>();

        var journalEntryService = new Mock<IJournalEntryService>();
        // Mock CreateEntryAsync so DualWritePaymentEntryAsync works for YER payments.
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
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((FinancialDocumentType docType, Guid docId, string desc, DateOnly date, Guid branch, Guid performedBy, Guid? sessionId, Guid? treasuryId, IEnumerable<(JournalAccountType, Guid, decimal, decimal, string?)> lines, bool autoSave, CancellationToken ct) =>
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
                new JournalEntry
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
                });

        var service = new PaymentService(db, currentUser.Object, notifications.Object, logger.Object, commissionService.Object, journalEntryService.Object);
        // TD-021 PR A2: read-side finance service extracted from FinanceService.
        var readService = new FinanceReadService(db, currentUser.Object);
        return (service, readService, branchId, cashierId);
    }

    private static Patient SeedPatient(AppDbContext db, Guid branchId)
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            FirstName = "مريض",
            LastName = "تجريبي",
            PatientNumber = $"P-{Guid.NewGuid().ToString()[..8]}",
            BranchId = branchId
        };
        db.Patients.Add(patient);
        db.SaveChanges();
        return patient;
    }

    // ─── Test 1: SAR payment persists Currency="SAR" ──────────────────────────

    [Fact]
    public async Task CreatePayment_WithSAR_PersistsCurrency()
    {
        await using var db = CreateContext();
        var (service, readService, branchId, cashierId) = CreateService(db);
        var patient = SeedPatient(db, branchId);
        CreateOpenSession(db, cashierId, branchId);

        var dto = await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 300m,
            PaymentMethod = "cash",
            Currency = "SAR",
            AccountCurrency = "SAR"
        });

        dto.Should().NotBeNull();
        dto.Currency.Should().Be("SAR");
        dto.AccountCurrency.Should().Be("SAR");
        dto.AppliedAmount.Should().Be(300m);

        // Verify it was actually persisted to the database.
        var payment = await db.Payments.FindAsync(dto.Id);
        payment.Should().NotBeNull();
        payment!.Currency.Should().Be("SAR");

        // MULTI-CURRENCY: foreign payments create a CashFlowTransaction in their
        // physical currency, but never mix with the YER drawer.
        var cashflow = await db.CashFlowTransactions
            .FirstOrDefaultAsync(c => c.ReferenceId == dto.Id);
        cashflow.Should().NotBeNull("foreign-currency payments must be tracked in the matching currency treasury");
        cashflow!.Currency.Should().Be("SAR");
        cashflow.Amount.Should().Be(300m);
    }

    // ─── Test 2: null Currency defaults to "YER" ──────────────────────────────

    [Fact]
    public async Task CreatePayment_WithoutCurrency_DefaultsToYER()
    {
        await using var db = CreateContext();
        var (service, readService, branchId, cashierId) = CreateService(db);
        var patient = SeedPatient(db, branchId);
        CreateOpenSession(db, cashierId, branchId);

        var dto = await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 5_000m,
            PaymentMethod = "cash"
            // Currency intentionally null — must default to "YER"
        });

        dto.Should().NotBeNull();
        dto.Currency.Should().Be("YER");

        var payment = await db.Payments.FindAsync(dto.Id);
        payment.Should().NotBeNull();
        payment!.Currency.Should().Be("YER");

        // YER payments DO create a CashFlowTransaction (unchanged behavior).
        var cashflow = await db.CashFlowTransactions
            .FirstOrDefaultAsync(c => c.ReferenceId == dto.Id);
        cashflow.Should().NotBeNull("YER payments must create a CashFlowTransaction (unchanged behavior)");
    }

    // ─── Test 3: invalid currency is rejected with an Arabic error ────────────

    [Fact]
    public void CreatePayment_InvalidCurrency_Rejected()
    {
        // FluentValidation auto-validation runs at the API pipeline level (Program.cs
        // wires AddFluentValidationAutoValidation). Unit tests instantiate the validator
        // directly to verify the rule. The controller catches ArgumentException +
        // returns BadRequest with the Arabic message — verified via the validator output.
        var validator = new CreatePaymentRequestValidator();

        var req = new CreatePaymentRequest
        {
            PatientId = Guid.NewGuid(),
            Amount = 100m,
            PaymentMethod = "cash",
            Currency = "EUR" // not in {YER, SAR, USD}
        };

        var result = validator.Validate(req);

        result.IsValid.Should().BeFalse("EUR is not a supported currency");
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreatePaymentRequest.Currency));
        var error = result.Errors.First(e => e.PropertyName == nameof(CreatePaymentRequest.Currency));
        error.ErrorMessage.Should().Contain("العملة يجب أن تكون YER أو SAR أو USD");
        error.ErrorMessage.Should().Contain("YER");
        error.ErrorMessage.Should().Contain("SAR");
        error.ErrorMessage.Should().Contain("USD");
    }

    // ─── Test 4: treasury balance excludes foreign-currency payments ──────────

    [Fact]
    public async Task TreasurySum_ExcludesForeignCurrency()
    {
        await using var db = CreateContext();
        var (service, readService, branchId, cashierId) = CreateService(db);
        var patient = SeedPatient(db, branchId);
        CreateOpenSession(db, cashierId, branchId);

        // Create a YER 5,000 payment → treasury balance += 5,000
        await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 5_000m,
            PaymentMethod = "cash",
            Currency = "YER"
        });

        // Create a SAR 3,000 payment → treasury balance MUST NOT change
        await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 3_000m,
            PaymentMethod = "cash",
            Currency = "SAR",
            AccountCurrency = "SAR"
        });

        // The YER treasury for this branch should hold 5,000 — NOT 8,000.
        var treasury = await db.Treasuries
            .FirstOrDefaultAsync(t => t.BranchId == branchId && t.Type == TreasuryType.Vault && t.Currency == "YER" && t.IsActive);
        treasury.Should().NotBeNull("a YER vault treasury should have been auto-created for the cash payment");
        treasury!.Balance.Should().Be(5_000m, "SAR 3,000 must be excluded from YER treasury balance — no mixing currencies");

        var sarTreasury = await db.Treasuries
            .FirstOrDefaultAsync(t => t.BranchId == branchId && t.Type == TreasuryType.Vault && t.Currency == "SAR" && t.IsActive);
        sarTreasury.Should().NotBeNull("a separate SAR vault treasury should track physical SAR cash");
        sarTreasury!.Balance.Should().Be(3_000m);

        // Also verify the total via a direct Payment sum filtered to YER only.
        var yerSum = await db.Payments
            .Where(p => p.PatientId == patient.Id && p.IsActive && (p.Currency == null || p.Currency == "YER"))
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;
        yerSum.Should().Be(5_000m, "YER-only sum must exclude the SAR payment");
    }

    // ─── Test 5: dashboard todayCollected excludes foreign-currency payments ─

    [Fact]
    public async Task DashboardSum_ExcludesForeignCurrency()
    {
        await using var db = CreateContext();
        var (service, readService, branchId, cashierId) = CreateService(db);
        var patient = SeedPatient(db, branchId);
        CreateOpenSession(db, cashierId, branchId);

        // Today's YER payment — counts toward todayCollected.
        await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 5_000m,
            PaymentMethod = "cash",
            Currency = "YER"
        });

        // Today's SAR payment — must NOT count toward todayCollected.
        await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 3_000m,
            PaymentMethod = "cash",
            Currency = "SAR",
            AccountCurrency = "SAR"
        });

        // Today's USD payment — must NOT count toward todayCollected.
        await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 100m,
            PaymentMethod = "cash",
            Currency = "USD",
            AccountCurrency = "USD"
        });

        var summary = await readService.GetSummaryAsync();

        // todayCollected should be 5,000 — not 8,100 (5,000 + 3,000 + 100).
        summary.TodayCollected.Should().Be(5_000m,
            "dashboard todayCollected is YER-only — SAR/USD payments are RECORDED but excluded from YER totals");
        summary.MonthCollected.Should().Be(5_000m,
            "dashboard monthCollected is YER-only — same exclusion applies");
    }
}
