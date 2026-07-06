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
            [1, 2, 3, 4], "image/jpeg", "gemini-3.5-flash", TestKey, CancellationToken.None);

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
    public async Task AnthropicProvider_SendsImageInBody_AndParsesNormalizedPoints()
    {
        var handler = new CapturingHandler(ResponseFromAnthropic());
        var provider = new AnthropicCephLandmarkDraftProvider(new StubFactory(handler));

        var result = await provider.GenerateAsync(
            [1, 2, 3, 4], "image/jpeg", "claude-sonnet-5", TestKey, CancellationToken.None);

        result.Should().HaveCount(8);
        result[0].Key.Should().Be("S");
        result[0].XNormalized.Should().Be(100);
        handler.Request!.Headers.GetValues("x-api-key").Single().Should().Be(TestKey);
        handler.Request.Headers.GetValues("anthropic-version").Single().Should().Be(AnthropicAiDraftProvider.AnthropicVersion);
        handler.Request.RequestUri!.ToString().Should().NotContain(TestKey);
        handler.Body.Should().Contain("\"type\":\"image\"");
        handler.Body.Should().Contain("\"type\":\"base64\"");
        handler.Body.Should().Contain(Convert.ToBase64String([1, 2, 3, 4]));
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
                    user.Object,
                    new Mock<ILogger<CephAiLandmarkDraftService>>().Object);

                var result = await service.GenerateAsync(analysis.Id, 2000, 1000);

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
    public async Task LandmarkService_WithAnthropicProviderConfigured_ReturnsUnsavedScaledDraft()
    {
        await CephAiDraftTests.WithEnvKeyAsync("ANTHROPIC_API_KEY", TestKey, async () =>
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
                    new Setting { Key = CephAiDraftService.ProviderSettingKey, Value = "anthropic" },
                    new Setting { Key = CephAiDraftService.ModelSettingKey, Value = "claude-sonnet-5" });
                await db.SaveChangesAsync();

                var handler = new CapturingHandler(ResponseFromAnthropic());
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
                var service = new CephAiLandmarkDraftService(
                    db,
                    settingsService,
                    [new AnthropicCephLandmarkDraftProvider(factory)],
                    vault,
                    user.Object,
                    new Mock<ILogger<CephAiLandmarkDraftService>>().Object);

                var result = await service.GenerateAsync(analysis.Id, 2000, 1000);

                result.Should().NotBeNull();
                result!.Landmarks.Should().HaveCount(8);
                result.Landmarks[0].X.Should().Be(200);
                result.Landmarks[0].Y.Should().Be(200);
                result.Landmarks.Should().OnlyContain(point => point.IsAiPlaced);
                result.Disclaimer.Should().Contain("مراجعة وتحريك كل نقطة");
                (await db.CephLandmarks.CountAsync()).Should().Be(0,
                    "the AI result must remain an unsaved draft");
                (await db.OrthodonticAiLogs.SingleAsync()).ModelId
                    .Should().Be("anthropic/claude-sonnet-5");
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
            Id = Guid.NewGuid(), OrthoCaseId = Guid.NewGuid(),
            AnalysisType = "steiner", XrayFileUrl = "/uploads/trace.jpg",
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
            user.Object, new Mock<ILogger<CephAiLandmarkDraftService>>().Object);

        // The honest "AI not enabled / no key" message must surface — never the
        // generic 500 that an audit-write failure used to mask it behind.
        var act = async () => await service.GenerateAsync(analysis.Id, 800, 600);
        (await act.Should().ThrowAsync<AqlanDentalPro.Application.Exceptions.CephAiUnavailableException>())
            .Which.Message.Should().Be(CephAiDraftService.DisabledMessageAr);
    }

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

    private static string ResponseFromAnthropic()
    {
        var generated =
            """{"landmarks":[{"key":"S","x":100,"y":200,"confidence":0.8},{"key":"N","x":200,"y":200,"confidence":0.8},{"key":"Or","x":300,"y":300,"confidence":0.7},{"key":"Po","x":150,"y":300,"confidence":0.7},{"key":"ANS","x":500,"y":400,"confidence":0.8},{"key":"PNS","x":350,"y":400,"confidence":0.7},{"key":"A","x":520,"y":480,"confidence":0.8},{"key":"B","x":540,"y":600,"confidence":0.8}]}""";
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            content = new[] { new { type = "text", text = generated } },
        });
    }
}
