using AqlanDentalPro.Application.Common;
using AqlanDentalPro.Infrastructure.Data;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// LABINV-REQ-010 — one place that answers "what is 1 SAR/USD worth in YER today".
///
/// <para>
/// The problem this replaces: every lab order screen asked the user to type the rate by
/// hand ("سعر الصرف الفعلي: 1 SAR = كم YER؟"). Nothing checked it, nothing remembered it,
/// and nothing compared it to what the last person typed. That number multiplies into
/// the order cost, the supplier bill, and the lab-cost deduction inside the doctor's
/// commission — so two staff disagreeing by a hundred rial produced two different
/// commissions for identical work, with no trace of which was intended.
/// </para>
///
/// <para>
/// <b>Why there is no live FX feed here.</b> Yemen's currency market is split: Sanaa and
/// Aden trade at rates that differ by roughly a factor of four (about 535 vs 1,950 rial
/// to the dollar). Public FX APIs publish the Central Bank's official rate, which is
/// neither of those. Wiring such a feed into a path that sets money would import a
/// confidently wrong number on a schedule. The owner sets the market rate — this service
/// only guarantees everyone uses the same one and reports how old it is.
/// </para>
///
/// <para>
/// This is a <b>read-through</b> resolver. It never writes <c>LabOrder.ExchangeRateToYer</c>;
/// the order still stores whatever was confirmed on the screen, so
/// <c>LabOrderFinanceSyncService</c> and commission logic are untouched.
/// </para>
/// </summary>
public sealed class ExchangeRateResolver(AppDbContext db)
{
    /// <summary>Currencies the clinic transacts in. YER is the base.</summary>
    public const string BaseCurrency = "YER";

    public static readonly string[] SupportedCurrencies = ["YER", "SAR", "USD"];

    /// <summary>Markets the clinic can price against.</summary>
    public static readonly string[] KnownMarkets = ["sanaa", "aden", "custom"];

    /// <summary>Arabic label for a market key, for display.</summary>
    public static string MarketLabelAr(string market) => market switch
    {
        "sanaa" => "سوق صنعاء",
        "aden" => "سوق عدن",
        "custom" => "سعر مخصص للمركز",
        _ => market,
    };

    /// <param name="Market">Active market key.</param>
    /// <param name="MarketLabel">Arabic label for the active market.</param>
    /// <param name="RatesToYer">Rate of one unit of each currency in YER. Always contains YER = 1.</param>
    /// <param name="UpdatedOn">Clinic-local date the rates were last reviewed; null when never.</param>
    /// <param name="StaleAfterDays">Review interval in days.</param>
    /// <param name="IsStale">
    /// True when the rates have never been reviewed, or were reviewed longer ago than
    /// <paramref name="StaleAfterDays"/>. A stale rate is still returned — hiding it would
    /// just push the user back to typing an unverifiable number — but the caller must
    /// show that it is stale rather than presenting it as current.
    /// </param>
    public sealed record ExchangeRateSnapshot(
        string Market,
        string MarketLabel,
        IReadOnlyDictionary<string, decimal> RatesToYer,
        DateOnly? UpdatedOn,
        int StaleAfterDays,
        bool IsStale)
    {
        /// <summary>Days since the last review; null when never reviewed.</summary>
        public int? AgeInDays => UpdatedOn is null
            ? null
            : Math.Max(0, ClinicTimeProvider.ClinicToday().DayNumber - UpdatedOn.Value.DayNumber);
    }

    /// <summary>
    /// Resolves the currently configured rates.
    /// </summary>
    /// <param name="market">
    /// Optional market override for preview ("what would Aden's rates be?"). When null the
    /// configured active market is used.
    /// </param>
    public async Task<ExchangeRateSnapshot> GetAsync(string? market = null, CancellationToken ct = default)
    {
        var settings = new FinanceSettingsReader(db);

        var active = Normalize(market ?? await settings.GetAsync(FinanceSettingsKeys.FxMarket, ct));

        var (usdKey, sarKey) = active switch
        {
            "aden" => (FinanceSettingsKeys.FxAdenUsdToYer, FinanceSettingsKeys.FxAdenSarToYer),
            "custom" => (FinanceSettingsKeys.FxCustomUsdToYer, FinanceSettingsKeys.FxCustomSarToYer),
            _ => (FinanceSettingsKeys.FxSanaaUsdToYer, FinanceSettingsKeys.FxSanaaSarToYer),
        };

        var usd = await settings.GetDecimalAsync(usdKey, ct);
        var sar = await settings.GetDecimalAsync(sarKey, ct);

        var staleAfterDays = await settings.GetIntAsync(FinanceSettingsKeys.FxStaleAfterDays, ct);
        if (staleAfterDays <= 0) staleAfterDays = 14;

        var updatedOn = ParseDate(await settings.GetAsync(FinanceSettingsKeys.FxRatesUpdatedOn, ct));

        var today = ClinicTimeProvider.ClinicToday();
        var isStale = updatedOn is null || today.DayNumber - updatedOn.Value.DayNumber > staleAfterDays;

        var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            [BaseCurrency] = 1m,
            ["USD"] = usd,
            ["SAR"] = sar,
        };

        return new ExchangeRateSnapshot(
            active,
            MarketLabelAr(active),
            rates,
            updatedOn,
            staleAfterDays,
            isStale);
    }

    /// <summary>
    /// Rate of one unit of <paramref name="currency"/> in YER, or null when the currency is
    /// unknown or the configured rate is not a usable positive number.
    /// </summary>
    /// <remarks>
    /// Returns null rather than 1 for an unusable rate. Silently substituting 1 would make a
    /// 400 USD crown cost 400 rial and look like a bargain in every report that followed.
    /// </remarks>
    public async Task<decimal?> GetRateToYerAsync(string currency, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(currency)) return null;

        var snapshot = await GetAsync(ct: ct);
        if (!snapshot.RatesToYer.TryGetValue(currency.Trim(), out var rate)) return null;
        return rate > 0m ? rate : null;
    }

    private static string Normalize(string? market)
    {
        var m = (market ?? "").Trim().ToLowerInvariant();
        return KnownMarkets.Contains(m) ? m : "sanaa";
    }

    private static DateOnly? ParseDate(string raw) =>
        DateOnly.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;
}
