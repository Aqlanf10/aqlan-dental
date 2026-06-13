using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
    public string? SecretValue { get; init; }
    public bool ClearStoredSecret { get; init; }
}

/// <summary>
/// Admin configuration for the Ceph AI draft assistant (batch C-D).
///
/// Hard security rules:
/// - API keys may come from environment variables or an encrypted admin
///   override. The controller never returns or logs the raw value.
///   keyStatus exposes only { configured, masked-last-4, source }.
/// - test-connection is a SAFE local check (env key presence + settings
///   sanity) — it never calls the external AI API.
/// </summary>
[ApiController]
[Route("api/ai-settings")]
[Authorize(Policy = "AdminOnly")]
public class AiSettingsController(
    AppDbContext db,
    CephAiDraftService aiService,
    AiApiKeyVault keyVault,
    ILogger<AiSettingsController> logger) : ControllerBase
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

        if (req.ClearStoredSecret)
            await keyVault.ClearStoredAsync(provider);
        else if (!string.IsNullOrWhiteSpace(req.SecretValue))
            await keyVault.StoreAsync(provider, req.SecretValue);

        await db.SaveChangesAsync();
        return Ok(await BuildPayloadAsync());
    }

    // POST /api/ai-settings/test-connection — SAFE local check only:
    // encrypted/environment key presence + settings sanity. NEVER calls the external AI API.
    [HttpPost("test-connection")]
    public async Task<IActionResult> TestConnection()
    {
        var settings = await aiService.GetSettingsAsync();

        if (!ProviderEnvVars.TryGetValue(settings.Provider, out var envVar))
            return Ok(new { ok = false, message = InvalidProviderMessageAr });

        if (settings.Provider == "openai")
            return Ok(new { ok = false, message = CephAiDraftService.UnsupportedProviderMessageAr("openai") });

        string? resolvedSecret;
        try
        {
            resolvedSecret = await keyVault.ResolveAsync(settings.Provider, envVar);
        }
        catch (InvalidOperationException ex)
        {
            return Ok(new { ok = false, message = ex.Message });
        }

        if (string.IsNullOrWhiteSpace(resolvedSecret))
            return Ok(new
            {
                ok = false,
                message = $"المفتاح غير مهيأ — أدخله من الإعدادات أو اضبط متغير البيئة {envVar}",
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
        var usage = 0;
        var usageAvailable = true;
        string? usageWarning = null;
        try
        {
            usage = await aiService.CountUsageThisMonthAsync();
        }
        catch (Exception ex) when (IsMissingAiLogTable(ex))
        {
            usageAvailable = false;
            usageWarning = "سجل استخدام الذكاء الاصطناعي غير متاح حتى تطبيق ترحيل قاعدة البيانات";
            logger.LogWarning(
                "OrthodonticAiLogs is not available. AI settings remain readable, but usage counters are disabled.");
        }

        return new
        {
            enabled = settings.Enabled,
            provider = settings.Provider,
            model = settings.Model,
            maxTokens = settings.MaxTokens,
            temperature = settings.Temperature,
            monthlyLimit = settings.MonthlyLimit,
            usageThisMonth = usage,
            usageAvailable,
            usageWarning,
            keyStatus = await BuildKeyStatusAsync(),
        };
    }

    private static bool IsMissingAiLogTable(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.UndefinedTable })
                return true;
        }

        return false;
    }

    private async Task<Dictionary<string, object>> BuildKeyStatusAsync()
    {
        var result = new Dictionary<string, object>();
        foreach (var provider in ProviderEnvVars)
        {
            var status = await keyVault.GetStatusAsync(provider.Key, provider.Value);
            result[provider.Key] = new
            {
                configured = status.Configured,
                masked = status.Masked,
                source = status.Source,
            };
        }
        return result;
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
