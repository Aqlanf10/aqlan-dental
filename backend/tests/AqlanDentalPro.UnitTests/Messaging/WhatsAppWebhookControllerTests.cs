using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using System.Text.Json;
using Xunit;

namespace AqlanDentalPro.UnitTests.Messaging;

/// <summary>
/// Unit tests for <see cref="WhatsAppWebhookController"/> — the public, unauthenticated
/// endpoint Meta calls to (1) verify the webhook and (2) deliver delivery-status callbacks.
///
/// Coverage map:
///   - Verify (GET): 200 with challenge when mode=subscribe + token matches; Forbid
///     otherwise; Forbid when WebhookVerifyToken config is missing/empty (fail-closed).
///   - Receive (POST): 401 when AppSecret is missing/empty (SEC-07 fail-closed).
///   - Receive (POST): 401 when X-Hub-Signature-256 header is absent.
///   - Receive (POST): 401 when signature doesn't match the body's HMAC.
///   - Receive (POST): 200 (no-op) when payload has no `entry` (malformed / non-Meta).
///   - Receive (POST): 200 + status update applied when signature matches and payload
///     contains a `delivered` status for a known WhatsAppMessage.
///
/// FAIL-CLOSED CONTRACT (the audit's central WhatsApp security pin):
///   SEC-07 — when <c>WhatsApp:AppSecret</c> is not configured, the webhook MUST reject
///   ALL POST callbacks with 401. Previously it returned true from ValidateSignatureAsync,
///   accepting forged status callbacks. The test
///   <c>Receive_AppSecretMissing_Returns401_FailClosed</c> pins this contract.
/// </summary>
public class WhatsAppWebhookControllerTests : IDisposable
{
    private readonly AppDbContext _db;

    public WhatsAppWebhookControllerTests()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"whatsapp-webhook-tests-{Guid.NewGuid()}")
            .Options);
    }

    public void Dispose() => _db.Dispose();

    // ── Verify (GET /api/whatsapp/webhook) ───────────────────────────────────────

    [Fact]
    public void Verify_CorrectModeAndToken_Returns200_WithChallenge()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["WhatsApp:WebhookVerifyToken"]).Returns("verify-token-123");
        var controller = MessagingTestData.BuildWebhookController(_db, config);

        var result = controller.Verify("subscribe", "verify-token-123", "challenge-abc");

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().Be("challenge-abc",
            "the webhook handshake echoes back the hub.challenge value verbatim");
    }

    [Fact]
    public void Verify_WrongToken_ReturnsForbid()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["WhatsApp:WebhookVerifyToken"]).Returns("correct-token");
        var controller = MessagingTestData.BuildWebhookController(_db, config);

        var result = controller.Verify("subscribe", "wrong-token", "challenge");

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public void Verify_WrongMode_ReturnsForbid()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["WhatsApp:WebhookVerifyToken"]).Returns("verify-token-123");
        var controller = MessagingTestData.BuildWebhookController(_db, config);

        var result = controller.Verify("not-subscribe", "verify-token-123", "challenge");

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public void Verify_TokenConfigMissing_ReturnsForbid_FailClosed()
    {
        // FAIL-CLOSED: when WebhookVerifyToken is not configured, the endpoint must
        // reject ALL verification attempts (no fallback to "no-token-required").
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["WhatsApp:WebhookVerifyToken"]).Returns((string?)null);
        var controller = MessagingTestData.BuildWebhookController(_db, config);

        var result = controller.Verify("subscribe", "any-token", "challenge");

        result.Should().BeOfType<ForbidResult>(
            "missing WebhookVerifyToken must fail-closed — Meta would never legitimately " +
            "call an unconfigured webhook");
    }

    // ── Receive (POST /api/whatsapp/webhook) — fail-closed + signature validation ─

    [Fact]
    public async Task Receive_AppSecretMissing_Returns401_FailClosed()
    {
        // SEC-07 FAIL-CLOSED CONTRACT: when WhatsApp:AppSecret is not configured, the
        // webhook MUST reject ALL POST callbacks with 401. Previously it returned true
        // from ValidateSignatureAsync, accepting forged status callbacks. This is the
        // audit's central WhatsApp security pin.
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["WhatsApp:AppSecret"]).Returns((string?)null);

        var controller = MessagingTestData.BuildWebhookController(_db, config);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        // No X-Hub-Signature-256 header — but that's irrelevant because AppSecret missing
        // fails first.

        var result = await controller.Receive();

        var unauthorized = result.Should().BeOfType<UnauthorizedResult>().Subject;
        unauthorized.StatusCode.Should().Be(401,
            "missing AppSecret must fail-closed with 401 — never accept unverified callbacks");
    }

    [Fact]
    public async Task Receive_AppSecretConfigured_ButNoSignatureHeader_Returns401()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["WhatsApp:AppSecret"]).Returns("the-secret");

        var httpContext = new DefaultHttpContext();
        // No X-Hub-Signature-256 header set

        var controller = MessagingTestData.BuildWebhookController(_db, config);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.Receive();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Receive_SignatureDoesNotMatch_Returns401()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["WhatsApp:AppSecret"]).Returns("the-secret");

        var bodyBytes = Encoding.UTF8.GetBytes("{}");
        var httpContext = BuildHttpContextWithBody(bodyBytes);
        httpContext.Request.Headers["X-Hub-Signature-256"] = "sha256=deadbeef";

        var controller = MessagingTestData.BuildWebhookController(_db, config);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.Receive();

        result.Should().BeOfType<UnauthorizedResult>(
            "a signature that doesn't match the body's HMAC must be rejected with 401");
    }

    [Fact]
    public async Task Receive_ValidSignature_EmptyPayload_Returns200_NoOp()
    {
        // A valid signature with an empty/non-Meta payload (no `entry` field) is a no-op:
        // the controller returns 200 without touching the DB. Meta may send pings or
        // unrelated callbacks; they must be acknowledged with 200.
        var appSecret = "the-secret";
        var bodyBytes = Encoding.UTF8.GetBytes("""{"object":"not_whatsapp"}""");
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["WhatsApp:AppSecret"]).Returns(appSecret);

        var httpContext = BuildHttpContextWithBody(bodyBytes);
        httpContext.Request.Headers["X-Hub-Signature-256"] = ComputeHmac(appSecret, bodyBytes);

        var controller = MessagingTestData.BuildWebhookController(_db, config);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.Receive();

        result.Should().BeOfType<OkResult>(
            "an empty/non-Meta payload must be acknowledged with a bare 200 — Meta retries on non-2xx");
    }

    [Fact]
    public async Task Receive_ValidSignature_DeliveredStatus_UpdatesMessageStatus()
    {
        // Happy path: Meta delivers a `delivered` status callback for a known message
        // (matched by externalId). The controller must update the DB row's Status to
        // "delivered" + stamp DeliveredAt. This is the only path where the webhook
        // actually mutates state.
        var appSecret = "the-secret";
        var externalId = "wamid.HBgM..." ;
        var message = new WhatsAppMessage
        {
            PatientId = Guid.NewGuid(),
            PhoneNumber = "+967777123456",
            TemplateType = "appointment_reminder",
            MessageContent = "تذكير",
            Status = "sent", // currently "sent"
            ExternalId = externalId,
            IsActive = true
        };
        _db.WhatsAppMessages.Add(message);
        await _db.SaveChangesAsync();

        var payload = $$"""
        {
          "object": "whatsapp_business_account",
          "entry": [{
            "id": "123",
            "changes": [{
              "value": {
                "statuses": [{
                  "id": "{{externalId}}",
                  "status": "delivered",
                  "timestamp": "1700000000"
                }]
              }
            }]
          }]
        }
        """;
        var bodyBytes = Encoding.UTF8.GetBytes(payload);
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["WhatsApp:AppSecret"]).Returns(appSecret);

        var httpContext = BuildHttpContextWithBody(bodyBytes);
        httpContext.Request.Headers["X-Hub-Signature-256"] = ComputeHmac(appSecret, bodyBytes);

        var controller = MessagingTestData.BuildWebhookController(_db, config);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.Receive();

        result.Should().BeOfType<OkResult>();

        _db.ChangeTracker.Clear();
        var stored = await _db.WhatsAppMessages.FirstAsync(m => m.Id == message.Id);
        stored.Status.Should().Be("delivered",
            "the webhook must advance the message Status from 'sent' to 'delivered'");
        stored.DeliveredAt.Should().NotBeNull(
            "a 'delivered' status callback must stamp DeliveredAt");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static DefaultHttpContext BuildHttpContextWithBody(byte[] bodyBytes)
    {
        var httpContext = new DefaultHttpContext();
        var stream = new MemoryStream(bodyBytes);
        stream.Position = 0;
        httpContext.Request.Body = stream;
        httpContext.Request.Method = "POST";
        httpContext.Request.ContentType = "application/json";
        return httpContext;
    }

    private static string ComputeHmac(string secret, byte[] body)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(body);
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
