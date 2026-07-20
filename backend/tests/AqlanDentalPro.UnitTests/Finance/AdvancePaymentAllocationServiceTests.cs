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

public class AdvancePaymentAllocationServiceTests
{
    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private static (Guid BranchId, Guid UserId, Patient Patient) SeedContext(AppDbContext db)
    {
        var branchId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            FirstName = "Test",
            LastName = "Patient",
            PatientNumber = $"PAT-{Guid.NewGuid():N}"[..20],
            BranchId = branchId
        };

        db.Branches.Add(new Branch { Id = branchId, Name = "Main" });
        db.Users.Add(new User { Id = userId, Username = "accountant", Role = UserRole.Accountant, BranchId = branchId });
        db.Patients.Add(patient);
        db.SaveChanges();
        return (branchId, userId, patient);
    }

    private static AdvancePaymentAllocationService CreateService(AppDbContext db, Guid userId, Mock<ICommissionService>? commission = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns(userId);
        currentUser.SetupGet(x => x.BranchId).Returns((Guid?)null);
        currentUser.SetupGet(x => x.IsAdmin).Returns(true);

        return new AdvancePaymentAllocationService(
            db,
            currentUser.Object,
            new JournalEntryService(db, new Mock<ILogger<JournalEntryService>>().Object),
            (commission ?? new Mock<ICommissionService>()).Object,
            new Mock<ILogger<AdvancePaymentAllocationService>>().Object);
    }

    private static Invoice AddInvoice(AppDbContext db, Patient patient, Guid userId, decimal amount, string currency = "YER")
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            InvoiceNumber = $"INV-{Guid.NewGuid():N}"[..24],
            Currency = currency,
            Status = InvoiceStatus.Issued,
            TotalAmount = amount,
            Subtotal = amount,
            CreatedBy = userId,
            UpdatedBy = userId
        };
        db.Invoices.Add(invoice);
        db.SaveChanges();
        return invoice;
    }

    private static void AddAdvance(AppDbContext db, Patient patient, Guid branchId, decimal amount, string currency = "YER")
    {
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            BranchId = branchId,
            Amount = amount,
            AppliedAmount = amount,
            Currency = currency,
            AccountCurrency = currency,
            ExchangeRateToAccountCurrency = 1m,
            PaymentDate = DateOnly.FromDateTime(DateTime.Today),
            ReceiptNumber = $"R-{Guid.NewGuid():N}"[..30]
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task AllocateAvailableAdvances_FullAdvance_PostsBalancedReclassificationAndPaysInvoice()
    {
        await using var db = CreateDb();
        var (branchId, userId, patient) = SeedContext(db);
        var invoice = AddInvoice(db, patient, userId, 100m);
        AddAdvance(db, patient, branchId, 100m);
        var commission = new Mock<ICommissionService>();
        var service = CreateService(db, userId, commission);

        var result = await service.AllocateAvailableAdvancesAsync(invoice.Id);

        result.AllocatedAmount.Should().Be(100m);
        result.RemainingInvoiceAmount.Should().Be(0m);
        result.AllocationCount.Should().Be(1);
        (await db.Invoices.FindAsync(invoice.Id))!.Status.Should().Be(InvoiceStatus.Paid);

        var allocation = await db.PaymentAllocations.SingleAsync();
        var entry = await db.JournalEntries.Include(e => e.Lines).SingleAsync(e => e.Id == allocation.JournalEntryId);
        entry.FinancialDocumentType.Should().Be(FinancialDocumentType.PaymentAllocation);
        entry.IsPosted.Should().BeTrue();
        entry.Currency.Should().Be("YER");
        entry.Lines.Single(l => l.AccountType == JournalAccountType.PatientAdvance).Debit.Should().Be(100m);
        entry.Lines.Single(l => l.AccountType == JournalAccountType.PatientReceivable).Credit.Should().Be(100m);
        entry.IsBalanced().Should().BeTrue();
        commission.Verify(x => x.TriggerOnPaymentCommissionsAsync(invoice.Id), Times.Once);
    }

    [Fact]
    public async Task AllocateAvailableAdvances_PartialAndExcessAmounts_OnlyAppliesInvoiceRemainder()
    {
        await using var db = CreateDb();
        var (branchId, userId, patient) = SeedContext(db);
        var invoice = AddInvoice(db, patient, userId, 100m);
        AddAdvance(db, patient, branchId, 40m);
        AddAdvance(db, patient, branchId, 90m);
        var service = CreateService(db, userId);

        var result = await service.AllocateAvailableAdvancesAsync(invoice.Id);

        result.AllocatedAmount.Should().Be(100m);
        result.RemainingInvoiceAmount.Should().Be(0m);
        (await db.Invoices.FindAsync(invoice.Id))!.Status.Should().Be(InvoiceStatus.Paid);
        (await db.PaymentAllocations.SumAsync(a => a.Amount)).Should().Be(100m);
        (await db.PaymentAllocations.OrderBy(a => a.CreatedAt).FirstAsync()).Amount.Should().Be(40m);
        (await db.PaymentAllocations.OrderBy(a => a.CreatedAt).Skip(1).FirstAsync()).Amount.Should().Be(60m);
    }

    [Fact]
    public async Task AllocateAvailableAdvances_DifferentAccountCurrency_DoesNotMixCurrencies()
    {
        await using var db = CreateDb();
        var (branchId, userId, patient) = SeedContext(db);
        var invoice = AddInvoice(db, patient, userId, 100m, "USD");
        AddAdvance(db, patient, branchId, 100m, "YER");
        var service = CreateService(db, userId);

        var result = await service.AllocateAvailableAdvancesAsync(invoice.Id);

        result.AllocatedAmount.Should().Be(0m);
        result.RemainingInvoiceAmount.Should().Be(100m);
        (await db.PaymentAllocations.CountAsync()).Should().Be(0);
        (await db.Invoices.FindAsync(invoice.Id))!.Status.Should().Be(InvoiceStatus.Issued);
    }

    [Fact]
    public async Task ReleaseInvoiceAllocations_PostsReversalAndReopensInvoiceBalance()
    {
        await using var db = CreateDb();
        var (branchId, userId, patient) = SeedContext(db);
        var invoice = AddInvoice(db, patient, userId, 100m);
        AddAdvance(db, patient, branchId, 100m);
        var service = CreateService(db, userId);
        await service.AllocateAvailableAdvancesAsync(invoice.Id);

        var result = await service.ReleaseInvoiceAllocationsAsync(invoice.Id);

        result.AllocatedAmount.Should().Be(100m);
        (await db.Invoices.FindAsync(invoice.Id))!.Status.Should().Be(InvoiceStatus.Issued);
        (await db.PaymentAllocations.IgnoreQueryFilters().SingleAsync()).IsActive.Should().BeFalse();

        var entries = await db.JournalEntries.Include(e => e.Lines)
            .Where(e => e.FinancialDocumentType == FinancialDocumentType.PaymentAllocation)
            .ToListAsync();
        entries.Should().HaveCount(2);
        entries.Single(e => e.IsReversal).Currency.Should().Be("YER");
        entries.Single(e => e.IsReversal).IsPosted.Should().BeTrue();
        entries.Sum(e => e.Lines.Sum(l => l.Debit - l.Credit)).Should().Be(0m);
    }

    [Fact]
    public async Task ReleaseInvoiceAllocations_WhenAlreadyReleased_DoesNotCreateAnotherReversal()
    {
        await using var db = CreateDb();
        var (branchId, userId, patient) = SeedContext(db);
        var invoice = AddInvoice(db, patient, userId, 100m);
        AddAdvance(db, patient, branchId, 100m);
        var service = CreateService(db, userId);
        await service.AllocateAvailableAdvancesAsync(invoice.Id);
        await service.ReleaseInvoiceAllocationsAsync(invoice.Id);

        var repeatedResult = await service.ReleaseInvoiceAllocationsAsync(invoice.Id);

        repeatedResult.AllocatedAmount.Should().Be(0m);
        repeatedResult.AllocationCount.Should().Be(0);
        (await db.JournalEntries.CountAsync(e => e.FinancialDocumentType == FinancialDocumentType.PaymentAllocation)).Should().Be(2);
    }
}
