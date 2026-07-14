using System.Text.Json.Serialization;
using AqlanDentalPro.Application.Serialization;

namespace AqlanDentalPro.Application.DTOs.Ceph;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CephBenchmarkManifestDto
{
    public string SchemaVersion { get; set; } = string.Empty;
    public string DatasetVersion { get; set; } = string.Empty;
    public string LandmarkDefinitionVersion { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public List<CephBenchmarkCaseDto> Cases { get; set; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CephBenchmarkCaseDto
{
    public Guid ImageId { get; set; }
    public string PatientGroupId { get; set; } = string.Empty;
    public string ImageSha256 { get; set; } = string.Empty;
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public double MillimetresPerPixel { get; set; }
    public string SourceSiteCode { get; set; } = string.Empty;
    public string DeviceCode { get; set; } = string.Empty;
    public CephBenchmarkSplit Split { get; set; }
    public CephImageOrientation Orientation { get; set; }
    public CephAgeBand AgeBand { get; set; }
    public CephSkeletalClass SkeletalClass { get; set; }
    public CephAnglePattern AnglePattern { get; set; }
    public HashSet<CephImageQualityFlag> QualityFlags { get; set; } = [];
    public CephDeidentificationEvidenceDto Deidentification { get; set; } = new();
    public List<CephReviewerAnnotationDto> Annotations { get; set; } = [];
    public List<CephGoldStandardPointDto> GoldStandard { get; set; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CephDeidentificationEvidenceDto
{
    public string MetadataProfileVersion { get; set; } = string.Empty;
    public DateTimeOffset SanitizedAt { get; set; }
    public string StewardAlias { get; set; } = string.Empty;
    public string DataUseApprovalId { get; set; } = string.Empty;
    public bool MetadataSanitized { get; set; }
    public bool PrivateTagsRemoved { get; set; }
    public CephPixelInspectionStatus PixelInspectionStatus { get; set; }
    public bool BurnedInIdentifierDetected { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CephReviewerAnnotationDto
{
    public string LandmarkKey { get; set; } = string.Empty;
    public string ReviewerAlias { get; set; } = string.Empty;
    public CephLandmarkVisibility Visibility { get; set; }
    public double? X { get; set; }
    public double? Y { get; set; }
    public CephDoubleContourDecision DoubleContourDecision { get; set; }
    public DateTimeOffset AnnotatedAt { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CephGoldStandardPointDto
{
    public string LandmarkKey { get; set; } = string.Empty;
    public CephLandmarkVisibility Visibility { get; set; }
    public double? X { get; set; }
    public double? Y { get; set; }
    public CephGoldStandardMethod Method { get; set; }
    public HashSet<CephGoldStandardDecisionCode> DecisionCodes { get; set; } = [];
    public string ApprovedByAlias { get; set; } = string.Empty;
    public DateTimeOffset ApprovedAt { get; set; }
}

public sealed class CephBenchmarkValidationResultDto
{
    public bool IsStructurallyValid { get; set; }
    public bool IsReleaseReady { get; set; }
    public int CaseCount { get; set; }
    public int GoldPointCount { get; set; }
    public List<CephBenchmarkValidationIssueDto> Issues { get; set; } = [];
    public List<CephAdjudicationQueueItemDto> AdjudicationQueue { get; set; } = [];
}

public sealed class CephBenchmarkValidationIssueDto
{
    public CephBenchmarkIssueSeverity Severity { get; set; }
    public CephBenchmarkIssueCategory Category { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

public sealed class CephAdjudicationQueueItemDto
{
    public Guid ImageId { get; set; }
    public string LandmarkKey { get; set; } = string.Empty;
    public int ReviewerCount { get; set; }
    public double? MaximumDisagreementMm { get; set; }
    public bool VisibilityConflict { get; set; }
    public bool DoubleContourConflict { get; set; }
    public bool RequiresAdjudication { get; set; }
    public bool HasGoldStandard { get; set; }
}

[JsonConverter(typeof(StrictJsonStringEnumConverter<CephBenchmarkSplit>))]
public enum CephBenchmarkSplit
{
    Unspecified,
    Training,
    Validation,
    InternalTest,
    ExternalClinicalTest,
}

[JsonConverter(typeof(StrictJsonStringEnumConverter<CephImageOrientation>))]
public enum CephImageOrientation
{
    Unknown,
    RightFacing,
    LeftFacing,
}

[JsonConverter(typeof(StrictJsonStringEnumConverter<CephAgeBand>))]
public enum CephAgeBand
{
    Unknown,
    Pediatric,
    Adult,
}

[JsonConverter(typeof(StrictJsonStringEnumConverter<CephSkeletalClass>))]
public enum CephSkeletalClass
{
    Unknown,
    ClassI,
    ClassII,
    ClassIII,
}

[JsonConverter(typeof(StrictJsonStringEnumConverter<CephAnglePattern>))]
public enum CephAnglePattern
{
    Unknown,
    Low,
    Average,
    High,
}

[JsonConverter(typeof(StrictJsonStringEnumConverter<CephImageQualityFlag>))]
public enum CephImageQualityFlag
{
    Normal,
    LowContrast,
    Blurred,
    DoubleContour,
    MetallicArtifact,
    MissingAnatomy,
    SevereCrop,
    LowResolution,
}

[JsonConverter(typeof(StrictJsonStringEnumConverter<CephPixelInspectionStatus>))]
public enum CephPixelInspectionStatus
{
    Pending,
    Passed,
    Failed,
}

[JsonConverter(typeof(StrictJsonStringEnumConverter<CephLandmarkVisibility>))]
public enum CephLandmarkVisibility
{
    Visible,
    NotVisible,
    Uncertain,
}

[JsonConverter(typeof(StrictJsonStringEnumConverter<CephDoubleContourDecision>))]
public enum CephDoubleContourDecision
{
    NotApplicable,
    ReceptorSide,
    ExplicitProtocolSide,
    Unresolved,
}

[JsonConverter(typeof(StrictJsonStringEnumConverter<CephGoldStandardMethod>))]
public enum CephGoldStandardMethod
{
    ConsensusWithinThreshold,
    ThirdReviewer,
    ConsensusPanel,
}

[JsonConverter(typeof(StrictJsonStringEnumConverter<CephGoldStandardDecisionCode>))]
public enum CephGoldStandardDecisionCode
{
    CoordinateAgreement,
    VisibilityResolved,
    DoubleContourResolved,
    CoordinateDisagreementResolved,
    AnatomyNotVisible,
}

[JsonConverter(typeof(StrictJsonStringEnumConverter<CephBenchmarkIssueSeverity>))]
public enum CephBenchmarkIssueSeverity
{
    Warning,
    Blocker,
    Error,
}

[JsonConverter(typeof(StrictJsonStringEnumConverter<CephBenchmarkIssueCategory>))]
public enum CephBenchmarkIssueCategory
{
    Structure,
    Privacy,
    Leakage,
    Annotation,
    Adjudication,
}
