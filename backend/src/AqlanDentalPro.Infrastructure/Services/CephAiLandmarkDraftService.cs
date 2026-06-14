using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Exceptions;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.Infrastructure.Services;

public sealed class CephAiLandmarkDraftService(
    AppDbContext db,
    CephAiDraftService settingsService,
    IEnumerable<ICephLandmarkDraftProvider> providers,
    AiApiKeyVault keyVault,
    ICurrentUserService currentUser,
    ILogger<CephAiLandmarkDraftService> logger)
{
    public const string Action = "ceph_landmark_draft";
    public const string ReviewDisclaimer =
        "هذه مسودة نقاط مولدة بنموذج رؤية عام وليست تتبعاً سيفالومترياً معتمداً. يجب على أخصائي التقويم مراجعة وتحريك كل نقطة قبل الحفظ والحساب.";

    private const int MaxImageBytes = 10 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string> LandmarkNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["S"] = "السرج",
            ["N"] = "الناسيون",
            ["Or"] = "قاع المدار",
            ["Po"] = "قمة المسمع",
            ["ANS"] = "الشوكة الأنفية الأمامية",
            ["PNS"] = "الشوكة الأنفية الخلفية",
            ["A"] = "النقطة A",
            ["B"] = "النقطة B",
            ["Pog"] = "البوجونيون",
            ["Gn"] = "الغناثيون",
            ["Me"] = "المنتون",
            ["Go"] = "الغونيون",
            ["Co"] = "الكونديليون",
            ["Ar"] = "الأرتيكولاري",
            ["D"] = "النقطة D",
            ["Pm"] = "البروتوبيرانس مينتال",
            ["U1T"] = "طرف القاطع العلوي",
            ["U1A"] = "ذروة القاطع العلوي",
            ["L1T"] = "طرف القاطع السفلي",
            ["L1A"] = "ذروة القاطع السفلي",
            ["LS"] = "الشفة العلوية",
            ["LI"] = "الشفة السفلية",
            ["Pn"] = "طرف الأنف",
            ["Cm"] = "الكولوميلا",
        };

    public async Task<CephAiTraceResultDto?> GenerateAsync(
        Guid analysisId,
        int imageWidth,
        int imageHeight,
        CancellationToken cancellationToken = default)
    {
        if (imageWidth <= 0 || imageHeight <= 0)
            throw new ArgumentException("أبعاد صورة الأشعة غير صالحة.");

        var analysis = await db.CephAnalyses
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == analysisId, cancellationToken);
        if (analysis is null)
            return null;

        var inputSummary = $"image:{imageWidth}x{imageHeight};requestedLandmarks:24";
        var settings = await settingsService.GetSettingsAsync();
        if (!settings.Enabled)
        {
            await WriteAuditAsync(analysisId, null, false, "feature_disabled", inputSummary, 0, bestEffort: true);
            throw new CephAiUnavailableException(CephAiDraftService.DisabledMessageAr);
        }

        var provider = providers.FirstOrDefault(p =>
            string.Equals(p.ProviderName, settings.Provider, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
        {
            await WriteAuditAsync(
                analysisId, null, false, $"vision_provider_unsupported:{settings.Provider}", inputSummary, 0, bestEffort: true);
            throw new CephAiUnavailableException(
                $"مزود {settings.Provider} لا يدعم مسودة نقاط السيفالومتري حالياً");
        }

        var secret = await keyVault.ResolveAsync(
            provider.ProviderName, provider.ApiKeyEnvVar, cancellationToken);
        if (string.IsNullOrWhiteSpace(secret))
        {
            await WriteAuditAsync(analysisId, null, false, "api_key_missing", inputSummary, 0, bestEffort: true);
            throw new CephAiUnavailableException(CephAiDraftService.MissingKeyMessageAr);
        }

        if (settings.MonthlyLimit > 0
            && await settingsService.CountUsageThisMonthAsync() >= settings.MonthlyLimit)
        {
            await WriteAuditAsync(
                analysisId, $"{provider.ProviderName}/{settings.Model}", false,
                "monthly_limit_reached", inputSummary, 0, bestEffort: true);
            throw new CephAiLimitReachedException(CephAiDraftService.MonthlyLimitMessageAr);
        }

        var (imageBytes, mimeType) = await ReadImageAsync(
            analysis.XrayFileUrl, cancellationToken);
        var modelId = $"{provider.ProviderName}/{settings.Model}";

        try
        {
            var points = await provider.GenerateAsync(
                imageBytes, mimeType, settings.Model, secret, cancellationToken);
            var landmarks = points.Select(point => new CephLandmarkDto
            {
                Key = point.Key,
                Name = LandmarkNames.GetValueOrDefault(point.Key, point.Key),
                X = point.XNormalized / 1000d * imageWidth,
                Y = point.YNormalized / 1000d * imageHeight,
                IsAiPlaced = true,
                Confidence = point.Confidence,
            }).ToList();

            await WriteAuditAsync(
                analysisId, modelId, true, null, inputSummary, landmarks.Count);

            return new CephAiTraceResultDto
            {
                Landmarks = landmarks,
                ModelId = modelId,
                Disclaimer = ReviewDisclaimer,
                GeneratedAt = DateTime.UtcNow,
            };
        }
        catch (Exception ex) when (
            ex is CephAiUpstreamException or HttpRequestException
                or TaskCanceledException or System.Text.Json.JsonException)
        {
            logger.LogError(ex, "AI landmark draft failed for analysis {AnalysisId}", analysisId);
            var reason = ex switch
            {
                TaskCanceledException => "vision_timeout",
                System.Text.Json.JsonException => "vision_invalid_json",
                CephAiUpstreamException upstream => upstream.Message,
                _ => "vision_network_error",
            };
            await WriteAuditAsync(analysisId, modelId, false, reason, inputSummary, 0, bestEffort: true);
            if (ex is CephAiUpstreamException)
                throw;
            throw new CephAiUpstreamException(reason, ex);
        }
    }

    private static async Task<(byte[] Bytes, string MimeType)> ReadImageAsync(
        string? xrayFileUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(xrayFileUrl)
            || !xrayFileUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "صورة السيفالومتري غير محفوظة في مخزن الملفات. أعد رفعها من الجهاز أو الرابط.");
        }

        var fileName = Path.GetFileName(xrayFileUrl);
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("مسار صورة السيفالومتري غير صالح.");

        var uploadsDirectory = Environment.GetEnvironmentVariable("UPLOADS_PATH");
        if (string.IsNullOrWhiteSpace(uploadsDirectory))
            uploadsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

        var root = Path.GetFullPath(uploadsDirectory);
        var filePath = Path.GetFullPath(Path.Combine(root, fileName));
        if (!filePath.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(filePath))
            throw new ArgumentException("ملف صورة السيفالومتري غير موجود.");

        var info = new FileInfo(filePath);
        if (info.Length is <= 0 or > MaxImageBytes)
            throw new ArgumentException("حجم صورة السيفالومتري غير صالح أو يتجاوز 10 ميجابايت.");

        var mimeType = Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => throw new ArgumentException("نوع صورة السيفالومتري غير مدعوم."),
        };

        return (await File.ReadAllBytesAsync(filePath, cancellationToken), mimeType);
    }

    // bestEffort = true ONLY on refusal/error paths: a failure there (e.g. the
    // OrthodonticAiLogs table missing on an un-migrated DB) must NEVER mask the
    // honest AI error, nor turn an expected 403/400 into a generic 500. The
    // SUCCESS path stays strict (bestEffort = false) so the monthly-limit count,
    // derived from successful audit rows, can never be silently bypassed.
    private async Task WriteAuditAsync(
        Guid analysisId,
        string? modelId,
        bool succeeded,
        string? errorSummary,
        string inputSummary,
        int outputLength,
        bool bestEffort = false)
    {
        try
        {
            db.OrthodonticAiLogs.Add(new OrthodonticAiLog
            {
                AnalysisId = analysisId,
                UserId = currentUser.UserId,
                Action = Action,
                ModelId = modelId,
                Succeeded = succeeded,
                ErrorSummary = errorSummary,
                InputSummary = inputSummary,
                OutputLength = outputLength,
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex) when (bestEffort)
        {
            logger.LogWarning(ex,
                "Failed to write OrthodonticAiLog audit row for analysis {AnalysisId} — continuing", analysisId);
            db.ChangeTracker.Clear(); // drop the failed insert so later saves stay clean
        }
    }
}
