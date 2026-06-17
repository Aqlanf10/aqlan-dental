using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
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

namespace AqlanDentalPro.UnitTests.Ortho;

/// <summary>
/// The ortho case clinical-draft assistant must reason over REAL cephalometric
/// evidence — the actual measurement values and the ceph diagnosis — not just a
/// measurement count, so a diagnosis/strategy draft is genuinely grounded in the
/// ceph findings. Patient identifiers must never reach the AI API.
/// </summary>
[Collection("ai-env")]
public class OrthoCaseDraftTests
{
    private const string PatientFirstName = "مريضحساس";
    private const string TestApiKey = "test-key-secret-abcd1234";

    private const string GeminiSuccessJson =
        """
        { "candidates": [ { "content": { "role": "model", "parts": [ { "text": "مسودة تشخيص — تتطلب مراجعة الأخصائي." } ] }, "finishReason": "STOP" } ] }
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

    private static OrthoCaseDraftService CreateService(AppDbContext db, HttpMessageHandler handler)
    {
        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.UserId).Returns(Guid.NewGuid());

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

        return new OrthoCaseDraftService(
            db, cephSettings, providers, user.Object, keyVault,
            new Mock<ILogger<OrthoCaseDraftService>>().Object);
    }

    private static async Task<Guid> SeedCaseWithCephAsync(AppDbContext db)
    {
        var patient = new Patient { Id = Guid.NewGuid(), FirstName = PatientFirstName, LastName = "تجريبي", PatientNumber = "P-991", IsActive = true };
        var orthoCase = new OrthoCase { Id = Guid.NewGuid(), PatientId = patient.Id, CaseNumber = "OC-DRAFT-1", IsActive = true };
        var ceph = new CephAnalysis { Id = Guid.NewGuid(), OrthoCaseId = orthoCase.Id, AnalysisDate = new DateOnly(2026, 6, 10), AnalysisType = "steiner", IsActive = true };
        db.AddRange(patient, orthoCase, ceph);
        db.CephMeasurements.AddRange(
            new CephMeasurement { AnalysisId = ceph.Id, MeasurementName = "SNA", MeasurementValue = 86, NormalValue = 82, Unit = "deg", Classification = "mild", IsActive = true },
            new CephMeasurement { AnalysisId = ceph.Id, MeasurementName = "ANB", MeasurementValue = 6, NormalValue = 2, Unit = "deg", Classification = "severe", IsActive = true });
        db.CephDiagnoses.Add(new CephDiagnosis
        {
            AnalysisId = ceph.Id,
            SkeletalClass = "Class II",
            VerticalPattern = "Normodivergent",
            DoctorApproved = false,
            IsActive = true,
        });
        await db.SaveChangesAsync();
        return orthoCase.Id;
    }

    [Fact]
    public async Task Draft_GroundsPromptInRealCephMeasurementsAndDiagnosis_NoPatientIdentifiers()
    {
        await CephAiDraftTests.WithEnvKeyAsync("GEMINI_API_KEY", TestApiKey, async () =>
        {
            await using var db = CreateDb();
            var caseId = await SeedCaseWithCephAsync(db);
            db.Settings.Add(new Setting { Key = CephAiDraftService.DraftEnabledSettingKey, Value = "true" });
            await db.SaveChangesAsync();

            var handler = new StubHandler(HttpStatusCode.OK, GeminiSuccessJson);
            var service = CreateService(db, handler);

            var result = await service.GenerateAsync(caseId, "diagnosis");

            result.Should().NotBeNull();
            result!.EvidenceUsed.Should().Contain("ceph");

            // The prompt sent to the provider carries the ACTUAL measurement
            // values and the ceph diagnosis — not just a count.
            var body = handler.LastRequestBody;
            body.Should().NotBeNull();
            body!.Should().Contain("SNA").And.Contain("86");
            body.Should().Contain("ANB").And.Contain("severe");
            body.Should().Contain("Class II").And.Contain("Normodivergent");
            body.Should().NotContain("measurements:2", "the count-only summary must be replaced by real values");
            body.Should().NotContain(PatientFirstName, "patient identifiers must never be sent to the AI API");

            // An unapproved ceph diagnosis surfaces as a warning for the doctor.
            result.Warnings.Should().Contain("ceph diagnosis is not approved yet");
        });
    }
}
