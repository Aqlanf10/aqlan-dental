using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using AqlanDentalPro.Infrastructure.Services.Ai;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ceph;

/// <summary>
/// Admin AI settings endpoints (api/ai-settings): GET must expose only a
/// masked key status (never the key itself), PUT validates with Arabic
/// errors and clamps numerics, and test-connection is a SAFE local check
/// that never calls the external AI API.
/// </summary>
[Collection("ai-env")]
public class AiSettingsTests
{
    private const string LongTestKey = "sk-test-very-secret-key-value-9876";

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            throw new InvalidOperationException("test-connection must NEVER call the external AI API");
        }
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static (AiSettingsController Controller, ThrowingHandler Handler) CreateController(AppDbContext db)
    {
        var handler = new ThrowingHandler();
        var factory = new StubHttpClientFactory(handler);
        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.UserId).Returns(Guid.NewGuid());

        var service = new CephAiDraftService(
            db,
            new IAiDraftProvider[] { new GeminiAiDraftProvider(factory), new AnthropicAiDraftProvider(factory) },
            user.Object,
            new Mock<ILogger<CephAiDraftService>>().Object);

        return (new AiSettingsController(db, service), handler);
    }

    private static JsonDocument ToJson(IActionResult result)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        return JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
    }

    // ─── GET ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_ReturnsDefaults_WhenNoSettingsRows()
    {
        await CephAiDraftTests.WithEnvKeyAsync("GEMINI_API_KEY", null, async () =>
        {
            await using var db = CreateDb();
            var (controller, _) = CreateController(db);

            using var json = ToJson(await controller.Get());
            var root = json.RootElement;

            root.GetProperty("enabled").GetBoolean().Should().BeFalse("default is disabled");
            root.GetProperty("provider").GetString().Should().Be("gemini");
            root.GetProperty("model").GetString().Should().Be("gemini-3.5-flash");
            root.GetProperty("maxTokens").GetInt32().Should().Be(1500);
            root.GetProperty("temperature").GetDouble().Should().Be(0.4);
            root.GetProperty("monthlyLimit").GetInt32().Should().Be(0);
            root.GetProperty("usageThisMonth").GetInt32().Should().Be(0);

            var keyStatus = root.GetProperty("keyStatus");
            foreach (var provider in new[] { "gemini", "anthropic", "openai" })
                keyStatus.GetProperty(provider).GetProperty("configured").ValueKind
                    .Should().BeOneOf(JsonValueKind.True, JsonValueKind.False);

            keyStatus.GetProperty("gemini").GetProperty("configured").GetBoolean().Should().BeFalse();
            keyStatus.GetProperty("gemini").GetProperty("masked").ValueKind.Should().Be(JsonValueKind.Null);
        });
    }

    [Fact]
    public async Task Get_MasksConfiguredKey_AndNeverLeaksTheRawKey()
    {
        await CephAiDraftTests.WithEnvKeyAsync("GEMINI_API_KEY", LongTestKey, async () =>
        {
            await using var db = CreateDb();
            var (controller, _) = CreateController(db);

            var result = await controller.Get();
            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            var rawJson = JsonSerializer.Serialize(ok.Value);

            rawJson.Should().Contain("********9876",
                "the key status must show only ******** plus the last 4 characters");
            rawJson.Should().NotContain(LongTestKey,
                "the raw API key must NEVER appear anywhere in the response");

            using var json = JsonDocument.Parse(rawJson);
            var gemini = json.RootElement.GetProperty("keyStatus").GetProperty("gemini");
            gemini.GetProperty("configured").GetBoolean().Should().BeTrue();
            gemini.GetProperty("masked").GetString().Should().Be("********9876");
        });
    }

    [Fact]
    public async Task Get_UsageThisMonth_CountsOnlySucceededRows()
    {
        await using var db = CreateDb();
        db.OrthodonticAiLogs.AddRange(
            new OrthodonticAiLog { AnalysisId = Guid.NewGuid(), Action = "ceph_draft_diagnosis", Succeeded = true, OutputLength = 5 },
            new OrthodonticAiLog { AnalysisId = Guid.NewGuid(), Action = "ceph_draft_diagnosis", Succeeded = true, OutputLength = 5 },
            new OrthodonticAiLog { AnalysisId = Guid.NewGuid(), Action = "ceph_draft_diagnosis", Succeeded = false, ErrorSummary = "feature_disabled" });
        await db.SaveChangesAsync();

        var (controller, _) = CreateController(db);
        using var json = ToJson(await controller.Get());

        json.RootElement.GetProperty("usageThisMonth").GetInt32().Should().Be(2,
            "failed attempts must not count against the monthly limit");
    }

    // ─── PUT ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Put_UpsertsSettingsRows_AndRoundTrips()
    {
        await using var db = CreateDb();
        var (controller, _) = CreateController(db);

        var result = await controller.Update(new UpdateAiSettingsRequest
        {
            Enabled = true,
            Provider = "anthropic",
            Model = "claude-sonnet-4-6",
            MaxTokens = 2000,
            Temperature = 0.2,
            MonthlyLimit = 50,
        });

        using var json = ToJson(result);
        var root = json.RootElement;
        root.GetProperty("enabled").GetBoolean().Should().BeTrue();
        root.GetProperty("provider").GetString().Should().Be("anthropic");
        root.GetProperty("model").GetString().Should().Be("claude-sonnet-4-6");
        root.GetProperty("maxTokens").GetInt32().Should().Be(2000);
        root.GetProperty("temperature").GetDouble().Should().Be(0.2);
        root.GetProperty("monthlyLimit").GetInt32().Should().Be(50);

        // Persisted as plain Settings rows — configurable, no hardcoding.
        (await db.Settings.SingleAsync(s => s.Key == "ai.ceph_draft_enabled")).Value.Should().Be("true");
        (await db.Settings.SingleAsync(s => s.Key == "ai.provider")).Value.Should().Be("anthropic");
        (await db.Settings.SingleAsync(s => s.Key == "ai.model")).Value.Should().Be("claude-sonnet-4-6");
        (await db.Settings.SingleAsync(s => s.Key == "ai.max_tokens")).Value.Should().Be("2000");
        (await db.Settings.SingleAsync(s => s.Key == "ai.temperature")).Value.Should().Be("0.2");
        (await db.Settings.SingleAsync(s => s.Key == "ai.monthly_limit")).Value.Should().Be("50");
    }

    [Fact]
    public async Task Put_ClampsNumericValues()
    {
        await using var db = CreateDb();
        var (controller, _) = CreateController(db);

        var result = await controller.Update(new UpdateAiSettingsRequest
        {
            Enabled = false,
            Provider = "gemini",
            Model = "gemini-3.5-flash",
            MaxTokens = 99999,      // > 8000
            Temperature = 7.5,      // > 1
            MonthlyLimit = -3,      // < 0
        });

        using var json = ToJson(result);
        json.RootElement.GetProperty("maxTokens").GetInt32().Should().Be(8000);
        json.RootElement.GetProperty("temperature").GetDouble().Should().Be(1.0);
        json.RootElement.GetProperty("monthlyLimit").GetInt32().Should().Be(0);

        var low = await controller.Update(new UpdateAiSettingsRequest
        {
            Provider = "gemini", Model = "gemini-3.5-flash", MaxTokens = 5, Temperature = -2,
        });
        using var lowJson = ToJson(low);
        lowJson.RootElement.GetProperty("maxTokens").GetInt32().Should().Be(100);
        lowJson.RootElement.GetProperty("temperature").GetDouble().Should().Be(0.0);
    }

    [Fact]
    public async Task Put_RejectsUnknownProvider_WithArabicMessage()
    {
        await using var db = CreateDb();
        var (controller, _) = CreateController(db);

        var result = await controller.Update(new UpdateAiSettingsRequest
        {
            Provider = "skynet", Model = "gemini-3.5-flash",
        });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value!.ToString().Should().Contain("مزود الذكاء الاصطناعي غير صالح");
        (await db.Settings.CountAsync()).Should().Be(0, "nothing is persisted on validation failure");
    }

    [Fact]
    public async Task Put_RejectsEmptyModel_WithArabicMessage()
    {
        await using var db = CreateDb();
        var (controller, _) = CreateController(db);

        var result = await controller.Update(new UpdateAiSettingsRequest { Provider = "gemini", Model = "  " });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value!.ToString().Should().Contain("اسم نموذج الذكاء الاصطناعي مطلوب");
    }

    [Fact]
    public async Task Put_AcceptsOpenAi_AsRecognizedProvider()
    {
        await using var db = CreateDb();
        var (controller, _) = CreateController(db);

        // "openai" is a valid configuration choice — only the draft call itself
        // refuses honestly («مزود openai غير مدعوم بعد») until it is implemented.
        var result = await controller.Update(new UpdateAiSettingsRequest
        {
            Provider = "openai", Model = "gpt-test",
        });

        using var json = ToJson(result);
        json.RootElement.GetProperty("provider").GetString().Should().Be("openai");
    }

    // ─── POST test-connection (SAFE — no external call) ──────────────────────

    [Fact]
    public async Task TestConnection_KeyConfigured_ReturnsLocalCheckMessage_WithoutCallingApi()
    {
        await CephAiDraftTests.WithEnvKeyAsync("GEMINI_API_KEY", LongTestKey, async () =>
        {
            await using var db = CreateDb();
            db.Settings.Add(new Setting { Key = CephAiDraftService.DraftEnabledSettingKey, Value = "true" });
            await db.SaveChangesAsync();
            var (controller, handler) = CreateController(db);

            using var json = ToJson(await controller.TestConnection());

            json.RootElement.GetProperty("ok").GetBoolean().Should().BeTrue();
            json.RootElement.GetProperty("message").GetString()
                .Should().Contain("تحقق محلي: المفتاح مهيأ — لم يتم إجراء اتصال خارجي",
                    "the response must be honestly labeled as a local check");
            handler.Calls.Should().Be(0, "test-connection must never call the external AI API");
        });
    }

    [Fact]
    public async Task TestConnection_KeyMissing_ReturnsHonestArabicFailure()
    {
        await CephAiDraftTests.WithEnvKeyAsync("GEMINI_API_KEY", null, async () =>
        {
            await using var db = CreateDb();
            var (controller, handler) = CreateController(db);

            using var json = ToJson(await controller.TestConnection());

            json.RootElement.GetProperty("ok").GetBoolean().Should().BeFalse();
            json.RootElement.GetProperty("message").GetString()
                .Should().Contain("GEMINI_API_KEY", "the admin must learn exactly which env var to set");
            handler.Calls.Should().Be(0);
        });
    }

    [Fact]
    public async Task TestConnection_OpenAiProvider_HonestUnsupportedMessage()
    {
        await CephAiDraftTests.WithEnvKeyAsync("OPENAI_API_KEY", LongTestKey, async () =>
        {
            await using var db = CreateDb();
            db.Settings.Add(new Setting { Key = CephAiDraftService.ProviderSettingKey, Value = "openai" });
            await db.SaveChangesAsync();
            var (controller, handler) = CreateController(db);

            using var json = ToJson(await controller.TestConnection());

            json.RootElement.GetProperty("ok").GetBoolean().Should().BeFalse();
            json.RootElement.GetProperty("message").GetString().Should().Contain("مزود openai غير مدعوم بعد");
            handler.Calls.Should().Be(0);
        });
    }

    // ─── Source checks (established project pattern) ─────────────────────────

    [Fact]
    public void AiSettingsController_NoSecretOrExceptionLeak_SourceCheck()
    {
        var controllerPath = CephAiDraftTests.ControllerSourcePath("AiSettingsController.cs");
        if (!File.Exists(controllerPath)) return;

        var content = File.ReadAllText(controllerPath);
        content.Should().Contain("AdminOnly", "AI settings are admin-only");
        content.Should().NotContain("ex.StackTrace");
        content.Should().NotContain("detail = ex.Message");
        // The endpoints must never accept or return a raw key value.
        content.Should().NotContain("apiKey =", "the settings controller never handles a raw key beyond masking");
    }
}
