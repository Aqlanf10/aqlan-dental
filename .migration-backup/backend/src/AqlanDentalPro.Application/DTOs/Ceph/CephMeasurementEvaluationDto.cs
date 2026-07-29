using System.Text.Json.Serialization;

namespace AqlanDentalPro.Application.DTOs.Ceph;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CephMeasurementEvaluationRequestDto
{
    public string ProtocolVersion { get; set; } = string.Empty;
    public string GeometryVersion { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public string PreprocessingVersion { get; set; } = string.Empty;
    public CephBenchmarkSplit EvaluationSplit { get; set; }
    public string ToleranceRegistryVersion { get; set; } = string.Empty;
    public string ToleranceApprovalId { get; set; } = string.Empty;
    public bool TolerancesFrozenBeforeUnblinding { get; set; }
    public int BootstrapReplicates { get; set; } = 2000;
    public long RandomSeed { get; set; } = 20260714;
    public CephBenchmarkManifestDto? Benchmark { get; set; }
    public List<CephPredictionCaseDto> CandidatePredictions { get; set; } = [];
    public string? ComparatorVersion { get; set; }
    public List<CephPredictionCaseDto> ComparatorPredictions { get; set; } = [];
    public List<CephMeasurementToleranceDto> Tolerances { get; set; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CephMeasurementToleranceDto
{
    public string MeasurementName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public double MaximumAbsoluteError { get; set; }
    public List<CephMeasurementCategoryDto> Categories { get; set; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CephMeasurementCategoryDto
{
    public string Label { get; set; } = string.Empty;
    public double? MinimumInclusive { get; set; }
    public double? MaximumExclusive { get; set; }
}

public sealed class CephMeasurementEvaluationResultDto
{
    public bool IsValid { get; set; }
    public string ProtocolVersion { get; set; } = string.Empty;
    public string GeometryVersion { get; set; } = string.Empty;
    public string DatasetVersion { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public string PreprocessingVersion { get; set; } = string.Empty;
    public CephBenchmarkSplit EvaluationSplit { get; set; }
    public string ToleranceRegistryVersion { get; set; } = string.Empty;
    public string ToleranceApprovalId { get; set; } = string.Empty;
    public string? ComparatorVersion { get; set; }
    public int BootstrapReplicates { get; set; }
    public long RandomSeed { get; set; }
    public List<CephEvaluationIssueDto> Issues { get; set; } = [];
    public List<CephMeasurementMetricDto> Measurements { get; set; } = [];
}

public sealed class CephMeasurementMetricDto
{
    public string MeasurementName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public double MaximumAbsoluteError { get; set; }
    public int EligibleCaseCount { get; set; }
    public int ObservedCaseCount { get; set; }
    public int FailureCaseCount { get; set; }
    public double? FailureRatePercent { get; set; }
    public double? MeanSignedError { get; set; }
    public double? MeanAbsoluteError { get; set; }
    public double? MedianAbsoluteError { get; set; }
    public double? SignedErrorStandardDeviation { get; set; }
    public double? P95AbsoluteError { get; set; }
    public double? BlandAltmanLowerLimit { get; set; }
    public double? BlandAltmanUpperLimit { get; set; }
    public double? WithinTolerancePercent { get; set; }
    public int CategoryEligibleCaseCount { get; set; }
    public int CategoryDisagreementCount { get; set; }
    public double? CategoryDisagreementPercent { get; set; }
    public CephPairedComparatorMetricDto? PairedComparator { get; set; }
}

public sealed class CephPairedComparatorMetricDto
{
    public int PairedCaseCount { get; set; }
    public double? CandidateMeanAbsoluteError { get; set; }
    public double? ComparatorMeanAbsoluteError { get; set; }
    public double? MeanAbsoluteErrorDelta { get; set; }
    public CephConfidenceIntervalDto? MeanAbsoluteErrorDelta95Ci { get; set; }
    public int CandidateWinCount { get; set; }
    public int TieCount { get; set; }
    public int CandidateLossCount { get; set; }
}
