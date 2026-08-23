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
    private readonly string _nameEn;
    private readonly string _locationEn;
    private readonly string _leadDoctorEn;
    private readonly string _leadDoctorCredentialsEn;
    private readonly bool _printEnglish;

    /// <summary>
    /// CORE-REQ-006 — the logo this clinic actually configured, resolved once alongside the
    /// text identity so a document never has to decide for itself where its logo comes from.
    /// Null when neither a configured upload nor the shipped file is readable.
    /// </summary>
    public byte[]? LogoBytes { get; private init; }

    private FinanceClinicIdentity(
        string name, string leadDoctor, string leadDoctorTitle,
        string leadDoctorCredentials, string phones, string location,
        string receiptFooterText, bool showLeadDoctor,
        string nameEn = "", string locationEn = "",
        string leadDoctorEn = "", string leadDoctorCredentialsEn = "",
        bool printEnglish = false)
    {
        _nameEn = nameEn ?? "";
        _locationEn = locationEn ?? "";
        _leadDoctorEn = leadDoctorEn ?? "";
        _leadDoctorCredentialsEn = leadDoctorCredentialsEn ?? "";
        _printEnglish = printEnglish;
        _name = name;
        _leadDoctor = leadDoctor;
        _leadDoctorTitle = leadDoctorTitle;
        _leadDoctorCredentials = leadDoctorCredentials;
        _phones = phones;
        _location = location;
        _receiptFooterText = receiptFooterText ?? "";
        _showLeadDoctor = showLeadDoctor;
    }

    /// <summary>
    /// CORE-REQ-006 — the print language, which is deliberately independent of the interface
    /// language: the clinic can run its screens in English and still hand the patient an
    /// Arabic receipt, or the reverse.
    ///
    /// <para>
    /// Resolved here rather than in each generator because this class is already the single
    /// reader every finance document goes through. The setting and the English identity keys
    /// existed and were configurable from the Settings screen, but nothing in the
    /// PDF-generating layer read either — so the switch changed nothing on the documents it
    /// was named for. Every accessor below now answers in the chosen language, and each falls
    /// back to Arabic when its English key is unset: a half-configured identity must not print
    /// a blank clinic name.
    /// </para>
    /// </summary>
    public bool PrintsEnglish => _printEnglish;

    public string Name => _printEnglish && !string.IsNullOrWhiteSpace(_nameEn)
        ? _nameEn
        : Or(_name, DefaultName);

    public string Location => _printEnglish && !string.IsNullOrWhiteSpace(_locationEn)
        ? _locationEn
        : Or(_location, DefaultLocation);

    // Phone numbers are digits; they do not translate.
    public string Phones => Or(_phones, DefaultPhones);

    public string LeadDoctor => _printEnglish && !string.IsNullOrWhiteSpace(_leadDoctorEn)
        ? _leadDoctorEn
        : _leadDoctor;

    /// <summary>
    /// The English lead-doctor setting already carries the title ("Dr. … — Orthodontic
    /// Specialist"), so printing a separate title line under it would repeat it.
    /// </summary>
    public string LeadDoctorTitle => _printEnglish && !string.IsNullOrWhiteSpace(_leadDoctorEn)
        ? ""
        : _leadDoctorTitle;

    public string LeadDoctorCredentials =>
        _printEnglish && !string.IsNullOrWhiteSpace(_leadDoctorCredentialsEn)
            ? _leadDoctorCredentialsEn
            : _leadDoctorCredentials;

    /// <summary>The lead-doctor identity block is printed only when configured.</summary>
    // These answer for the language actually being printed, not for the raw Arabic fields.
    // Reading the raw field left a dangling separator on the English receipt — "Dr Aqlan — " —
    // because the title is folded into the English doctor name and resolves to empty.
    public bool HasLeadDoctor => !string.IsNullOrWhiteSpace(LeadDoctor);
    public bool HasLeadDoctorTitle => !string.IsNullOrWhiteSpace(LeadDoctorTitle);
    public bool HasLeadDoctorCredentials => !string.IsNullOrWhiteSpace(LeadDoctorCredentials);

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
        // CORE-REQ-006 — print language and the English identity it selects. These live under
        // website.* because that is where the owner already maintains them in Settings; the
        // key's prefix says where it is edited, not who may read it.
        "website.printLanguage",
        "website.clinicNameEn",
        "website.addressEn",
        "website.leadDoctorEn",
        "website.leadDoctorCredentialsEn",
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

        // Anything other than an explicit "en" prints Arabic — an unrecognised or empty value
        // must not leave a document in no language.
        var printEnglish = string.Equals(Get("website.printLanguage"), "en", StringComparison.OrdinalIgnoreCase);

        return new FinanceClinicIdentity(
            Get("clinic.name"),
            Get("clinic.lead_doctor"),
            Get("clinic.lead_doctor_title"),
            Get("clinic.lead_doctor_credentials"),
            Get("clinic.phones"),
            Get("clinic.location"),
            Get(FinanceSettingsKeys.ReceiptFooterText),
            showLeadDoctor,
            Get("website.clinicNameEn"),
            Get("website.addressEn"),
            Get("website.leadDoctorEn"),
            Get("website.leadDoctorCredentialsEn"),
            printEnglish)
        {
            LogoBytes = await PdfLogoCache.ResolveAsync(db, ct),
        };
    }

    private static bool ParseBool(string raw, bool fallback)
    {
        var r = (raw ?? "").Trim().ToLowerInvariant();
        if (r is "true" or "1" or "yes" or "on") return true;
        if (r is "false" or "0" or "no" or "off") return false;
        return fallback;
    }
}
