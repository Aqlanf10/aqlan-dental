using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
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
public class SettingsController(AppDbContext db, ICurrentUserService currentUser) : ControllerBase
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
    };
}

public class UpdateSettingRequest
{
    public string? Value { get; set; }
    public string? Category { get; set; }
}
