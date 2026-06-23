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

    /// <summary>
    /// All finance keys mapped to their default values. Defaults are chosen to
    /// preserve the current hardcoded/seeded behavior — changing a setting from
    /// the UI is the only way to alter production behavior.
    /// </summary>
    public static readonly Dictionary<string, string> Defaults = new()
    {
        [DefaultConsultationFee]              = "5000",
        [MaxDiscountPercentage]               = "100",   // 100 = no restriction (current behavior)
        [CashierDefaultOpeningBalance]        = "0",
        [PaymentMethodsDefaultVisibility]     = "all",   // informational — all active methods are shown
        [CommissionDefaultRecognitionMode]    = "OnPaymentCollection",
        [CommissionDefaultDoctorPercentage]   = "40",
        [CommissionDefaultBaseRule]           = "AfterDiscountAndCosts",
        [ReceiptFooterText]                   = "",      // empty = no custom footer
        [ReceiptShowLeadDoctor]               = "true",  // current behavior: lead-doctor block prints
    };

    /// <summary>True if <paramref name="key"/> is a known finance setting.</summary>
    public static bool IsKnownKey(string key) => Defaults.ContainsKey(key);
}
