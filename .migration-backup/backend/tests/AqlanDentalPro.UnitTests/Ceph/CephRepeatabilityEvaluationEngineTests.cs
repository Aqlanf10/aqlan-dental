using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Services;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ceph;

public sealed class CephRepeatabilityEvaluationEngineTests
{
    private static readonly DateTimeOffset FixedUtc =
        new(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Evaluate_ReportsThreeRunDisplacementIccAndConfidenceCoverage()
    {
        var request = BuildRequest();
        AddRun(request, "run-001", 0, 0.9);
        AddRun(request, "run-002", 2, 0.9);
        AddRun(request, "run-003", -2, 0.4);

        var result = CephRepeatabilityEvaluationEngine.Evaluate(request);

        result.IsValid.Should().BeTrue();
        result.GeometryVersion.Should().Be(CephLateralGeometryEngine.Version);
        var a = result.Landmarks.Single(item => item.LandmarkKey == "A");
        a.EligibleCaseCount.Should().Be(2);
        a.CompleteThreeRunCaseCount.Should().Be(2);
        a.MissingStatusConsistencyPercent.Should().Be(100);
        a.MeanPairwiseDisplacementMm.Should().BeApproximately(0.2666666667, 1e-9);
        a.MeanWithinCaseRadialSdMm.Should().BeApproximately(0.2, 1e-9);

        var sna = result.Measurements.Single(item => item.MeasurementName == "SNA");
        sna.CompleteThreeRunCaseCount.Should().Be(2);
        sna.MeanWithinCaseStandardDeviation.Should().BeGreaterThan(0);
        sna.AbsoluteAgreementIcc.Should().NotBeNull();
        sna.AbsoluteAgreementIcc.Should().BeLessThanOrEqualTo(1);

        result.ConfidenceCoverage.Should().HaveCount(2);
        var strict = result.ConfidenceCoverage.Single(item => item.ConfidenceThreshold == 0.8);
        strict.CoveragePercent.Should().BeLessThan(100);
        strict.AboveClinicalErrorPointRunCount.Should().BeGreaterThan(0);
        strict.ReferredAboveClinicalErrorPointRunCount.Should().BeGreaterThan(0);
        strict.MissingOrRejectedPointRunCount.Should().Be(0);
        strict.MissingOrRejectedReferralPercent.Should().BeNull();
    }

    [Fact]
    public void Evaluate_ReportsMissingStatusConsistencyAndCompleteTriplets()
    {
        var request = BuildRequest();
        AddRun(request, "run-001", 0, 0.9);
        AddRun(request, "run-002", 0, 0.9);
        AddRun(request, "run-003", 0, 0.9, missingImageIndex: 0, missingKey: "A");

        var result = CephRepeatabilityEvaluationEngine.Evaluate(request);

        result.IsValid.Should().BeTrue();
        var a = result.Landmarks.Single(item => item.LandmarkKey == "A");
        a.CompleteThreeRunCaseCount.Should().Be(1);
        a.MissingStatusConsistentCaseCount.Should().Be(1);
        a.MissingStatusConsistencyPercent.Should().Be(50);
        var coverage = result.ConfidenceCoverage.Single(item => item.ConfidenceThreshold == 0.8);
        coverage.MissingOrRejectedPointRunCount.Should().Be(1);
        coverage.ReferredMissingOrRejectedPointRunCount.Should().Be(1);
        coverage.MissingOrRejectedReferralPercent.Should().Be(100);
    }

    [Fact]
    public void Evaluate_RequiresExactlyThreeUniqueRunsAndValidThresholds()
    {
        var request = BuildRequest();
        AddRun(request, "run-001", 0, 0.9);
        AddRun(request, "run-001", 0, 0.9);
        request.ConfidenceThresholds = [0.5, 0.5];

        var result = CephRepeatabilityEvaluationEngine.Evaluate(request);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(item => item.Code == "runs.exactly-three-required");
        result.Issues.Should().Contain(item => item.Code == "confidenceThresholds.invalid");
    }

    [Fact]
    public void Evaluate_IsDeterministicAndDoesNotReturnPatientLinkage()
    {
        var request = BuildRequest();
        AddRun(request, "run-001", 0, 0.9);
        AddRun(request, "run-002", 1, 0.8);
        AddRun(request, "run-003", -1, 0.7);

        var first = CephRepeatabilityEvaluationEngine.Evaluate(request);
        var second = CephRepeatabilityEvaluationEngine.Evaluate(request);

        first.Should().BeEquivalentTo(second, options => options.WithStrictOrdering());
        System.Text.Json.JsonSerializer.Serialize(first).Should().NotContain("patientGroupId");
        System.Text.Json.JsonSerializer.Serialize(first).Should().NotContain(new string('a', 64));
    }

    private static CephRepeatabilityEvaluationRequestDto BuildRequest() => new()
    {
        ProtocolVersion = CephRepeatabilityEvaluationEngine.ProtocolVersion,
        GeometryVersion = CephLateralGeometryEngine.Version,
        ModelVersion = "candidate-001",
        PreprocessingVersion = "preprocess-001",
        EvaluationSplit = CephBenchmarkSplit.InternalTest,
        ClinicalErrorThresholdMm = 0.1,
        ConfidenceThresholds = [0.5, 0.8],
        Benchmark = new CephBenchmarkManifestDto
        {
            SchemaVersion = CephBenchmarkManifestValidator.SchemaVersion,
            DatasetVersion = "pilot-001",
            LandmarkDefinitionVersion = CephBenchmarkManifestValidator.LandmarkDefinitionVersion,
            CreatedAt = FixedUtc,
            Cases =
            [
                BuildCase('a', 'a', Guid.Parse("11111111-1111-1111-1111-111111111111"), 0),
                BuildCase('b', 'b', Guid.Parse("22222222-2222-2222-2222-222222222222"), 40),
            ],
        },
    };

    private static CephBenchmarkCaseDto BuildCase(
        char patientHashCharacter,
        char imageHashCharacter,
        Guid imageId,
        double aOffset)
    {
        var benchmarkCase = new CephBenchmarkCaseDto
        {
            ImageId = imageId,
            PatientGroupId = new string(patientHashCharacter, 64),
            ImageSha256 = new string(imageHashCharacter, 64),
            ImageWidth = 2000,
            ImageHeight = 2000,
            MillimetresPerPixel = 0.1,
            SourceSiteCode = "SITE-001",
            DeviceCode = "DEV-001",
            Split = CephBenchmarkSplit.InternalTest,
            Orientation = CephImageOrientation.RightFacing,
            AgeBand = CephAgeBand.Adult,
            SkeletalClass = CephSkeletalClass.ClassI,
            AnglePattern = CephAnglePattern.Average,
            QualityFlags = [CephImageQualityFlag.Normal],
            Deidentification = new()
            {
                MetadataProfileVersion = "DICOM-PS3.15-v1",
                SanitizedAt = FixedUtc,
                StewardAlias = "STW-0001",
                DataUseApprovalId = "APP-CEPH-0001",
                MetadataSanitized = true,
                PrivateTagsRemoved = true,
                PixelInspectionStatus = CephPixelInspectionStatus.Passed,
                BurnedInIdentifierDetected = false,
            },
        };

        var index = 0;
        foreach (var key in CephBenchmarkManifestValidator.CoreLandmarkKeys.OrderBy(item => item))
        {
            var x = 100d + index * 13;
            var y = 150d + index * index * 2 + (key == "A" ? aOffset : 0);
            benchmarkCase.Annotations.Add(Annotation(key, "REV-0001", x, y));
            benchmarkCase.Annotations.Add(Annotation(key, "REV-0002", x + 1, y + 1));
            benchmarkCase.GoldStandard.Add(new()
            {
                LandmarkKey = key,
                Visibility = CephLandmarkVisibility.Visible,
                X = x + 0.5,
                Y = y + 0.5,
                Method = CephGoldStandardMethod.ConsensusWithinThreshold,
                DecisionCodes = [CephGoldStandardDecisionCode.CoordinateAgreement],
                ApprovedByAlias = "REV-0001",
                ApprovedAt = FixedUtc,
            });
            index++;
        }
        return benchmarkCase;
    }

    private static CephReviewerAnnotationDto Annotation(string key, string reviewer, double x, double y) => new()
    {
        LandmarkKey = key,
        ReviewerAlias = reviewer,
        Visibility = CephLandmarkVisibility.Visible,
        X = x,
        Y = y,
        DoubleContourDecision = CephDoubleContourDecision.NotApplicable,
        AnnotatedAt = FixedUtc,
    };

    private static void AddRun(
        CephRepeatabilityEvaluationRequestDto request,
        string runId,
        double aVerticalOffsetPixels,
        double aConfidence,
        int? missingImageIndex = null,
        string? missingKey = null)
    {
        var run = new CephRepeatedPredictionRunDto { RunId = runId };
        for (var imageIndex = 0; imageIndex < request.Benchmark!.Cases.Count; imageIndex++)
        {
            var benchmarkCase = request.Benchmark.Cases[imageIndex];
            run.Predictions.Add(new CephPredictionCaseDto
            {
                ImageId = benchmarkCase.ImageId,
                Points = benchmarkCase.GoldStandard.Select(gold =>
                {
                    var missing = imageIndex == missingImageIndex && gold.LandmarkKey == missingKey;
                    return new CephPredictionPointDto
                    {
                        LandmarkKey = gold.LandmarkKey,
                        Status = missing ? CephPredictionStatus.NotFound : CephPredictionStatus.Predicted,
                        X = missing ? null : gold.X,
                        Y = missing ? null : gold.Y + (gold.LandmarkKey == "A" ? aVerticalOffsetPixels : 0),
                        Confidence = gold.LandmarkKey == "A" ? aConfidence : 0.95,
                    };
                }).ToList(),
            });
        }
        request.Runs.Add(run);
    }
}
