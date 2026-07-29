using System.Text.Json.Serialization;

namespace AqlanDentalPro.Application.DTOs.Ceph;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CephRepeatabilityEvaluationRequestDto
{
    public string ProtocolVersion { get; set; } = string.Empty;
    public string GeometryVersion { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public string PreprocessingVersion { get; set; } = string.Empty;
    public CephBenchmarkSplit EvaluationSplit { get; set; }
    public double ClinicalErrorThresholdMm { get; set; }
    public List<double> ConfidenceThresholds { get; set; } = [];
    public CephBenchmarkManifestDto? Benchmark { get; set; }
    public List<CephRepeatedPredictionRunDto> Runs { get; set; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CephRepeatedPredictionRunDto
{
    public string RunId { get; set; } = string.Empty;
    public List<CephPredictionCaseDto> Predictions { get; set; } = [];
}

public sealed class CephRepeatabilityEvaluationResultDto
{
    public bool IsValid { get; set; }
    public string ProtocolVersion { get; set; } = string.Empty;
    public string GeometryVersion { get; set; } = string.Empty;
    public string DatasetVersion { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public string PreprocessingVersion { get; set; } = string.Empty;
    public CephBenchmarkSplit EvaluationSplit { get; set; }
    public double ClinicalErrorThresholdMm { get; set; }
    public List<CephEvaluationIssueDto> Issues { get; set; } = [];
    public List<CephLandmarkRepeatabilityMetricDto> Landmarks { get; set; } = [];
    public List<CephMeasurementRepeatabilityMetricDto> Measurements { get; set; } = [];
    public List<CephConfidenceCoverageMetricDto> ConfidenceCoverage { get; set; } = [];
}

public sealed class CephLandmarkRepeatabilityMetricDto
{
    public string LandmarkKey { get; set; } = string.Empty;
    public int EligibleCaseCount { get; set; }
    public int CompleteThreeRunCaseCount { get; set; }
    public int MissingStatusConsistentCaseCount { get; set; }
    public double? MissingStatusConsistencyPercent { get; set; }
    public double? MeanPairwiseDisplacementMm { get; set; }
    public double? MedianPairwiseDisplacementMm { get; set; }
    public double? P95PairwiseDisplacementMm { get; set; }
    public double? MaximumPairwiseDisplacementMm { get; set; }
    public double? MeanWithinCaseRadialSdMm { get; set; }
    public double? P95WithinCaseRadialSdMm { get; set; }
}

public sealed class CephMeasurementRepeatabilityMetricDto
{
    public string MeasurementName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public int EligibleCaseCount { get; set; }
    public int CompleteThreeRunCaseCount { get; set; }
    public double? MeanWithinCaseStandardDeviation { get; set; }
    public double? P95WithinCaseStandardDeviation { get; set; }
    public double? AbsoluteAgreementIcc { get; set; }
}

public sealed class CephConfidenceCoverageMetricDto
{
    public double ConfidenceThreshold { get; set; }
    public int EligiblePointRunCount { get; set; }
    public int RetainedPointRunCount { get; set; }
    public double? CoveragePercent { get; set; }
    public double? RetainedMeanRadialErrorMm { get; set; }
    public int AboveClinicalErrorPointRunCount { get; set; }
    public int ReferredAboveClinicalErrorPointRunCount { get; set; }
    public double? AboveClinicalErrorReferralSensitivityPercent { get; set; }
    public int MissingOrRejectedPointRunCount { get; set; }
    public int ReferredMissingOrRejectedPointRunCount { get; set; }
    public double? MissingOrRejectedReferralPercent { get; set; }
}
