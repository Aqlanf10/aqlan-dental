using System.Text.Json;
using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ceph;

public sealed class CephMeasurementEvaluationEngineTests
{
    private static readonly DateTimeOffset FixedUtc =
        new(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Evaluate_ComputesBiasBlandAltmanToleranceCategoriesAndPairedDelta()
    {
        var request = BuildRequest();
        AddPredictions(request.CandidatePredictions, request.Benchmark!.Cases[0], 0);
        AddPredictions(request.CandidatePredictions, request.Benchmark.Cases[1], 30);
        AddPredictions(request.ComparatorPredictions, request.Benchmark.Cases[0], 0);
        AddPredictions(request.ComparatorPredictions, request.Benchmark.Cases[1], 10);

        var first = CephMeasurementEvaluationEngine.Evaluate(request);
        var second = CephMeasurementEvaluationEngine.Evaluate(request);

        first.IsValid.Should().BeTrue();
        first.Should().BeEquivalentTo(second, options => options.WithStrictOrdering());
        first.GeometryVersion.Should().Be(CephLateralGeometryEngine.Version);
        var sna = first.Measurements.Should().ContainSingle().Subject;
        sna.MeasurementName.Should().Be("SNA");
        sna.EligibleCaseCount.Should().Be(2);
        sna.ObservedCaseCount.Should().Be(2);
        sna.MeanSignedError.Should().NotBe(0);
        sna.MeanAbsoluteError.Should().BeGreaterThan(0);
        sna.SignedErrorStandardDeviation.Should().NotBeNull();
        sna.BlandAltmanLowerLimit.Should().NotBeNull();
        sna.BlandAltmanUpperLimit.Should().NotBeNull();
        sna.WithinTolerancePercent.Should().BeInRange(0, 100);
        sna.CategoryEligibleCaseCount.Should().Be(2);
        sna.PairedComparator.Should().NotBeNull();
        sna.PairedComparator!.PairedCaseCount.Should().Be(2);
        sna.PairedComparator.MeanAbsoluteErrorDelta.Should().BeGreaterThan(0);
        sna.PairedComparator.MeanAbsoluteErrorDelta95Ci.Should().NotBeNull();
        sna.PairedComparator.CandidateLossCount.Should().Be(1);
        sna.PairedComparator.TieCount.Should().Be(1);
    }

    [Fact]
    public void Evaluate_PreservesMissingDerivedMeasurementAsFailure()
    {
        var request = BuildRequest();
        AddPredictions(request.CandidatePredictions, request.Benchmark!.Cases[0], 0);
        AddPredictions(request.CandidatePredictions, request.Benchmark.Cases[1], 0, "A");

        var result = CephMeasurementEvaluationEngine.Evaluate(request);

        result.IsValid.Should().BeTrue();
        var sna = result.Measurements.Single();
        sna.EligibleCaseCount.Should().Be(2);
        sna.ObservedCaseCount.Should().Be(1);
        sna.FailureCaseCount.Should().Be(1);
        sna.FailureRatePercent.Should().Be(50);
    }

    [Theory]
    [InlineData("ADP-CEPH-GEOMETRY-v0", true, "geometryVersion.unsupported")]
    [InlineData("ADP-CEPH-GEOMETRY-v1", false, "tolerances.not-frozen-before-unblinding")]
    public void Evaluate_RejectsUnfrozenScientificContract(
        string geometryVersion,
        bool frozen,
        string expectedCode)
    {
        var request = BuildRequest();
        request.GeometryVersion = geometryVersion;
        request.TolerancesFrozenBeforeUnblinding = frozen;

        var result = CephMeasurementEvaluationEngine.Evaluate(request);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(item => item.Code == expectedCode);
    }

    [Fact]
    public void Evaluate_RejectsOverlappingCategoriesAndTrainingSplit()
    {
        var request = BuildRequest();
        request.EvaluationSplit = CephBenchmarkSplit.Training;
        request.Tolerances[0].Categories =
        [
            new() { Label = "one", MaximumExclusive = 10 },
            new() { Label = "two", MinimumInclusive = 5 },
        ];

        var result = CephMeasurementEvaluationEngine.Evaluate(request);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(item => item.Code == "evaluationSplit.test-or-validation-required");
        result.Issues.Should().Contain(item => item.Code == "category.range.overlap");
    }

    [Fact]
    public void Controller_UsesStrictMeasurementJsonContract()
    {
        var controller = new CephBenchmarkController();
        var unknownProperty = JsonSerializer.Deserialize<JsonElement>("""
            {
              "protocolVersion":"ADP-CEPH-MEAS-VAL-v1",
              "geometryVersion":"ADP-CEPH-GEOMETRY-v1",
              "modelVersion":"model-001",
              "preprocessingVersion":"prep-001",
              "evaluationSplit":"InternalTest",
              "toleranceRegistryVersion":"tol-001",
              "toleranceApprovalId":"APP-001",
              "tolerancesFrozenBeforeUnblinding":true,
              "bootstrapReplicates":100,
              "randomSeed":1,
              "benchmark":null,
              "candidatePredictions":[],
              "comparatorPredictions":[],
              "tolerances":[],
              "unexpected":true
            }
            """);

        var response = controller.EvaluateMeasurements(unknownProperty);

        response.Should().BeOfType<BadRequestObjectResult>();
    }

    private static CephMeasurementEvaluationRequestDto BuildRequest() => new()
    {
        ProtocolVersion = CephMeasurementEvaluationEngine.ProtocolVersion,
        GeometryVersion = CephLateralGeometryEngine.Version,
        ModelVersion = "candidate-001",
        PreprocessingVersion = "preprocess-001",
        EvaluationSplit = CephBenchmarkSplit.InternalTest,
        ToleranceRegistryVersion = "tol-registry-001",
        ToleranceApprovalId = "APP-CEPH-TOL-001",
        TolerancesFrozenBeforeUnblinding = true,
        BootstrapReplicates = CephLandmarkEvaluationEngine.MinimumBootstrapReplicates,
        RandomSeed = 20260714,
        ComparatorVersion = "comparator-001",
        Tolerances =
        [
            new CephMeasurementToleranceDto
            {
                MeasurementName = "SNA",
                Unit = "\u00b0",
                MaximumAbsoluteError = 2,
                Categories =
                [
                    new() { Label = "low", MaximumExclusive = 80 },
                    new() { Label = "reference", MinimumInclusive = 80, MaximumExclusive = 84 },
                    new() { Label = "high", MinimumInclusive = 84 },
                ],
            },
        ],
        Benchmark = new CephBenchmarkManifestDto
        {
            SchemaVersion = CephBenchmarkManifestValidator.SchemaVersion,
            DatasetVersion = "pilot-001",
            LandmarkDefinitionVersion = CephBenchmarkManifestValidator.LandmarkDefinitionVersion,
            CreatedAt = FixedUtc,
            Cases =
            [
                BuildCase('a', 'a', Guid.Parse("11111111-1111-1111-1111-111111111111")),
                BuildCase('b', 'b', Guid.Parse("22222222-2222-2222-2222-222222222222")),
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
            var y = 150d + index * index * 2;
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

    private static void AddPredictions(
        ICollection<CephPredictionCaseDto> destination,
        CephBenchmarkCaseDto benchmarkCase,
        double aVerticalOffsetPixels,
        string? missingKey = null)
    {
        destination.Add(new CephPredictionCaseDto
        {
            ImageId = benchmarkCase.ImageId,
            Points = benchmarkCase.GoldStandard.Select(gold => new CephPredictionPointDto
            {
                LandmarkKey = gold.LandmarkKey,
                Status = gold.LandmarkKey == missingKey ? CephPredictionStatus.NotFound : CephPredictionStatus.Predicted,
                X = gold.LandmarkKey == missingKey ? null : gold.X,
                Y = gold.LandmarkKey == missingKey
                    ? null
                    : gold.Y + (gold.LandmarkKey == "A" ? aVerticalOffsetPixels : 0),
                Confidence = 0.9,
            }).ToList(),
        });
    }
}
