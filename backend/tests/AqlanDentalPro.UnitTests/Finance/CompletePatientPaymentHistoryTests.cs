using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Finance;

public class CompletePatientPaymentHistoryTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static (PaymentService payments, FinanceReadService financeRead) CreateServices(AppDbContext db)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.IsAdmin).Returns(true);
        currentUser.SetupGet(user => user.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(user => user.BranchId).Returns((Guid?)null);

        var payments = new PaymentService(
            db,
            currentUser.Object,
            new Mock<INotificationService>().Object,
            NullLogger<PaymentService>.Instance,
            new Mock<ICommissionService>().Object,
            new Mock<IJournalEntryService>().Object);
        var financeRead = new FinanceReadService(db, currentUser.Object);
        return (payments, financeRead);
    }

    private static async Task<Guid> SeedPatientWithPayments(AppDbContext db, int count)
    {
        var patientId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        db.Branches.Add(new Branch { Id = branchId, Name = "الفرع الرئيسي" });
        db.Patients.Add(new Patient
        {
            Id = patientId,
            PatientNumber = "P-HISTORY-001",
            FirstName = "سجل",
            LastName = "كامل",
            BranchId = branchId,
        });

        for (var index = 1; index <= count; index++)
        {
            db.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(),
                PatientId = patientId,
                BranchId = branchId,
                Amount = index * 1_000m,
                PaymentDate = new DateOnly(2026, 1, 1).AddDays(index),
                PaymentMethod = "cash",
                ServiceDescription = $"دفعة {index}",
                IsActive = true,
            });
        }

        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            BranchId = branchId,
            Amount = 999_000m,
            PaymentDate = new DateOnly(2026, 2, 1),
            PaymentMethod = "cash",
            IsActive = false,
        });
        await db.SaveChangesAsync();
        return patientId;
    }

    [Fact]
    public async Task PatientScopedPaymentRead_ReturnsAllActiveRowsBeyondDefaultPageSize()
    {
        await using var db = CreateDb();
        var patientId = await SeedPatientWithPayments(db, 25);
        var (payments, _) = CreateServices(db);

        var result = await payments.GetPatientPaymentsAsync(patientId);

        result.Should().HaveCount(25);
        result.Select(payment => payment.Amount).Should().BeInDescendingOrder();
        result.Should().NotContain(payment => payment.Amount == 999_000m);
    }

    [Fact]
    public async Task AccountStatement_ExposesFullHistoryAndKeepsLegacyRecentWindow()
    {
        await using var db = CreateDb();
        var patientId = await SeedPatientWithPayments(db, 25);
        var (_, financeRead) = CreateServices(db);

        var result = await financeRead.GetAccountStatementAsync(patientId);

        result.Should().NotBeNull();
        result!.TotalPaymentsCount.Should().Be(25);
        result.Payments.Should().HaveCount(25);
        result.RecentPayments.Should().HaveCount(20);
        result.Payments.First().Amount.Should().Be(25_000m);
        result.Payments.Last().Amount.Should().Be(1_000m);
    }
}
