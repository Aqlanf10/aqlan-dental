using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Finance;

public sealed class W11JournalApplicationSafetyTests
{
    [Fact]
    public async Task CreateEntry_InsideActiveClosedPeriod_IsRejectedWithoutWrite()
    {
        await using var db = CreateDb();
        var (branchId, userId) = SeedIdentity(db);
        var date = new DateOnly(2026, 8, 1);
        db.AccountingPeriods.Add(ClosedPeriod(branchId, userId, date));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var act = () => service.CreateEntryAsync(
            FinancialDocumentType.Expense,
            Guid.NewGuid(),
            "closed-period create",
            date,
            branchId,
            userId,
            null,
            null,
            BalancedLines(branchId));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*closed accounting period*");
        (await db.JournalEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task PostEntry_AfterItsPeriodCloses_IsRejectedAndRemainsDraft()
    {
        await using var db = CreateDb();
        var (branchId, userId) = SeedIdentity(db);
        var date = new DateOnly(2026, 8, 1);
        var service = CreateService(db);
        var entry = await service.CreateEntryAsync(
            FinancialDocumentType.Expense,
            Guid.NewGuid(),
            "draft before close",
            date,
            branchId,
            userId,
            null,
            null,
            BalancedLines(branchId));

        db.AccountingPeriods.Add(ClosedPeriod(branchId, userId, date));
        await db.SaveChangesAsync();

        var act = () => service.PostEntryAsync(entry.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*closed accounting period*");
        entry.IsPosted.Should().BeFalse();
        entry.PostedAt.Should().BeNull();
    }

    [Fact]
    public async Task InactiveClosedPeriod_DoesNotBlockPosting()
    {
        await using var db = CreateDb();
        var (branchId, userId) = SeedIdentity(db);
        var date = new DateOnly(2026, 8, 1);
        var period = ClosedPeriod(branchId, userId, date);
        period.IsActive = false;
        db.AccountingPeriods.Add(period);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var entry = await service.CreateEntryAsync(
            FinancialDocumentType.Expense,
            Guid.NewGuid(),
            "inactive close",
            date,
            branchId,
            userId,
            null,
            null,
            BalancedLines(branchId));
        await service.PostEntryAsync(entry.Id);

        entry.IsPosted.Should().BeTrue();
    }

    private static AppDbContext CreateDb() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"w11-journal-{Guid.NewGuid()}")
            .Options);

    private static JournalEntryService CreateService(AppDbContext db) =>
        new(db, NullLogger<JournalEntryService>.Instance);

    private static (Guid BranchId, Guid UserId) SeedIdentity(AppDbContext db)
    {
        var branch = new Branch { Id = Guid.NewGuid(), Name = "W11 branch", IsActive = true };
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = $"w11-{Guid.NewGuid():N}",
            BranchId = branch.Id,
            IsActive = true
        };
        db.AddRange(branch, user);
        db.SaveChanges();
        return (branch.Id, user.Id);
    }

    private static AccountingPeriod ClosedPeriod(Guid branchId, Guid userId, DateOnly date) => new()
    {
        BranchId = branchId,
        Name = "W11 closed day",
        StartDate = date,
        EndDate = date,
        Status = "Closed",
        ClosedAt = DateTime.UtcNow,
        ClosedBy = userId,
        IsActive = true
    };

    private static IEnumerable<(JournalAccountType accountType, Guid accountId, decimal debit, decimal credit, string? description)>
        BalancedLines(Guid accountId)
    {
        yield return (JournalAccountType.Expense, accountId, 100m, 0m, "debit");
        yield return (JournalAccountType.OwnerEquity, accountId, 0m, 100m, "credit");
    }
}
