using AqlanDentalPro.Application.Common;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AqlanDentalPro.UnitTests.Lab;

/// <summary>
/// LABINV-REQ-010 — exchange rates for lab orders.
///
/// <para>
/// These tests defend a money path, not a display. The rate resolved here multiplies into
/// the lab order cost, the supplier bill, and the lab-cost deduction inside the doctor's
/// commission. The behaviours worth pinning are therefore: the same market always yields
/// the same rate, an unusable rate is refused rather than quietly replaced by 1, and a
/// rate nobody has reviewed is reported as unreviewed instead of as current.
/// </para>
/// </summary>
public class ExchangeRateResolverTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task SetAsync(AppDbContext db, params (string Key, string Value)[] pairs)
    {
        foreach (var (key, value) in pairs)
        {
            db.Settings.Add(new Setting
            {
                Key = key,
                Value = value,
                Category = FinanceSettingsKeys.Category,
            });
        }
        await db.SaveChangesAsync();
    }

    // ── Defaults ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Defaults_To_Sanaa_Market_When_Nothing_Configured()
    {
        using var db = CreateDb();
        var snapshot = await new ExchangeRateResolver(db).GetAsync();

        snapshot.Market.Should().Be("sanaa");
        snapshot.MarketLabel.Should().Be("سوق صنعاء");
        snapshot.RatesToYer["USD"].Should().Be(535m);
        snapshot.RatesToYer["SAR"].Should().Be(142m);
    }

    [Fact]
    public async Task Base_Currency_Is_Always_One()
    {
        using var db = CreateDb();
        var snapshot = await new ExchangeRateResolver(db).GetAsync();

        snapshot.RatesToYer["YER"].Should().Be(1m);
    }

    // ── Markets ───────────────────────────────────────────────────────────────

    /// <summary>
    /// The whole reason markets exist. Sanaa and Aden differ by roughly a factor of four,
    /// so resolving the wrong one is not a rounding error — it misprices the order by 300%.
    /// </summary>
    [Fact]
    public async Task Aden_Market_Yields_Aden_Rates_Not_Sanaa_Rates()
    {
        using var db = CreateDb();
        await SetAsync(db, (FinanceSettingsKeys.FxMarket, "aden"));

        var snapshot = await new ExchangeRateResolver(db).GetAsync();

        snapshot.Market.Should().Be("aden");
        snapshot.MarketLabel.Should().Be("سوق عدن");
        snapshot.RatesToYer["USD"].Should().Be(1950m);
        snapshot.RatesToYer["SAR"].Should().Be(515m);
    }

    [Fact]
    public async Task Custom_Market_Yields_The_Clinic_Configured_Rates()
    {
        using var db = CreateDb();
        await SetAsync(db,
            (FinanceSettingsKeys.FxMarket, "custom"),
            (FinanceSettingsKeys.FxCustomUsdToYer, "1400"),
            (FinanceSettingsKeys.FxCustomSarToYer, "372"));

        var snapshot = await new ExchangeRateResolver(db).GetAsync();

        snapshot.Market.Should().Be("custom");
        snapshot.RatesToYer["USD"].Should().Be(1400m);
        snapshot.RatesToYer["SAR"].Should().Be(372m);
    }

    [Fact]
    public async Task Explicit_Market_Argument_Previews_Without_Changing_The_Configured_One()
    {
        using var db = CreateDb();
        await SetAsync(db, (FinanceSettingsKeys.FxMarket, "sanaa"));
        var resolver = new ExchangeRateResolver(db);

        var preview = await resolver.GetAsync("aden");
        var configured = await resolver.GetAsync();

        preview.RatesToYer["USD"].Should().Be(1950m);
        configured.RatesToYer["USD"].Should().Be(535m, "previewing a market must not change the active one");
    }

    [Fact]
    public async Task Unknown_Market_Falls_Back_To_Sanaa_Rather_Than_Throwing()
    {
        using var db = CreateDb();
        await SetAsync(db, (FinanceSettingsKeys.FxMarket, "timbuktu"));

        var snapshot = await new ExchangeRateResolver(db).GetAsync();

        snapshot.Market.Should().Be("sanaa");
    }

    // ── Staleness ─────────────────────────────────────────────────────────────

    /// <summary>
    /// A rate nobody ever reviewed must not be presented as current. The seeded defaults are
    /// a starting point, not a statement that someone checked the market today.
    /// </summary>
    [Fact]
    public async Task Never_Reviewed_Rates_Are_Stale()
    {
        using var db = CreateDb();
        var snapshot = await new ExchangeRateResolver(db).GetAsync();

        snapshot.UpdatedOn.Should().BeNull();
        snapshot.AgeInDays.Should().BeNull();
        snapshot.IsStale.Should().BeTrue();
    }

    [Fact]
    public async Task Rates_Reviewed_Today_Are_Not_Stale()
    {
        using var db = CreateDb();
        var today = ClinicTimeProvider.ClinicToday();
        await SetAsync(db, (FinanceSettingsKeys.FxRatesUpdatedOn, today.ToString("yyyy-MM-dd")));

        var snapshot = await new ExchangeRateResolver(db).GetAsync();

        snapshot.UpdatedOn.Should().Be(today);
        snapshot.AgeInDays.Should().Be(0);
        snapshot.IsStale.Should().BeFalse();
    }

    [Fact]
    public async Task Rates_Become_Stale_The_Day_After_The_Configured_Window()
    {
        var today = ClinicTimeProvider.ClinicToday();

        using (var inside = CreateDb())
        {
            await SetAsync(inside,
                (FinanceSettingsKeys.FxStaleAfterDays, "14"),
                (FinanceSettingsKeys.FxRatesUpdatedOn, today.AddDays(-14).ToString("yyyy-MM-dd")));

            var snapshot = await new ExchangeRateResolver(inside).GetAsync();
            snapshot.AgeInDays.Should().Be(14);
            snapshot.IsStale.Should().BeFalse("exactly at the window is still within it");
        }

        using var outside = CreateDb();
        await SetAsync(outside,
            (FinanceSettingsKeys.FxStaleAfterDays, "14"),
            (FinanceSettingsKeys.FxRatesUpdatedOn, today.AddDays(-15).ToString("yyyy-MM-dd")));

        var stale = await new ExchangeRateResolver(outside).GetAsync();
        stale.AgeInDays.Should().Be(15);
        stale.IsStale.Should().BeTrue("one day past the window must flip the flag");
    }

    [Fact]
    public async Task Unparsable_Review_Date_Is_Treated_As_Never_Reviewed()
    {
        using var db = CreateDb();
        await SetAsync(db, (FinanceSettingsKeys.FxRatesUpdatedOn, "not-a-date"));

        var snapshot = await new ExchangeRateResolver(db).GetAsync();

        snapshot.UpdatedOn.Should().BeNull();
        snapshot.IsStale.Should().BeTrue();
    }

    [Fact]
    public async Task Nonsensical_Stale_Window_Falls_Back_To_Fourteen_Days()
    {
        using var db = CreateDb();
        await SetAsync(db, (FinanceSettingsKeys.FxStaleAfterDays, "0"));

        var snapshot = await new ExchangeRateResolver(db).GetAsync();

        snapshot.StaleAfterDays.Should().Be(14);
    }

    // ── GetRateToYerAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetRateToYer_Returns_The_Configured_Rate()
    {
        using var db = CreateDb();
        await SetAsync(db, (FinanceSettingsKeys.FxMarket, "aden"));

        var rate = await new ExchangeRateResolver(db).GetRateToYerAsync("USD");

        rate.Should().Be(1950m);
    }

    [Fact]
    public async Task GetRateToYer_Is_Case_Insensitive()
    {
        using var db = CreateDb();

        var rate = await new ExchangeRateResolver(db).GetRateToYerAsync("usd");

        rate.Should().Be(535m);
    }

    /// <summary>
    /// The single most important assertion in this file. Returning 1 for an unusable rate
    /// would make a 400 USD crown cost 400 rial and read as a bargain in every report after
    /// it. Refusing forces the caller to deal with the missing rate.
    /// </summary>
    [Fact]
    public async Task GetRateToYer_Refuses_A_Zero_Rate_Instead_Of_Substituting_One()
    {
        using var db = CreateDb();
        await SetAsync(db,
            (FinanceSettingsKeys.FxMarket, "custom"),
            (FinanceSettingsKeys.FxCustomUsdToYer, "0"));

        var rate = await new ExchangeRateResolver(db).GetRateToYerAsync("USD");

        rate.Should().BeNull();
    }

    [Fact]
    public async Task GetRateToYer_Refuses_An_Unknown_Currency()
    {
        using var db = CreateDb();

        (await new ExchangeRateResolver(db).GetRateToYerAsync("EUR")).Should().BeNull();
        (await new ExchangeRateResolver(db).GetRateToYerAsync("")).Should().BeNull();
    }

    [Fact]
    public async Task GetRateToYer_Returns_One_For_The_Base_Currency()
    {
        using var db = CreateDb();

        (await new ExchangeRateResolver(db).GetRateToYerAsync("YER")).Should().Be(1m);
    }
}
