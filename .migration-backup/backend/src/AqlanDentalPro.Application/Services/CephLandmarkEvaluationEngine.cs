using System.Text.RegularExpressions;
using AqlanDentalPro.Application.DTOs.Ceph;

namespace AqlanDentalPro.Application.Services;

public static partial class CephLandmarkEvaluationEngine
{
    public const string ProtocolVersion = "ADP-CEPH-VAL-v1";
    public const int MinimumBootstrapReplicates = 100;
    public const int MaximumBootstrapReplicates = 5_000;
    public const int WideConfidenceIntervalPatientThreshold = 30;

    private static readonly double[] SdrThresholds = [1, 1.5, 2, 2.5, 3, 4];

    public static CephLandmarkEvaluationResultDto Evaluate(CephLandmarkEvaluationRequestDto? request)
    {
        var result = CreateResult(request);
        if (request is null)
        {
            AddIssue(result, "request.required", "$");
            return result;
        }

        ValidateHeader(request, result);
        if (HasErrors(result))
            return result;
        if (request.Benchmark is null)
        {
            AddIssue(result, "benchmark.required", "$.benchmark");
            return result;
        }

        var benchmarkValidation = CephBenchmarkManifestValidator.Validate(request.Benchmark);
        foreach (var issue in benchmarkValidation.Issues)
            AddIssue(
                result,
                $"benchmark.{issue.Code}",
                $"$.benchmark{TrimRoot(issue.Path)}",
                issue.Severity == CephBenchmarkIssueSeverity.Warning
                    ? CephEvaluationIssueSeverity.Warning
                    : CephEvaluationIssueSeverity.Error);

        if (!benchmarkValidation.IsReleaseReady)
            return result;

        var casesById = request.Benchmark.Cases.ToDictionary(item => item.ImageId);
        var predictionsById = ValidatePredictions(request.Predictions, casesById, result);
        if (HasErrors(result))
            return result;

        var evaluatedCases = request.Benchmark.Cases
            .Where(item => item.Split == request.EvaluationSplit)
            .OrderBy(item => item.ImageId)
            .ToList();
        if (evaluatedCases.Count == 0)
        {
            AddIssue(result, "benchmark.no-reportable-cases", "$.benchmark.cases");
            return result;
        }

        var observations = BuildObservations(evaluatedCases, predictionsById);
        if (observations.All(item => !item.IsVisibleGold))
        {
            AddIssue(result, "benchmark.no-visible-core-gold-points", "$.benchmark.cases");
            return result;
        }

        result.IsValid = true;
        result.Overall = Summarize(observations, request, "overall");
        result.ByLandmark = observations
            .GroupBy(item => item.LandmarkKey, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new CephLandmarkMetricDto
            {
                LandmarkKey = group.Key,
                Metrics = Summarize(group, request, $"landmark:{group.Key}"),
            })
            .ToList();
        result.ByCase = observations
            .GroupBy(item => item.Case.ImageId)
            .OrderBy(group => group.Key)
            .Select(group => new CephCaseMetricDto
            {
                ImageId = group.Key,
                Metrics = Summarize(group, request, $"case:{group.Key:D}"),
            })
            .ToList();
        result.BySubgroup = BuildSubgroups(observations, request);
        return result;
    }

    private static CephLandmarkEvaluationResultDto CreateResult(CephLandmarkEvaluationRequestDto? request) => new()
    {
        ProtocolVersion = request?.ProtocolVersion ?? string.Empty,
        DatasetVersion = request?.Benchmark?.DatasetVersion ?? string.Empty,
        ModelVersion = request?.ModelVersion ?? string.Empty,
        PreprocessingVersion = request?.PreprocessingVersion ?? string.Empty,
        EvaluationSplit = request?.EvaluationSplit ?? CephBenchmarkSplit.Unspecified,
        BootstrapReplicates = request?.BootstrapReplicates ?? 0,
        RandomSeed = request?.RandomSeed ?? 0,
    };

    private static void ValidateHeader(
        CephLandmarkEvaluationRequestDto request,
        CephLandmarkEvaluationResultDto result)
    {
        if (!string.Equals(request.ProtocolVersion, ProtocolVersion, StringComparison.Ordinal))
            AddIssue(result, "protocolVersion.unsupported", "$.protocolVersion");
        if (!VersionTokenRegex().IsMatch(request.ModelVersion))
            AddIssue(result, "modelVersion.invalid", "$.modelVersion");
        if (!VersionTokenRegex().IsMatch(request.PreprocessingVersion))
            AddIssue(result, "preprocessingVersion.invalid", "$.preprocessingVersion");
        if (request.EvaluationSplit is CephBenchmarkSplit.Unspecified or CephBenchmarkSplit.Training
            || !Enum.IsDefined(request.EvaluationSplit))
            AddIssue(result, "evaluationSplit.test-or-validation-required", "$.evaluationSplit");
        if (request.BootstrapReplicates is < MinimumBootstrapReplicates or > MaximumBootstrapReplicates)
            AddIssue(result, "bootstrapReplicates.out-of-range", "$.bootstrapReplicates");
    }

    private static Dictionary<Guid, CephPredictionCaseDto> ValidatePredictions(
        List<CephPredictionCaseDto>? predictions,
        IReadOnlyDictionary<Guid, CephBenchmarkCaseDto> casesById,
        CephLandmarkEvaluationResultDto result)
    {
        var predictionsById = new Dictionary<Guid, CephPredictionCaseDto>();
        if (predictions is null)
        {
            AddIssue(result, "predictions.required", "$.predictions");
            return predictionsById;
        }
        if (predictions.Count > 10_000)
        {
            AddIssue(result, "predictions.limit", "$.predictions");
            return predictionsById;
        }

        for (var caseIndex = 0; caseIndex < predictions.Count; caseIndex++)
        {
            var prediction = predictions[caseIndex];
            var casePath = $"$.predictions[{caseIndex}]";
            if (prediction is null)
            {
                AddIssue(result, "prediction-case.required", casePath);
                continue;
            }
            if (prediction.ImageId == Guid.Empty || !predictionsById.TryAdd(prediction.ImageId, prediction))
            {
                AddIssue(result, "prediction-case.imageId.invalid-or-duplicate", $"{casePath}.imageId");
                continue;
            }
            if (!casesById.TryGetValue(prediction.ImageId, out var benchmarkCase))
            {
                AddIssue(result, "prediction-case.imageId.unknown", $"{casePath}.imageId");
                continue;
            }

            ValidatePredictionPoints(prediction.Points, benchmarkCase, casePath, result);
        }

        return predictionsById;
    }

    private static void ValidatePredictionPoints(
        List<CephPredictionPointDto>? points,
        CephBenchmarkCaseDto benchmarkCase,
        string casePath,
        CephLandmarkEvaluationResultDto result)
    {
        if (points is null)
        {
            AddIssue(result, "prediction-points.required", $"{casePath}.points");
            return;
        }
        if (points.Count > CephBenchmarkManifestValidator.AllowedLandmarkKeys.Count)
        {
            AddIssue(result, "prediction-points.limit", $"{casePath}.points");
            return;
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (var pointIndex = 0; pointIndex < points.Count; pointIndex++)
        {
            var point = points[pointIndex];
            var pointPath = $"{casePath}.points[{pointIndex}]";
            if (point is null)
            {
                AddIssue(result, "prediction-point.required", pointPath);
                continue;
            }
            if (!CephBenchmarkManifestValidator.AllowedLandmarkKeys.Contains(point.LandmarkKey))
                AddIssue(result, "prediction-point.landmarkKey.unsupported", $"{pointPath}.landmarkKey");
            else if (!keys.Add(point.LandmarkKey))
                AddIssue(result, "prediction-point.landmarkKey.duplicate", $"{pointPath}.landmarkKey");

            if (!Enum.IsDefined(point.Status))
                AddIssue(result, "prediction-point.status.invalid", $"{pointPath}.status");
            if (point.Confidence is { } confidence
                && (!double.IsFinite(confidence) || confidence is < 0 or > 1))
                AddIssue(result, "prediction-point.confidence.invalid", $"{pointPath}.confidence");

            if (point.Status == CephPredictionStatus.Predicted)
            {
                if (point.X is not { } x || point.Y is not { } y
                    || !double.IsFinite(x) || !double.IsFinite(y)
                    || x < 0 || x > benchmarkCase.ImageWidth
                    || y < 0 || y > benchmarkCase.ImageHeight)
                    AddIssue(result, "prediction-point.coordinates.invalid", pointPath);
            }
            else if (point.X is not null || point.Y is not null)
            {
                AddIssue(result, "prediction-point.coordinates.must-be-null", pointPath);
            }
        }
    }

    private static List<Observation> BuildObservations(
        IReadOnlyList<CephBenchmarkCaseDto> cases,
        IReadOnlyDictionary<Guid, CephPredictionCaseDto> predictionsById)
    {
        var observations = new List<Observation>(cases.Count * CephBenchmarkManifestValidator.CoreLandmarkKeys.Count);
        foreach (var benchmarkCase in cases)
        {
            var predictionPoints = predictionsById.TryGetValue(benchmarkCase.ImageId, out var predictionCase)
                ? predictionCase.Points.ToDictionary(item => item.LandmarkKey, StringComparer.Ordinal)
                : new Dictionary<string, CephPredictionPointDto>(StringComparer.Ordinal);

            foreach (var gold in benchmarkCase.GoldStandard
                         .Where(item => CephBenchmarkManifestValidator.CoreLandmarkKeys.Contains(item.LandmarkKey))
                         .OrderBy(item => item.LandmarkKey, StringComparer.Ordinal))
            {
                predictionPoints.TryGetValue(gold.LandmarkKey, out var prediction);
                double? errorMm = null;
                if (gold.Visibility == CephLandmarkVisibility.Visible
                    && prediction?.Status == CephPredictionStatus.Predicted)
                {
                    var dx = prediction.X!.Value - gold.X!.Value;
                    var dy = prediction.Y!.Value - gold.Y!.Value;
                    errorMm = Math.Sqrt(dx * dx + dy * dy) * benchmarkCase.MillimetresPerPixel;
                }

                observations.Add(new Observation(
                    benchmarkCase,
                    gold.LandmarkKey,
                    gold.Visibility == CephLandmarkVisibility.Visible,
                    prediction?.Status == CephPredictionStatus.Predicted,
                    errorMm));
            }
        }

        return observations;
    }

    private static CephEvaluationMetricSummaryDto Summarize(
        IEnumerable<Observation> source,
        CephLandmarkEvaluationRequestDto request,
        string bootstrapScope)
    {
        var observations = source.ToList();
        var eligible = observations.Where(item => item.IsVisibleGold).ToList();
        var errors = eligible.Where(item => item.ErrorMm.HasValue).Select(item => item.ErrorMm!.Value).ToList();
        var notVisible = observations.Where(item => !item.IsVisibleGold).ToList();
        var caseGroups = eligible.GroupBy(item => item.Case.ImageId).ToList();
        var clusterCount = eligible.Select(item => item.Case.PatientGroupId).Distinct(StringComparer.Ordinal).Count();
        var summary = new CephEvaluationMetricSummaryDto
        {
            EligiblePointCount = eligible.Count,
            ObservedPointCount = errors.Count,
            FailurePointCount = eligible.Count - errors.Count,
            FailureRatePercent = eligible.Count == 0
                ? null
                : Percent(eligible.Count - errors.Count, eligible.Count),
            GoldNotVisiblePointCount = notVisible.Count,
            PredictedOnNotVisiblePointCount = notVisible.Count(item => item.HasPrediction),
            PredictedOnNotVisibleRatePercent = notVisible.Count == 0
                ? null
                : Percent(notVisible.Count(item => item.HasPrediction), notVisible.Count),
            PatientClusterCount = clusterCount,
            CaseCount = caseGroups.Count,
            CompleteCaseCount = caseGroups.Count(group => group.All(item => item.ErrorMm.HasValue)),
            MeanRadialErrorMm = errors.Count == 0 ? null : errors.Average(),
            MedianRadialErrorMm = Quantile(errors, 0.5),
            StandardDeviationMm = SampleStandardDeviation(errors),
            P90RadialErrorMm = Quantile(errors, 0.9),
            P95RadialErrorMm = Quantile(errors, 0.95),
            MaximumRadialErrorMm = errors.Count == 0 ? null : errors.Max(),
        };
        summary.AllLandmarksCompleteRatePercent = summary.CaseCount == 0
            ? null
            : Percent(summary.CompleteCaseCount, summary.CaseCount);

        var bootstrap = Bootstrap(eligible, request.BootstrapReplicates, request.RandomSeed, bootstrapScope);
        summary.MeanRadialError95Ci = ToInterval(bootstrap.MeanErrors);
        foreach (var threshold in SdrThresholds)
        {
            var observedSuccesses = errors.Count(error => error <= threshold);
            var thresholdBootstrap = bootstrap.SdrPercentByThreshold[threshold];
            summary.Sdr.Add(new CephSdrMetricDto
            {
                ThresholdMm = threshold,
                ObservedOnlyPercent = errors.Count == 0 ? null : Percent(observedSuccesses, errors.Count),
                FailurePenalizedPercent = eligible.Count == 0
                    ? null
                    : Percent(observedSuccesses, eligible.Count),
                FailurePenalized95Ci = ToInterval(thresholdBootstrap),
            });
        }

        return summary;
    }

    private static List<CephSubgroupMetricDto> BuildSubgroups(
        IReadOnlyList<Observation> observations,
        CephLandmarkEvaluationRequestDto request)
    {
        var memberships = new List<(string Dimension, string Value, Observation Observation)>();
        foreach (var observation in observations)
        {
            memberships.Add(("split", observation.Case.Split.ToString(), observation));
            memberships.Add(("site", observation.Case.SourceSiteCode, observation));
            memberships.Add(("device", observation.Case.DeviceCode, observation));
            memberships.Add(("ageBand", observation.Case.AgeBand.ToString(), observation));
            memberships.Add(("skeletalClass", observation.Case.SkeletalClass.ToString(), observation));
            memberships.Add(("anglePattern", observation.Case.AnglePattern.ToString(), observation));
            foreach (var flag in observation.Case.QualityFlags.OrderBy(item => item))
                memberships.Add(("qualityFlag", flag.ToString(), observation));
        }

        return memberships
            .GroupBy(item => (item.Dimension, item.Value))
            .OrderBy(group => group.Key.Dimension, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Value, StringComparer.Ordinal)
            .Select(group =>
            {
                var groupedObservations = group.Select(item => item.Observation).ToList();
                var patientCount = groupedObservations
                    .Where(item => item.IsVisibleGold)
                    .Select(item => item.Case.PatientGroupId)
                    .Distinct(StringComparer.Ordinal)
                    .Count();
                return new CephSubgroupMetricDto
                {
                    Dimension = group.Key.Dimension,
                    Value = group.Key.Value,
                    PatientClusterCount = patientCount,
                    WideConfidenceIntervalWarning = patientCount < WideConfidenceIntervalPatientThreshold,
                    Metrics = Summarize(
                        groupedObservations,
                        request,
                        $"subgroup:{group.Key.Dimension}:{group.Key.Value}"),
                };
            })
            .ToList();
    }

    private static BootstrapResult Bootstrap(
        IReadOnlyList<Observation> eligible,
        int replicates,
        long seed,
        string scope)
    {
        var result = new BootstrapResult();
        foreach (var threshold in SdrThresholds)
            result.SdrPercentByThreshold[threshold] = [];

        var clusters = eligible
            .GroupBy(item => item.Case.PatientGroupId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => ClusterAggregate.Create(group))
            .ToList();
        if (clusters.Count < 2 || eligible.Count == 0)
            return result;

        var random = new SplitMix64(unchecked((ulong)seed ^ StableHash(scope)));
        for (var replicate = 0; replicate < replicates; replicate++)
        {
            var eligibleCount = 0;
            var observedCount = 0;
            var errorSum = 0d;
            var successes = new int[SdrThresholds.Length];
            for (var draw = 0; draw < clusters.Count; draw++)
            {
                var cluster = clusters[random.NextInt(clusters.Count)];
                eligibleCount += cluster.EligibleCount;
                observedCount += cluster.ObservedCount;
                errorSum += cluster.ErrorSum;
                for (var thresholdIndex = 0; thresholdIndex < SdrThresholds.Length; thresholdIndex++)
                    successes[thresholdIndex] += cluster.Successes[thresholdIndex];
            }

            if (observedCount > 0)
                result.MeanErrors.Add(errorSum / observedCount);
            for (var thresholdIndex = 0; thresholdIndex < SdrThresholds.Length; thresholdIndex++)
                result.SdrPercentByThreshold[SdrThresholds[thresholdIndex]]
                    .Add(Percent(successes[thresholdIndex], eligibleCount));
        }

        return result;
    }

    private static CephConfidenceIntervalDto? ToInterval(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
            return null;
        return new CephConfidenceIntervalDto
        {
            Lower = Quantile(values, 0.025)!.Value,
            Upper = Quantile(values, 0.975)!.Value,
        };
    }

    private static double Percent(int numerator, int denominator) =>
        denominator == 0 ? 0 : 100d * numerator / denominator;

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
        if (lower == upper)
            return sorted[lower];
        return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
    }

    private static double? SampleStandardDeviation(IReadOnlyList<double> values)
    {
        if (values.Count < 2)
            return null;
        var mean = values.Average();
        var squaredDifferenceSum = values.Sum(value => Math.Pow(value - mean, 2));
        return Math.Sqrt(squaredDifferenceSum / (values.Count - 1));
    }

    private static void AddIssue(
        CephLandmarkEvaluationResultDto result,
        string code,
        string path,
        CephEvaluationIssueSeverity severity = CephEvaluationIssueSeverity.Error) =>
        result.Issues.Add(new CephEvaluationIssueDto { Severity = severity, Code = code, Path = path });

    private static bool HasErrors(CephLandmarkEvaluationResultDto result) =>
        result.Issues.Any(issue => issue.Severity == CephEvaluationIssueSeverity.Error);

    private static string TrimRoot(string path) => path.StartsWith('$') ? path[1..] : path;

    private static ulong StableHash(string value)
    {
        const ulong offset = 14695981039346656037;
        const ulong prime = 1099511628211;
        var hash = offset;
        foreach (var character in value)
        {
            hash ^= character;
            hash *= prime;
        }
        return hash;
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionTokenRegex();

    private sealed record Observation(
        CephBenchmarkCaseDto Case,
        string LandmarkKey,
        bool IsVisibleGold,
        bool HasPrediction,
        double? ErrorMm);

    private sealed class BootstrapResult
    {
        public List<double> MeanErrors { get; } = [];
        public Dictionary<double, List<double>> SdrPercentByThreshold { get; } = [];
    }

    private sealed record ClusterAggregate(
        int EligibleCount,
        int ObservedCount,
        double ErrorSum,
        int[] Successes)
    {
        public static ClusterAggregate Create(IEnumerable<Observation> source)
        {
            var observations = source.ToList();
            var errors = observations.Where(item => item.ErrorMm.HasValue).Select(item => item.ErrorMm!.Value).ToList();
            return new ClusterAggregate(
                observations.Count,
                errors.Count,
                errors.Sum(),
                SdrThresholds.Select(threshold => errors.Count(error => error <= threshold)).ToArray());
        }
    }

    private struct SplitMix64(ulong state)
    {
        private ulong _state = state;

        public int NextInt(int exclusiveUpperBound)
        {
            _state += 0x9E3779B97F4A7C15UL;
            var value = _state;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            value ^= value >> 31;
            return (int)(value % (uint)exclusiveUpperBound);
        }
    }
}
