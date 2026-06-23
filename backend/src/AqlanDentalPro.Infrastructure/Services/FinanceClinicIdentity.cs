using AqlanDentalPro.Application.Common;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// Clinic identity for finance PDFs (receipt vouchers, statements, invoices),
/// read from the Settings table (clinic.* keys) — NO hardcoding, per the owner's
/// report-identity decision. Every accessor falls back to the prior printed text
/// when its key is unset, so existing documents never regress before the keys
/// are configured; the lead-doctor block only appears once clinic.lead_doctor is
/// set.
///
/// FIN-SETTINGS: also reads <c>finance.receipt.footer_text</c> and
/// <c>finance.receipt.show_lead_doctor</c> so the clinic owner can configure a
/// custom receipt footer and toggle the lead-doctor block on/off from the
/// Settings screen. Both default to the current behavior (empty footer, lead
/// doctor shown when clinic.lead_doctor is set).
/// </summary>
public sealed class FinanceClinicIdentity
{
    public const string DefaultName = "مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان";
    public const string DefaultLocation = "تعز، اليمن — شارع التحرير الأعلى";
    public const string DefaultPhones = "هاتف: 04-253028";

    private readonly string _name;
    private readonly string _leadDoctor;
    private readonly string _leadDoctorTitle;
    private readonly string _leadDoctorCredentials;
    private readonly string _phones;
    private readonly string _location;
    private readonly string _receiptFooterText;
    private readonly bool _showLeadDoctor;

    private FinanceClinicIdentity(
        string name, string leadDoctor, string leadDoctorTitle,
        string leadDoctorCredentials, string phones, string location,
        string receiptFooterText, bool showLeadDoctor)
    {
        _name = name;
        _leadDoctor = leadDoctor;
        _leadDoctorTitle = leadDoctorTitle;
        _leadDoctorCredentials = leadDoctorCredentials;
        _phones = phones;
        _location = location;
        _receiptFooterText = receiptFooterText ?? "";
        _showLeadDoctor = showLeadDoctor;
    }

    public string Name => Or(_name, DefaultName);
    public string Location => Or(_location, DefaultLocation);
    public string Phones => Or(_phones, DefaultPhones);
    public string LeadDoctor => _leadDoctor;
    public string LeadDoctorTitle => _leadDoctorTitle;
    public string LeadDoctorCredentials => _leadDoctorCredentials;

    /// <summary>The lead-doctor identity block is printed only when configured.</summary>
    public bool HasLeadDoctor => !string.IsNullOrWhiteSpace(_leadDoctor);
    public bool HasLeadDoctorTitle => !string.IsNullOrWhiteSpace(_leadDoctorTitle);
    public bool HasLeadDoctorCredentials => !string.IsNullOrWhiteSpace(_leadDoctorCredentials);

    /// <summary>
    /// FIN-SETTINGS: custom receipt footer text (e.g. "شكراً لزيارتكم"). Empty when
    /// the owner has not configured one — the PDF generator falls back to the
    /// legacy hard-coded thank-you line in that case.
    /// </summary>
    public string ReceiptFooterText => _receiptFooterText ?? "";

    /// <summary>
    /// FIN-SETTINGS: whether the lead-doctor block should print on receipts/invoices.
    /// Defaults to <c>true</c> (the current behavior). When <c>false</c>, PDF
    /// generators must omit the lead-doctor block even if <see cref="HasLeadDoctor"/>.
    /// </summary>
    public bool ShowLeadDoctor => _showLeadDoctor;

    /// <summary>True when the lead-doctor block should actually print (configured AND enabled).</summary>
    public bool ShouldRenderLeadDoctor => ShowLeadDoctor && HasLeadDoctor;

    /// <summary>Compact one-line contacts ("phones | location") for tight footers.</summary>
    public string ContactLine => $"{Phones}  |  {Location}";

    private static string Or(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    /// <summary>Identity with all keys unset — renders the legacy fallback text.</summary>
    public static readonly FinanceClinicIdentity Fallback = new("", "", "", "", "", "", "", true);

    private static readonly string[] Keys =
    {
        "clinic.name", "clinic.lead_doctor", "clinic.lead_doctor_title",
        "clinic.lead_doctor_credentials", "clinic.phones", "clinic.location",
        // FIN-SETTINGS — receipt defaults
        FinanceSettingsKeys.ReceiptFooterText,
        FinanceSettingsKeys.ReceiptShowLeadDoctor,
    };

    /// <summary>Reads the clinic.* identity keys + finance receipt keys from Settings (no hardcoding).</summary>
    public static async Task<FinanceClinicIdentity> ResolveAsync(AppDbContext db, CancellationToken ct = default)
    {
        var map = await db.Settings
            .Where(s => Keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        string Get(string key) => map.TryGetValue(key, out var v) && v is not null ? v.Trim() : "";

        bool showLeadDoctor = ParseBool(Get(FinanceSettingsKeys.ReceiptShowLeadDoctor),
            fallback: bool.TryParse(FinanceSettingsKeys.Defaults[FinanceSettingsKeys.ReceiptShowLeadDoctor], out var fb) && fb);

        return new FinanceClinicIdentity(
            Get("clinic.name"),
            Get("clinic.lead_doctor"),
            Get("clinic.lead_doctor_title"),
            Get("clinic.lead_doctor_credentials"),
            Get("clinic.phones"),
            Get("clinic.location"),
            Get(FinanceSettingsKeys.ReceiptFooterText),
            showLeadDoctor);
    }

    private static bool ParseBool(string raw, bool fallback)
    {
        var r = (raw ?? "").Trim().ToLowerInvariant();
        if (r is "true" or "1" or "yes" or "on") return true;
        if (r is "false" or "0" or "no" or "off") return false;
        return fallback;
    }
}
