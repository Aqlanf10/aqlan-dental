using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize(Policy = "AdminOnly")]
public class SettingsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var settings = await db.Settings
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

        foreach (var (key, value) in request)
        {
            if (!allowedKeys.Contains(key)) continue;

            var dbKey = $"website.{key}";
            var setting = await db.Settings.FirstOrDefaultAsync(s => s.Key == dbKey);
            if (setting == null)
            {
                db.Settings.Add(new Domain.Entities.Setting
                {
                    Key = dbKey,
                    Value = value ?? "",
                    Category = "website",
                    UpdatedAt = DateTime.UtcNow
                });
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
