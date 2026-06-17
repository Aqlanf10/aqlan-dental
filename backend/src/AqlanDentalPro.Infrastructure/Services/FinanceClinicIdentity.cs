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

    private FinanceClinicIdentity(
        string name, string leadDoctor, string leadDoctorTitle,
        string leadDoctorCredentials, string phones, string location)
    {
        _name = name;
        _leadDoctor = leadDoctor;
        _leadDoctorTitle = leadDoctorTitle;
        _leadDoctorCredentials = leadDoctorCredentials;
        _phones = phones;
        _location = location;
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

    /// <summary>Compact one-line contacts ("phones | location") for tight footers.</summary>
    public string ContactLine => $"{Phones}  |  {Location}";

    private static string Or(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    /// <summary>Identity with all keys unset — renders the legacy fallback text.</summary>
    public static readonly FinanceClinicIdentity Fallback = new("", "", "", "", "", "");

    private static readonly string[] Keys =
    {
        "clinic.name", "clinic.lead_doctor", "clinic.lead_doctor_title",
        "clinic.lead_doctor_credentials", "clinic.phones", "clinic.location",
    };

    /// <summary>Reads the clinic.* identity keys from Settings (no hardcoding).</summary>
    public static async Task<FinanceClinicIdentity> ResolveAsync(AppDbContext db, CancellationToken ct = default)
    {
        var map = await db.Settings
            .Where(s => Keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        string Get(string key) => map.TryGetValue(key, out var v) && v is not null ? v.Trim() : "";

        return new FinanceClinicIdentity(
            Get("clinic.name"),
            Get("clinic.lead_doctor"),
            Get("clinic.lead_doctor_title"),
            Get("clinic.lead_doctor_credentials"),
            Get("clinic.phones"),
            Get("clinic.location"));
    }
}
