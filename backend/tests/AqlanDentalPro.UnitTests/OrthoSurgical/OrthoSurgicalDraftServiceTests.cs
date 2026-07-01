using AqlanDentalPro.Application.Exceptions;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using AqlanDentalPro.Infrastructure.Services.Ai;
using AqlanDentalPro.UnitTests.Ceph;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using System.Text;
using Xunit;

namespace AqlanDentalPro.UnitTests.OrthoSurgical;

/// <summary>
/// Sprint A8 — tests for <see cref="OrthoSurgicalDraftService"/>. Mirrors
/// OrthoCaseDraftTests: real evidence in the prompt (not just counts), no patient
/// identifiers ever sent, honest disabled/limit/missing-key errors, and every
/// attempt logged to OrthodonticAiLogs.
/// </summary>
[Collection("ai-env")]
public class OrthoSurgicalDraftServiceTests
{
    private const string PatientFirstName = "مريضحساسجراحي";
    private const string TestApiKey = "test-key-secret-abcd1234";

    private const string GeminiSuccessJson =
        """
        { "candidates": [ { "content": { "role": "model", "parts": [ { "text": "مسودة — تتطلب مراجعة الأخصائيين." } ] }, "finishReason": "STOP" } ] }
        """;

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private sealed class StubHandler(HttpStatusCode status, string responseBody) : HttpMessageHandler
    {
        public string? LastRequestBody { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent(responseBody, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static OrthoSurgicalDraftService CreateService(AppDbContext db, HttpMessageHandler handler, Guid? userId = null)
    {
        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.UserId).Returns(userId ?? Guid.NewGuid());

        var factory = new StubHttpClientFactory(handler);
        var providers = new IAiDraftProvider[]
        {
            new GeminiAiDraftProvider(factory),
            new AnthropicAiDraftProvider(factory),
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiSettings:EncryptionKey"] = "unit-test-ai-encryption-key-that-is-long-enough",
            })
            .Build();
        var keyVault = new AiApiKeyVault(db, configuration, new Mock<ILogger<AiApiKeyVault>>().Object);
        var cephSettings = new CephAiDraftService(db, providers, user.Object, keyVault, new Mock<ILogger<CephAiDraftService>>().Object);

        return new OrthoSurgicalDraftService(
            db, cephSettings, providers, user.Object, keyVault,
            new Mock<ILogger<OrthoSurgicalDraftService>>().Object);
    }

    private static async Task<Guid> SeedFullCaseAsync(AppDbContext db)
    {
        var patient = new Patient { PatientNumber = "P-AI-1", FirstName = PatientFirstName, LastName = "تجريبي", IsActive = true };
        db.Patients.Add(patient);
        var orthoCase = new OrthoCase { PatientId = patient.Id, CaseNumber = "OC-AI-1", IsActive = true };
        db.OrthoCases.Add(orthoCase);
        await db.SaveChangesAsync();

        db.OrthoDiagnoses.Add(new OrthoDiagnosis
        {
            OrthoCaseId = orthoCase.Id, SkeletalClassification = "Class III",
            Summary = "تراجع الفك العلوي", IsActive = true
        });

        var ceph = new CephAnalysis { OrthoCaseId = orthoCase.Id, AnalysisType = "steiner", IsApproved = true, IsActive = true };
        db.CephAnalyses.Add(ceph);
        await db.SaveChangesAsync();
        db.CephMeasurements.Add(new CephMeasurement
        {
            AnalysisId = ceph.Id, MeasurementName = "ANB", MeasurementValue = -3, Unit = "deg", IsActive = true
        });

        var c = new OrthoSurgicalCase
        {
            CaseNumber = "OS-AI-001", PatientId = patient.Id, OrthoCaseId = orthoCase.Id,
            CephAnalysisId = ceph.Id, Status = OrthoSurgicalStatus.SurgeonReviewPending, IsActive = true
        };
        db.OrthoSurgicalCases.Add(c);
        await db.SaveChangesAsync();

        db.SurgeonReviews.Add(new SurgeonReview
        {
            OrthoSurgicalCaseId = c.Id, Decision = "Approved", ProposedProcedure = "Le Fort I advancement", IsActive = true
        });
        await db.SaveChangesAsync();

        return c.Id;
    }

    [Fact]
    public async Task Draft_CaseSummary_GroundsPromptInRealEvidence_NoPatientIdentifiers()
    {
        await CephAiDraftTests.WithEnvKeyAsync("GEMINI_API_KEY", TestApiKey, async () =>
        {
            await using var db = CreateDb();
            var caseId = await SeedFullCaseAsync(db);
            db.Settings.Add(new Setting { Key = CephAiDraftService.DraftEnabledSettingKey, Value = "true" });
            await db.SaveChangesAsync();

            var handler = new StubHandler(HttpStatusCode.OK, GeminiSuccessJson);
            var service = CreateService(db, handler);

            var result = await service.GenerateAsync(caseId, "case_summary");

            result.Should().NotBeNull();
            result!.EvidenceUsed.Should().Contain("diagnosis").And.Contain("ceph").And.Contain("surgeonReview");
            result.Disclaimer.Should().Contain("لا تُعد قرارًا جراحيًا نهائيًا");

            var body = handler.LastRequestBody;
            body.Should().NotBeNull();
            body!.Should().Contain("Class III").And.Contain("ANB").And.Contain("-3");
            body.Should().Contain("Le Fort I advancement");
            body.Should().NotContain(PatientFirstName, "patient identifiers must never be sent to the AI API");
        });
    }

    [Theory]
    [InlineData("referral_letter")]
    [InlineData("patient_explanation")]
    [InlineData("joint_plan_draft")]
    public async Task Draft_AllFourSections_Succeed(string section)
    {
        await CephAiDraftTests.WithEnvKeyAsync("GEMINI_API_KEY", TestApiKey, async () =>
        {
            await using var db = CreateDb();
            var caseId = await SeedFullCaseAsync(db);
            db.Settings.Add(new Setting { Key = CephAiDraftService.DraftEnabledSettingKey, Value = "true" });
            await db.SaveChangesAsync();

            var handler = new StubHandler(HttpStatusCode.OK, GeminiSuccessJson);
            var service = CreateService(db, handler);

            var result = await service.GenerateAsync(caseId, section);

            result.Should().NotBeNull();
            result!.Section.Should().Be(section);
        });
    }

    [Fact]
    public async Task Draft_InvalidSection_ThrowsArgumentException()
    {
        await using var db = CreateDb();
        var caseId = await SeedFullCaseAsync(db);
        var service = CreateService(db, new StubHandler(HttpStatusCode.OK, GeminiSuccessJson));

        var act = () => service.GenerateAsync(caseId, "not_a_real_section");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Draft_MissingCase_ReturnsNull()
    {
        await using var db = CreateDb();
        var service = CreateService(db, new StubHandler(HttpStatusCode.OK, GeminiSuccessJson));

        var result = await service.GenerateAsync(Guid.NewGuid(), "case_summary");

        result.Should().BeNull();
    }

    [Fact]
    public async Task Draft_FeatureDisabled_ThrowsUnavailable_AndAuditsFailure()
    {
        await using var db = CreateDb();
        var caseId = await SeedFullCaseAsync(db);
        // No Settings row → DraftEnabledSettingKey defaults to disabled.
        var service = CreateService(db, new StubHandler(HttpStatusCode.OK, GeminiSuccessJson));

        var act = () => service.GenerateAsync(caseId, "case_summary");

        await act.Should().ThrowAsync<CephAiUnavailableException>();

        var logged = await db.OrthodonticAiLogs.SingleAsync(l => l.AnalysisId == caseId);
        logged.Action.Should().Be("ortho_surgical_draft:case_summary");
        logged.Succeeded.Should().BeFalse();
        logged.ErrorSummary.Should().Be("feature_disabled");
    }

    [Fact]
    public async Task Draft_Success_AuditsWithModelIdAndOutputLength()
    {
        await CephAiDraftTests.WithEnvKeyAsync("GEMINI_API_KEY", TestApiKey, async () =>
        {
            await using var db = CreateDb();
            var caseId = await SeedFullCaseAsync(db);
            db.Settings.Add(new Setting { Key = CephAiDraftService.DraftEnabledSettingKey, Value = "true" });
            await db.SaveChangesAsync();
            var userId = Guid.NewGuid();

            var handler = new StubHandler(HttpStatusCode.OK, GeminiSuccessJson);
            var service = CreateService(db, handler, userId);

            var result = await service.GenerateAsync(caseId, "case_summary");

            var logged = await db.OrthodonticAiLogs.SingleAsync(l => l.AnalysisId == caseId);
            logged.Succeeded.Should().BeTrue();
            logged.UserId.Should().Be(userId);
            logged.ModelId.Should().NotBeNullOrEmpty();
            logged.OutputLength.Should().Be(result!.Draft.Length);
        });
    }
}
