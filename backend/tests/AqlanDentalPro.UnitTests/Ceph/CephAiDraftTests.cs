using AqlanDentalPro.Application.Exceptions;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using AqlanDentalPro.Infrastructure.Services.Ai;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using System.Text;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ceph;

/// <summary>
/// Serializes every test class that mutates the process-wide AI key env vars
/// (GEMINI_API_KEY / ANTHROPIC_API_KEY / OPENAI_API_KEY).
/// </summary>
[CollectionDefinition("ai-env")]
public class AiEnvCollection;

/// <summary>
/// C-D tests: the LLM draft-diagnosis assistant must never fake AI — honest
/// Arabic errors when disabled/unsupported/unconfigured, a real provider call
/// when enabled (mocked HttpMessageHandler here for BOTH gemini and
/// anthropic), the configurable monthly limit, the mandatory review
/// disclaimer on every output, and a full audit row (success AND failure)
/// with counts-only input summaries and provider/model in ModelId.
/// </summary>
[Collection("ai-env")]
public class CephAiDraftTests
{
    private const string PatientFirstName = "مريضحساس";
    private const string TestApiKey = "test-key-secret-abcd1234";

    // ─── Test plumbing ────────────────────────────────────────────────────────

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    /// <summary>Captures the outgoing request and returns a canned response.</summary>
    private sealed class StubHandler(HttpStatusCode status, string responseBody) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static CephAiDraftService CreateService(
        AppDbContext db,
        HttpMessageHandler? handler = null,
        Guid? userId = null)
    {
        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.UserId).Returns(userId ?? Guid.NewGuid());

        var factory = new StubHttpClientFactory(handler ?? new StubHandler(HttpStatusCode.OK, "{}"));
        var providers = new IAiDraftProvider[]
        {
            new GeminiAiDraftProvider(factory),
            new AnthropicAiDraftProvider(factory),
        };

        return new CephAiDraftService(
            db,
            providers,
            user.Object,
            new Mock<ILogger<CephAiDraftService>>().Object);
    }

    private static async Task<Guid> SeedAnalysisAsync(AppDbContext db)
    {
        var patient = new Patient { Id = Guid.NewGuid(), FirstName = PatientFirstName, LastName = "تجريبي", PatientNumber = "P-771", IsActive = true };
        var orthoCase = new OrthoCase { Id = Guid.NewGuid(), PatientId = patient.Id, CaseNumber = "OC-AI-1", IsActive = true };
        var analysis = new CephAnalysis
        {
            Id = Guid.NewGuid(),
            OrthoCaseId = orthoCase.Id,
            AnalysisDate = new DateOnly(2026, 6, 1),
            AnalysisType = "steiner",
            IsActive = true,
        };
        db.AddRange(patient, orthoCase, analysis);
        db.CephMeasurements.AddRange(
            new CephMeasurement { AnalysisId = analysis.Id, MeasurementName = "SNA", MeasurementValue = 86, NormalValue = 82, StdDeviation = 2, Unit = "°", Classification = "mild", IsActive = true },
            new CephMeasurement { AnalysisId = analysis.Id, MeasurementName = "ANB", MeasurementValue = 6, NormalValue = 2, StdDeviation = 1, Unit = "°", Classification = "severe", IsActive = true });
        db.CephDiagnoses.Add(new CephDiagnosis
        {
            AnalysisId = analysis.Id,
            SkeletalClass = "Class II",
            VerticalPattern = "Normodivergent",
            AiRecommendation = "التشخيص الهيكلي: الصنف الثاني الهيكلي",
            IsActive = true,
        });
        await db.SaveChangesAsync();
        return analysis.Id;
    }

    private static async Task EnableFeatureAsync(
        AppDbContext db, string? provider = null, string? modelId = null, int? monthlyLimit = null)
    {
        db.Settings.Add(new Setting { Key = CephAiDraftService.DraftEnabledSettingKey, Value = "true" });
        if (provider is not null)
            db.Settings.Add(new Setting { Key = CephAiDraftService.ProviderSettingKey, Value = provider });
        if (modelId is not null)
            db.Settings.Add(new Setting { Key = CephAiDraftService.ModelSettingKey, Value = modelId });
        if (monthlyLimit is not null)
            db.Settings.Add(new Setting { Key = CephAiDraftService.MonthlyLimitSettingKey, Value = monthlyLimit.Value.ToString() });
        await db.SaveChangesAsync();
    }

    /// <summary>Runs an action with the given AI key env var set to a value (or removed), then restores it.</summary>
    internal static async Task WithEnvKeyAsync(string envVar, string? value, Func<Task> action)
    {
        var saved = Environment.GetEnvironmentVariable(envVar);
        Environment.SetEnvironmentVariable(envVar, value);
        try { await action(); }
        finally { Environment.SetEnvironmentVariable(envVar, saved); }
    }

    private const string GeminiSuccessJson =
        """
        {
          "candidates": [
            {
              "content": {
                "role": "model",
                "parts": [ { "text": "مسودة تشخيص: الصنف الثاني الهيكلي — تتطلب مراجعة الأخصائي." } ]
              },
              "finishReason": "STOP"
            }
          ]
        }
        """;

    private const string AnthropicSuccessJson =
        """
        {
          "id": "msg_test",
          "model": "claude-sonnet-4-6",
          "content": [ { "type": "text", "text": "مسودة تشخيص: الصنف الثاني الهيكلي — تتطلب مراجعة الأخصائي." } ],
          "stop_reason": "end_turn"
        }
        """;

    // ─── Honest availability errors ──────────────────────────────────────────

    [Fact]
    public async Task FlagDisabled_ReturnsHonestArabicError_AndGenerateThrows()
    {
        await using var db = CreateDb();
        var analysisId = await SeedAnalysisAsync(db);
        // No "ai.ceph_draft_enabled" row at all → default DISABLED.
        var service = CreateService(db);

        var (enabled, error) = await service.GetAvailabilityAsync();
        enabled.Should().BeFalse();
        error.Should().Be("مساعد الذكاء الاصطناعي معطل من الإعدادات");

        var act = () => service.GenerateDraftAsync(analysisId);
        (await act.Should().ThrowAsync<CephAiUnavailableException>())
            .WithMessage("مساعد الذكاء الاصطناعي معطل من الإعدادات");

        // Audit every call — even the honest refusal to run.
        var log = await db.OrthodonticAiLogs.SingleAsync(l => l.AnalysisId == analysisId);
        log.Succeeded.Should().BeFalse();
        log.ErrorSummary.Should().Be("feature_disabled");
    }

    [Fact]
    public async Task EnabledButNoApiKey_ReturnsExactRequiredArabicMessage()
    {
        await WithEnvKeyAsync("GEMINI_API_KEY", null, async () =>
        {
            await using var db = CreateDb();
            var analysisId = await SeedAnalysisAsync(db);
            await EnableFeatureAsync(db); // default provider = gemini

            var service = CreateService(db);

            var (enabled, error) = await service.GetAvailabilityAsync();
            enabled.Should().BeFalse();
            error.Should().Be("ميزة الذكاء الاصطناعي غير مفعلة أو لم يتم إعداد مفتاح API بعد.");

            var act = () => service.GenerateDraftAsync(analysisId);
            (await act.Should().ThrowAsync<CephAiUnavailableException>())
                .WithMessage("ميزة الذكاء الاصطناعي غير مفعلة أو لم يتم إعداد مفتاح API بعد.");

            var log = await db.OrthodonticAiLogs.SingleAsync(l => l.AnalysisId == analysisId);
            log.Succeeded.Should().BeFalse();
            log.ErrorSummary.Should().Be("api_key_missing");
        });
    }

    [Fact]
    public async Task OpenAiProvider_RecognizedButUnavailable_HonestArabicError()
    {
        await WithEnvKeyAsync("OPENAI_API_KEY", "some-openai-key", async () =>
        {
            await using var db = CreateDb();
            var analysisId = await SeedAnalysisAsync(db);
            await EnableFeatureAsync(db, provider: "openai");
            var service = CreateService(db);

            // Even with a key present: no fake AI, no silent fallback — honest refusal.
            var act = () => service.GenerateDraftAsync(analysisId);
            (await act.Should().ThrowAsync<CephAiUnavailableException>())
                .WithMessage("مزود openai غير مدعوم بعد");

            var log = await db.OrthodonticAiLogs.SingleAsync(l => l.AnalysisId == analysisId);
            log.Succeeded.Should().BeFalse();
            log.ErrorSummary.Should().Be("provider_unsupported:openai");
        });
    }

    // ─── Success path — gemini (default provider, mocked API) ────────────────

    [Fact]
    public async Task Success_Gemini_UsesHeaderKeyAndDefaultModel_AndAuditsProviderModel()
    {
        await WithEnvKeyAsync("GEMINI_API_KEY", TestApiKey, async () =>
        {
            await using var db = CreateDb();
            var analysisId = await SeedAnalysisAsync(db);
            await EnableFeatureAsync(db); // provider + model omitted → defaults gemini / gemini-3.5-flash
            var userId = Guid.NewGuid();
            var handler = new StubHandler(HttpStatusCode.OK, GeminiSuccessJson);
            var service = CreateService(db, handler, userId: userId);

            var result = await service.GenerateDraftAsync(analysisId);

            result.Should().NotBeNull();
            result!.Draft.Should().Contain("مسودة تشخيص");
            result.ModelId.Should().Be("gemini/gemini-3.5-flash");
            result.Disclaimer.Should().Be(
                "هذه مسودة مولّدة بالذكاء الاصطناعي — تتطلب مراجعة واعتماد أخصائي التقويم قبل أي استخدام سريري.",
                "every LLM output must carry the mandatory review disclaimer");

            // Real Gemini wire format: correct URL, key in the header, NEVER in the URL.
            handler.LastRequest.Should().NotBeNull();
            var url = handler.LastRequest!.RequestUri!.ToString();
            url.Should().Be("https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash:generateContent");
            url.Should().NotContain(TestApiKey, "the API key must never appear in URLs/logs");
            handler.LastRequest.RequestUri!.Query.Should().BeEmpty("no query string — the key travels in a header");
            handler.LastRequest.Headers.GetValues("x-goog-api-key").Single().Should().Be(TestApiKey);

            // Gemini body shape + defaults.
            handler.LastRequestBody.Should().Contain("systemInstruction").And.Contain("generationConfig");
            handler.LastRequestBody.Should().Contain("\"maxOutputTokens\":1500").And.Contain("\"temperature\":0.4");

            // Prompt carries the measurement values — and nothing about the patient.
            handler.LastRequestBody.Should().Contain("SNA").And.Contain("86");
            handler.LastRequestBody.Should().NotContain(PatientFirstName,
                "patient identifiers must never be sent to the AI API");

            // Audit row: success, counts only, provider+model in ModelId.
            var log = await db.OrthodonticAiLogs.SingleAsync(l => l.AnalysisId == analysisId);
            log.Succeeded.Should().BeTrue();
            log.Action.Should().Be("ceph_draft_diagnosis");
            log.UserId.Should().Be(userId);
            log.ModelId.Should().Be("gemini/gemini-3.5-flash");
            log.OutputLength.Should().Be(result.Draft.Length);
            log.InputSummary.Should().Be("measurements:2;diagnosisPresent:true;examPresent:false",
                "the input summary must hold counts only");
            log.InputSummary.Should().NotContain(PatientFirstName);
            log.ErrorSummary.Should().BeNull();
        });
    }

    // ─── Success path — anthropic (mocked API) ───────────────────────────────

    [Fact]
    public async Task Success_Anthropic_UsesAnthropicWireFormat()
    {
        await WithEnvKeyAsync("ANTHROPIC_API_KEY", TestApiKey, async () =>
        {
            await using var db = CreateDb();
            var analysisId = await SeedAnalysisAsync(db);
            await EnableFeatureAsync(db, provider: "anthropic", modelId: "claude-sonnet-4-6");
            var handler = new StubHandler(HttpStatusCode.OK, AnthropicSuccessJson);
            var service = CreateService(db, handler);

            var result = await service.GenerateDraftAsync(analysisId);

            result.Should().NotBeNull();
            result!.Draft.Should().Contain("مسودة تشخيص");
            result.ModelId.Should().Be("anthropic/claude-sonnet-4-6");

            var url = handler.LastRequest!.RequestUri!.ToString();
            url.Should().Be("https://api.anthropic.com/v1/messages");
            url.Should().NotContain(TestApiKey, "the API key must never appear in URLs/logs");
            handler.LastRequest.Headers.GetValues("x-api-key").Single().Should().Be(TestApiKey);
            handler.LastRequest.Headers.GetValues("anthropic-version").Single().Should().Be("2023-06-01");

            handler.LastRequestBody.Should().Contain("\"model\":\"claude-sonnet-4-6\"",
                "the model id must come from the Settings key ai.model — no hardcoding");
            handler.LastRequestBody.Should().Contain("\"max_tokens\":1500").And.Contain("\"temperature\":0.4");
            handler.LastRequestBody.Should().NotContain(PatientFirstName);

            var log = await db.OrthodonticAiLogs.SingleAsync(l => l.AnalysisId == analysisId);
            log.Succeeded.Should().BeTrue();
            log.ModelId.Should().Be("anthropic/claude-sonnet-4-6");
        });
    }

    // ─── Settings clamps ──────────────────────────────────────────────────────

    [Fact]
    public async Task Settings_DefaultsAndClamps_AreApplied()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        // Defaults when no rows exist.
        var defaults = await service.GetSettingsAsync();
        defaults.Enabled.Should().BeFalse("the feature is OFF by default");
        defaults.Provider.Should().Be("gemini");
        defaults.Model.Should().Be("gemini-3.5-flash");
        defaults.MaxTokens.Should().Be(1500);
        defaults.Temperature.Should().Be(0.4);
        defaults.MonthlyLimit.Should().Be(0, "0 = unlimited");

        // Out-of-range stored values are clamped, never trusted blindly.
        db.Settings.AddRange(
            new Setting { Key = CephAiDraftService.MaxTokensSettingKey, Value = "50" },
            new Setting { Key = CephAiDraftService.TemperatureSettingKey, Value = "3.7" },
            new Setting { Key = CephAiDraftService.MonthlyLimitSettingKey, Value = "-5" });
        await db.SaveChangesAsync();

        var clamped = await service.GetSettingsAsync();
        clamped.MaxTokens.Should().Be(100, "ai.max_tokens clamps to 100-8000");
        clamped.Temperature.Should().Be(1.0, "ai.temperature clamps to 0-1");
        clamped.MonthlyLimit.Should().Be(0);
    }

    // ─── Monthly limit ────────────────────────────────────────────────────────

    [Fact]
    public async Task MonthlyLimitReached_RejectsWithArabicMessage_AndAudits()
    {
        await WithEnvKeyAsync("GEMINI_API_KEY", TestApiKey, async () =>
        {
            await using var db = CreateDb();
            var analysisId = await SeedAnalysisAsync(db);
            await EnableFeatureAsync(db, monthlyLimit: 1);

            // One successful call already logged this month → limit of 1 reached.
            db.OrthodonticAiLogs.Add(new OrthodonticAiLog
            {
                AnalysisId = analysisId,
                Action = CephAiDraftService.ActionDraftDiagnosis,
                ModelId = "gemini/gemini-3.5-flash",
                Succeeded = true,
                OutputLength = 10,
            });
            await db.SaveChangesAsync();

            var handler = new StubHandler(HttpStatusCode.OK, GeminiSuccessJson);
            var service = CreateService(db, handler);

            var act = () => service.GenerateDraftAsync(analysisId);
            (await act.Should().ThrowAsync<CephAiLimitReachedException>())
                .WithMessage("تم بلوغ الحد الشهري لاستخدام الذكاء الاصطناعي");

            handler.LastRequest.Should().BeNull("the upstream API must not be called once the limit is reached");

            var log = await db.OrthodonticAiLogs
                .SingleAsync(l => l.AnalysisId == analysisId && l.ErrorSummary == "monthly_limit_reached");
            log.Succeeded.Should().BeFalse();

            // Failed attempts never consume the limit — only Succeeded rows count.
            (await service.CountUsageThisMonthAsync()).Should().Be(1);
        });
    }

    // ─── Upstream failure ─────────────────────────────────────────────────────

    [Fact]
    public async Task Http500FromApi_ThrowsTypedUpstreamError_AndAuditsFailure()
    {
        await WithEnvKeyAsync("GEMINI_API_KEY", TestApiKey, async () =>
        {
            await using var db = CreateDb();
            var analysisId = await SeedAnalysisAsync(db);
            await EnableFeatureAsync(db);
            var handler = new StubHandler(HttpStatusCode.InternalServerError, """{"type":"error"}""");
            var service = CreateService(db, handler);

            var act = () => service.GenerateDraftAsync(analysisId);
            await act.Should().ThrowAsync<CephAiUpstreamException>(
                "the controller maps this to the Arabic 502 «تعذر الاتصال بخدمة الذكاء الاصطناعي — حاول لاحقًا»");

            var log = await db.OrthodonticAiLogs.SingleAsync(l => l.AnalysisId == analysisId);
            log.Succeeded.Should().BeFalse();
            log.ErrorSummary.Should().Be("HTTP 500 from AI API",
                "the error summary must be a short status-code reason — no exception dumps, no secrets");
            log.ErrorSummary.Should().NotContain(TestApiKey);
            log.OutputLength.Should().Be(0);
        });
    }

    // ─── Prompt safety ────────────────────────────────────────────────────────

    [Fact]
    public void SystemPrompt_IsArabicClinical_AndForbidsInventingValues()
    {
        var system = CephAiDraftService.BuildSystemPrompt();

        system.Should().Contain("أنت مساعد لأخصائي تقويم الأسنان");
        system.Should().Contain("لا تخترع أي قيم قياس",
            "the model must be explicitly instructed to never invent measurement values");
        system.Should().Contain(CephAiDraftService.DisclaimerAr,
            "the model is instructed to end every draft with the review disclaimer");
        // Structured sections requested:
        system.Should().Contain("التشخيص الهيكلي").And.Contain("خيارات الخطة").And.Contain("فحوصات إضافية");
    }

    [Fact]
    public void UserContent_IncludesMeasurementValuesNormsAndRuleSummary()
    {
        var measurements = new List<CephMeasurement>
        {
            new() { MeasurementName = "SNA", MeasurementValue = 86, NormalValue = 82, StdDeviation = 2, Unit = "°", Classification = "mild" },
        };
        var diagnosis = new CephDiagnosis { SkeletalClass = "Class II", AiRecommendation = "ملخص محرك القواعد" };

        var content = CephAiDraftService.BuildUserContent("steiner", measurements, diagnosis, exam: null);

        content.Should().Contain("SNA").And.Contain("86").And.Contain("82").And.Contain("± 2");
        content.Should().Contain("mild");
        content.Should().Contain("Class II").And.Contain("ملخص محرك القواعد");
        content.Should().Contain("دون اختراع أي قيمة غير واردة");
    }

    // ─── Endpoint contract ────────────────────────────────────────────────────

    [Fact]
    public async Task DraftEndpoint_MissingAnalysis_Returns404Arabic()
    {
        await WithEnvKeyAsync("GEMINI_API_KEY", TestApiKey, async () =>
        {
            await using var db = CreateDb();
            await EnableFeatureAsync(db);
            var aiService = CreateService(db, new StubHandler(HttpStatusCode.OK, GeminiSuccessJson));

            var user = new Mock<ICurrentUserService>();
            user.Setup(u => u.UserId).Returns(Guid.NewGuid());
            var controller = new AqlanDentalPro.API.Controllers.CephController(
                new AqlanDentalPro.Application.Services.CephService(
                    db, user.Object, new Mock<ILogger<AqlanDentalPro.Application.Services.CephService>>().Object));

            var result = await controller.DraftDiagnosis(
                Guid.NewGuid(), aiService,
                new Mock<ILogger<AqlanDentalPro.API.Controllers.CephController>>().Object);

            var notFound = result.Should().BeOfType<Microsoft.AspNetCore.Mvc.NotFoundObjectResult>().Subject;
            notFound.Value!.ToString().Should().Contain("تحليل السيفالومتري غير موجود");
        });
    }

    [Fact]
    public async Task DraftEndpoint_Disabled_Returns403WithHonestMessage()
    {
        await using var db = CreateDb();
        var analysisId = await SeedAnalysisAsync(db); // feature flag NOT enabled
        var aiService = CreateService(db);

        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.UserId).Returns(Guid.NewGuid());
        var controller = new AqlanDentalPro.API.Controllers.CephController(
            new AqlanDentalPro.Application.Services.CephService(
                db, user.Object, new Mock<ILogger<AqlanDentalPro.Application.Services.CephService>>().Object));

        var result = await controller.DraftDiagnosis(
            analysisId, aiService,
            new Mock<ILogger<AqlanDentalPro.API.Controllers.CephController>>().Object);

        var status = result.Should().BeOfType<Microsoft.AspNetCore.Mvc.ObjectResult>().Subject;
        status.StatusCode.Should().Be(403);
        status.Value!.ToString().Should().Contain("مساعد الذكاء الاصطناعي معطل من الإعدادات");
    }

    // ─── Source checks (established project pattern) ─────────────────────────

    [Fact]
    public void DraftEndpoint_NoExceptionDetailLeak_SourceCheck()
    {
        var controllerPath = ControllerSourcePath("CephController.cs");
        if (!File.Exists(controllerPath)) return;

        var content = File.ReadAllText(controllerPath);
        content.Should().Contain("تعذر الاتصال بخدمة الذكاء الاصطناعي — حاول لاحقًا",
            "upstream AI failures must surface as the honest Arabic 502");
        // Security: exception internals must never be sent to clients.
        content.Should().NotContain("detail = ex.Message",
            "error responses must not leak exception messages to clients");
        content.Should().NotContain("message = ex.StackTrace");
        content.Should().NotContain("ex.StackTrace");
        content.Should().NotContain("type = ex.GetType().Name");
    }

    [Fact]
    public void AutoTraceEndpoint_RemainsHonest501_SourceCheck()
    {
        var controllerPath = ControllerSourcePath("CephController.cs");
        if (!File.Exists(controllerPath)) return;

        var content = File.ReadAllText(controllerPath);
        content.Should().Contain("Status501NotImplemented",
            "the auto-trace placeholder must stay an honest 501 — C-D replaces nothing existing");
    }

    internal static string ControllerSourcePath(string fileName) => Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..",
        "backend", "src", "AqlanDentalPro.API", "Controllers", fileName);
}
