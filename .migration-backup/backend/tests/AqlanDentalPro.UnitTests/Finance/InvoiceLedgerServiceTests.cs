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
/// TD-021 PR A1 — characterization tests for the InvoiceLedgerService extraction.
///
/// Purpose: lock in the contract of the two methods moved out of FinanceService so any
/// future refactor (e.g. PR A2–A4 of the same plan) cannot silently regress the accrual
/// journal behavior. The previous coverage lived spread across FinanceV3* tests through
/// the FinanceService facade; this file names the contract directly against the new
/// IInvoiceLedgerService surface.
///
/// Behavior preserved byte-for-byte from FinanceService (see git blame on the moved
/// methods for the prior art):
///   - PostInvoiceIssuedEntryAsync: Debit PatientReceivable / Credit Revenue, auto-posted
///   - ReverseInvoiceIssuedEntryAsync: mirrored reversal of the original issuance JE,
///     auto-posted; logs a warning and returns silently if no original exists
/// </summary>
public class InvoiceLedgerServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static (Guid branchId, Guid userId) SeedBranchAndUser(AppDbContext db)
    {
        var branchId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        db.Branches.Add(new Branch { Id = branchId, Name = "الفرع الرئيسي" });
        db.Users.Add(new User { Id = userId, Username = "admin1", BranchId = branchId, Role = UserRole.Admin });
        db.SaveChanges();
        return (branchId, userId);
    }

    private static Patient SeedPatient(AppDbContext db, Guid branchId)
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            FirstName = "مريض",
            LastName = "اختبار",
            PatientNumber = $"P-{Guid.NewGuid():N}"[..20],
            BranchId = branchId
        };
        db.Patients.Add(patient);
        db.SaveChanges();
        return patient;
    }

    private static InvoiceLedgerService CreateService(AppDbContext db, Guid userId,
        out JournalEntryService jeService)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(userId);
        currentUser.SetupGet(c => c.BranchId).Returns((Guid?)null);
        currentUser.SetupGet(c => c.IsAdmin).Returns(true);

        jeService = new JournalEntryService(db, new Mock<ILogger<JournalEntryService>>().Object);
        return new InvoiceLedgerService(
            db, currentUser.Object, jeService,
            new Mock<ILogger<InvoiceLedgerService>>().Object);
    }

    // ─── Issuance ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task PostInvoiceIssuedEntry_CreatesBalancedPostedJournalEntry_DebitReceivable_CreditRevenue()
    {
        await using var db = CreateContext();
        var (branchId, userId) = SeedBranchAndUser(db);
        var patient = SeedPatient(db, branchId);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-ILED-001",
            Status = InvoiceStatus.Issued,
            TotalAmount = 75_000m,
            Subtotal = 75_000m,
            CreatedBy = userId
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var svc = CreateService(db, userId, out _);

        await svc.PostInvoiceIssuedEntryAsync(invoice.Id);

        var je = await db.JournalEntries
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.FinancialDocumentId == invoice.Id
                && e.FinancialDocumentType == FinancialDocumentType.Invoice
                && !e.IsReversal);

        je.Should().NotBeNull("issuance must create a JE");
        je!.IsPosted.Should().BeTrue("issuance JE must be auto-posted");
        je.PostedAt.Should().NotBeNull();
        je.BranchId.Should().Be(branchId, "issuance JE must inherit the patient's branch");

        var receivable = je.Lines.SingleOrDefault(l => l.AccountType == JournalAccountType.PatientReceivable);
        var revenue = je.Lines.SingleOrDefault(l => l.AccountType == JournalAccountType.Revenue);
        receivable.Should().NotBeNull();
        receivable!.Debit.Should().Be(75_000m);
        receivable.Credit.Should().Be(0m);
        revenue.Should().NotBeNull();
        revenue!.Credit.Should().Be(75_000m);
        revenue.Debit.Should().Be(0m);

        // Balanced entry
        je.Lines.Sum(l => l.Debit).Should().Be(je.Lines.Sum(l => l.Credit));
    }

    [Fact]
    public async Task PostInvoiceIssuedEntry_WhenInvoiceNotInIssuedStatus_ThrowsArgumentException()
    {
        await using var db = CreateContext();
        var (branchId, userId) = SeedBranchAndUser(db);
        var patient = SeedPatient(db, branchId);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-ILED-DRAFT",
            Status = InvoiceStatus.Draft, // not Issued — must be rejected
            TotalAmount = 30_000m,
            Subtotal = 30_000m,
            CreatedBy = userId
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var svc = CreateService(db, userId, out _);

        var act = () => svc.PostInvoiceIssuedEntryAsync(invoice.Id);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*not in Issued status*");
    }

    [Fact]
    public async Task PostInvoiceIssuedEntry_WhenInvoiceMissing_ThrowsArgumentException()
    {
        await using var db = CreateContext();
        var (_, userId) = SeedBranchAndUser(db);
        var svc = CreateService(db, userId, out _);

        var act = () => svc.PostInvoiceIssuedEntryAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*not found*");
    }

    // ─── Reversal ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReverseInvoiceIssuedEntry_CreatesPostedReversal_ThatNetsOriginalToZero()
    {
        await using var db = CreateContext();
        var (branchId, userId) = SeedBranchAndUser(db);
        var patient = SeedPatient(db, branchId);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-ILED-REV",
            Status = InvoiceStatus.Issued,
            TotalAmount = 60_000m,
            Subtotal = 60_000m,
            CreatedBy = userId
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var svc = CreateService(db, userId, out _);

        // First post issuance
        await svc.PostInvoiceIssuedEntryAsync(invoice.Id);

        // Now reverse
        await svc.ReverseInvoiceIssuedEntryAsync(invoice.Id);

        var reversal = await db.JournalEntries
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.FinancialDocumentId == invoice.Id
                && e.FinancialDocumentType == FinancialDocumentType.Invoice
                && e.IsReversal);

        reversal.Should().NotBeNull("cancellation must create a reversal JE");
        reversal!.IsPosted.Should().BeTrue("reversal must be auto-posted");

        // Revenue nets to zero across original + reversal
        var revenueLines = await db.JournalLines
            .Where(l => l.AccountType == JournalAccountType.Revenue
                && l.JournalEntry.FinancialDocumentId == invoice.Id
                && l.JournalEntry.IsPosted)
            .ToListAsync();
        (revenueLines.Sum(l => l.Credit) - revenueLines.Sum(l => l.Debit))
            .Should().Be(0m, "revenue must net to zero after reversal");

        // Receivable nets to zero across original + reversal
        var receivableLines = await db.JournalLines
            .Where(l => l.AccountType == JournalAccountType.PatientReceivable
                && l.JournalEntry.FinancialDocumentId == invoice.Id
                && l.JournalEntry.IsPosted)
            .ToListAsync();
        (receivableLines.Sum(l => l.Debit) - receivableLines.Sum(l => l.Credit))
            .Should().Be(0m, "receivable must net to zero after reversal");
    }

    [Fact]
    public async Task ReverseInvoiceIssuedEntry_WhenNoOriginalExists_DoesNotThrow_AndPersistsNoReversal()
    {
        await using var db = CreateContext();
        var (branchId, userId) = SeedBranchAndUser(db);
        var patient = SeedPatient(db, branchId);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-ILED-NOREV",
            Status = InvoiceStatus.Issued,
            TotalAmount = 10_000m,
            Subtotal = 10_000m,
            CreatedBy = userId
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var svc = CreateService(db, userId, out _);

        // No prior issuance JE exists — reverse must be a silent no-op (matches
        // FinanceService behavior: logs warning and returns).
        var act = () => svc.ReverseInvoiceIssuedEntryAsync(invoice.Id);
        await act.Should().NotThrowAsync();

        var reversalCount = await db.JournalEntries
            .CountAsync(e => e.FinancialDocumentId == invoice.Id && e.IsReversal);
        reversalCount.Should().Be(0, "no reversal should be persisted when no original JE exists");
    }
}
