using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AqlanDentalPro.API.Controllers;

/// <summary>
/// Receives delivery-status callbacks from Meta WhatsApp Cloud API.
/// This endpoint must be publicly reachable (no auth) — Meta calls it directly.
/// Configure in Meta App Dashboard → WhatsApp → Configuration → Webhook URL:
///   https://your-api-domain.com/api/whatsapp/webhook
/// Required env vars: WhatsApp__WebhookVerifyToken, WhatsApp__AppSecret
/// </summary>
[ApiController]
[Route("api/whatsapp/webhook")]
[AllowAnonymous]
[EnableCors("AllowPublicApi")]
public class WhatsAppWebhookController(
    AppDbContext db,
    IConfiguration config,
    ILogger<WhatsAppWebhookController> logger) : ControllerBase
{
    // ── GET: Meta webhook verification handshake ──────────────────────────────
    [HttpGet]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")]         string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")]    string? challenge)
    {
        var expectedToken = config["WhatsApp:WebhookVerifyToken"];

        if (mode == "subscribe"
            && !string.IsNullOrEmpty(expectedToken)
            && verifyToken == expectedToken)
        {
            logger.LogInformation("WhatsApp webhook verified successfully");
            return Ok(challenge);
        }

        logger.LogWarning("WhatsApp webhook verification failed — mode={Mode}, tokenMatch={Match}",
            mode, verifyToken == expectedToken);
        return Forbid();
    }

    // ── POST: Meta status update notifications ────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Receive()
    {
        // Validate HMAC-SHA256 signature from X-Hub-Signature-256 header
        if (!await ValidateSignatureAsync())
        {
            logger.LogWarning("WhatsApp webhook: invalid HMAC signature — request rejected");
            return Unauthorized();
        }

        Request.Body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(Request.Body);
        var root = doc.RootElement;

        // Walk the Meta payload: object → entry[] → changes[] → value → statuses[]
        if (!root.TryGetProperty("entry", out var entries)) return Ok();

        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("changes", out var changes)) continue;

            foreach (var change in changes.EnumerateArray())
            {
                if (!change.TryGetProperty("value", out var value)) continue;
                if (!value.TryGetProperty("statuses", out var statuses)) continue;

                foreach (var status in statuses.EnumerateArray())
                {
                    await ProcessStatusUpdateAsync(status);
                }
            }
        }

        return Ok();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task ProcessStatusUpdateAsync(JsonElement statusEl)
    {
        if (!statusEl.TryGetProperty("id", out var idProp)) return;
        var externalId = idProp.GetString();
        if (string.IsNullOrEmpty(externalId)) return;

        var newStatus = statusEl.TryGetProperty("status", out var sProp)
            ? sProp.GetString() ?? ""
            : "";

        // Map Meta status codes to our internal status values
        var mappedStatus = newStatus switch
        {
            "sent"      => "sent",
            "delivered" => "delivered",
            "read"      => "read",
            "failed"    => "failed",
            _           => null
        };

        if (mappedStatus is null) return;

        var message = await db.WhatsAppMessages
            .FirstOrDefaultAsync(m => m.ExternalId == externalId);

        if (message is null) return;

        // Only advance status — never regress (e.g., don't overwrite "read" with "delivered")
        var statusRank = new Dictionary<string, int>
        {
            ["pending"]   = 0,
            ["sent"]      = 1,
            ["delivered"] = 2,
            ["read"]      = 3,
            ["failed"]    = -1
        };

        var current = statusRank.GetValueOrDefault(message.Status, 0);
        var incoming = statusRank.GetValueOrDefault(mappedStatus, 0);

        if (mappedStatus != "failed" && incoming <= current) return;

        message.Status = mappedStatus;

        if (mappedStatus == "delivered" && message.DeliveredAt is null)
            message.DeliveredAt = DateTime.UtcNow;

        if (mappedStatus == "failed")
        {
            if (statusEl.TryGetProperty("errors", out var errors))
            {
                var firstError = errors.EnumerateArray().FirstOrDefault();
                if (firstError.ValueKind != JsonValueKind.Undefined
                    && firstError.TryGetProperty("message", out var errMsg))
                {
                    var errText = errMsg.GetString() ?? "Meta reported failure";
                    message.ErrorMessage = errText.Length > 500 ? errText[..500] : errText;
                }
            }
        }

        await db.SaveChangesAsync();

        logger.LogInformation(
            "WhatsApp status update: externalId={ExternalId} → {Status}",
            externalId, mappedStatus);
    }

    private async Task<bool> ValidateSignatureAsync()
    {
        var appSecret = config["WhatsApp:AppSecret"];

        // SEC-07: Fail closed when AppSecret is not configured. Previously this returned
        // true (accepting any request without HMAC verification), allowing forged webhook
        // status callbacks. In production, WhatsApp:AppSecret must be set; in dev, set a
        // test secret or the webhook will reject all requests.
        if (string.IsNullOrEmpty(appSecret))
        {
            logger.LogWarning("WhatsApp:AppSecret not configured — webhook request REJECTED (fail-closed). Set WhatsApp:AppSecret to accept webhook callbacks.");
            return false;
        }

        if (!Request.Headers.TryGetValue("X-Hub-Signature-256", out var sigHeader))
            return false;

        var signature = sigHeader.ToString();
        if (!signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            return false;

        // Buffer the body so we can both validate and parse it
        Request.EnableBuffering();
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms);
        var body = ms.ToArray();
        Request.Body.Position = 0;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var computed = hmac.ComputeHash(body);
        var computedHex = "sha256=" + Convert.ToHexString(computed).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHex),
            Encoding.UTF8.GetBytes(signature));
    }
}
