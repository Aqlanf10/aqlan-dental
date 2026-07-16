namespace AqlanDentalPro.Domain.Enums;

public enum CephPilotProjectStatus
{
    Draft,
    ReadyForAnnotation,
    AnnotationInProgress,
    AwaitingAdjudication,
    AdjudicationInProgress,
    GoldStandardComplete,
    ReleaseReady,
    Evaluated,
    Archived,
}

public enum CephPilotCaseStatus
{
    Uploaded,
    Ready,
    ReviewerAInProgress,
    ReviewerASubmitted,
    ReviewerBInProgress,
    ReviewerBSubmitted,
    ComparisonReady,
    NeedsAdjudication,
    Adjudicated,
    ReleaseReady,
}

public enum CephPilotSourceType
{
    AccountOwnedWebCephExport,
    ApprovedDeidentifiedUpload,
}

public enum CephPilotSplit
{
    Pilot,
    Validation,
    InternalTest,
    ExternalClinicalTest,
}

public enum CephPilotQualityCategory
{
    Normal,
    LowContrast,
    Blur,
    DoubleContour,
    Metal,
    MissingAnatomy,
    SevereCrop,
    LowResolution,
    Mixed,
}

public enum CephPilotSkeletalCategory
{
    ClassI,
    ClassII,
    ClassIII,
}

public enum CephPilotVerticalCategory
{
    HighAngle,
    AverageAngle,
    LowAngle,
}

public enum CephPilotCalibrationSource
{
    SanitizedDicomPixelSpacing,
    CalibrationRuler,
}

public enum CephPilotExportArtifactType
{
    WebCephLandmarkTable,
    WebCephMeasurementReport,
}

public enum CephPilotMigrationDecision
{
    Pending,
    BenchmarkOnly,
    Rejected,
}

public enum CephPilotReviewerSlot
{
    A,
    B,
}

public enum CephPilotReviewStatus
{
    Draft,
    Submitted,
}

public enum CephPilotLandmarkVisibility
{
    Visible,
    NotVisible,
}

public enum CephPilotContourDecision
{
    NotApplicable,
    SingleContour,
    ReceptorSideContour,
    Unresolvable,
}
