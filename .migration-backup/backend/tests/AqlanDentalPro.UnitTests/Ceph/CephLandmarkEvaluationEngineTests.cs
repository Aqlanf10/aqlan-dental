using System.Text.Json;
using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.API.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ceph;

public sealed class CephLandmarkEvaluationEngineTests
{
    private static readonly DateTimeOffset FixedUtc =
        new(2026, 7, 14, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Evaluate_ComputesObservedAndFailurePenalizedMetrics()
    {
        var request = BuildRequest();
        AddPredictions(request, request.Benchmark!.Cases[0], 0);
        AddPredictions(request, request.Benchmark.Cases[1], 2, missingKey: "S");

        var result = CephLandmarkEvaluationEngine.Evaluate(request);

        result.IsValid.Should().BeTrue();
        result.Overall.Should().NotBeNull();
        var overall = result.Overall!;
        overall.EligiblePointCount.Should().Be(48);
        overall.ObservedPointCount.Should().Be(47);
        overall.FailurePointCount.Should().Be(1);
        overall.FailureRatePercent.Should().BeApproximately(100d / 48, 1e-10);
        overall.MeanRadialErrorMm.Should().BeApproximately(46d / 47, 1e-10);
        var expectedSampleSd = Math.Sqrt(
            (24 * Math.Pow(46d / 47, 2) + 23 * Math.Pow(2 - 46d / 47, 2)) / 46);
        overall.StandardDeviationMm.Should().BeApproximately(expectedSampleSd, 1e-10);
        overall.MedianRadialErrorMm.Should().Be(0);
        overall.P90RadialErrorMm.Should().Be(2);
        overall.P95RadialErrorMm.Should().Be(2);
        overall.MaximumRadialErrorMm.Should().Be(2);
        overall.CompleteCaseCount.Should().Be(1);
        overall.AllLandmarksCompleteRatePercent.Should().Be(50);

        Metric(overall, 1).ObservedOnlyPercent.Should().BeApproximately(100d * 24 / 47, 1e-10);
        Metric(overall, 1).FailurePenalizedPercent.Should().Be(50);
        Metric(overall, 2).ObservedOnlyPercent.Should().Be(100);
        Metric(overall, 2).FailurePenalizedPercent.Should().BeApproximately(100d * 47 / 48, 1e-10);
    }

    [Fact]
    public void Evaluate_TreatsOmittedCasesAndPointsAsFailures()
    {
        var request = BuildRequest();
        AddPredictions(request, request.Benchmark!.Cases[0], 0);

        var result = CephLandmarkEvaluationEngine.Evaluate(request);

        result.IsValid.Should().BeTrue();
        result.Overall!.EligiblePointCount.Should().Be(48);
        result.Overall.ObservedPointCount.Should().Be(24);
        result.Overall.FailurePointCount.Should().Be(24);
        result.Overall.FailureRatePercent.Should().Be(50);
        result.Overall.AllLandmarksCompleteRatePercent.Should().Be(50);
        Metric(result.Overall, 4).FailurePenalizedPercent.Should().Be(50);
    }

    [Fact]
    public void Evaluate_UsesInclusiveSdrThresholds()
    {
        var request = BuildRequest();
        AddPredictions(request, request.Benchmark!.Cases[0], 1);
        AddPredictions(request, request.Benchmark.Cases[1], 1.0001);

        var result = CephLandmarkEvaluationEngine.Evaluate(request);

        Metric(result.Overall!, 1).FailurePenalizedPercent.Should().Be(50);
        Metric(result.Overall!, 1.5).FailurePenalizedPercent.Should().Be(100);
    }

    [Fact]
    public void Evaluate_ReportsPredictionsOnNotVisibleGoldSeparately()
    {
        var request = BuildRequest();
        foreach (var benchmarkCase in request.Benchmark!.Cases)
        {
            var invisible = benchmarkCase.GoldStandard.Single(item => item.LandmarkKey == "S");
            invisible.Visibility = CephLandmarkVisibility.NotVisible;
            invisible.X = null;
            invisible.Y = null;
            invisible.DecisionCodes = [CephGoldStandardDecisionCode.AnatomyNotVisible];
            foreach (var annotation in benchmarkCase.Annotations.Where(item => item.LandmarkKey == "S"))
            {
                annotation.Visibility = CephLandmarkVisibility.NotVisible;
                annotation.X = null;
                annotation.Y = null;
            }
        }
        AddPredictions(request, request.Benchmark.Cases[0], 0);
        AddPredictions(request, request.Benchmark.Cases[1], 0);

        var result = CephLandmarkEvaluationEngine.Evaluate(request);

        result.IsValid.Should().BeTrue();
        result.Overall!.EligiblePointCount.Should().Be(46);
        result.Overall.GoldNotVisiblePointCount.Should().Be(2);
        result.Overall.PredictedOnNotVisiblePointCount.Should().Be(2);
        result.Overall.PredictedOnNotVisibleRatePercent.Should().Be(100);
        var invisibleLandmark = result.ByLandmark.Single(item => item.LandmarkKey == "S").Metrics;
        invisibleLandmark.EligiblePointCount.Should().Be(0);
        invisibleLandmark.FailureRatePercent.Should().BeNull();
        invisibleLandmark.AllLandmarksCompleteRatePercent.Should().BeNull();
        invisibleLandmark.Sdr.Should().OnlyContain(item => item.FailurePenalizedPercent == null);
    }

    [Fact]
    public void Evaluate_RejectsInvalidPredictionContracts()
    {
        var request = BuildRequest();
        var benchmarkCase = request.Benchmark!.Cases[0];
        request.Predictions =
        [
            new CephPredictionCaseDto
            {
                ImageId = benchmarkCase.ImageId,
                Points =
                [
                    new CephPredictionPointDto
                    {
                        LandmarkKey = "S",
                        Status = CephPredictionStatus.Predicted,
                        X = -1,
                        Y = 10,
                    },
                    new CephPredictionPointDto
                    {
                        LandmarkKey = "S",
                        Status = CephPredictionStatus.NotFound,
                        X = 10,
                    },
                    new CephPredictionPointDto { LandmarkKey = "unsupported" },
                ],
            },
            new CephPredictionCaseDto { ImageId = benchmarkCase.ImageId },
            new CephPredictionCaseDto { ImageId = Guid.Parse("99999999-9999-9999-9999-999999999999") },
        ];

        var result = CephLandmarkEvaluationEngine.Evaluate(request);

        result.IsValid.Should().BeFalse();
        result.Issues.Select(item => item.Code).Should().Contain([
            "prediction-point.coordinates.invalid",
            "prediction-point.landmarkKey.duplicate",
            "prediction-point.coordinates.must-be-null",
            "prediction-point.landmarkKey.unsupported",
            "prediction-case.imageId.invalid-or-duplicate",
            "prediction-case.imageId.unknown",
        ]);
        result.Overall.Should().BeNull();
    }

    [Fact]
    public void Evaluate_BootstrapIsDeterministicAndClusteredByPatient()
    {
        var request = BuildRequest(samePatient: true);
        AddPredictions(request, request.Benchmark!.Cases[0], 0);
        AddPredictions(request, request.Benchmark.Cases[1], 2);

        var oneCluster = CephLandmarkEvaluationEngine.Evaluate(request);
        var repeated = CephLandmarkEvaluationEngine.Evaluate(request);

        oneCluster.Overall!.PatientClusterCount.Should().Be(1);
        oneCluster.Overall.MeanRadialError95Ci.Should().BeNull();
        Metric(oneCluster.Overall, 2).FailurePenalized95Ci.Should().BeNull();
        JsonSerializer.Serialize(oneCluster).Should().Be(JsonSerializer.Serialize(repeated));

        request.Benchmark.Cases[1].PatientGroupId = new string('b', 64);
        var twoClusters = CephLandmarkEvaluationEngine.Evaluate(request);
        twoClusters.Overall!.PatientClusterCount.Should().Be(2);
        twoClusters.Overall.MeanRadialError95Ci.Should().NotBeNull();
        twoClusters.Overall.MeanRadialError95Ci!.Lower.Should().Be(0);
        twoClusters.Overall.MeanRadialError95Ci.Upper.Should().Be(2);
    }

    [Fact]
    public void Evaluate_ProducesCaseLandmarkAndSubgroupReportsWithoutPatientHashes()
    {
        var request = BuildRequest();
        AddPredictions(request, request.Benchmark!.Cases[0], 0);
        AddPredictions(request, request.Benchmark.Cases[1], 2);

        var result = CephLandmarkEvaluationEngine.Evaluate(request);
        var json = JsonSerializer.Serialize(result);

        result.ByCase.Should().HaveCount(2);
        result.ByCase.Should().OnlyContain(item => item.Metrics.Sdr.Count == 6);
        result.ByCase.Should().OnlyContain(item => item.Metrics.CaseCount == 1);
        result.ByLandmark.Should().HaveCount(24);
        result.BySubgroup.Should().Contain(item => item.Dimension == "split" && item.Value == "InternalTest");
        result.BySubgroup.Should().Contain(item => item.Dimension == "site" && item.Value == "SITE-001");
        result.BySubgroup.Should().OnlyContain(item => item.WideConfidenceIntervalWarning);
        json.Should().NotContain(new string('a', 64));
        json.Should().NotContain(new string('b', 64));
        json.ToLowerInvariant().Should().NotContain("patientgroupid");
    }

    [Fact]
    public void Evaluate_ExcludesTrainingCasesFromEveryReport()
    {
        var request = BuildRequest();
        request.Benchmark!.Cases[0].Split = CephBenchmarkSplit.Training;
        AddPredictions(request, request.Benchmark.Cases[0], 0);
        AddPredictions(request, request.Benchmark.Cases[1], 2);

        var result = CephLandmarkEvaluationEngine.Evaluate(request);

        result.IsValid.Should().BeTrue();
        result.Overall!.CaseCount.Should().Be(1);
        result.ByCase.Should().ContainSingle().Which.ImageId.Should().Be(request.Benchmark.Cases[1].ImageId);
        result.BySubgroup.Should().NotContain(item => item.Dimension == "split" && item.Value == "Training");
    }

    [Fact]
    public void Evaluate_DoesNotPoolValidationWithLockedTestSplits()
    {
        var request = BuildRequest();
        request.Benchmark!.Cases[0].Split = CephBenchmarkSplit.Validation;
        AddPredictions(request, request.Benchmark.Cases[0], 0);
        AddPredictions(request, request.Benchmark.Cases[1], 2);

        var internalTest = CephLandmarkEvaluationEngine.Evaluate(request);

        internalTest.IsValid.Should().BeTrue();
        internalTest.EvaluationSplit.Should().Be(CephBenchmarkSplit.InternalTest);
        internalTest.Overall!.CaseCount.Should().Be(1);
        internalTest.Overall.MeanRadialErrorMm.Should().BeApproximately(2, 1e-10);

        request.EvaluationSplit = CephBenchmarkSplit.Validation;
        var validation = CephLandmarkEvaluationEngine.Evaluate(request);
        validation.IsValid.Should().BeTrue();
        validation.Overall!.CaseCount.Should().Be(1);
        validation.Overall.MeanRadialErrorMm.Should().BeApproximately(0, 1e-10);

        request.EvaluationSplit = CephBenchmarkSplit.Training;
        var training = CephLandmarkEvaluationEngine.Evaluate(request);
        training.IsValid.Should().BeFalse();
        training.Issues.Should().Contain(item => item.Code == "evaluationSplit.test-or-validation-required");
    }

    [Fact]
    public void Controller_EvaluatesStatelesslyAndRejectsLooseJsonContracts()
    {
        var request = BuildRequest();
        AddPredictions(request, request.Benchmark!.Cases[0], 0);
        AddPredictions(request, request.Benchmark.Cases[1], 0);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.Serialize(request, options);
        var numericEnum = json.Replace("\"status\":\"Predicted\"", "\"status\":0", StringComparison.Ordinal);
        var compositeEnum = json.Replace(
            "\"status\":\"Predicted\"",
            "\"status\":\"Predicted, NotFound\"",
            StringComparison.Ordinal);
        var undeclared = json.Replace(
            "\"protocolVersion\":",
            "\"patientName\":\"synthetic-only\",\"protocolVersion\":",
            StringComparison.Ordinal);
        var controller = new CephBenchmarkController();

        var ok = controller.EvaluateLandmarks(ParseJsonElement(json))
            .Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<CephLandmarkEvaluationResultDto>()
            .Which.IsValid.Should().BeTrue();
        controller.EvaluateLandmarks(ParseJsonElement(numericEnum))
            .Should().BeOfType<BadRequestObjectResult>();
        controller.EvaluateLandmarks(ParseJsonElement(compositeEnum))
            .Should().BeOfType<BadRequestObjectResult>();
        controller.EvaluateLandmarks(ParseJsonElement(undeclared))
            .Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void Evaluate_EnforcesPredictionCollectionLimits()
    {
        var excessiveCases = BuildRequest();
        excessiveCases.Predictions = Enumerable.Range(0, 10_001)
            .Select(_ => new CephPredictionCaseDto())
            .ToList();

        var casesResult = CephLandmarkEvaluationEngine.Evaluate(excessiveCases);
        casesResult.IsValid.Should().BeFalse();
        casesResult.Issues.Should().Contain(item => item.Code == "predictions.limit");

        var excessivePoints = BuildRequest();
        excessivePoints.Predictions =
        [
            new CephPredictionCaseDto
            {
                ImageId = excessivePoints.Benchmark!.Cases[0].ImageId,
                Points = Enumerable.Range(0, 28)
                    .Select(_ => new CephPredictionPointDto())
                    .ToList(),
            },
        ];

        var pointsResult = CephLandmarkEvaluationEngine.Evaluate(excessivePoints);
        pointsResult.IsValid.Should().BeFalse();
        pointsResult.Issues.Should().Contain(item => item.Code == "prediction-points.limit");
    }

    private static CephSdrMetricDto Metric(CephEvaluationMetricSummaryDto summary, double threshold) =>
        summary.Sdr.Single(item => item.ThresholdMm == threshold);

    private static JsonElement ParseJsonElement(string json) =>
        JsonSerializer.Deserialize<JsonElement>(json);

    private static CephLandmarkEvaluationRequestDto BuildRequest(bool samePatient = false) => new()
    {
        ProtocolVersion = CephLandmarkEvaluationEngine.ProtocolVersion,
        ModelVersion = "landmark-model-001",
        PreprocessingVersion = "preprocess-001",
        EvaluationSplit = CephBenchmarkSplit.InternalTest,
        BootstrapReplicates = CephLandmarkEvaluationEngine.MinimumBootstrapReplicates,
        RandomSeed = 20260714,
        Benchmark = new CephBenchmarkManifestDto
        {
            SchemaVersion = CephBenchmarkManifestValidator.SchemaVersion,
            DatasetVersion = "pilot-001",
            LandmarkDefinitionVersion = CephBenchmarkManifestValidator.LandmarkDefinitionVersion,
            CreatedAt = FixedUtc,
            Cases =
            [
                BuildCase('a', 'a', Guid.Parse("11111111-1111-1111-1111-111111111111")),
                BuildCase(samePatient ? 'a' : 'b', 'b', Guid.Parse("22222222-2222-2222-2222-222222222222")),
            ],
        },
    };

    private static CephBenchmarkCaseDto BuildCase(char patientHashCharacter, char imageHashCharacter, Guid imageId)
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
            Deidentification = new CephDeidentificationEvidenceDto
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
            var x = 100d + index * 10;
            var y = 100d + index * 8;
            benchmarkCase.Annotations.Add(Annotation(key, "REV-0001", x, y));
            benchmarkCase.Annotations.Add(Annotation(key, "REV-0002", x + 1, y + 1));
            benchmarkCase.GoldStandard.Add(new CephGoldStandardPointDto
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

    private static void AddPredictions(
        CephLandmarkEvaluationRequestDto request,
        CephBenchmarkCaseDto benchmarkCase,
        double errorMm,
        string? missingKey = null)
    {
        request.Predictions.Add(new CephPredictionCaseDto
        {
            ImageId = benchmarkCase.ImageId,
            Points = benchmarkCase.GoldStandard.Select(gold => new CephPredictionPointDto
            {
                LandmarkKey = gold.LandmarkKey,
                Status = gold.LandmarkKey == missingKey
                    ? CephPredictionStatus.NotFound
                    : CephPredictionStatus.Predicted,
                X = gold.LandmarkKey == missingKey ? null : (gold.X ?? 100) + errorMm / benchmarkCase.MillimetresPerPixel,
                Y = gold.LandmarkKey == missingKey ? null : gold.Y ?? 100,
                Confidence = 0.9,
            }).ToList(),
        });
    }
}
