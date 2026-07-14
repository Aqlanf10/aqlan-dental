using System.Text.Json.Serialization;
using AqlanDentalPro.Application.Serialization;

namespace AqlanDentalPro.Application.DTOs.Ceph;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CephLandmarkEvaluationRequestDto
{
    public string ProtocolVersion { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public string PreprocessingVersion { get; set; } = string.Empty;
    public CephBenchmarkSplit EvaluationSplit { get; set; }
    public int BootstrapReplicates { get; set; } = 2000;
    public long RandomSeed { get; set; } = 20260714;
    public CephBenchmarkManifestDto? Benchmark { get; set; }
    public List<CephPredictionCaseDto> Predictions { get; set; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CephPredictionCaseDto
{
    public Guid ImageId { get; set; }
    public List<CephPredictionPointDto> Points { get; set; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CephPredictionPointDto
{
    public string LandmarkKey { get; set; } = string.Empty;
    public CephPredictionStatus Status { get; set; }
    public double? X { get; set; }
    public double? Y { get; set; }
    public double? Confidence { get; set; }
}

public sealed class CephLandmarkEvaluationResultDto
{
    public bool IsValid { get; set; }
    public string ProtocolVersion { get; set; } = string.Empty;
    public string DatasetVersion { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public string PreprocessingVersion { get; set; } = string.Empty;
    public CephBenchmarkSplit EvaluationSplit { get; set; }
    public int BootstrapReplicates { get; set; }
    public long RandomSeed { get; set; }
    public List<CephEvaluationIssueDto> Issues { get; set; } = [];
    public CephEvaluationMetricSummaryDto? Overall { get; set; }
    public List<CephLandmarkMetricDto> ByLandmark { get; set; } = [];
    public List<CephCaseMetricDto> ByCase { get; set; } = [];
    public List<CephSubgroupMetricDto> BySubgroup { get; set; } = [];
}

public sealed class CephEvaluationIssueDto
{
    public CephEvaluationIssueSeverity Severity { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

public sealed class CephEvaluationMetricSummaryDto
{
    public int EligiblePointCount { get; set; }
    public int ObservedPointCount { get; set; }
    public int FailurePointCount { get; set; }
    public double? FailureRatePercent { get; set; }
    public int GoldNotVisiblePointCount { get; set; }
    public int PredictedOnNotVisiblePointCount { get; set; }
    public double? PredictedOnNotVisibleRatePercent { get; set; }
    public int PatientClusterCount { get; set; }
    public int CaseCount { get; set; }
    public int CompleteCaseCount { get; set; }
    public double? AllLandmarksCompleteRatePercent { get; set; }
    public double? MeanRadialErrorMm { get; set; }
    public double? MedianRadialErrorMm { get; set; }
    public double? StandardDeviationMm { get; set; }
    public double? P90RadialErrorMm { get; set; }
    public double? P95RadialErrorMm { get; set; }
    public double? MaximumRadialErrorMm { get; set; }
    public CephConfidenceIntervalDto? MeanRadialError95Ci { get; set; }
    public List<CephSdrMetricDto> Sdr { get; set; } = [];
}

public sealed class CephSdrMetricDto
{
    public double ThresholdMm { get; set; }
    public double? ObservedOnlyPercent { get; set; }
    public double? FailurePenalizedPercent { get; set; }
    public CephConfidenceIntervalDto? FailurePenalized95Ci { get; set; }
}

public sealed class CephConfidenceIntervalDto
{
    public double Lower { get; set; }
    public double Upper { get; set; }
}

public sealed class CephLandmarkMetricDto
{
    public string LandmarkKey { get; set; } = string.Empty;
    public CephEvaluationMetricSummaryDto Metrics { get; set; } = new();
}

public sealed class CephCaseMetricDto
{
    public Guid ImageId { get; set; }
    public CephEvaluationMetricSummaryDto Metrics { get; set; } = new();
}

public sealed class CephSubgroupMetricDto
{
    public string Dimension { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int PatientClusterCount { get; set; }
    public bool WideConfidenceIntervalWarning { get; set; }
    public CephEvaluationMetricSummaryDto Metrics { get; set; } = new();
}

[JsonConverter(typeof(StrictJsonStringEnumConverter<CephPredictionStatus>))]
public enum CephPredictionStatus
{
    Predicted,
    NotFound,
    Rejected,
}

[JsonConverter(typeof(StrictJsonStringEnumConverter<CephEvaluationIssueSeverity>))]
public enum CephEvaluationIssueSeverity
{
    Warning,
    Error,
}
