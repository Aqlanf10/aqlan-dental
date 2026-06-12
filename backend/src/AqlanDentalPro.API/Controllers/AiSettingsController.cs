using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

/// <summary>Request body of PUT /api/ai-settings.</summary>
public sealed class UpdateAiSettingsRequest
{
    public bool Enabled { get; init; }
    public string Provider { get; init; } = CephAiDraftService.DefaultProvider;
    public string Model { get; init; } = CephAiDraftService.DefaultModelId;
    public int MaxTokens { get; init; } = CephAiDraftService.DefaultMaxTokens;
    public double Temperature { get; init; } = CephAiDraftService.DefaultTemperature;
    public int MonthlyLimit { get; init; } = CephAiDraftService.DefaultMonthlyLimit;
}

/// <summary>
/// Admin configuration for the Ceph AI draft assistant (batch C-D).
///
/// Hard security rules:
/// - API keys live ONLY in server environment variables (Railway) — this
///   controller NEVER returns a key, never accepts one, and never logs one.
///   keyStatus exposes only { configured, masked-last-4 }.
/// - test-connection is a SAFE local check (env key presence + settings
///   sanity) — it never calls the external AI API.
/// </summary>
[ApiController]
[Route("api/ai-settings")]
[Authorize(Policy = "AdminOnly")]
public class AiSettingsController(AppDbContext db, CephAiDraftService aiService) : ControllerBase
{
    /// <summary>Recognized providers and their env vars — openai is recognized but not implemented yet.</summary>
    private static readonly IReadOnlyDictionary<string, string> ProviderEnvVars =
        new Dictionary<string, string>
        {
            ["gemini"] = "GEMINI_API_KEY",
            ["anthropic"] = "ANTHROPIC_API_KEY",
            ["openai"] = "OPENAI_API_KEY",
        };

    public const string InvalidProviderMessageAr =
        "مزود الذكاء الاصطناعي غير صالح — القيم المسموحة: gemini أو anthropic أو openai";
    public const string ModelRequiredMessageAr = "اسم نموذج الذكاء الاصطناعي مطلوب";
    public const string TestConnectionOkMessageAr =
        "تحقق محلي: المفتاح مهيأ — لم يتم إجراء اتصال خارجي";

    // GET /api/ai-settings — effective settings + usage + masked key status.
    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await BuildPayloadAsync());

    // PUT /api/ai-settings — upserts the ai.* Settings rows (validated + clamped).
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateAiSettingsRequest req)
    {
        var provider = (req.Provider ?? "").Trim().ToLowerInvariant();
        if (!ProviderEnvVars.ContainsKey(provider))
            return BadRequest(new { message = InvalidProviderMessageAr });

        if (string.IsNullOrWhiteSpace(req.Model))
            return BadRequest(new { message = ModelRequiredMessageAr });

        var maxTokens = Math.Clamp(req.MaxTokens, CephAiDraftService.MinMaxTokens, CephAiDraftService.MaxMaxTokens);
        var temperature = Math.Clamp(req.Temperature, 0d, 1d);
        var monthlyLimit = Math.Max(0, req.MonthlyLimit);

        await UpsertAsync(CephAiDraftService.DraftEnabledSettingKey, req.Enabled ? "true" : "false");
        await UpsertAsync(CephAiDraftService.ProviderSettingKey, provider);
        await UpsertAsync(CephAiDraftService.ModelSettingKey, req.Model.Trim());
        await UpsertAsync(CephAiDraftService.MaxTokensSettingKey,
            maxTokens.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await UpsertAsync(CephAiDraftService.TemperatureSettingKey,
            temperature.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await UpsertAsync(CephAiDraftService.MonthlyLimitSettingKey,
            monthlyLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));

        await db.SaveChangesAsync();
        return Ok(await BuildPayloadAsync());
    }

    // POST /api/ai-settings/test-connection — SAFE local check only:
    // env key presence + settings sanity. NEVER calls the external AI API.
    [HttpPost("test-connection")]
    public async Task<IActionResult> TestConnection()
    {
        var settings = await aiService.GetSettingsAsync();

        if (!ProviderEnvVars.TryGetValue(settings.Provider, out var envVar))
            return Ok(new { ok = false, message = InvalidProviderMessageAr });

        if (settings.Provider == "openai")
            return Ok(new { ok = false, message = CephAiDraftService.UnsupportedProviderMessageAr("openai") });

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(envVar)))
            return Ok(new
            {
                ok = false,
                message = $"المفتاح غير مهيأ على الخادم — أضف متغير البيئة {envVar} في إعدادات الاستضافة (Railway)",
            });

        if (string.IsNullOrWhiteSpace(settings.Model))
            return Ok(new { ok = false, message = ModelRequiredMessageAr });

        var note = settings.Enabled ? "" : " — تنبيه: الميزة لا تزال معطلة من الإعدادات";
        return Ok(new { ok = true, message = TestConnectionOkMessageAr + note });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<object> BuildPayloadAsync()
    {
        var settings = await aiService.GetSettingsAsync();
        var usage = await aiService.CountUsageThisMonthAsync();

        return new
        {
            enabled = settings.Enabled,
            provider = settings.Provider,
            model = settings.Model,
            maxTokens = settings.MaxTokens,
            temperature = settings.Temperature,
            monthlyLimit = settings.MonthlyLimit,
            usageThisMonth = usage,
            keyStatus = ProviderEnvVars.ToDictionary(p => p.Key, p => KeyStatusFor(p.Value)),
        };
    }

    /// <summary>Masked status only — the key itself never leaves the server.</summary>
    private static object KeyStatusFor(string envVar)
    {
        var key = Environment.GetEnvironmentVariable(envVar);
        if (string.IsNullOrWhiteSpace(key))
            return new { configured = false, masked = (string?)null };

        // Show the last 4 characters only for keys long enough that this
        // reveals nothing useful; otherwise mask completely.
        var masked = key.Length > 8 ? $"********{key[^4..]}" : "********";
        return new { configured = true, masked = (string?)masked };
    }

    private async Task UpsertAsync(string key, string value)
    {
        var setting = await db.Settings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting is null)
        {
            db.Settings.Add(new Domain.Entities.Setting
            {
                Key = key,
                Value = value,
                Category = "ai",
                UpdatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            setting.Value = value;
            setting.UpdatedAt = DateTime.UtcNow;
        }
    }
}
