using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

public sealed class CephPilotCase : BaseEntity
{
    public Guid ProjectId { get; set; }
    public int CaseSequence { get; set; }
    public string CaseCode { get; set; } = string.Empty;
    public CephPilotSourceType SourceType { get; set; }
    public string? SourceReference { get; set; }
    public string ImageStorageKey { get; set; } = string.Empty;
    public string ImageSha256 { get; set; } = string.Empty;
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public decimal? MmPerPixel { get; set; }
    public CephPilotCalibrationSource? CalibrationSource { get; set; }
    public string Orientation { get; set; } = "lateral-facing-right";
    public string SiteCode { get; set; } = string.Empty;
    public string DeviceCode { get; set; } = string.Empty;
    public string PatientGroupToken { get; set; } = string.Empty;
    public CephPilotSplit Split { get; set; } = CephPilotSplit.Pilot;
    public CephPilotQualityCategory QualityCategory { get; set; }
    public CephPilotSkeletalCategory? SkeletalCategory { get; set; }
    public CephPilotVerticalCategory? VerticalCategory { get; set; }
    public bool DeIdentificationConfirmed { get; set; }
    public bool PixelInspectionConfirmed { get; set; }
    public bool NoBarcodeOrQrConfirmed { get; set; }
    public bool LegalBasisConfirmed { get; set; }
    public bool MetadataSanitized { get; set; }
    public bool OrientationConfirmed { get; set; }
    public CephPilotCaseStatus Status { get; set; } = CephPilotCaseStatus.Uploaded;
    public CephPilotMigrationDecision MigrationDecision { get; set; } = CephPilotMigrationDecision.Pending;
    public int Revision { get; set; } = 1;

    public CephPilotProject Project { get; set; } = null!;
    public ICollection<CephPilotExportArtifact> ExportArtifacts { get; set; } = [];
    public ICollection<CephPilotReviewSession> ReviewSessions { get; set; } = [];
}
