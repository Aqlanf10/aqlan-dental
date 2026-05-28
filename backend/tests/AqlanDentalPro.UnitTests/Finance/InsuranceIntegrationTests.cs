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
/// اختبارات وحدة لدمج التأمين الطبي (Insurance Claims) مع الفواتير.
/// تشمل: حساب التغطية والتحمل (Co-pay)، تقسيم القيد المحاسبي،
/// تسوية المطالبات، رفض المطالبات عند إلغاء الفاتورة، والتوازن المحاسبي.
/// </summary>
public class InsuranceIntegrationTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>تسجيل جميع بنود القيود المحاسبية التي تم إنشاؤها لغرض التحقق.</summary>
    private sealed class JournalLineCapture
    {
        public List<List<(JournalAccountType, Guid, decimal, decimal, string?)>> AllEntries { get; } = new();
    }

    private static (FinanceService service, Guid branchId, Guid cashierId, JournalLineCapture capture) CreateService(AppDbContext db)
    {
        var branchId = Guid.NewGuid();
        var cashierId = Guid.NewGuid();

        db.Branches.Add(new Branch { Id = branchId, Name = "الفرع الرئيسي" });
        db.Users.Add(new User { Id = cashierId, Username = "cashier1", BranchId = branchId });
        db.SaveChanges();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(cashierId);
        currentUser.SetupGet(c => c.BranchId).Returns(branchId);
        currentUser.SetupGet(c => c.IsAdmin).Returns(true);

        var notifications = new Mock<INotificationService>();
        var logger = new Mock<ILogger<FinanceService>>();
        var commissionService = new Mock<ICommissionService>();
        var journalEntryService = new Mock<IJournalEntryService>();

        var capture = new JournalLineCapture();

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
            .Callback((FinancialDocumentType docType, Guid docId, string desc, DateOnly date, Guid branch, Guid performedBy, Guid? sessionId, Guid? treasuryId, IEnumerable<(JournalAccountType, Guid, decimal, decimal, string?)> lines, CancellationToken ct) =>
            {
                capture.AllEntries.Add(lines.ToList());
            })
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
                        JournalEntryId = entry.Id,
                        AccountType = accountType,
                        AccountId = accountId,
                        Debit = debit,
                        Credit = credit,
                        Description = lineDesc
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
                var originalEntry = db.JournalEntries
                    .Include(e => e.Lines)
                    .FirstOrDefault(e => e.Id == originalId);

                var reversal = new JournalEntry
                {
                    Id = Guid.NewGuid(),
                    EntryNumber = "JE-REV-001",
                    FinancialDocumentId = originalEntry?.FinancialDocumentId ?? Guid.Empty,
                    FinancialDocumentType = originalEntry?.FinancialDocumentType ?? FinancialDocumentType.Invoice,
                    Description = reason,
                    EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    BranchId = originalEntry?.BranchId ?? Guid.Empty,
                    PerformedBy = performedBy,
                    IsPosted = false,
                    IsReversal = true,
                    ReversalOfEntryId = originalId,
                };

                if (originalEntry != null)
                {
                    foreach (var line in originalEntry.Lines)
                    {
                        reversal.Lines.Add(new JournalLine
                        {
                            Id = Guid.NewGuid(),
                            JournalEntryId = reversal.Id,
                            AccountType = line.AccountType,
                            AccountId = line.AccountId,
                            Debit = line.Credit,
                            Credit = line.Debit,
                            Description = line.Description
                        });
                    }
                }

                return reversal;
            });

        var service = new FinanceService(db, currentUser.Object, notifications.Object, logger.Object, commissionService.Object, journalEntryService.Object);
        return (service, branchId, cashierId, capture);
    }

    private static Guid SeedPatient(AppDbContext db, Guid branchId)
    {
        var patientId = Guid.NewGuid();
        db.Patients.Add(new Patient
        {
            Id = patientId,
            PatientNumber = $"P-{Guid.NewGuid().ToString()[..8].ToUpper()}",
            FirstName = "أحمد",
            LastName = "محمد",
            BranchId = branchId
        });
        db.SaveChanges();
        return patientId;
    }

    private static Guid SeedInsuranceCompany(AppDbContext db, decimal defaultCoveragePercentage = 80m)
    {
        var companyId = Guid.NewGuid();
        db.Set<InsuranceCompany>().Add(new InsuranceCompany
        {
            Id = companyId,
            Name = "شركة التأمين الاجتماعي",
            ContactEmail = "claims@insurance.com",
            Phone = "777123456",
            DefaultCoveragePercentage = defaultCoveragePercentage
        });
        db.SaveChanges();
        return companyId;
    }

    private static Guid SeedInvoice(AppDbContext db, Guid patientId, decimal totalAmount, Guid? insuranceCompanyId = null, decimal? coveragePercentage = null)
    {
        var invoiceId = Guid.NewGuid();
        var invoice = new Invoice
        {
            Id = invoiceId,
            InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-001",
            PatientId = patientId,
            Status = InvoiceStatus.Issued,
            Subtotal = totalAmount,
            TotalAmount = totalAmount,
            TaxPercentage = 0,
            TaxAmount = 0,
            Currency = "YER",
            ExchangeRate = 1.0m,
        };

        if (insuranceCompanyId.HasValue)
        {
            var company = db.Set<InsuranceCompany>().Find(insuranceCompanyId.Value)!;
            decimal coveragePercent = coveragePercentage ?? company.DefaultCoveragePercentage;
            decimal coveredAmount = Math.Round(totalAmount * (coveragePercent / 100m), 2);
            decimal patientCoPay = totalAmount - coveredAmount;

            var claim = new InsuranceClaim
            {
                InvoiceId = invoiceId,
                InsuranceCompanyId = insuranceCompanyId.Value,
                PatientId = patientId,
                TotalAmount = totalAmount,
                CoveredAmount = coveredAmount,
                PatientCoPay = patientCoPay,
                Status = ClaimStatus.Pending
            };
            db.Set<InsuranceClaim>().Add(claim);
            invoice.InsuranceClaim = claim;
            invoice.InsuranceClaimId = claim.Id;
        }

        db.Invoices.Add(invoice);
        db.SaveChanges();
        return invoiceId;
    }

    private static void SeedTreasury(AppDbContext db, Guid branchId)
    {
        if (db.Treasuries.Any(t => t.BranchId == branchId)) return;

        db.Treasuries.Add(new Treasury
        {
            Name = "الصندوق الرئيسي",
            BranchId = branchId,
            Type = TreasuryType.Vault,
            Balance = 500_000m
        });
        db.Treasuries.Add(new Treasury
        {
            Name = "حساب بنكي",
            BranchId = branchId,
            Type = TreasuryType.Bank,
            Balance = 1_000_000m
        });
        db.SaveChanges();
    }

    // ─── PostInvoiceIssuedEntryAsync — Split Debit Tests ────────────────

    [Fact]
    public async Task PostInvoiceIssuedEntry_WithInsurance_SplitsDebitBetweenPatientAndInsurance()
    {
        await using var db = CreateContext();
        var (service, branchId, _, capture) = CreateService(db);
        var patientId = SeedPatient(db, branchId);
        var companyId = SeedInsuranceCompany(db, 80m);
        var invoiceId = SeedInvoice(db, patientId, 100_000m, companyId);

        await service.PostInvoiceIssuedEntryAsync(invoiceId);

        capture.AllEntries.Should().HaveCount(1);
        var lines = capture.AllEntries[0];

        // Should have 3 lines: Revenue (credit), PatientReceivable (debit), InsuranceReceivable (debit)
        lines.Should().HaveCount(3);
        lines.Any(l => l.Item1 == JournalAccountType.Revenue && l.Item4 == 100_000m).Should().BeTrue();
        lines.Any(l => l.Item1 == JournalAccountType.PatientReceivable && l.Item3 == 20_000m).Should().BeTrue();
        lines.Any(l => l.Item1 == JournalAccountType.InsuranceReceivable && l.Item3 == 80_000m).Should().BeTrue();
    }

    [Fact]
    public async Task PostInvoiceIssuedEntry_WithoutInsurance_DebitsFullAmountToPatientReceivable()
    {
        await using var db = CreateContext();
        var (service, branchId, _, capture) = CreateService(db);
        var patientId = SeedPatient(db, branchId);
        var invoiceId = SeedInvoice(db, patientId, 50_000m);

        await service.PostInvoiceIssuedEntryAsync(invoiceId);

        capture.AllEntries.Should().HaveCount(1);
        var lines = capture.AllEntries[0];

        // Should have 2 lines: PatientReceivable (debit full), Revenue (credit full)
        lines.Should().HaveCount(2);
        lines.Any(l => l.Item1 == JournalAccountType.PatientReceivable && l.Item3 == 50_000m).Should().BeTrue();
        lines.Any(l => l.Item1 == JournalAccountType.Revenue && l.Item4 == 50_000m).Should().BeTrue();
    }

    [Fact]
    public async Task PostInvoiceIssuedEntry_WithInsurance_BalancedJournalEntry()
    {
        await using var db = CreateContext();
        var (service, branchId, _, capture) = CreateService(db);
        var patientId = SeedPatient(db, branchId);
        var companyId = SeedInsuranceCompany(db, 75m);
        var invoiceId = SeedInvoice(db, patientId, 200_000m, companyId);

        await service.PostInvoiceIssuedEntryAsync(invoiceId);

        capture.AllEntries.Should().HaveCount(1);
        var lines = capture.AllEntries[0];
        var totalDebits = lines.Sum(l => l.Item3);
        var totalCredits = lines.Sum(l => l.Item4);
        totalDebits.Should().Be(totalCredits);
        totalDebits.Should().Be(200_000m);
    }

    [Fact]
    public async Task PostInvoiceIssuedEntry_CoPay_Equals_TotalAmount_Minus_CoveredAmount()
    {
        await using var db = CreateContext();
        var (service, branchId, _, capture) = CreateService(db);
        var patientId = SeedPatient(db, branchId);
        var companyId = SeedInsuranceCompany(db, 60m);
        var invoiceId = SeedInvoice(db, patientId, 150_000m, companyId);

        await service.PostInvoiceIssuedEntryAsync(invoiceId);

        capture.AllEntries.Should().HaveCount(1);
        var lines = capture.AllEntries[0];
        var patientDebit = lines.Where(l => l.Item1 == JournalAccountType.PatientReceivable).Sum(l => l.Item3);
        var insuranceDebit = lines.Where(l => l.Item1 == JournalAccountType.InsuranceReceivable).Sum(l => l.Item3);
        patientDebit.Should().Be(60_000m); // 40% co-pay
        insuranceDebit.Should().Be(90_000m); // 60% coverage
    }

    // ─── Claim Settlement Tests ─────────────────────────────────────────

    [Fact]
    public async Task SettleInsuranceClaim_UpdatesClaimStatus_ToPaid()
    {
        await using var db = CreateContext();
        var (service, branchId, _, _) = CreateService(db);
        var patientId = SeedPatient(db, branchId);
        var companyId = SeedInsuranceCompany(db, 80m);
        SeedTreasury(db, branchId);
        var invoiceId = SeedInvoice(db, patientId, 100_000m, companyId);

        var claim = await db.Set<InsuranceClaim>().FirstAsync(c => c.InvoiceId == invoiceId);

        var result = await service.SettleInsuranceClaimAsync(claim.Id, new SettleInsuranceClaimRequest
        {
            ReferenceNotes = "شيك رقم 12345"
        });

        result.Should().NotBeNull();
        result.Status.Should().Be("Paid");
        result.CoveredAmount.Should().Be(80_000m);
    }

    [Fact]
    public async Task SettleInsuranceClaim_Rejects_AlreadySettledClaim()
    {
        await using var db = CreateContext();
        var (service, branchId, _, _) = CreateService(db);
        var patientId = SeedPatient(db, branchId);
        var companyId = SeedInsuranceCompany(db, 80m);
        SeedTreasury(db, branchId);
        var invoiceId = SeedInvoice(db, patientId, 100_000m, companyId);

        var claim = await db.Set<InsuranceClaim>().FirstAsync(c => c.InvoiceId == invoiceId);

        await service.SettleInsuranceClaimAsync(claim.Id, new SettleInsuranceClaimRequest { ReferenceNotes = "شيك 1" });

        var act = () => service.SettleInsuranceClaimAsync(claim.Id, new SettleInsuranceClaimRequest { ReferenceNotes = "شيك 2" });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*مسددة مسبقاً*");
    }

    [Fact]
    public async Task SettleInsuranceClaim_Rejects_RejectedClaim()
    {
        await using var db = CreateContext();
        var (service, branchId, _, _) = CreateService(db);
        var patientId = SeedPatient(db, branchId);
        var companyId = SeedInsuranceCompany(db, 80m);
        var invoiceId = SeedInvoice(db, patientId, 100_000m, companyId);

        var claim = await db.Set<InsuranceClaim>().FirstAsync(c => c.InvoiceId == invoiceId);
        claim.Status = ClaimStatus.Rejected;
        await db.SaveChangesAsync();

        var act = () => service.SettleInsuranceClaimAsync(claim.Id, new SettleInsuranceClaimRequest());

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*مرفوضة*");
    }

    [Fact]
    public async Task SettleInsuranceClaim_Throws_WhenClaimNotFound()
    {
        await using var db = CreateContext();
        var (service, _, _, _) = CreateService(db);

        var act = () => service.SettleInsuranceClaimAsync(Guid.NewGuid(), new SettleInsuranceClaimRequest());

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*غير موجودة*");
    }

    [Fact]
    public async Task SettleInsuranceClaim_CreatesBalancedJournalEntry()
    {
        await using var db = CreateContext();
        var (service, branchId, _, capture) = CreateService(db);
        var patientId = SeedPatient(db, branchId);
        var companyId = SeedInsuranceCompany(db, 80m);
        SeedTreasury(db, branchId);
        var invoiceId = SeedInvoice(db, patientId, 100_000m, companyId);

        var claim = await db.Set<InsuranceClaim>().FirstAsync(c => c.InvoiceId == invoiceId);

        await service.SettleInsuranceClaimAsync(claim.Id, new SettleInsuranceClaimRequest
        {
            ReferenceNotes = "حوالة بنكية HW-2026-001"
        });

        // Find the settlement entry (should be the last one or the one with Treasury debit)
        var settlementLines = capture.AllEntries.LastOrDefault();
        settlementLines.Should().NotBeNull();
        settlementLines.Should().HaveCount(2);
        settlementLines.Any(l => l.Item1 == JournalAccountType.Treasury && l.Item3 == 80_000m).Should().BeTrue();
        settlementLines.Any(l => l.Item1 == JournalAccountType.InsuranceReceivable && l.Item4 == 80_000m).Should().BeTrue();

        var totalDebits = settlementLines.Sum(l => l.Item3);
        var totalCredits = settlementLines.Sum(l => l.Item4);
        totalDebits.Should().Be(totalCredits);
    }

    [Fact]
    public async Task SettleInsuranceClaim_UpdatesTreasuryBalance()
    {
        await using var db = CreateContext();
        var (service, branchId, _, _) = CreateService(db);
        var patientId = SeedPatient(db, branchId);
        var companyId = SeedInsuranceCompany(db, 80m);
        SeedTreasury(db, branchId);
        var invoiceId = SeedInvoice(db, patientId, 100_000m, companyId);

        var claim = await db.Set<InsuranceClaim>().FirstAsync(c => c.InvoiceId == invoiceId);

        var bankTreasuryBefore = await db.Treasuries
            .FirstAsync(t => t.BranchId == branchId && t.Type == TreasuryType.Bank);
        var balanceBefore = bankTreasuryBefore.Balance;

        await service.SettleInsuranceClaimAsync(claim.Id, new SettleInsuranceClaimRequest());

        var bankTreasuryAfter = await db.Treasuries
            .FirstAsync(t => t.BranchId == branchId && t.Type == TreasuryType.Bank);
        bankTreasuryAfter.Balance.Should().Be(balanceBefore + 80_000m);
    }

    // ─── Custom Coverage Percentage Tests ───────────────────────────────

    [Fact]
    public async Task PostInvoiceIssuedEntry_WithCustomCoverage_OverridesCompanyDefault()
    {
        await using var db = CreateContext();
        var (service, branchId, _, capture) = CreateService(db);
        var patientId = SeedPatient(db, branchId);
        var companyId = SeedInsuranceCompany(db, 80m);
        var invoiceId = SeedInvoice(db, patientId, 100_000m, companyId, coveragePercentage: 50m);

        await service.PostInvoiceIssuedEntryAsync(invoiceId);

        capture.AllEntries.Should().HaveCount(1);
        var lines = capture.AllEntries[0];
        var patientDebit = lines.Where(l => l.Item1 == JournalAccountType.PatientReceivable).Sum(l => l.Item3);
        var insuranceDebit = lines.Where(l => l.Item1 == JournalAccountType.InsuranceReceivable).Sum(l => l.Item3);
        patientDebit.Should().Be(50_000m); // 50% co-pay
        insuranceDebit.Should().Be(50_000m); // 50% coverage
    }

    // ─── 100% Coverage Edge Case ────────────────────────────────────────

    [Fact]
    public async Task PostInvoiceIssuedEntry_With100PercentCoverage_NoPatientDebit()
    {
        await using var db = CreateContext();
        var (service, branchId, _, capture) = CreateService(db);
        var patientId = SeedPatient(db, branchId);
        var companyId = SeedInsuranceCompany(db, 100m);
        var invoiceId = SeedInvoice(db, patientId, 75_000m, companyId);

        await service.PostInvoiceIssuedEntryAsync(invoiceId);

        capture.AllEntries.Should().HaveCount(1);
        var lines = capture.AllEntries[0];
        // Only 2 lines: InsuranceReceivable (debit) + Revenue (credit)
        lines.Should().HaveCount(2);
        lines.Any(l => l.Item1 == JournalAccountType.InsuranceReceivable && l.Item3 == 75_000m).Should().BeTrue();
        lines.Any(l => l.Item1 == JournalAccountType.Revenue && l.Item4 == 75_000m).Should().BeTrue();
        lines.Any(l => l.Item1 == JournalAccountType.PatientReceivable).Should().BeFalse();
    }

    // ─── 0% Coverage Edge Case ──────────────────────────────────────────

    [Fact]
    public async Task PostInvoiceIssuedEntry_WithZeroCoverage_NoInsuranceDebit()
    {
        await using var db = CreateContext();
        var (service, branchId, _, capture) = CreateService(db);
        var patientId = SeedPatient(db, branchId);
        var companyId = SeedInsuranceCompany(db, 0m);
        var invoiceId = SeedInvoice(db, patientId, 75_000m, companyId);

        await service.PostInvoiceIssuedEntryAsync(invoiceId);

        capture.AllEntries.Should().HaveCount(1);
        var lines = capture.AllEntries[0];
        // Only 2 lines: PatientReceivable (debit) + Revenue (credit)
        lines.Should().HaveCount(2);
        lines.Any(l => l.Item1 == JournalAccountType.PatientReceivable && l.Item3 == 75_000m).Should().BeTrue();
        lines.Any(l => l.Item1 == JournalAccountType.Revenue && l.Item4 == 75_000m).Should().BeTrue();
        lines.Any(l => l.Item1 == JournalAccountType.InsuranceReceivable).Should().BeFalse();
    }

    // ─── Full Cycle Test ────────────────────────────────────────────────

    [Fact]
    public async Task FullInsuranceCycle_IssuanceAndSettlement_ProducesBalancedBooks()
    {
        await using var db = CreateContext();
        var (service, branchId, _, capture) = CreateService(db);
        var patientId = SeedPatient(db, branchId);
        var companyId = SeedInsuranceCompany(db, 80m);
        SeedTreasury(db, branchId);
        var invoiceId = SeedInvoice(db, patientId, 100_000m, companyId);

        var claim = await db.Set<InsuranceClaim>().FirstAsync(c => c.InvoiceId == invoiceId);

        // Step 1: Issue invoice → JE1 (PatientReceivable 20K + InsuranceReceivable 80K = Revenue 100K)
        await service.PostInvoiceIssuedEntryAsync(invoiceId);

        // Step 2: Settle claim → JE2 (Treasury 80K = InsuranceReceivable 80K)
        await service.SettleInsuranceClaimAsync(claim.Id, new SettleInsuranceClaimRequest { ReferenceNotes = "حوالة بنكية" });

        // Should have 2 journal entries
        capture.AllEntries.Should().HaveCount(2);

        // Both entries should balance individually
        foreach (var entryLines in capture.AllEntries)
        {
            entryLines.Sum(l => l.Item3).Should().Be(entryLines.Sum(l => l.Item4));
        }

        // Verify claim is now Paid
        var settledClaim = await db.Set<InsuranceClaim>().FindAsync(claim.Id);
        settledClaim!.Status.Should().Be(ClaimStatus.Paid);
    }

    // ─── Claim Auto-Rejection on Invoice Cancellation ──────────────────

    [Fact]
    public async Task ReverseInvoiceIssuedEntry_AutoRejects_PendingInsuranceClaim()
    {
        await using var db = CreateContext();
        var (service, branchId, _, _) = CreateService(db);
        var patientId = SeedPatient(db, branchId);
        var companyId = SeedInsuranceCompany(db, 80m);
        var invoiceId = SeedInvoice(db, patientId, 100_000m, companyId);

        // First: post issuance entry (this creates a mock JE but doesn't persist it to InMemory DB)
        await service.PostInvoiceIssuedEntryAsync(invoiceId);

        // Since the mock doesn't persist JEs to DB, we need to seed one manually
        // so that ReverseInvoiceIssuedEntryAsync can find it and proceed with the reversal
        var originalEntry = new JournalEntry
        {
            Id = Guid.NewGuid(),
            EntryNumber = "JE-TEST-ORIG",
            FinancialDocumentId = invoiceId,
            FinancialDocumentType = FinancialDocumentType.Invoice,
            Description = "إصدار فاتورة تأمين",
            EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            BranchId = branchId,
            PerformedBy = Guid.NewGuid(),
            IsPosted = true,
            IsReversal = false,
        };
        originalEntry.Lines.Add(new JournalLine
        {
            Id = Guid.NewGuid(),
            JournalEntryId = originalEntry.Id,
            AccountType = JournalAccountType.PatientReceivable,
            AccountId = patientId,
            Debit = 20_000m,
            Credit = 0m,
        });
        originalEntry.Lines.Add(new JournalLine
        {
            Id = Guid.NewGuid(),
            JournalEntryId = originalEntry.Id,
            AccountType = JournalAccountType.InsuranceReceivable,
            AccountId = companyId,
            Debit = 80_000m,
            Credit = 0m,
        });
        originalEntry.Lines.Add(new JournalLine
        {
            Id = Guid.NewGuid(),
            JournalEntryId = originalEntry.Id,
            AccountType = JournalAccountType.Revenue,
            AccountId = invoiceId,
            Debit = 0m,
            Credit = 100_000m,
        });
        db.JournalEntries.Add(originalEntry);
        await db.SaveChangesAsync();

        // Now: reverse (simulate invoice cancellation)
        await service.ReverseInvoiceIssuedEntryAsync(invoiceId);

        // Verify the claim was auto-rejected
        var claim = await db.Set<InsuranceClaim>().FirstAsync(c => c.InvoiceId == invoiceId);
        claim.Status.Should().Be(ClaimStatus.Rejected);
        claim.RejectionReason.Should().Contain("إلغاء الفاتورة");
    }

    // ─── Insurance Claim DTO Mapping Tests ──────────────────────────────

    [Fact]
    public async Task SettleInsuranceClaim_ReturnsCorrectDto()
    {
        await using var db = CreateContext();
        var (service, branchId, _, _) = CreateService(db);
        var patientId = SeedPatient(db, branchId);
        var companyId = SeedInsuranceCompany(db, 80m);
        SeedTreasury(db, branchId);
        var invoiceId = SeedInvoice(db, patientId, 100_000m, companyId);

        var claim = await db.Set<InsuranceClaim>().FirstAsync(c => c.InvoiceId == invoiceId);

        var result = await service.SettleInsuranceClaimAsync(claim.Id, new SettleInsuranceClaimRequest
        {
            ReferenceNotes = "شيك رقم CHK-2026-001"
        });

        result.Should().NotBeNull();
        result.Id.Should().Be(claim.Id);
        result.InvoiceId.Should().Be(invoiceId);
        result.InsuranceCompanyId.Should().Be(companyId);
        result.PatientId.Should().Be(patientId);
        result.TotalAmount.Should().Be(100_000m);
        result.CoveredAmount.Should().Be(80_000m);
        result.PatientCoPay.Should().Be(20_000m);
        result.Status.Should().Be("Paid");
    }

    // ─── Rounding Precision Tests ───────────────────────────────────────

    [Fact]
    public async Task PostInvoiceIssuedEntry_WithNonRoundAmount_RoundsCorrectly()
    {
        await using var db = CreateContext();
        var (service, branchId, _, capture) = CreateService(db);
        var patientId = SeedPatient(db, branchId);
        var companyId = SeedInsuranceCompany(db, 70m);
        var invoiceId = SeedInvoice(db, patientId, 99_999m, companyId);

        await service.PostInvoiceIssuedEntryAsync(invoiceId);

        capture.AllEntries.Should().HaveCount(1);
        var lines = capture.AllEntries[0];
        var totalDebits = lines.Sum(l => l.Item3);
        var totalCredits = lines.Sum(l => l.Item4);
        // Debits must equal credits
        totalDebits.Should().Be(totalCredits);
    }
}
