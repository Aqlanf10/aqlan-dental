using AqlanDentalPro.Application.DTOs.Commission;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AqlanDentalPro.IntegrationTests;

/// <summary>
/// W07 acceptance proof against PostgreSQL. The guarantees under test rely on
/// advisory locks, a unique idempotency key, deferred allocation constraints and
/// one database transaction, none of which EF's InMemory provider can reproduce.
/// </summary>
public sealed class CommissionPaymentAtomicityTests
    : IClassFixture<TestWebAppFactory>, IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;
    private Guid _branchId;
    private Guid _doctorId;
    private Guid _lineItemId;
    private Guid _cashierId;
    private Guid _treasuryId;

    private const decimal OpeningBalance = 100_000m;

    public CommissionPaymentAtomicityTests(TestWebAppFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
        await using var db = await _factory.CreateDbContextAsync();

        var branch = new Branch { Name = "فرع اختبار W07", IsActive = true };
        var cashier = new User
        {
            Username = $"w07-{Guid.NewGuid():N}"[..16],
            PasswordHash = "test", PasswordSalt = "test",
            Role = UserRole.Admin, BranchId = branch.Id, IsActive = true
        };
        var doctor = new Doctor
        {
            UserId = cashier.Id, Name = "طبيب اختبار W07",
            BranchId = branch.Id, IsActive = true
        };
        var patient = new Patient
        {
            PatientNumber = $"P-{Guid.NewGuid():N}"[..14],
            FirstName = "مريض", LastName = "W07",
            BranchId = branch.Id, IsActive = true
        };
        var treasury = new Treasury
        {
            Name = "درج W07", Type = TreasuryType.Vault, Currency = "YER",
            Balance = OpeningBalance, BranchId = branch.Id, IsActive = true
        };
        var session = new CashierSession
        {
            SessionNumber = $"W07-{Guid.NewGuid():N}"[..18],
            CashierId = cashier.Id, BranchId = branch.Id,
            OpeningTime = DateTime.UtcNow, OpeningBalance = OpeningBalance,
            ExpectedClosingCash = OpeningBalance, Status = SessionStatus.Open,
            TreasuryId = treasury.Id, IsActive = true
        };
        var invoice = new Invoice
        {
            PatientId = patient.Id, InvoiceNumber = $"INV-{Guid.NewGuid():N}"[..18],
            Currency = "YER", Status = InvoiceStatus.Issued,
            Subtotal = 10_000m, TotalAmount = 10_000m,
            CreatedBy = cashier.Id, IsActive = true
        };
        var line = new InvoiceLineItem
        {
            InvoiceId = invoice.Id, ServiceNameSnapshot = "علاج W07", Description = "علاج W07",
            Quantity = 1, UnitPrice = 10_000m, TotalPrice = 10_000m,
            DoctorId = doctor.Id, NetCommissionableAmount = 10_000m,
            DoctorCommissionPercentage = 100m, DoctorCommissionAmount = 10_000m,
            CommissionStatus = CommissionStatus.Approved,
            CommissionApprovedBy = cashier.Id, CommissionApprovedAt = DateTime.UtcNow,
            IsActive = true
        };

        db.AddRange(branch, cashier, doctor, patient, treasury, session, invoice, line);
        await db.SaveChangesAsync();

        _branchId = branch.Id;
        _doctorId = doctor.Id;
        _lineItemId = line.Id;
        _cashierId = cashier.Id;
        _treasuryId = treasury.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ConcurrentRetries_WithSameKey_PostExactlyOnce()
    {
        var request = Request(6_000m, "W07-SAME-KEY");

        var results = await Task.WhenAll(
            Task.Run(() => RecordAsync(request)),
            Task.Run(() => RecordAsync(request)));

        results[0].Id.Should().Be(results[1].Id);

        await using var db = await _factory.CreateDbContextAsync();
        (await db.DoctorCommissionPayments.CountAsync()).Should().Be(1);
        (await db.DoctorCommissionPaymentAllocations.CountAsync()).Should().Be(1);
        (await db.CashFlowTransactions.CountAsync(x => x.Category == FinancialCategory.DoctorCommission))
            .Should().Be(1);
        (await db.JournalEntries.CountAsync(x =>
            x.FinancialDocumentType == FinancialDocumentType.CommissionPayment && !x.IsReversal))
            .Should().Be(1);
        (await db.Treasuries.Where(x => x.Id == _treasuryId).Select(x => x.Balance).SingleAsync())
            .Should().Be(OpeningBalance - 6_000m);
    }

    [Fact]
    public async Task ConcurrentDistinctRequests_CannotOverpayTheSameCommission()
    {
        var outcomes = await Task.WhenAll(
            Task.Run(() => TryRecordAsync(Request(6_000m, "W07-RACER-A"))),
            Task.Run(() => TryRecordAsync(Request(6_000m, "W07-RACER-B"))));

        outcomes.Count(x => x).Should().Be(1,
            "the doctor lock must re-check the remaining balance after the winning commit");

        await using var db = await _factory.CreateDbContextAsync();
        (await db.DoctorCommissionPayments.SumAsync(x => x.Amount)).Should().Be(6_000m);
        (await db.DoctorCommissionPaymentAllocations.SumAsync(x => x.Amount)).Should().Be(6_000m);
        (await db.Treasuries.Where(x => x.Id == _treasuryId).Select(x => x.Balance).SingleAsync())
            .Should().Be(OpeningBalance - 6_000m);
    }

    private RecordCommissionPaymentRequest Request(decimal amount, string key) => new(
        DoctorId: _doctorId,
        Amount: amount,
        PaymentDate: new DateOnly(2026, 8, 6),
        PaymentMethod: "cash",
        ReferenceNumber: null,
        Notes: "W07 integration test",
        LineItemIds: [_lineItemId],
        BranchId: _branchId,
        Currency: "YER",
        IdempotencyKey: key);

    private async Task<DoctorCommissionPaymentDto> RecordAsync(RecordCommissionPaymentRequest request)
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICommissionService>();
        return await service.RecordPaymentAsync(request, _cashierId);
    }

    private async Task<bool> TryRecordAsync(RecordCommissionPaymentRequest request)
    {
        try
        {
            await RecordAsync(request);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
