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
/// Default behavior (setting absent or "false"): warn-only — the outflow
/// proceeds even if the balance goes negative, preserving the behavior of
/// existing deployments. When the setting
/// "finance.prevent_negative_treasury_balance" is "true"/"1", outflows that
/// would drive the balance negative are rejected with an Arabic error.
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

    [Fact]
    public async Task Decrement_GoingNegative_DefaultSetting_Allows()
    {
        await using var db = CreateDb();
        var branchId = await SeedTreasuryAsync(db, balance: 100m);
        var service = CreateService(db);

        // No setting row at all → warn-only, outflow proceeds (legacy behavior).
        await service.DecrementTreasuryBalanceAsync(branchId, "cash", 250m);
        await db.SaveChangesAsync();

        var treasury = await db.Treasuries.SingleAsync();
        treasury.Balance.Should().Be(-150m);
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
    [InlineData("0")]
    [InlineData("")]
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
