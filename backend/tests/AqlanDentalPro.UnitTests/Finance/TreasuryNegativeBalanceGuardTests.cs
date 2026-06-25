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
/// Tests for the configurable negative-treasury-balance guard in
/// TreasuryResolutionService.DecrementTreasuryBalanceAsync.
///
/// Default behavior (setting absent/empty or "true"/"1"): BLOCK (audit §5.2) —
/// an outflow that would drive the balance negative is rejected with an Arabic
/// error, so a clinic can never silently overdraft its treasury. Only an explicit
/// Admin opt-out ("false"/"0"/"off"/"no") falls back to legacy warn-only behavior.
/// </summary>
public class TreasuryNegativeBalanceGuardTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static TreasuryResolutionService CreateService(AppDbContext db) =>
        new(db, new Mock<ILogger<TreasuryResolutionService>>().Object);

    private static async Task<Guid> SeedTreasuryAsync(AppDbContext db, decimal balance)
    {
        var branchId = Guid.NewGuid();
        db.Branches.Add(new Branch { Id = branchId, Name = "Main" });
        db.Treasuries.Add(new Treasury
        {
            Id = Guid.NewGuid(),
            Name = "درج كاشير",
            Type = TreasuryType.Vault,
            BranchId = branchId,
            Balance = balance,
            IsActive = true
        });
        await db.SaveChangesAsync();
        return branchId;
    }

    [Fact]
    public async Task Decrement_WithSufficientBalance_Succeeds()
    {
        await using var db = CreateDb();
        var branchId = await SeedTreasuryAsync(db, balance: 1000m);
        var service = CreateService(db);

        await service.DecrementTreasuryBalanceAsync(branchId, "cash", 400m);
        await db.SaveChangesAsync();

        var treasury = await db.Treasuries.SingleAsync();
        treasury.Balance.Should().Be(600m);
    }

    [Theory]
    [InlineData(null)]   // no setting row at all
    [InlineData("")]     // empty value row
    public async Task Decrement_GoingNegative_DefaultOrEmpty_Blocks(string? settingValue)
    {
        await using var db = CreateDb();
        var branchId = await SeedTreasuryAsync(db, balance: 100m);
        if (settingValue is not null)
        {
            db.Settings.Add(new Setting
            {
                Key = TreasuryResolutionService.PreventNegativeBalanceSettingKey,
                Value = settingValue,
                Category = "finance"
            });
            await db.SaveChangesAsync();
        }
        var service = CreateService(db);

        // BLOCK BY DEFAULT (audit §5.2): absent or empty setting enforces the guard.
        var act = () => service.DecrementTreasuryBalanceAsync(branchId, "cash", 250m);

        var ex = await act.Should().ThrowAsync<ArgumentException>();
        ex.Which.Message.Should().Contain("غير كافٍ");

        var treasury = await db.Treasuries.SingleAsync();
        treasury.Balance.Should().Be(100m, "balance must not change when the default guard rejects the outflow");
    }

    [Theory]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("1")]
    public async Task Decrement_GoingNegative_WithEnforcementEnabled_Throws(string settingValue)
    {
        await using var db = CreateDb();
        var branchId = await SeedTreasuryAsync(db, balance: 100m);
        db.Settings.Add(new Setting
        {
            Key = TreasuryResolutionService.PreventNegativeBalanceSettingKey,
            Value = settingValue,
            Category = "finance"
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var act = () => service.DecrementTreasuryBalanceAsync(branchId, "cash", 250m);

        var ex = await act.Should().ThrowAsync<ArgumentException>();
        ex.Which.Message.Should().Contain("رصيد الخزينة");
        ex.Which.Message.Should().Contain("غير كافٍ");

        var treasury = await db.Treasuries.SingleAsync();
        treasury.Balance.Should().Be(100m, "balance must not change when the guard rejects the outflow");
    }

    [Theory]
    [InlineData("false")]
    [InlineData("FALSE")]
    [InlineData("0")]
    [InlineData("off")]
    [InlineData("no")]
    public async Task Decrement_GoingNegative_WithEnforcementDisabled_Allows(string settingValue)
    {
        await using var db = CreateDb();
        var branchId = await SeedTreasuryAsync(db, balance: 100m);
        db.Settings.Add(new Setting
        {
            Key = TreasuryResolutionService.PreventNegativeBalanceSettingKey,
            Value = settingValue,
            Category = "finance"
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.DecrementTreasuryBalanceAsync(branchId, "cash", 250m);
        await db.SaveChangesAsync();

        var treasury = await db.Treasuries.SingleAsync();
        treasury.Balance.Should().Be(-150m);
    }

    [Fact]
    public async Task Decrement_ExactBalance_WithEnforcementEnabled_Succeeds()
    {
        await using var db = CreateDb();
        var branchId = await SeedTreasuryAsync(db, balance: 500m);
        db.Settings.Add(new Setting
        {
            Key = TreasuryResolutionService.PreventNegativeBalanceSettingKey,
            Value = "true",
            Category = "finance"
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        // Draining the treasury to exactly zero is allowed.
        await service.DecrementTreasuryBalanceAsync(branchId, "cash", 500m);
        await db.SaveChangesAsync();

        var treasury = await db.Treasuries.SingleAsync();
        treasury.Balance.Should().Be(0m);
    }
}
