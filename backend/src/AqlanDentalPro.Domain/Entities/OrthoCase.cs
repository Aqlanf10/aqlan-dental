using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

public class OrthoCase : BaseEntity
{
    public string CaseNumber { get; set; } = string.Empty;
    public Guid PatientId { get; set; }
    public Guid? DoctorId { get; set; }
    public Guid? BranchId { get; set; }
    public string? ApplianceType { get; set; }
    public DateOnly? StartDate { get; set; }
    public int? ExpectedDurationMonths { get; set; }
    public string? CurrentStage { get; set; }
    public int StagePercentage { get; set; } = 0;
    /// <summary>M2 FIX: Changed from string to OrthoCaseStatus enum with HasConversion&lt;string&gt; for DB compatibility.</summary>
    public OrthoCaseStatus Status { get; set; } = OrthoCaseStatus.Active;
    public string? ExtractionDecisionValue { get; set; }
    public string? RetentionPlan { get; set; }
    public decimal? TotalFee { get; set; }

    public Patient Patient { get; set; } = null!;
    public Doctor? Doctor { get; set; }
    public Branch? Branch { get; set; }
    public OrthoClinicalExam? ClinicalExam { get; set; }
    public ICollection<ProblemListItem> ProblemList { get; set; } = [];
    public ICollection<TreatmentPlan> TreatmentPlans { get; set; } = [];
    public ICollection<OrthoVisit> Visits { get; set; } = [];
    public ICollection<TreatmentStage> Stages { get; set; } = [];
    public RetentionRecord? RetentionRecord { get; set; }
    public ICollection<CephAnalysis> CephAnalyses { get; set; } = [];
    public ICollection<ModelAnalysis> ModelAnalyses { get; set; } = [];
    public ExtractionDecision? ExtractionDecision { get; set; }
    public ICollection<LabOrder> LabOrders { get; set; } = [];
    public ICollection<ClinicalPhoto> Photos { get; set; } = [];
    public OrthoDiagnosis? Diagnosis { get; set; }
    public ICollection<OrthoClinicalPhoto> OrthoClinicalPhotos { get; set; } = [];
}
