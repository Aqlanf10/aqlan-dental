namespace AqlanDentalPro.Application.DTOs.Ortho;

public class OrthoCaseListDto
{
    public Guid Id { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientNumber { get; set; } = string.Empty;
    public string? DoctorName { get; set; }
    public string? DoctorColor { get; set; }
    public string? ApplianceType { get; set; }
    public string? StartDate { get; set; }
    public int? ExpectedDurationMonths { get; set; }
    public string? CurrentStage { get; set; }
    public int StagePercentage { get; set; }
    public string Status { get; set; } = "active";
    public decimal? TotalFee { get; set; }
}

public class OrthoCaseDetailDto : OrthoCaseListDto
{
    public string? ExtractionDecisionValue { get; set; }
    public string? RetentionPlan { get; set; }
    public Guid? DoctorId { get; set; }
    public List<TreatmentStageDto> Stages { get; set; } = [];
    public List<OrthoVisitListDto> RecentVisits { get; set; } = [];
}
