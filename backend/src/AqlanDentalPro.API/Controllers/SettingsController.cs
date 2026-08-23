using AqlanDentalPro.Application.Common;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

/// <summary>Sprint 2 — DTO for creating a payment method.</summary>
public sealed class CreatePaymentMethodRequest
{
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public bool RequiresReferenceNumber { get; init; }
    public string? AccountName { get; init; }
    public string? AccountNumber { get; init; }
    public string? Notes { get; init; }
    public int SortOrder { get; init; }
    public Guid? BranchId { get; init; }
}

/// <summary>Sprint 2 — DTO for updating a payment method.</summary>
public sealed class UpdatePaymentMethodRequest
{
    public string? Name { get; init; }
    public string? Code { get; init; }
    public bool? RequiresReferenceNumber { get; init; }
    public string? AccountName { get; init; }
    public string? AccountNumber { get; init; }
    public string? Notes { get; init; }
    public int? SortOrder { get; init; }
}

[ApiController]
[Route("api/settings")]
[Authorize(Policy = "AdminOnly")]
public class SettingsController(AppDbContext db, ICurrentUserService currentUser, IAuditService audit) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        // SEC-25 FIX: Filter out sensitive settings (AI keys, SMTP passwords) from the GetAll
        // response. These are managed through dedicated endpoints (e.g. AiApiKeyVault) that
        // return masked status, not raw values. Previously GetAll returned everything including
        // encrypted AI key ciphertext — if any future setting is added without encryption (e.g.
        // SMTP password), it would be exposed here.
        var settings = await db.Settings
            .Where(s => !s.Key.StartsWith("ai.secret.") && !s.Key.StartsWith("smtp."))
            .OrderBy(s => s.Category).ThenBy(s => s.Key)
            .Select(s => new { s.Key, s.Value, s.Category })
            .ToListAsync();

        var dict = settings.ToDictionary(s => s.Key, s => s.Value);
        return Ok(dict);
    }

    [HttpPut("{key}")]
    public async Task<IActionResult> Update(string key, [FromBody] UpdateSettingRequest req)
    {
        var setting = await db.Settings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting == null)
        {
            db.Settings.Add(new Domain.Entities.Setting
            {
                Key = key,
                Value = req.Value,
                Category = req.Category,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            setting.Value = req.Value;
            setting.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return Ok(new { key, value = req.Value });
    }

    /// <summary>جلب إعدادات الموقع (للمشرف)</summary>
    [HttpGet("website")]
    public async Task<IActionResult> GetWebsiteSettings()
    {
        var defaults = GetWebsiteDefaults();

        var settings = await db.Settings
            .AsNoTracking()
            .Where(s => s.Category == "website")
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        var result = new Dictionary<string, string?>();
        foreach (var (key, defaultValue) in defaults)
        {
            result[key] = settings.GetValueOrDefault($"website.{key}") ?? defaultValue;
        }

        return Ok(result);
    }

    /// <summary>تحديث إعدادات الموقع (دفعات)</summary>
    [HttpPut("website")]
    public async Task<IActionResult> UpdateWebsiteSettings([FromBody] Dictionary<string, string?> request)
    {
        var allowedKeys = GetWebsiteDefaults().Keys.ToHashSet();

        // Batch-load all existing website settings in one query instead of N queries
        var validKeys = request.Keys.Where(k => allowedKeys.Contains(k)).Select(k => $"website.{k}").ToList();
        var existingSettings = await db.Settings
            .Where(s => validKeys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s);

        foreach (var (key, value) in request)
        {
            if (!allowedKeys.Contains(key)) continue;

            var dbKey = $"website.{key}";
            if (!existingSettings.TryGetValue(dbKey, out var setting))
            {
                setting = new Domain.Entities.Setting
                {
                    Key = dbKey,
                    Value = value ?? "",
                    Category = "website",
                    UpdatedAt = DateTime.UtcNow
                };
                db.Settings.Add(setting);
            }
            else
            {
                setting.Value = value ?? "";
                setting.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync();

        // Return updated settings
        var updated = await db.Settings
            .AsNoTracking()
            .Where(s => s.Category == "website")
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        var result = new Dictionary<string, string?>();
        foreach (var (key, defaultValue) in GetWebsiteDefaults())
        {
            result[key] = updated.GetValueOrDefault($"website.{key}") ?? defaultValue;
        }

        return Ok(result);
    }

    // ─── FIN-SETTINGS — Finance settings (mirror the website batch pattern) ──────────

    /// <summary>جلب إعدادات المالية (المدير + المحاسب للقراءة)</summary>
    /// <remarks>
    /// Returns ALL finance.* keys with their current values or defaults applied.
    /// Read access is open to Admin and Accountant (ReportsAccess policy) so the
    /// Accountant can SEE the configured limits. Write access stays Admin-only.
    /// </remarks>
    [HttpGet("finance")]
    [Authorize(Policy = "ReportsAccess")]
    public async Task<IActionResult> GetFinanceSettings()
    {
        var defaults = FinanceSettingsKeys.Defaults;

        var stored = await db.Settings
            .AsNoTracking()
            .Where(s => s.Category == FinanceSettingsKeys.Category)
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        var result = new Dictionary<string, string?>();
        foreach (var (key, defaultValue) in defaults)
        {
            result[key] = stored.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v)
                ? v!.Trim()
                : defaultValue;
        }
        return Ok(result);
    }

    /// <summary>دليل درجات اللون المعتمد لأوامر المختبر (متاح لجميع الموظفين)</summary>
    /// <remarks>
    /// LABINV-REQ-007. Served from a StaffOnly route rather than <c>GET /finance</c>
    /// because that route requires <c>ReportsAccess</c> (Admin/Accountant) and the people
    /// who actually order lab work — reception and nursing — do not hold it. Reading the
    /// guide through a route they cannot call would have left the picker permanently
    /// empty for its only users.
    ///
    /// An empty stored value means "use the client's built-in VITA Classical guide"; the
    /// list is not duplicated here so the two cannot drift apart.
    /// </remarks>
    [HttpGet("lab-shade-guide")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> GetLabShadeGuide(CancellationToken ct = default)
    {
        var settings = new FinanceSettingsReader(db);
        var raw = await settings.GetAsync(FinanceSettingsKeys.LabShadeGuide, ct);

        var shades = raw
            .Split([',', '،'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(new { shades });
    }

    /// <summary>أسعار الصرف المعتمدة حاليًا (متاح لجميع الموظفين)</summary>
    /// <remarks>
    /// <para>
    /// LABINV-REQ-010. Read access is <c>StaffOnly</c>, not Admin: the person creating a lab
    /// order in a foreign currency is reception or a nurse, and they are exactly who used to
    /// type the rate by hand. Writing the rates stays Admin-only through <c>PUT /finance</c>.
    /// </para>
    /// <para>
    /// The response always carries <c>isStale</c> and <c>updatedOn</c>. A rate that nobody has
    /// reviewed is still returned — withholding it only sends the user back to guessing — but
    /// the caller must present it as unreviewed rather than as current.
    /// </para>
    /// </remarks>
    [HttpGet("exchange-rates")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> GetExchangeRates([FromQuery] string? market, CancellationToken ct = default)
    {
        var resolver = new ExchangeRateResolver(db);
        var snapshot = await resolver.GetAsync(market, ct);

        return Ok(new
        {
            market = snapshot.Market,
            marketLabel = snapshot.MarketLabel,
            baseCurrency = ExchangeRateResolver.BaseCurrency,
            currencies = ExchangeRateResolver.SupportedCurrencies,
            ratesToYer = snapshot.RatesToYer,
            updatedOn = snapshot.UpdatedOn,
            ageInDays = snapshot.AgeInDays,
            staleAfterDays = snapshot.StaleAfterDays,
            isStale = snapshot.IsStale,
            markets = ExchangeRateResolver.KnownMarkets
                .Select(m => new { key = m, label = ExchangeRateResolver.MarketLabelAr(m) })
                .ToList(),
        });
    }

    /// <summary>تحديث إعدادات المالية (المدير فقط — دفعات)</summary>
    /// <remarks>
    /// Accepts a partial dict of <c>{ "finance.xxx": "value", ... }</c>. Unknown
    /// keys are rejected with a 400 Arabic message. Each value is validated:
    /// <list type="bullet">
    /// <item><c>finance.max_discount_percentage</c> — 0–100</item>
    /// <item><c>finance.commission.default_doctor_percentage</c> — 0–100</item>
    /// <item><c>finance.commission.default_recognition_mode</c> — valid enum name</item>
    /// <item><c>finance.commission.default_base_rule</c> — valid enum name</item>
    /// <item><c>finance.receipt.show_lead_doctor</c> — "true" | "false"</item>
    /// </list>
    /// Every successful PUT is audit-logged.
    /// </remarks>
    [HttpPut("finance")]
    public async Task<IActionResult> UpdateFinanceSettings([FromBody] Dictionary<string, string?> request)
    {
        if (request is null || request.Count == 0)
            return BadRequest(new { message = "لا توجد قيم للتحديث" });

        var allowedKeys = FinanceSettingsKeys.Defaults.Keys.ToHashSet();

        // Reject unknown keys (don't silently drop — surface a clear Arabic error).
        var unknown = request.Keys.Where(k => !allowedKeys.Contains(k)).ToList();
        if (unknown.Count > 0)
            return BadRequest(new { message = $"مفاتيح غير معروفة: {string.Join("، ", unknown)}" });

        // Validate values per-key.
        foreach (var (key, value) in request)
        {
            var raw = (value ?? "").Trim();
            var error = ValidateFinanceValue(key, raw);
            if (error is not null)
                return BadRequest(new { message = error, key });
        }

        // Batch-load existing finance rows so we can compute audit old-vs-new.
        var keysToUpdate = request.Keys.ToList();
        var existing = await db.Settings
            .Where(s => keysToUpdate.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s);

        var oldData = new Dictionary<string, string?>();
        var newData = new Dictionary<string, string?>();

        foreach (var (key, value) in request)
        {
            var raw = (value ?? "").Trim();

            if (!existing.TryGetValue(key, out var setting))
            {
                oldData[key] = FinanceSettingsKeys.Defaults.GetValueOrDefault(key);
                setting = new Setting
                {
                    Key = key,
                    Value = raw,
                    Category = FinanceSettingsKeys.Category,
                    UpdatedAt = DateTime.UtcNow,
                };
                db.Settings.Add(setting);
            }
            else
            {
                oldData[key] = setting.Value;
                setting.Value = raw;
                setting.UpdatedAt = DateTime.UtcNow;
            }
            newData[key] = raw;
        }

        await db.SaveChangesAsync();

        await audit.LogAsync(
            AuditAction.Update,
            "Setting",
            resourceId: null,
            oldData: oldData,
            newData: newData,
            details: "FIN-SETTINGS: finance settings updated");

        // Return updated values (re-read so the response reflects what was stored).
        var updated = await db.Settings
            .AsNoTracking()
            .Where(s => s.Category == FinanceSettingsKeys.Category)
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        var result = new Dictionary<string, string?>();
        foreach (var (key, defaultValue) in FinanceSettingsKeys.Defaults)
        {
            result[key] = updated.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v)
                ? v!.Trim()
                : defaultValue;
        }
        return Ok(result);
    }

    /// <summary>
    /// Per-key validation. Returns an Arabic error message when invalid, or
    /// <c>null</c> when the value is acceptable. Empty values are allowed for
    /// free-text fields (footer_text) — they reset to the default on read.
    /// </summary>
    private static string? ValidateFinanceValue(string key, string raw)
    {
        switch (key)
        {
            case FinanceSettingsKeys.MaxDiscountPercentage:
            {
                if (!decimal.TryParse(raw, out var pct))
                    return "قيمة غير صالحة — يُتوقع رقم";
                if (pct < 0 || pct > 100)
                    return "نسبة الخصم القصوى يجب أن تكون بين 0 و 100";
                return null;
            }
            case FinanceSettingsKeys.CommissionDefaultDoctorPercentage:
            {
                if (!decimal.TryParse(raw, out var pct))
                    return "قيمة غير صالحة — يُتوقع رقم";
                if (pct < 0 || pct > 100)
                    return "نسبة عمولة الطبيب الافتراضية يجب أن تكون بين 0 و 100";
                return null;
            }
            case FinanceSettingsKeys.DefaultConsultationFee:
            case FinanceSettingsKeys.CashierDefaultOpeningBalance:
            {
                if (!decimal.TryParse(raw, out _))
                    return "قيمة غير صالحة — يُتوقع رقم";
                return null;
            }
            case FinanceSettingsKeys.CommissionDefaultRecognitionMode:
            {
                if (!Enum.TryParse<CommissionRecognitionMode>(raw, ignoreCase: true, out _))
                    return "وضع احتساب العمولة غير صالح";
                return null;
            }
            case FinanceSettingsKeys.CommissionDefaultBaseRule:
            {
                if (!Enum.TryParse<CommissionBaseRule>(raw, ignoreCase: true, out _))
                    return "قاعدة احتساب العمولة غير صالحة";
                return null;
            }
            case FinanceSettingsKeys.ReceiptShowLeadDoctor:
            {
                var r = raw.ToLowerInvariant();
                if (r is not "true" and not "false")
                    return "قيمة غير صالحة — يُتوقع true أو false";
                return null;
            }
            // LABINV-REQ-010 — exchange rates feed order cost and therefore commission,
            // so a malformed value here is a money bug, not a display bug.
            case FinanceSettingsKeys.FxMarket:
            {
                if (!ExchangeRateResolver.KnownMarkets.Contains(raw.ToLowerInvariant()))
                    return "السوق غير معروف — القيم المقبولة: sanaa أو aden أو custom";
                return null;
            }
            case FinanceSettingsKeys.FxSanaaUsdToYer:
            case FinanceSettingsKeys.FxSanaaSarToYer:
            case FinanceSettingsKeys.FxAdenUsdToYer:
            case FinanceSettingsKeys.FxAdenSarToYer:
            case FinanceSettingsKeys.FxCustomUsdToYer:
            case FinanceSettingsKeys.FxCustomSarToYer:
            {
                if (!decimal.TryParse(raw, out var rate))
                    return "قيمة غير صالحة — يُتوقع رقم";
                // Zero or negative is rejected rather than stored: a zero rate makes every
                // foreign-currency lab order cost nothing, and it would do so quietly.
                if (rate <= 0m)
                    return "سعر الصرف يجب أن يكون أكبر من صفر";
                return null;
            }
            case FinanceSettingsKeys.FxStaleAfterDays:
            {
                if (!int.TryParse(raw, out var days))
                    return "قيمة غير صالحة — يُتوقع عدد أيام";
                if (days < 1 || days > 365)
                    return "مدة مراجعة سعر الصرف يجب أن تكون بين يوم واحد و 365 يومًا";
                return null;
            }
            case FinanceSettingsKeys.FxRatesUpdatedOn:
            {
                // Empty is meaningful: "never reviewed". It must stay settable so the owner
                // can reset the review clock rather than being forced to claim a false date.
                if (string.IsNullOrWhiteSpace(raw)) return null;
                if (!DateOnly.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, out var d))
                    return "تاريخ غير صالح — الصيغة المتوقعة yyyy-MM-dd";
                if (d > ClinicTimeProvider.ClinicToday())
                    return "لا يمكن أن يكون تاريخ مراجعة سعر الصرف في المستقبل";
                return null;
            }
            case FinanceSettingsKeys.PaymentMethodsDefaultVisibility:
            {
                if (string.IsNullOrWhiteSpace(raw))
                    return "القيمة مطلوبة";
                return null;
            }
            // Free text — accept anything (including empty to reset to default).
            case FinanceSettingsKeys.ReceiptFooterText:
            default:
                return null;
        }
    }

    // ─── Sprint 2 — Payment Method Settings ─────────────────────────────────

    /// <summary>جلب طرق الدفع النشطة (متاح لجميع الموظفين)</summary>
    [HttpGet("payment-methods")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> GetPaymentMethods()
    {
        var methods = await db.PaymentMethodSettings
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .Select(p => new {
                p.Id, p.Name, p.Code, p.IsActive,
                p.RequiresReferenceNumber, p.AccountName, p.AccountNumber,
                p.Notes, p.SortOrder, p.BranchId
            })
            .ToListAsync();
        return Ok(methods);
    }

    /// <summary>إنشاء طريقة دفع جديدة (المدير فقط)</summary>
    [HttpPost("payment-methods")]
    public async Task<IActionResult> CreatePaymentMethod([FromBody] CreatePaymentMethodRequest req)
    {
        // Only Admin can create
        if (!currentUser.IsAdmin) return Forbid();

        var method = new PaymentMethodSetting
        {
            Name = req.Name,
            Code = req.Code,
            IsActive = true,
            RequiresReferenceNumber = req.RequiresReferenceNumber,
            AccountName = req.AccountName,
            AccountNumber = req.AccountNumber,
            Notes = req.Notes,
            SortOrder = req.SortOrder,
            BranchId = req.BranchId
        };
        db.PaymentMethodSettings.Add(method);
        await db.SaveChangesAsync();
        return Ok(new { method.Id });
    }

    /// <summary>تحديث طريقة دفع (المدير فقط)</summary>
    [HttpPut("payment-methods/{id:guid}")]
    public async Task<IActionResult> UpdatePaymentMethod(Guid id, [FromBody] UpdatePaymentMethodRequest req)
    {
        if (!currentUser.IsAdmin) return Forbid();
        var method = await db.PaymentMethodSettings.FindAsync(id);
        if (method is null) return NotFound();

        if (req.Name != null) method.Name = req.Name;
        if (req.Code != null) method.Code = req.Code;
        if (req.RequiresReferenceNumber.HasValue) method.RequiresReferenceNumber = req.RequiresReferenceNumber.Value;
        if (req.AccountName != null) method.AccountName = req.AccountName;
        if (req.AccountNumber != null) method.AccountNumber = req.AccountNumber;
        if (req.Notes != null) method.Notes = req.Notes;
        if (req.SortOrder.HasValue) method.SortOrder = req.SortOrder.Value;

        await db.SaveChangesAsync();
        return Ok(new { id });
    }

    /// <summary>تفعيل/تعطيل طريقة دفع (المدير فقط)</summary>
    [HttpPost("payment-methods/{id:guid}/toggle")]
    public async Task<IActionResult> TogglePaymentMethod(Guid id)
    {
        if (!currentUser.IsAdmin) return Forbid();
        var method = await db.PaymentMethodSettings.FindAsync(id);
        if (method is null) return NotFound();

        method.IsActive = !method.IsActive;
        await db.SaveChangesAsync();
        return Ok(new { id, method.IsActive });
    }

    private static Dictionary<string, string> GetWebsiteDefaults() => new()
    {
        ["clinicName"]           = "مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان",
        ["heroTitle"]            = "ابتسامة تجمع بين دقة العلم ولمسة الفن",
        ["heroSubtitle"]         = "مركز الدكتور عقلان الكامل يقدم رعاية متكاملة في تقويم وزراعة وتجميل الأسنان، مع تشخيص دقيق وخطط علاج واضحة ومتابعة مستمرة لكل حالة.",
        ["marketingSlogan"]      = "قيادة طبية… وابتسامة بثقة",
        ["aboutText"]            = "يقدم مركز الدكتور عقلان الكامل خدمات تخصصية شاملة في تقويم وزراعة وتجميل الأسنان، معتمدين على تشخيص دقيق، وخطط علاج واضحة، ومتابعة مستمرة للحالات للمساعدة في الوصول إلى نتائج علاجية دقيقة ومناسبة لكل حالة.",
        ["phone"]                = "04-253028",
        ["whatsapp"]             = "967770245745",
        ["address"]              = "تعز، اليمن — شارع التحرير الأعلى",
        ["workingHours"]         = "السبت – الخميس: 8 ص – 8 م",
        ["facebook"]             = "",
        ["instagram"]            = "",
        ["logoUrl"]              = "",
        ["heroImageUrl"]         = "",
        ["servicesSectionTitle"] = "حلول طبية متكاملة لابتسامة صحية وواثقة",
        ["bookingButtonText"]    = "احجز موعدك الآن",
        ["whatsappButtonText"]   = "تواصل عبر الواتساب",
        // CORE-REQ-006 — identity for forms the patient carries outside the clinic, and the
        // language they print in. Editable here so they stop being literals in components.
        ["clinicNameEn"]             = "Dr. Aqlan Alkamel Center for Orthodontics, Dental Implants & Cosmetic Dentistry",
        ["addressEn"]                = "Upper Al-Tahrir Street, Taiz, Yemen",
        ["leadDoctorEn"]             = "Dr. Aqlan Alkamel — Orthodontic Specialist",
        ["leadDoctorCredentialsEn"]  = "Central University of Manila — Philippines",
        // Arabic: it is what the clinic prints. English is a deliberate choice, not a default.
        ["printLanguage"]            = "ar",
    };
}

public class UpdateSettingRequest
{
    public string? Value { get; set; }
    public string? Category { get; set; }
}
