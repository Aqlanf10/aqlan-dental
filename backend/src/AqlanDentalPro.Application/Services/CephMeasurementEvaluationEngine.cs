using System.Text.RegularExpressions;
using AqlanDentalPro.Application.DTOs.Ceph;

namespace AqlanDentalPro.Application.Services;

public static partial class CephMeasurementEvaluationEngine
{
    public const string ProtocolVersion = "ADP-CEPH-MEAS-VAL-v1";

    private static readonly string[] AnalysisGroups =
        ["steiner", "tweed", "mcnamara", "ricketts", "downs", "jarabak", "wits"];

    public static CephMeasurementEvaluationResultDto Evaluate(CephMeasurementEvaluationRequestDto? request)
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
        ValidateTolerances(request.Tolerances, result);
        if (HasErrors(result))
            return result;

        var candidateValidation = ValidatePredictionSet(
            request,
            request.CandidatePredictions,
            request.ModelVersion,
            "candidatePredictions");
        CopyIssues(candidateValidation, result, "candidate");

        CephLandmarkEvaluationResultDto? comparatorValidation = null;
        if (!string.IsNullOrWhiteSpace(request.ComparatorVersion))
        {
            comparatorValidation = ValidatePredictionSet(
                request,
                request.ComparatorPredictions,
                request.ComparatorVersion,
                "comparatorPredictions");
            CopyIssues(comparatorValidation, result, "comparator");
        }
        else if (request.ComparatorPredictions is { Count: > 0 })
        {
            AddIssue(result, "comparatorVersion.required", "$.comparatorVersion");
        }

        if (HasErrors(result))
            return result;

        var candidateById = request.CandidatePredictions.ToDictionary(item => item.ImageId);
        var comparatorById = (request.ComparatorPredictions ?? []).ToDictionary(item => item.ImageId);
        var evaluatedCases = request.Benchmark!.Cases
            .Where(item => item.Split == request.EvaluationSplit)
            .OrderBy(item => item.ImageId)
            .ToList();
        var observations = BuildObservations(evaluatedCases, candidateById, comparatorById, request.Tolerances);

        foreach (var tolerance in request.Tolerances.OrderBy(item => item.MeasurementName, StringComparer.Ordinal))
        {
            var measurementObservations = observations
                .Where(item => item.Tolerance.MeasurementName == tolerance.MeasurementName)
                .ToList();
            if (measurementObservations.Count == 0)
            {
                AddIssue(
                    result,
                    "measurement.no-eligible-gold-cases",
                    $"$.tolerances[{request.Tolerances.IndexOf(tolerance)}]",
                    CephEvaluationIssueSeverity.Warning);
            }
            result.Measurements.Add(Summarize(measurementObservations, tolerance, request));
        }

        result.IsValid = true;
        return result;
    }

    private static CephMeasurementEvaluationResultDto CreateResult(CephMeasurementEvaluationRequestDto? request) => new()
    {
        ProtocolVersion = request?.ProtocolVersion ?? string.Empty,
        GeometryVersion = request?.GeometryVersion ?? string.Empty,
        DatasetVersion = request?.Benchmark?.DatasetVersion ?? string.Empty,
        ModelVersion = request?.ModelVersion ?? string.Empty,
        PreprocessingVersion = request?.PreprocessingVersion ?? string.Empty,
        EvaluationSplit = request?.EvaluationSplit ?? CephBenchmarkSplit.Unspecified,
        ToleranceRegistryVersion = request?.ToleranceRegistryVersion ?? string.Empty,
        ToleranceApprovalId = request?.ToleranceApprovalId ?? string.Empty,
        ComparatorVersion = request?.ComparatorVersion,
        BootstrapReplicates = request?.BootstrapReplicates ?? 0,
        RandomSeed = request?.RandomSeed ?? 0,
    };

    private static void ValidateHeader(
        CephMeasurementEvaluationRequestDto request,
        CephMeasurementEvaluationResultDto result)
    {
        if (request.ProtocolVersion != ProtocolVersion)
            AddIssue(result, "protocolVersion.unsupported", "$.protocolVersion");
        if (request.GeometryVersion != CephLateralGeometryEngine.Version)
            AddIssue(result, "geometryVersion.unsupported", "$.geometryVersion");
        if (!VersionTokenRegex().IsMatch(request.ModelVersion))
            AddIssue(result, "modelVersion.invalid", "$.modelVersion");
        if (!VersionTokenRegex().IsMatch(request.PreprocessingVersion))
            AddIssue(result, "preprocessingVersion.invalid", "$.preprocessingVersion");
        if (!VersionTokenRegex().IsMatch(request.ToleranceRegistryVersion))
            AddIssue(result, "toleranceRegistryVersion.invalid", "$.toleranceRegistryVersion");
        if (!ApprovalTokenRegex().IsMatch(request.ToleranceApprovalId))
            AddIssue(result, "toleranceApprovalId.invalid", "$.toleranceApprovalId");
        if (!request.TolerancesFrozenBeforeUnblinding)
            AddIssue(result, "tolerances.not-frozen-before-unblinding", "$.tolerancesFrozenBeforeUnblinding");
        if (request.EvaluationSplit is CephBenchmarkSplit.Unspecified or CephBenchmarkSplit.Training
            || !Enum.IsDefined(request.EvaluationSplit))
            AddIssue(result, "evaluationSplit.test-or-validation-required", "$.evaluationSplit");
        if (request.BootstrapReplicates is < CephLandmarkEvaluationEngine.MinimumBootstrapReplicates
            or > CephLandmarkEvaluationEngine.MaximumBootstrapReplicates)
            AddIssue(result, "bootstrapReplicates.out-of-range", "$.bootstrapReplicates");
        if (request.ComparatorVersion is not null && !VersionTokenRegex().IsMatch(request.ComparatorVersion))
            AddIssue(result, "comparatorVersion.invalid", "$.comparatorVersion");
    }

    private static void ValidateTolerances(
        List<CephMeasurementToleranceDto>? tolerances,
        CephMeasurementEvaluationResultDto result)
    {
        if (tolerances is null || tolerances.Count == 0)
        {
            AddIssue(result, "tolerances.required", "$.tolerances");
            return;
        }
        if (tolerances.Count > 100)
        {
            AddIssue(result, "tolerances.limit", "$.tolerances");
            return;
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < tolerances.Count; index++)
        {
            var tolerance = tolerances[index];
            var path = $"$.tolerances[{index}]";
            if (tolerance is null || !MeasurementNameRegex().IsMatch(tolerance.MeasurementName)
                || !names.Add(tolerance.MeasurementName))
                AddIssue(result, "tolerance.measurementName.invalid-or-duplicate", $"{path}.measurementName");
            if (tolerance is null)
                continue;
            if (tolerance.Unit is not ("mm" or "\u00b0" or "%"))
                AddIssue(result, "tolerance.unit.unsupported", $"{path}.unit");
            if (!double.IsFinite(tolerance.MaximumAbsoluteError) || tolerance.MaximumAbsoluteError <= 0)
                AddIssue(result, "tolerance.maximumAbsoluteError.invalid", $"{path}.maximumAbsoluteError");
            ValidateCategories(tolerance.Categories, path, result);
        }
    }

    private static void ValidateCategories(
        List<CephMeasurementCategoryDto>? categories,
        string tolerancePath,
        CephMeasurementEvaluationResultDto result)
    {
        if (categories is null || categories.Count == 0)
            return;
        if (categories.Count > 20)
        {
            AddIssue(result, "categories.limit", $"{tolerancePath}.categories");
            return;
        }

        var labels = new HashSet<string>(StringComparer.Ordinal);
        var ranges = new List<(double Minimum, double Maximum)>();
        for (var index = 0; index < categories.Count; index++)
        {
            var category = categories[index];
            var path = $"{tolerancePath}.categories[{index}]";
            if (category is null || !CategoryLabelRegex().IsMatch(category.Label) || !labels.Add(category.Label))
                AddIssue(result, "category.label.invalid-or-duplicate", $"{path}.label");
            if (category is null)
                continue;
            var minimum = category.MinimumInclusive ?? double.NegativeInfinity;
            var maximum = category.MaximumExclusive ?? double.PositiveInfinity;
            if (double.IsNaN(minimum) || double.IsNaN(maximum) || minimum >= maximum)
            {
                AddIssue(result, "category.range.invalid", path);
                continue;
            }
            if (ranges.Any(range => minimum < range.Maximum && maximum > range.Minimum))
                AddIssue(result, "category.range.overlap", path);
            ranges.Add((minimum, maximum));
        }
    }

    private static CephLandmarkEvaluationResultDto ValidatePredictionSet(
        CephMeasurementEvaluationRequestDto request,
        List<CephPredictionCaseDto>? predictions,
        string modelVersion,
        string scope) =>
        CephLandmarkEvaluationEngine.Evaluate(new CephLandmarkEvaluationRequestDto
        {
            ProtocolVersion = CephLandmarkEvaluationEngine.ProtocolVersion,
            ModelVersion = modelVersion,
            PreprocessingVersion = request.PreprocessingVersion,
            EvaluationSplit = request.EvaluationSplit,
            BootstrapReplicates = CephLandmarkEvaluationEngine.MinimumBootstrapReplicates,
            RandomSeed = unchecked(request.RandomSeed ^ (long)StableHash(scope)),
            Benchmark = request.Benchmark,
            Predictions = predictions!,
        });

    private static void CopyIssues(
        CephLandmarkEvaluationResultDto source,
        CephMeasurementEvaluationResultDto target,
        string prefix)
    {
        foreach (var issue in source.Issues)
        {
            target.Issues.Add(new CephEvaluationIssueDto
            {
                Severity = issue.Severity,
                Code = $"{prefix}.{issue.Code}",
                Path = issue.Path.StartsWith("$.predictions", StringComparison.Ordinal)
                    ? $"$.{prefix}Predictions{issue.Path[13..]}"
                    : issue.Path,
            });
        }
    }

    private static List<MeasurementObservation> BuildObservations(
        IEnumerable<CephBenchmarkCaseDto> cases,
        IReadOnlyDictionary<Guid, CephPredictionCaseDto> candidateById,
        IReadOnlyDictionary<Guid, CephPredictionCaseDto> comparatorById,
        IEnumerable<CephMeasurementToleranceDto> tolerances)
    {
        var observations = new List<MeasurementObservation>();
        foreach (var benchmarkCase in cases)
        {
            var pixelsPerMillimeter = 1d / benchmarkCase.MillimetresPerPixel;
            var gold = CalculateMeasurements(ToGoldPoints(benchmarkCase), pixelsPerMillimeter);
            var candidate = candidateById.TryGetValue(benchmarkCase.ImageId, out var candidateCase)
                ? CalculateMeasurements(ToPredictedPoints(candidateCase), pixelsPerMillimeter)
                : [];
            var comparator = comparatorById.TryGetValue(benchmarkCase.ImageId, out var comparatorCase)
                ? CalculateMeasurements(ToPredictedPoints(comparatorCase), pixelsPerMillimeter)
                : [];

            foreach (var tolerance in tolerances)
            {
                if (!gold.TryGetValue(tolerance.MeasurementName, out var goldMeasurement)
                    || goldMeasurement.Unit != tolerance.Unit)
                    continue;
                candidate.TryGetValue(tolerance.MeasurementName, out var candidateMeasurement);
                comparator.TryGetValue(tolerance.MeasurementName, out var comparatorMeasurement);
                observations.Add(new(
                    benchmarkCase.PatientGroupId,
                    tolerance,
                    goldMeasurement.Value,
                    candidateMeasurement?.Value,
                    comparatorMeasurement?.Value));
            }
        }
        return observations;
    }

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

    private static CephMeasurementMetricDto Summarize(
        List<MeasurementObservation> observations,
        CephMeasurementToleranceDto tolerance,
        CephMeasurementEvaluationRequestDto request)
    {
        var observed = observations.Where(item => item.CandidateValue.HasValue).ToList();
        var signedErrors = observed.Select(item => item.CandidateValue!.Value - item.GoldValue).ToList();
        var absoluteErrors = signedErrors.Select(Math.Abs).ToList();
        var sd = SampleStandardDeviation(signedErrors);
        var categoryPairs = observed
            .Select(item => (Gold: Classify(item.GoldValue, tolerance.Categories), Candidate: Classify(item.CandidateValue!.Value, tolerance.Categories)))
            .Where(item => item.Gold is not null && item.Candidate is not null)
            .ToList();
        var categoryDisagreements = categoryPairs.Count(item => item.Gold != item.Candidate);

        return new CephMeasurementMetricDto
        {
            MeasurementName = tolerance.MeasurementName,
            Unit = tolerance.Unit,
            MaximumAbsoluteError = tolerance.MaximumAbsoluteError,
            EligibleCaseCount = observations.Count,
            ObservedCaseCount = observed.Count,
            FailureCaseCount = observations.Count - observed.Count,
            FailureRatePercent = Percent(observations.Count - observed.Count, observations.Count),
            MeanSignedError = Mean(signedErrors),
            MeanAbsoluteError = Mean(absoluteErrors),
            MedianAbsoluteError = Quantile(absoluteErrors, 0.5),
            SignedErrorStandardDeviation = sd,
            P95AbsoluteError = Quantile(absoluteErrors, 0.95),
            BlandAltmanLowerLimit = sd.HasValue ? signedErrors.Average() - 1.96 * sd.Value : null,
            BlandAltmanUpperLimit = sd.HasValue ? signedErrors.Average() + 1.96 * sd.Value : null,
            WithinTolerancePercent = observed.Count == 0
                ? null
                : Percent(absoluteErrors.Count(value => value <= tolerance.MaximumAbsoluteError), observed.Count),
            CategoryEligibleCaseCount = categoryPairs.Count,
            CategoryDisagreementCount = categoryDisagreements,
            CategoryDisagreementPercent = Percent(categoryDisagreements, categoryPairs.Count),
            PairedComparator = SummarizeComparator(observations, request, tolerance.MeasurementName),
        };
    }

    private static CephPairedComparatorMetricDto? SummarizeComparator(
        List<MeasurementObservation> observations,
        CephMeasurementEvaluationRequestDto request,
        string measurementName)
    {
        if (string.IsNullOrWhiteSpace(request.ComparatorVersion))
            return null;
        var paired = observations
            .Where(item => item.CandidateValue.HasValue && item.ComparatorValue.HasValue)
            .Select(item => new PairedObservation(
                item.PatientGroupId,
                Math.Abs(item.CandidateValue!.Value - item.GoldValue),
                Math.Abs(item.ComparatorValue!.Value - item.GoldValue)))
            .ToList();
        var deltas = paired.Select(item => item.CandidateAbsoluteError - item.ComparatorAbsoluteError).ToList();
        return new CephPairedComparatorMetricDto
        {
            PairedCaseCount = paired.Count,
            CandidateMeanAbsoluteError = Mean(paired.Select(item => item.CandidateAbsoluteError).ToList()),
            ComparatorMeanAbsoluteError = Mean(paired.Select(item => item.ComparatorAbsoluteError).ToList()),
            MeanAbsoluteErrorDelta = Mean(deltas),
            MeanAbsoluteErrorDelta95Ci = BootstrapPairedDelta(paired, request, measurementName),
            CandidateWinCount = deltas.Count(value => value < 0),
            TieCount = deltas.Count(value => value == 0),
            CandidateLossCount = deltas.Count(value => value > 0),
        };
    }

    private static CephConfidenceIntervalDto? BootstrapPairedDelta(
        List<PairedObservation> observations,
        CephMeasurementEvaluationRequestDto request,
        string measurementName)
    {
        var clusters = observations
            .GroupBy(item => item.PatientGroupId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.ToList())
            .ToList();
        if (clusters.Count < 2)
            return null;

        var random = new SplitMix64(unchecked((ulong)request.RandomSeed ^ StableHash(measurementName)));
        var means = new List<double>(request.BootstrapReplicates);
        for (var replicate = 0; replicate < request.BootstrapReplicates; replicate++)
        {
            var sampled = new List<double>();
            for (var draw = 0; draw < clusters.Count; draw++)
            {
                sampled.AddRange(clusters[random.NextInt(clusters.Count)]
                    .Select(item => item.CandidateAbsoluteError - item.ComparatorAbsoluteError));
            }
            means.Add(sampled.Average());
        }
        return new CephConfidenceIntervalDto
        {
            Lower = Quantile(means, 0.025)!.Value,
            Upper = Quantile(means, 0.975)!.Value,
        };
    }

    private static string? Classify(double value, IEnumerable<CephMeasurementCategoryDto> categories) =>
        categories.FirstOrDefault(item =>
            (!item.MinimumInclusive.HasValue || value >= item.MinimumInclusive.Value)
            && (!item.MaximumExclusive.HasValue || value < item.MaximumExclusive.Value))?.Label;

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
        CephMeasurementEvaluationResultDto result,
        string code,
        string path,
        CephEvaluationIssueSeverity severity = CephEvaluationIssueSeverity.Error) =>
        result.Issues.Add(new() { Severity = severity, Code = code, Path = path });

    private static bool HasErrors(CephMeasurementEvaluationResultDto result) =>
        result.Issues.Any(item => item.Severity == CephEvaluationIssueSeverity.Error);

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

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._:/-]{2,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex ApprovalTokenRegex();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex CategoryLabelRegex();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex MeasurementNameRegex();

    private sealed record MeasurementObservation(
        string PatientGroupId,
        CephMeasurementToleranceDto Tolerance,
        double GoldValue,
        double? CandidateValue,
        double? ComparatorValue);

    private sealed record PairedObservation(
        string PatientGroupId,
        double CandidateAbsoluteError,
        double ComparatorAbsoluteError);

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
