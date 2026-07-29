using System.Text.RegularExpressions;
using AqlanDentalPro.Application.DTOs.Ceph;

namespace AqlanDentalPro.Application.Services;

public static partial class CephRepeatabilityEvaluationEngine
{
    public const string ProtocolVersion = "ADP-CEPH-REPEAT-v1";
    public const int RequiredRunCount = 3;

    private static readonly string[] AnalysisGroups =
        ["steiner", "tweed", "mcnamara", "ricketts", "downs", "jarabak", "wits"];

    public static CephRepeatabilityEvaluationResultDto Evaluate(CephRepeatabilityEvaluationRequestDto? request)
    {
        var result = CreateResult(request);
        if (request is null)
        {
            AddIssue(result, "request.required", "$");
            return result;
        }

        ValidateHeader(request, result);
        if (request.Benchmark is null)
            AddIssue(result, "benchmark.required", "$.benchmark");
        ValidateRuns(request, result);
        if (HasErrors(result))
            return result;

        var evaluatedCases = request.Benchmark!.Cases
            .Where(item => item.Split == request.EvaluationSplit)
            .OrderBy(item => item.ImageId)
            .ToList();
        var runMaps = request.Runs.Select(run => run.Predictions.ToDictionary(item => item.ImageId)).ToList();

        result.Landmarks = BuildLandmarkMetrics(evaluatedCases, runMaps);
        result.Measurements = BuildMeasurementMetrics(evaluatedCases, runMaps);
        result.ConfidenceCoverage = BuildConfidenceCoverage(evaluatedCases, runMaps, request);
        result.IsValid = true;
        return result;
    }

    private static CephRepeatabilityEvaluationResultDto CreateResult(CephRepeatabilityEvaluationRequestDto? request) => new()
    {
        ProtocolVersion = request?.ProtocolVersion ?? string.Empty,
        GeometryVersion = request?.GeometryVersion ?? string.Empty,
        DatasetVersion = request?.Benchmark?.DatasetVersion ?? string.Empty,
        ModelVersion = request?.ModelVersion ?? string.Empty,
        PreprocessingVersion = request?.PreprocessingVersion ?? string.Empty,
        EvaluationSplit = request?.EvaluationSplit ?? CephBenchmarkSplit.Unspecified,
        ClinicalErrorThresholdMm = request?.ClinicalErrorThresholdMm ?? 0,
    };

    private static void ValidateHeader(
        CephRepeatabilityEvaluationRequestDto request,
        CephRepeatabilityEvaluationResultDto result)
    {
        if (request.ProtocolVersion != ProtocolVersion)
            AddIssue(result, "protocolVersion.unsupported", "$.protocolVersion");
        if (request.GeometryVersion != CephLateralGeometryEngine.Version)
            AddIssue(result, "geometryVersion.unsupported", "$.geometryVersion");
        if (!VersionTokenRegex().IsMatch(request.ModelVersion))
            AddIssue(result, "modelVersion.invalid", "$.modelVersion");
        if (!VersionTokenRegex().IsMatch(request.PreprocessingVersion))
            AddIssue(result, "preprocessingVersion.invalid", "$.preprocessingVersion");
        if (request.EvaluationSplit is CephBenchmarkSplit.Unspecified or CephBenchmarkSplit.Training
            || !Enum.IsDefined(request.EvaluationSplit))
            AddIssue(result, "evaluationSplit.test-or-validation-required", "$.evaluationSplit");
        if (!double.IsFinite(request.ClinicalErrorThresholdMm) || request.ClinicalErrorThresholdMm <= 0)
            AddIssue(result, "clinicalErrorThresholdMm.invalid", "$.clinicalErrorThresholdMm");
        if (request.ConfidenceThresholds is null || request.ConfidenceThresholds.Count == 0)
        {
            AddIssue(result, "confidenceThresholds.required", "$.confidenceThresholds");
        }
        else if (request.ConfidenceThresholds.Count > 20
            || request.ConfidenceThresholds.Any(value => !double.IsFinite(value) || value < 0 || value > 1)
            || request.ConfidenceThresholds.Distinct().Count() != request.ConfidenceThresholds.Count)
        {
            AddIssue(result, "confidenceThresholds.invalid", "$.confidenceThresholds");
        }
    }

    private static void ValidateRuns(
        CephRepeatabilityEvaluationRequestDto request,
        CephRepeatabilityEvaluationResultDto result)
    {
        if (request.Runs is null || request.Runs.Count != RequiredRunCount)
        {
            AddIssue(result, "runs.exactly-three-required", "$.runs");
            return;
        }

        var runIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < request.Runs.Count; index++)
        {
            var run = request.Runs[index];
            var path = $"$.runs[{index}]";
            if (run is null || !VersionTokenRegex().IsMatch(run.RunId) || !runIds.Add(run.RunId))
            {
                AddIssue(result, "run.runId.invalid-or-duplicate", $"{path}.runId");
                continue;
            }

            var validation = CephLandmarkEvaluationEngine.Evaluate(new CephLandmarkEvaluationRequestDto
            {
                ProtocolVersion = CephLandmarkEvaluationEngine.ProtocolVersion,
                ModelVersion = request.ModelVersion,
                PreprocessingVersion = request.PreprocessingVersion,
                EvaluationSplit = request.EvaluationSplit,
                BootstrapReplicates = CephLandmarkEvaluationEngine.MinimumBootstrapReplicates,
                RandomSeed = 20260714 + index,
                Benchmark = request.Benchmark,
                Predictions = run.Predictions!,
            });
            foreach (var issue in validation.Issues)
            {
                result.Issues.Add(new CephEvaluationIssueDto
                {
                    Severity = issue.Severity,
                    Code = $"run.{issue.Code}",
                    Path = issue.Path.StartsWith("$.predictions", StringComparison.Ordinal)
                        ? $"{path}.predictions{issue.Path[13..]}"
                        : issue.Path,
                });
            }
        }
    }

    private static List<CephLandmarkRepeatabilityMetricDto> BuildLandmarkMetrics(
        IReadOnlyList<CephBenchmarkCaseDto> cases,
        IReadOnlyList<Dictionary<Guid, CephPredictionCaseDto>> runMaps)
    {
        var results = new List<CephLandmarkRepeatabilityMetricDto>();
        foreach (var landmarkKey in CephBenchmarkManifestValidator.CoreLandmarkKeys.Order(StringComparer.Ordinal))
        {
            var eligibleCount = 0;
            var completeCount = 0;
            var consistentCount = 0;
            var pairwiseDisplacements = new List<double>();
            var withinCaseRadialSd = new List<double>();
            foreach (var benchmarkCase in cases)
            {
                var gold = benchmarkCase.GoldStandard.First(item => item.LandmarkKey == landmarkKey);
                if (gold.Visibility != CephLandmarkVisibility.Visible)
                    continue;
                eligibleCount++;
                var points = runMaps.Select(map => PredictedPoint(map, benchmarkCase.ImageId, landmarkKey)).ToList();
                var statuses = points.Select(item => item is not null).ToList();
                if (statuses.All(status => status == statuses[0]))
                    consistentCount++;
                if (points.Any(item => item is null))
                    continue;

                completeCount++;
                var resolved = points.Select(item => item!).ToList();
                for (var first = 0; first < resolved.Count; first++)
                {
                    for (var second = first + 1; second < resolved.Count; second++)
                    {
                        pairwiseDisplacements.Add(
                            CephLateralGeometryEngine.Distance(resolved[first], resolved[second])
                            * benchmarkCase.MillimetresPerPixel);
                    }
                }
                var centroid = new CephGeometryPoint(resolved.Average(item => item.X), resolved.Average(item => item.Y));
                var radialVariance = resolved.Sum(item => Math.Pow(
                    CephLateralGeometryEngine.Distance(item, centroid) * benchmarkCase.MillimetresPerPixel,
                    2)) / (resolved.Count - 1);
                withinCaseRadialSd.Add(Math.Sqrt(radialVariance));
            }

            results.Add(new CephLandmarkRepeatabilityMetricDto
            {
                LandmarkKey = landmarkKey,
                EligibleCaseCount = eligibleCount,
                CompleteThreeRunCaseCount = completeCount,
                MissingStatusConsistentCaseCount = consistentCount,
                MissingStatusConsistencyPercent = Percent(consistentCount, eligibleCount),
                MeanPairwiseDisplacementMm = Mean(pairwiseDisplacements),
                MedianPairwiseDisplacementMm = Quantile(pairwiseDisplacements, 0.5),
                P95PairwiseDisplacementMm = Quantile(pairwiseDisplacements, 0.95),
                MaximumPairwiseDisplacementMm = pairwiseDisplacements.Count == 0 ? null : pairwiseDisplacements.Max(),
                MeanWithinCaseRadialSdMm = Mean(withinCaseRadialSd),
                P95WithinCaseRadialSdMm = Quantile(withinCaseRadialSd, 0.95),
            });
        }
        return results;
    }

    private static List<CephMeasurementRepeatabilityMetricDto> BuildMeasurementMetrics(
        IReadOnlyList<CephBenchmarkCaseDto> cases,
        IReadOnlyList<Dictionary<Guid, CephPredictionCaseDto>> runMaps)
    {
        var caseRows = new List<MeasurementCaseRow>();
        foreach (var benchmarkCase in cases)
        {
            var pixelsPerMillimeter = 1d / benchmarkCase.MillimetresPerPixel;
            var gold = CalculateMeasurements(ToGoldPoints(benchmarkCase), pixelsPerMillimeter);
            var runs = runMaps.Select(map => map.TryGetValue(benchmarkCase.ImageId, out var prediction)
                ? CalculateMeasurements(ToPredictedPoints(prediction), pixelsPerMillimeter)
                : []).ToList();
            caseRows.Add(new(gold, runs));
        }

        var measurementNames = caseRows.SelectMany(item => item.Gold.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        var results = new List<CephMeasurementRepeatabilityMetricDto>();
        foreach (var measurementName in measurementNames)
        {
            var eligible = caseRows.Where(item => item.Gold.ContainsKey(measurementName)).ToList();
            var completeValues = eligible
                .Select(item => item.Runs.All(run => run.ContainsKey(measurementName))
                    ? item.Runs.Select(run => run[measurementName].Value).ToArray()
                    : null)
                .Where(item => item is not null)
                .Select(item => item!)
                .ToList();
            var withinCaseSd = completeValues.Select(SampleStandardDeviation).Select(item => item!.Value).ToList();
            var example = eligible[0].Gold[measurementName];
            results.Add(new CephMeasurementRepeatabilityMetricDto
            {
                MeasurementName = measurementName,
                Unit = example.Unit,
                EligibleCaseCount = eligible.Count,
                CompleteThreeRunCaseCount = completeValues.Count,
                MeanWithinCaseStandardDeviation = Mean(withinCaseSd),
                P95WithinCaseStandardDeviation = Quantile(withinCaseSd, 0.95),
                AbsoluteAgreementIcc = AbsoluteAgreementIcc(completeValues),
            });
        }
        return results;
    }

    private static List<CephConfidenceCoverageMetricDto> BuildConfidenceCoverage(
        IReadOnlyList<CephBenchmarkCaseDto> cases,
        IReadOnlyList<Dictionary<Guid, CephPredictionCaseDto>> runMaps,
        CephRepeatabilityEvaluationRequestDto request)
    {
        var observations = new List<ConfidenceObservation>();
        foreach (var benchmarkCase in cases)
        {
            foreach (var gold in benchmarkCase.GoldStandard.Where(item =>
                         CephBenchmarkManifestValidator.CoreLandmarkKeys.Contains(item.LandmarkKey)
                         && item.Visibility == CephLandmarkVisibility.Visible))
            {
                foreach (var runMap in runMaps)
                {
                    var prediction = Prediction(runMap, benchmarkCase.ImageId, gold.LandmarkKey);
                    var predicted = prediction?.Status == CephPredictionStatus.Predicted
                        && prediction.X.HasValue && prediction.Y.HasValue;
                    var error = predicted
                        ? Math.Sqrt(Math.Pow(prediction!.X!.Value - gold.X!.Value, 2)
                            + Math.Pow(prediction.Y!.Value - gold.Y!.Value, 2)) * benchmarkCase.MillimetresPerPixel
                        : (double?)null;
                    observations.Add(new(predicted, prediction?.Confidence, error));
                }
            }
        }

        return request.ConfidenceThresholds.Order().Select(threshold =>
        {
            var retained = observations.Where(item => item.Predicted
                && item.Confidence.HasValue && item.Confidence.Value >= threshold).ToList();
            var aboveError = observations.Where(item => item.ErrorMm > request.ClinicalErrorThresholdMm).ToList();
            var referredAboveError = aboveError.Count(item => !item.Confidence.HasValue || item.Confidence.Value < threshold);
            var missing = observations.Where(item => !item.Predicted).ToList();
            return new CephConfidenceCoverageMetricDto
            {
                ConfidenceThreshold = threshold,
                EligiblePointRunCount = observations.Count,
                RetainedPointRunCount = retained.Count,
                CoveragePercent = Percent(retained.Count, observations.Count),
                RetainedMeanRadialErrorMm = Mean(retained.Where(item => item.ErrorMm.HasValue).Select(item => item.ErrorMm!.Value).ToList()),
                AboveClinicalErrorPointRunCount = aboveError.Count,
                ReferredAboveClinicalErrorPointRunCount = referredAboveError,
                AboveClinicalErrorReferralSensitivityPercent = Percent(referredAboveError, aboveError.Count),
                MissingOrRejectedPointRunCount = missing.Count,
                ReferredMissingOrRejectedPointRunCount = missing.Count,
                MissingOrRejectedReferralPercent = Percent(missing.Count, missing.Count),
            };
        }).ToList();
    }

    private static double? AbsoluteAgreementIcc(IReadOnlyList<double[]> rows)
    {
        if (rows.Count < 2 || rows.Any(row => row.Length != RequiredRunCount))
            return null;
        var subjectCount = rows.Count;
        var runCount = RequiredRunCount;
        var grandMean = rows.SelectMany(row => row).Average();
        var subjectMeans = rows.Select(row => row.Average()).ToArray();
        var runMeans = Enumerable.Range(0, runCount).Select(run => rows.Average(row => row[run])).ToArray();
        var subjectMeanSquare = runCount * subjectMeans.Sum(mean => Math.Pow(mean - grandMean, 2)) / (subjectCount - 1);
        var runMeanSquare = subjectCount * runMeans.Sum(mean => Math.Pow(mean - grandMean, 2)) / (runCount - 1);
        var residualSum = rows.SelectMany((row, subject) => row.Select((value, run) =>
            Math.Pow(value - subjectMeans[subject] - runMeans[run] + grandMean, 2))).Sum();
        var errorMeanSquare = residualSum / ((subjectCount - 1) * (runCount - 1));
        var denominator = subjectMeanSquare + (runCount - 1) * errorMeanSquare
            + runCount * (runMeanSquare - errorMeanSquare) / subjectCount;
        return Math.Abs(denominator) < 1e-12 ? null : (subjectMeanSquare - errorMeanSquare) / denominator;
    }

    private static CephGeometryPoint? PredictedPoint(
        IReadOnlyDictionary<Guid, CephPredictionCaseDto> runMap,
        Guid imageId,
        string landmarkKey)
    {
        var prediction = Prediction(runMap, imageId, landmarkKey);
        return prediction?.Status == CephPredictionStatus.Predicted
            && prediction.X.HasValue && prediction.Y.HasValue
            ? new(prediction.X.Value, prediction.Y.Value)
            : null;
    }

    private static CephPredictionPointDto? Prediction(
        IReadOnlyDictionary<Guid, CephPredictionCaseDto> runMap,
        Guid imageId,
        string landmarkKey) =>
        runMap.TryGetValue(imageId, out var predictionCase)
            ? predictionCase.Points.FirstOrDefault(item => item.LandmarkKey == landmarkKey)
            : null;

    private static Dictionary<string, CephGeometryMeasurement> CalculateMeasurements(
        IReadOnlyDictionary<string, CephGeometryPoint> points,
        double pixelsPerMillimeter) =>
        CephLateralGeometryEngine.Calculate(points, pixelsPerMillimeter, AnalysisGroups)
            .ToDictionary(item => item.Name, StringComparer.Ordinal);

    private static Dictionary<string, CephGeometryPoint> ToGoldPoints(CephBenchmarkCaseDto benchmarkCase) =>
        benchmarkCase.GoldStandard
            .Where(item => item.Visibility == CephLandmarkVisibility.Visible && item.X.HasValue && item.Y.HasValue)
            .ToDictionary(item => item.LandmarkKey, item => new CephGeometryPoint(item.X!.Value, item.Y!.Value), StringComparer.Ordinal);

    private static Dictionary<string, CephGeometryPoint> ToPredictedPoints(CephPredictionCaseDto predictionCase) =>
        predictionCase.Points
            .Where(item => item.Status == CephPredictionStatus.Predicted && item.X.HasValue && item.Y.HasValue)
            .ToDictionary(item => item.LandmarkKey, item => new CephGeometryPoint(item.X!.Value, item.Y!.Value), StringComparer.Ordinal);

    private static double? Mean(IReadOnlyCollection<double> values) => values.Count == 0 ? null : values.Average();

    private static double? Percent(int numerator, int denominator) =>
        denominator == 0 ? null : 100d * numerator / denominator;

    private static double? Quantile(IReadOnlyList<double> values, double probability)
    {
        if (values.Count == 0)
            return null;
        var sorted = values.Order().ToArray();
        if (sorted.Length == 1)
            return sorted[0];
        var position = (sorted.Length - 1) * probability;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        return lower == upper
            ? sorted[lower]
            : sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
    }

    private static double? SampleStandardDeviation(IReadOnlyList<double> values)
    {
        if (values.Count < 2)
            return null;
        var mean = values.Average();
        return Math.Sqrt(values.Sum(value => Math.Pow(value - mean, 2)) / (values.Count - 1));
    }

    private static void AddIssue(
        CephRepeatabilityEvaluationResultDto result,
        string code,
        string path,
        CephEvaluationIssueSeverity severity = CephEvaluationIssueSeverity.Error) =>
        result.Issues.Add(new() { Severity = severity, Code = code, Path = path });

    private static bool HasErrors(CephRepeatabilityEvaluationResultDto result) =>
        result.Issues.Any(item => item.Severity == CephEvaluationIssueSeverity.Error);

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionTokenRegex();

    private sealed record MeasurementCaseRow(
        Dictionary<string, CephGeometryMeasurement> Gold,
        List<Dictionary<string, CephGeometryMeasurement>> Runs);

    private sealed record ConfidenceObservation(bool Predicted, double? Confidence, double? ErrorMm);
}
