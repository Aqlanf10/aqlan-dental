using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using AqlanDentalPro.Infrastructure.Services.Ai;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ceph;

[Collection("ai-env")]
public class CephAiLandmarkDraftTests
{
    private const string TestKey = "temporary-test-key-not-a-real-secret";

    private sealed class CapturingHandler(string responseBody) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    [Fact]
    public async Task GeminiProvider_SendsImageInBody_AndParsesNormalizedPoints()
    {
        var handler = new CapturingHandler(ResponseWithEightPoints());
        var provider = new GeminiCephLandmarkDraftProvider(new StubFactory(handler));

        var result = await provider.GenerateAsync(
            [1, 2, 3, 4], "image/jpeg", "gemini-3.5-flash", TestKey,
            CephAiPrecision.Draft, CancellationToken.None);

        result.Should().HaveCount(8);
        result[0].Key.Should().Be("S");
        result[0].XNormalized.Should().Be(100);
        handler.Request!.Headers.GetValues("x-goog-api-key").Single().Should().Be(TestKey);
        handler.Request.RequestUri!.ToString().Should().NotContain(TestKey);
        handler.Body.Should().Contain("inline_data");
        handler.Body.Should().Contain(Convert.ToBase64String([1, 2, 3, 4]));
        handler.Body.Should().Contain("responseMimeType");
    }

    [Fact]
    public async Task GeminiProvider_HighPrecision_DropsLandmarksUnderConfidenceThreshold()
    {
        // 9 high-confidence points + 3 low-confidence ones. In HIGH precision the
        // provider must drop the 3 low-confidence points (leaving 9); DRAFT keeps all 12.
        var generated =
            """{"landmarks":[{"key":"S","x":100,"y":200,"confidence":0.9},{"key":"N","x":200,"y":200,"confidence":0.85},{"key":"Or","x":300,"y":300,"confidence":0.8},{"key":"Po","x":150,"y":300,"confidence":0.75},{"key":"ANS","x":500,"y":400,"confidence":0.8},{"key":"PNS","x":350,"y":400,"confidence":0.7},{"key":"A","x":520,"y":480,"confidence":0.85},{"key":"B","x":540,"y":600,"confidence":0.85},{"key":"Pog","x":560,"y":650,"confidence":0.85},{"key":"Gn","x":570,"y":670,"confidence":0.4},{"key":"Me","x":575,"y":690,"confidence":0.35},{"key":"Go","x":100,"y":550,"confidence":0.2}]}""";
        var response = JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new { content = new { parts = new[] { new { text = generated } } } },
            },
        });
        var handler = new CapturingHandler(response);
        var provider = new GeminiCephLandmarkDraftProvider(new StubFactory(handler));

        var high = await provider.GenerateAsync(
            [1, 2, 3, 4], "image/jpeg", "gemini-3.5-flash", TestKey,
            CephAiPrecision.High, CancellationToken.None);
        high.Should().HaveCount(9, "high precision drops confidence < 0.5");
        high.Should().OnlyContain(p => (p.Confidence ?? 0) >= 0.5);
        high.Select(p => p.Key).Should().NotContain(new[] { "Gn", "Me", "Go" });

        handler = new CapturingHandler(response);
        var provider2 = new GeminiCephLandmarkDraftProvider(new StubFactory(handler));
        var draft = await provider2.GenerateAsync(
            [1, 2, 3, 4], "image/jpeg", "gemini-3.5-flash", TestKey,
            CephAiPrecision.Draft, CancellationToken.None);
        draft.Should().HaveCount(12, "draft keeps all defensible landmarks");
    }

    [Fact]
    public async Task GeminiProvider_ParsesReasoningField_WhenModelReturnsIt()
    {
        var generated =
            """{"landmarks":[{"key":"S","x":100,"y":200,"confidence":0.85,"reasoning":"center of sella turcica outline"},{"key":"N","x":200,"y":200,"confidence":0.85},{"key":"Or","x":300,"y":300,"confidence":0.8},{"key":"Po","x":150,"y":300,"confidence":0.8},{"key":"ANS","x":500,"y":400,"confidence":0.85},{"key":"PNS","x":350,"y":400,"confidence":0.8},{"key":"A","x":520,"y":480,"confidence":0.85},{"key":"B","x":540,"y":600,"confidence":0.85}]}""";
        var response = JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new { content = new { parts = new[] { new { text = generated } } } },
            },
        });
        var handler = new CapturingHandler(response);
        var provider = new GeminiCephLandmarkDraftProvider(new StubFactory(handler));

        var result = await provider.GenerateAsync(
            [1, 2, 3, 4], "image/jpeg", "gemini-3.5-flash", TestKey,
            CephAiPrecision.Draft, CancellationToken.None);

        result[0].Key.Should().Be("S");
        result[0].Reasoning.Should().Be("center of sella turcica outline");
        result[1].Reasoning.Should().BeNull();
    }

    [Fact]
    public async Task GeminiProvider_RefineAsync_ReturnsOnlyTheRequestedLandmark()
    {
        var generated =
            """{"landmarks":[{"key":"S","x":120,"y":210,"confidence":0.9,"reasoning":"center of sella outline reconfirmed"}]}""";
        var response = JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new { content = new { parts = new[] { new { text = generated } } } },
            },
        });
        var handler = new CapturingHandler(response);
        var provider = new GeminiCephLandmarkDraftProvider(new StubFactory(handler));

        var refined = await provider.RefineAsync(
            [1, 2, 3, 4], "image/jpeg", "gemini-3.5-flash", TestKey,
            new CephLandmarkRefineTarget("S", 100, 200), CancellationToken.None);

        refined.Should().NotBeNull();
        refined!.Key.Should().Be("S");
        refined.XNormalized.Should().Be(120);
        refined.YNormalized.Should().Be(210);
        refined.Reasoning.Should().Contain("sella");
    }

    // ─── Anthropic (Claude) vision provider — feature parity with Gemini ──────

    [Fact]
    public async Task AnthropicProvider_SendsImageAsBase64Block_WithHeaderKey_AndParsesPoints()
    {
        var handler = new CapturingHandler(AnthropicResponseWithEightPoints());
        var provider = new AnthropicCephLandmarkDraftProvider(new StubFactory(handler));

        var result = await provider.GenerateAsync(
            [1, 2, 3, 4], "image/png", "claude-sonnet-5", TestKey,
            CephAiPrecision.Draft, CancellationToken.None);

        result.Should().HaveCount(8);
        result[0].Key.Should().Be("S");
        result[0].XNormalized.Should().Be(100);
        // API key travels ONLY in the x-api-key header, never the URL.
        handler.Request!.Headers.GetValues("x-api-key").Single().Should().Be(TestKey);
        handler.Request.Headers.GetValues("anthropic-version").Single().Should().Be("2023-06-01");
        handler.Request.RequestUri!.ToString().Should().NotContain(TestKey);
        handler.Request.RequestUri!.ToString().Should().Be(AnthropicAiDraftProvider.MessagesUrl);
        // Anthropic multimodal wire format: base64 image block + text block.
        handler.Body.Should().Contain("\"type\":\"image\"");
        handler.Body.Should().Contain("\"media_type\":\"image/png\"");
        handler.Body.Should().Contain(Convert.ToBase64String([1, 2, 3, 4]));
    }

    [Fact]
    public async Task AnthropicProvider_NormalizesJpgMediaType_ToJpeg()
    {
        var handler = new CapturingHandler(AnthropicResponseWithEightPoints());
        var provider = new AnthropicCephLandmarkDraftProvider(new StubFactory(handler));

        await provider.GenerateAsync(
            [9, 9, 9], "image/jpg", "claude-sonnet-5", TestKey,
            CephAiPrecision.Draft, CancellationToken.None);

        // Anthropic rejects "image/jpg" — the provider must remap to canonical "image/jpeg".
        handler.Body.Should().Contain("\"media_type\":\"image/jpeg\"");
        handler.Body.Should().NotContain("image/jpg\"");
    }

    [Fact]
    public async Task AnthropicProvider_HighPrecision_DropsLandmarksUnderConfidenceThreshold()
    {
        var generated =
            """{"landmarks":[{"key":"S","x":100,"y":200,"confidence":0.9},{"key":"N","x":200,"y":200,"confidence":0.85},{"key":"Or","x":300,"y":300,"confidence":0.8},{"key":"Po","x":150,"y":300,"confidence":0.75},{"key":"ANS","x":500,"y":400,"confidence":0.8},{"key":"PNS","x":350,"y":400,"confidence":0.7},{"key":"A","x":520,"y":480,"confidence":0.85},{"key":"B","x":540,"y":600,"confidence":0.85},{"key":"Pog","x":560,"y":650,"confidence":0.85},{"key":"Gn","x":570,"y":670,"confidence":0.4},{"key":"Me","x":575,"y":690,"confidence":0.35}]}""";
        var handler = new CapturingHandler(WrapAnthropic(generated));
        var provider = new AnthropicCephLandmarkDraftProvider(new StubFactory(handler));

        var high = await provider.GenerateAsync(
            [1, 2, 3, 4], "image/png", "claude-sonnet-5", TestKey,
            CephAiPrecision.High, CancellationToken.None);
        high.Should().HaveCount(9, "high precision drops confidence < 0.5");
        high.Select(p => p.Key).Should().NotContain(new[] { "Gn", "Me" });
    }

    [Fact]
    public async Task AnthropicProvider_RefineAsync_ReturnsOnlyTheRequestedLandmark()
    {
        var generated =
            """{"landmarks":[{"key":"S","x":120,"y":210,"confidence":0.9,"reasoning":"center of sella outline reconfirmed"}]}""";
        var handler = new CapturingHandler(WrapAnthropic(generated));
        var provider = new AnthropicCephLandmarkDraftProvider(new StubFactory(handler));

        var refined = await provider.RefineAsync(
            [1, 2, 3, 4], "image/png", "claude-sonnet-5", TestKey,
            new CephLandmarkRefineTarget("S", 100, 200), CancellationToken.None);

        refined.Should().NotBeNull();
        refined!.Key.Should().Be("S");
        refined.XNormalized.Should().Be(120);
        refined.YNormalized.Should().Be(210);
        refined.Reasoning.Should().Contain("sella");
    }

    [Fact]
    public async Task LandmarkService_SelectsAnthropicProvider_WhenConfigured_ReturnsUnsavedDraft()
    {
        await CephAiDraftTests.WithEnvKeyAsync("ANTHROPIC_API_KEY", TestKey, async () =>
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), $"ceph-ai-anthropic-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);
            var previousUploadsPath = Environment.GetEnvironmentVariable("UPLOADS_PATH");
            Environment.SetEnvironmentVariable("UPLOADS_PATH", tempDirectory);
            try
            {
                await File.WriteAllBytesAsync(Path.Combine(tempDirectory, "trace.jpg"), [1, 2, 3, 4]);
                await using var db = CreateDb();
                var analysis = new CephAnalysis
                {
                    Id = Guid.NewGuid(),
                    OrthoCaseId = Guid.NewGuid(),
                    AnalysisType = "steiner",
                    XrayFileUrl = "/uploads/trace.jpg",
                };
                db.CephAnalyses.Add(analysis);
                db.Settings.AddRange(
                    new Setting { Key = CephAiDraftService.DraftEnabledSettingKey, Value = "true" },
                    new Setting { Key = CephAiDraftService.ProviderSettingKey, Value = "anthropic" },
                    new Setting { Key = CephAiDraftService.ModelSettingKey, Value = "claude-sonnet-5" });
                await db.SaveChangesAsync();

                var handler = new CapturingHandler(AnthropicResponseWithEightPoints());
                var factory = new StubFactory(handler);
                var user = new Mock<ICurrentUserService>();
                user.Setup(x => x.UserId).Returns(Guid.NewGuid());
                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["AiSettings:EncryptionKey"] = "unit-test-ai-encryption-key-that-is-long-enough",
                    })
                    .Build();
                var vault = new AiApiKeyVault(
                    db, configuration, new Mock<ILogger<AiApiKeyVault>>().Object);
                var settingsService = new CephAiDraftService(
                    db,
                    [new AnthropicAiDraftProvider(factory)],
                    user.Object,
                    vault,
                    new Mock<ILogger<CephAiDraftService>>().Object);
                // Both landmark providers are registered — the service must pick
                // the anthropic one because ai.provider = "anthropic".
                var service = new CephAiLandmarkDraftService(
                    db,
                    settingsService,
                    [
                        new GeminiCephLandmarkDraftProvider(factory),
                        new AnthropicCephLandmarkDraftProvider(factory),
                    ],
                    vault,
                    new CephAiModelRegistryService(
                        db,
                        [
                            new GeminiCephLandmarkDraftProvider(factory),
                            new AnthropicCephLandmarkDraftProvider(factory),
                        ],
                        user.Object),
                    user.Object,
                    new Mock<ILogger<CephAiLandmarkDraftService>>().Object);

                var result = await service.GenerateAsync(
                    analysis.Id, 2000, 1000, CephAiPrecision.Draft);

                result.Should().NotBeNull();
                result!.Landmarks.Should().HaveCount(8);
                result.InferenceRunId.Should().NotBeEmpty();
                result.ModelRegistryKey.Should().StartWith("observed:");
                result.Landmarks[0].X.Should().Be(200);
                result.Landmarks.Should().OnlyContain(point => point.IsAiPlaced);
                result.Disclaimer.Should().Contain("مراجعة وتحريك كل نقطة");
                // Confirm the anthropic wire format was actually used.
                handler.Body.Should().Contain("\"type\":\"image\"");
                handler.Request!.Headers.Contains("x-api-key").Should().BeTrue();
                (await db.CephLandmarks.CountAsync()).Should().Be(0,
                    "the AI result must remain an unsaved draft");
                var inference = await db.CephAiInferenceRuns.SingleAsync();
                inference.Status.Should().Be("succeeded");
                inference.OriginalPredictionsJson.Should().Contain("xNormalized");
                (await db.OrthodonticAiLogs.SingleAsync()).ModelId
                    .Should().Contain("anthropic");
            }
            finally
            {
                Environment.SetEnvironmentVariable("UPLOADS_PATH", previousUploadsPath);
                Directory.Delete(tempDirectory, recursive: true);
            }
        });
    }

    [Fact]
    public async Task LandmarkService_ReturnsUnsavedScaledDraft_AndWritesAuditOnly()
    {
        await CephAiDraftTests.WithEnvKeyAsync("GEMINI_API_KEY", TestKey, async () =>
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), $"ceph-ai-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);
            var previousUploadsPath = Environment.GetEnvironmentVariable("UPLOADS_PATH");
            Environment.SetEnvironmentVariable("UPLOADS_PATH", tempDirectory);
            try
            {
                await File.WriteAllBytesAsync(Path.Combine(tempDirectory, "trace.jpg"), [1, 2, 3, 4]);
                await using var db = CreateDb();
                var analysis = new CephAnalysis
                {
                    Id = Guid.NewGuid(),
                    OrthoCaseId = Guid.NewGuid(),
                    AnalysisType = "steiner",
                    XrayFileUrl = "/uploads/trace.jpg",
                };
                db.CephAnalyses.Add(analysis);
                db.Settings.AddRange(
                    new Setting { Key = CephAiDraftService.DraftEnabledSettingKey, Value = "true" },
                    new Setting { Key = CephAiDraftService.ProviderSettingKey, Value = "gemini" },
                    new Setting { Key = CephAiDraftService.ModelSettingKey, Value = "gemini-3.5-flash" });
                await db.SaveChangesAsync();

                var handler = new CapturingHandler(ResponseWithEightPoints());
                var factory = new StubFactory(handler);
                var user = new Mock<ICurrentUserService>();
                user.Setup(x => x.UserId).Returns(Guid.NewGuid());
                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["AiSettings:EncryptionKey"] = "unit-test-ai-encryption-key-that-is-long-enough",
                    })
                    .Build();
                var vault = new AiApiKeyVault(
                    db, configuration, new Mock<ILogger<AiApiKeyVault>>().Object);
                var settingsService = new CephAiDraftService(
                    db,
                    [new GeminiAiDraftProvider(factory)],
                    user.Object,
                    vault,
                    new Mock<ILogger<CephAiDraftService>>().Object);
                var service = new CephAiLandmarkDraftService(
                    db,
                    settingsService,
                    [new GeminiCephLandmarkDraftProvider(factory)],
                    vault,
                    new CephAiModelRegistryService(
                        db, [new GeminiCephLandmarkDraftProvider(factory)], user.Object),
                    user.Object,
                    new Mock<ILogger<CephAiLandmarkDraftService>>().Object);

                var result = await service.GenerateAsync(
                    analysis.Id, 2000, 1000, CephAiPrecision.Draft);

                result.Should().NotBeNull();
                result!.Landmarks.Should().HaveCount(8);
                result.Landmarks[0].X.Should().Be(200);
                result.Landmarks[0].Y.Should().Be(200);
                result.Landmarks.Should().OnlyContain(point => point.IsAiPlaced);
                result.Disclaimer.Should().Contain("مراجعة وتحريك كل نقطة");
                (await db.CephLandmarks.CountAsync()).Should().Be(0,
                    "the AI result must remain an unsaved draft");
                (await db.OrthodonticAiLogs.SingleAsync()).Action
                    .Should().Be(CephAiLandmarkDraftService.Action);
            }
            finally
            {
                Environment.SetEnvironmentVariable("UPLOADS_PATH", previousUploadsPath);
                Directory.Delete(tempDirectory, recursive: true);
            }
        });
    }

    [Fact]
    public async Task LandmarkService_WhenAiDisabled_ThrowsHonestUnavailable_NotGenericError()
    {
        await using var db = CreateDb();
        var analysis = new CephAnalysis
        {
            Id = Guid.NewGuid(),
            OrthoCaseId = Guid.NewGuid(),
            AnalysisType = "steiner",
            XrayFileUrl = "/uploads/trace.jpg",
        };
        db.CephAnalyses.Add(analysis);
        await db.SaveChangesAsync(); // no ai.* settings → AI disabled by default

        var factory = new StubFactory(new CapturingHandler(ResponseWithEightPoints()));
        var user = new Mock<ICurrentUserService>();
        user.Setup(x => x.UserId).Returns(Guid.NewGuid());
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiSettings:EncryptionKey"] = "unit-test-ai-encryption-key-that-is-long-enough",
            }).Build();
        var vault = new AiApiKeyVault(db, configuration, new Mock<ILogger<AiApiKeyVault>>().Object);
        var settingsService = new CephAiDraftService(
            db, [new GeminiAiDraftProvider(factory)], user.Object, vault,
            new Mock<ILogger<CephAiDraftService>>().Object);
        var service = new CephAiLandmarkDraftService(
            db, settingsService, [new GeminiCephLandmarkDraftProvider(factory)], vault,
            new CephAiModelRegistryService(
                db, [new GeminiCephLandmarkDraftProvider(factory)], user.Object), user.Object,
            new Mock<ILogger<CephAiLandmarkDraftService>>().Object);

        // The honest "AI not enabled / no key" message must surface — never the
        // generic 500 that an audit-write failure used to mask it behind.
        var act = async () => await service.GenerateAsync(analysis.Id, 800, 600, CephAiPrecision.Draft);
        (await act.Should().ThrowAsync<AqlanDentalPro.Application.Exceptions.CephAiUnavailableException>())
            .Which.Message.Should().Be(CephAiDraftService.DisabledMessageAr);
    }

    [Fact]
    public async Task LandmarkService_RefineLandmark_ScalesPixelCoords_AndKeepsResultUnsaved()
    {
        await CephAiDraftTests.WithEnvKeyAsync("GEMINI_API_KEY", TestKey, async () =>
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), $"ceph-ai-refine-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);
            var previousUploadsPath = Environment.GetEnvironmentVariable("UPLOADS_PATH");
            Environment.SetEnvironmentVariable("UPLOADS_PATH", tempDirectory);
            try
            {
                await File.WriteAllBytesAsync(Path.Combine(tempDirectory, "trace.jpg"), [1, 2, 3, 4]);
                await using var db = CreateDb();
                var analysis = new CephAnalysis
                {
                    Id = Guid.NewGuid(),
                    OrthoCaseId = Guid.NewGuid(),
                    AnalysisType = "steiner",
                    XrayFileUrl = "/uploads/trace.jpg",
                };
                db.CephAnalyses.Add(analysis);
                db.Settings.AddRange(
                    new Setting { Key = CephAiDraftService.DraftEnabledSettingKey, Value = "true" },
                    new Setting { Key = CephAiDraftService.ProviderSettingKey, Value = "gemini" },
                    new Setting { Key = CephAiDraftService.ModelSettingKey, Value = "gemini-3.5-flash" });
                await db.SaveChangesAsync();

                var generated =
                    """{"landmarks":[{"key":"S","x":120,"y":210,"confidence":0.9,"reasoning":"center of sella outline reconfirmed"}]}""";
                var response = System.Text.Json.JsonSerializer.Serialize(new
                {
                    candidates = new[]
                    {
                        new { content = new { parts = new[] { new { text = generated } } } },
                    },
                });
                var handler = new CapturingHandler(response);
                var factory = new StubFactory(handler);
                var user = new Mock<ICurrentUserService>();
                user.Setup(x => x.UserId).Returns(Guid.NewGuid());
                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["AiSettings:EncryptionKey"] = "unit-test-ai-encryption-key-that-is-long-enough",
                    })
                    .Build();
                var vault = new AiApiKeyVault(
                    db, configuration, new Mock<ILogger<AiApiKeyVault>>().Object);
                var settingsService = new CephAiDraftService(
                    db,
                    [new GeminiAiDraftProvider(factory)],
                    user.Object,
                    vault,
                    new Mock<ILogger<CephAiDraftService>>().Object);
                var service = new CephAiLandmarkDraftService(
                    db,
                    settingsService,
                    [new GeminiCephLandmarkDraftProvider(factory)],
                    vault,
                    new CephAiModelRegistryService(
                        db, [new GeminiCephLandmarkDraftProvider(factory)], user.Object),
                    user.Object,
                    new Mock<ILogger<CephAiLandmarkDraftService>>().Object);

                // Current pixel position (200, 100) on a 2000x1000 image maps to
                // normalized (100, 100) on the 0..1000 grid. The model returns
                // (120, 210) which must scale back to (240, 210) in pixels.
                var result = await service.RefineLandmarkAsync(
                    analysis.Id, "S", 2000, 1000, 200, 100);

                result.Should().NotBeNull();
                result!.Landmark.Should().NotBeNull();
                result.Landmark!.Key.Should().Be("S");
                result.Landmark.X.Should().Be(240);
                result.Landmark.Y.Should().Be(210);
                result.Landmark.IsAiPlaced.Should().BeTrue();
                result.Landmark.Reasoning.Should().Contain("sella");
                result.Disclaimer.Should().Contain("مراجعة وتحريك كل نقطة");
                (await db.CephLandmarks.CountAsync()).Should().Be(0,
                    "the refined point must NOT be auto-saved");
                (await db.OrthodonticAiLogs.SingleAsync()).Action
                    .Should().Be(CephAiLandmarkDraftService.RefineAction);
            }
            finally
            {
                Environment.SetEnvironmentVariable("UPLOADS_PATH", previousUploadsPath);
                Directory.Delete(tempDirectory, recursive: true);
            }
        });
    }

    [Fact]
    public async Task LandmarkService_HighPrecision_KeepsResultUnsaved_AndWritesAudit()
    {
        await CephAiDraftTests.WithEnvKeyAsync("GEMINI_API_KEY", TestKey, async () =>
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), $"ceph-ai-high-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);
            var previousUploadsPath = Environment.GetEnvironmentVariable("UPLOADS_PATH");
            Environment.SetEnvironmentVariable("UPLOADS_PATH", tempDirectory);
            try
            {
                await File.WriteAllBytesAsync(Path.Combine(tempDirectory, "trace.jpg"), [1, 2, 3, 4]);
                await using var db = CreateDb();
                var analysis = new CephAnalysis
                {
                    Id = Guid.NewGuid(),
                    OrthoCaseId = Guid.NewGuid(),
                    AnalysisType = "steiner",
                    XrayFileUrl = "/uploads/trace.jpg",
                };
                db.CephAnalyses.Add(analysis);
                db.Settings.AddRange(
                    new Setting { Key = CephAiDraftService.DraftEnabledSettingKey, Value = "true" },
                    new Setting { Key = CephAiDraftService.ProviderSettingKey, Value = "gemini" },
                    new Setting { Key = CephAiDraftService.ModelSettingKey, Value = "gemini-3.5-flash" });
                await db.SaveChangesAsync();

                var handler = new CapturingHandler(ResponseWithEightPoints());
                var factory = new StubFactory(handler);
                var user = new Mock<ICurrentUserService>();
                user.Setup(x => x.UserId).Returns(Guid.NewGuid());
                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["AiSettings:EncryptionKey"] = "unit-test-ai-encryption-key-that-is-long-enough",
                    })
                    .Build();
                var vault = new AiApiKeyVault(
                    db, configuration, new Mock<ILogger<AiApiKeyVault>>().Object);
                var settingsService = new CephAiDraftService(
                    db,
                    [new GeminiAiDraftProvider(factory)],
                    user.Object,
                    vault,
                    new Mock<ILogger<CephAiDraftService>>().Object);
                var service = new CephAiLandmarkDraftService(
                    db,
                    settingsService,
                    [new GeminiCephLandmarkDraftProvider(factory)],
                    vault,
                    new CephAiModelRegistryService(
                        db, [new GeminiCephLandmarkDraftProvider(factory)], user.Object),
                    user.Object,
                    new Mock<ILogger<CephAiLandmarkDraftService>>().Object);

                var result = await service.GenerateAsync(
                    analysis.Id, 2000, 1000, CephAiPrecision.High);

                result.Should().NotBeNull();
                result!.Landmarks.Should().HaveCount(8);
                result.Disclaimer.Should().Contain("مراجعة وتحريك كل نقطة");
                (await db.CephLandmarks.CountAsync()).Should().Be(0,
                    "the high-precision draft must remain unsaved");
                var log = await db.OrthodonticAiLogs.SingleAsync();
                log.Action.Should().Be(CephAiLandmarkDraftService.Action);
                log.InputSummary.Should().Contain("precision:high");
            }
            finally
            {
                Environment.SetEnvironmentVariable("UPLOADS_PATH", previousUploadsPath);
                Directory.Delete(tempDirectory, recursive: true);
            }
        });
    }

    /// <summary>Wrap raw model JSON in Anthropic's Messages API response envelope.</summary>
    private static string WrapAnthropic(string generatedText) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            content = new[] { new { type = "text", text = generatedText } },
        });

    private static string AnthropicResponseWithEightPoints() =>
        WrapAnthropic(
            """{"landmarks":[{"key":"S","x":100,"y":200,"confidence":0.8},{"key":"N","x":200,"y":200,"confidence":0.8},{"key":"Or","x":300,"y":300,"confidence":0.7},{"key":"Po","x":150,"y":300,"confidence":0.7},{"key":"ANS","x":500,"y":400,"confidence":0.8},{"key":"PNS","x":350,"y":400,"confidence":0.7},{"key":"A","x":520,"y":480,"confidence":0.8},{"key":"B","x":540,"y":600,"confidence":0.8}]}""");

    private static string ResponseWithEightPoints()
    {
        var generated =
            """{"landmarks":[{"key":"S","x":100,"y":200,"confidence":0.8},{"key":"N","x":200,"y":200,"confidence":0.8},{"key":"Or","x":300,"y":300,"confidence":0.7},{"key":"Po","x":150,"y":300,"confidence":0.7},{"key":"ANS","x":500,"y":400,"confidence":0.8},{"key":"PNS","x":350,"y":400,"confidence":0.7},{"key":"A","x":520,"y":480,"confidence":0.8},{"key":"B","x":540,"y":600,"confidence":0.8}]}""";
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[] { new { text = generated } },
                    },
                },
            },
        });
    }
}
