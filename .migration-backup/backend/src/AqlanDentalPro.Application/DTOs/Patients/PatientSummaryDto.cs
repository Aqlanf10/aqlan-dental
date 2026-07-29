namespace AqlanDentalPro.Application.DTOs.Patients;

public sealed class PatientSummaryDto
{
    public int TotalAppointments { get; set; }
    public int CompletedAppointments { get; set; }
    public int ActiveOrthoCases { get; set; }
    public decimal? TotalPaid { get; set; }
    public decimal? TotalOutstanding { get; set; }
    public decimal? UnbilledVisitsAmount { get; set; }
    public int PrescriptionsCount { get; set; }

    public string? LastVisitDate { get; set; }
    public string? LastVisitDoctor { get; set; }
    public string? LastVisitDiagnosis { get; set; }
    public string? NextAppointmentDate { get; set; }
    public string? NextAppointmentTime { get; set; }
    public string? NextAppointmentType { get; set; }
    public string? NextAppointmentDoctor { get; set; }
    public string? ChiefComplaint { get; set; }
    public string? CurrentDiagnosis { get; set; }
    public string? NextPlannedStep { get; set; }
    public List<PatientOrthoSummaryDto> ActiveOrthoSummary { get; set; } = [];
    public List<PatientSurgerySummaryDto> ActiveSurgerySummary { get; set; } = [];
    public List<string> MedicalAlerts { get; set; } = [];
}

public sealed class PatientOrthoSummaryDto
{
    public string CaseNumber { get; set; } = string.Empty;
    public string? ApplianceType { get; set; }
    public int StagePercentage { get; set; }
}

public sealed class PatientSurgerySummaryDto
{
    public string CaseNumber { get; set; } = string.Empty;
    public string SurgeryType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
