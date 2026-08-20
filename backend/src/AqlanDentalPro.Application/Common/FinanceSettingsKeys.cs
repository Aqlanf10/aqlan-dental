namespace AqlanDentalPro.Application.Common;

/// <summary>
/// FIN-SETTINGS — clinic-configurable finance values stored under the
/// <c>finance.*</c> namespace in the Settings table. Every key has a default
/// that mirrors the current production behavior, so the clinic owner can change
/// values from the Settings screen WITHOUT any silent money-behavior change.
/// Per-service commission fields on <c>ClinicService</c> are intentionally NOT
/// here — only the global default-for-new-services is configurable here.
/// </summary>
public static class FinanceSettingsKeys
{
    public const string Category = "finance";

    // ── Fees & discounts ────────────────────────────────────────────────────
    public const string DefaultConsultationFee = "finance.default_consultation_fee";
    public const string MaxDiscountPercentage  = "finance.max_discount_percentage";

    // ── Operational expenses ────────────────────────────────────────────────
    /// <summary>Expenses above this amount (YER) require managerial approval before posting.</summary>
    public const string ExpenseApprovalThreshold = "finance.expenses.approval_threshold";

    // ── Treasury safety ──────────────────────────────────────────────────────
    /// <summary>
    /// When "true" (default), outflows that would drive a treasury balance below zero are
    /// blocked. Admin can set "false" to fall back to warn-only. Must match
    /// TreasuryResolutionService.PreventNegativeBalanceSettingKey.
    /// </summary>
    public const string PreventNegativeTreasuryBalance = "finance.prevent_negative_treasury_balance";

    // ── Cashier sessions ────────────────────────────────────────────────────
    public const string CashierDefaultOpeningBalance = "finance.cashier_session.default_opening_balance";

    // ── Payment methods ─────────────────────────────────────────────────────
    public const string PaymentMethodsDefaultVisibility = "finance.payment_methods.default_visibility";

    // ── Commission defaults (used when creating NEW services) ───────────────
    public const string CommissionDefaultRecognitionMode   = "finance.commission.default_recognition_mode";
    public const string CommissionDefaultDoctorPercentage  = "finance.commission.default_doctor_percentage";
    public const string CommissionDefaultBaseRule          = "finance.commission.default_base_rule";

    // ── Receipt / finance document defaults ─────────────────────────────────
    public const string ReceiptFooterText     = "finance.receipt.footer_text";
    public const string ReceiptShowLeadDoctor = "finance.receipt.show_lead_doctor";

    // ── Lab performance thresholds (CORE-LAB-015) ───────────────────────────
    // These are contractual expectations of a lab, not display constants: the owner
    // renegotiates turnaround time and acceptable remake rates per lab contract. They
    // used to be literals inside the reports JSX — written twice each, once on the
    // summary card and once on the row badge, so editing one made the two disagree
    // silently about whether the same lab was performing acceptably.

    /// <summary>Remake percentage at or above which a lab is flagged red.</summary>
    public const string LabRemakeRateAlarm = "finance.lab.remake_rate_alarm";

    /// <summary>Remake percentage at or above which a lab is flagged amber.</summary>
    public const string LabRemakeRateWarn = "finance.lab.remake_rate_warn";

    /// <summary>Overdue percentage at or above which a lab is flagged red.</summary>
    public const string LabOverdueRateAlarm = "finance.lab.overdue_rate_alarm";

    /// <summary>Overdue percentage at or above which a lab is flagged amber.</summary>
    public const string LabOverdueRateWarn = "finance.lab.overdue_rate_warn";

    /// <summary>Working days from sending to receiving, above which turnaround is flagged.</summary>
    public const string LabTurnaroundDaysTarget = "finance.lab.turnaround_days_target";

    /// <summary>On-time percentage at or above which a lab is considered good.</summary>
    public const string LabOnTimeRateGood = "finance.lab.on_time_rate_good";

    /// <summary>On-time percentage below which a lab is flagged red.</summary>
    public const string LabOnTimeRateWarn = "finance.lab.on_time_rate_warn";

    // ── Payables ageing buckets (CORE-LAB-020) ──────────────────────────────
    // Where one ageing bucket ends and the next begins is a credit-terms decision, not a
    // display constant: a lab billing net-30 and a materials vendor billing net-60 do not
    // become "late" on the same day. The defaults below are the 30/60/90 convention.

    /// <summary>Days past due at which the first ageing bucket ends.</summary>
    public const string PayablesAgingBucket1Days = "finance.payables.aging_bucket_1_days";

    /// <summary>Days past due at which the second ageing bucket ends.</summary>
    public const string PayablesAgingBucket2Days = "finance.payables.aging_bucket_2_days";

    /// <summary>Days past due at which the third ageing bucket ends; beyond it is the last bucket.</summary>
    public const string PayablesAgingBucket3Days = "finance.payables.aging_bucket_3_days";

    // ── Exchange rates (LABINV-REQ-010) ─────────────────────────────────────
    // Why these live in Settings and are NOT fetched from a public FX API:
    //
    // Yemen has a split currency market. The rate in Sanaa and the rate in Aden are
    // different currencies in practice — around 535 vs 1,950 rial to the dollar. Public
    // FX APIs return the Central Bank's official rate, which matches neither market and
    // is roughly a quarter of the Aden street rate. Wiring such a feed into a path that
    // sets lab cost — and therefore the doctor's commission — would import a wrong
    // number automatically and confidently. The owner sets the market rate; the system
    // only makes sure the same rate is used everywhere and says how old it is.

    /// <summary>Active market whose rates are offered by default: "sanaa" | "aden" | "custom".</summary>
    public const string FxMarket = "finance.fx.market";

    public const string FxSanaaUsdToYer = "finance.fx.sanaa.usd_to_yer";
    public const string FxSanaaSarToYer = "finance.fx.sanaa.sar_to_yer";
    public const string FxAdenUsdToYer  = "finance.fx.aden.usd_to_yer";
    public const string FxAdenSarToYer  = "finance.fx.aden.sar_to_yer";
    public const string FxCustomUsdToYer = "finance.fx.custom.usd_to_yer";
    public const string FxCustomSarToYer = "finance.fx.custom.sar_to_yer";

    /// <summary>Clinic-local date (yyyy-MM-dd) the rates were last reviewed. Empty = never.</summary>
    public const string FxRatesUpdatedOn = "finance.fx.rates_updated_on";

    /// <summary>Days after which the stored rate is shown as stale and must be reviewed.</summary>
    public const string FxStaleAfterDays = "finance.fx.stale_after_days";

    /// <summary>
    /// All finance keys mapped to their default values. Defaults are chosen to
    /// preserve the current hardcoded/seeded behavior — changing a setting from
    /// the UI is the only way to alter production behavior.
    /// </summary>
    public static readonly Dictionary<string, string> Defaults = new()
    {
        [DefaultConsultationFee]              = "5000",
        [MaxDiscountPercentage]               = "100",   // 100 = no restriction (current behavior)
        [ExpenseApprovalThreshold]            = "50000", // preserves the previous hardcoded 50,000 YER threshold
        [PreventNegativeTreasuryBalance]      = "true",  // block overdrafts by default (audit §5.2); Admin may set "false"
        [CashierDefaultOpeningBalance]        = "0",
        [PaymentMethodsDefaultVisibility]     = "all",   // informational — all active methods are shown
        [CommissionDefaultRecognitionMode]    = "OnPaymentCollection",
        [CommissionDefaultDoctorPercentage]   = "40",
        [CommissionDefaultBaseRule]           = "AfterDiscountAndCosts",
        [ReceiptFooterText]                   = "",      // empty = no custom footer
        [ReceiptShowLeadDoctor]               = "true",  // current behavior: lead-doctor block prints

        // Defaults reproduce exactly the literals that were in the reports JSX, so moving
        // them here changes no colour on any existing screen.
        [LabRemakeRateAlarm]                  = "10",
        [LabRemakeRateWarn]                   = "5",
        [LabOverdueRateAlarm]                 = "15",
        [LabOverdueRateWarn]                  = "5",
        [LabTurnaroundDaysTarget]             = "7",
        [LabOnTimeRateGood]                   = "90",
        [LabOnTimeRateWarn]                   = "70",

        // Exchange rates. The two market defaults reproduce the rates the clinic's own
        // Android lab app shipped with, so adopting this feature changes no number the
        // clinic was already using. "rates_updated_on" is deliberately empty: the system
        // must not claim these were reviewed today when nobody has reviewed them.
        [FxMarket]                            = "sanaa",
        [FxSanaaUsdToYer]                     = "535",
        [FxSanaaSarToYer]                     = "142",
        [FxAdenUsdToYer]                      = "1950",
        [FxAdenSarToYer]                      = "515",
        [FxCustomUsdToYer]                    = "535",
        [FxCustomSarToYer]                    = "142",
        [FxRatesUpdatedOn]                    = "",
        [FxStaleAfterDays]                    = "14",

        [PayablesAgingBucket1Days]            = "30",
        [PayablesAgingBucket2Days]            = "60",
        [PayablesAgingBucket3Days]            = "90",
    };

    /// <summary>True if <paramref name="key"/> is a known finance setting.</summary>
    public static bool IsKnownKey(string key) => Defaults.ContainsKey(key);
}
