namespace AqlanDentalPro.Application.DTOs.Ortho;

public class OrthoVisitListDto
{
    public Guid Id { get; set; }
    public int VisitNumber { get; set; }
    public string VisitDate { get; set; } = string.Empty;
    public string? VisitType { get; set; }
    public string? CurrentStage { get; set; }
    public string? WireUpper { get; set; }
    public string? WireLower { get; set; }
    public string? ElasticsType { get; set; }
    public decimal? CurrentOverjet { get; set; }
    public decimal? CurrentOverbite { get; set; }
    public string? ClinicalNotes { get; set; }
    public string? NextAppointmentDate { get; set; }
    public string? NextAppointmentType { get; set; }
    public string? DoctorName { get; set; }
}

public class CreateOrthoVisitRequest
{
    public string VisitDate { get; set; } = string.Empty;
    public string? VisitType { get; set; }
    public string? CurrentStage { get; set; }
    public string? WireUpper { get; set; }
    public string? WireLower { get; set; }
    public string? ElasticsType { get; set; }
    public decimal? CurrentOverjet { get; set; }
    public decimal? CurrentOverbite { get; set; }
    public string? ClinicalNotes { get; set; }
    public string? PatientInstructions { get; set; }
    public string? NextAppointmentDate { get; set; }
    public string? NextAppointmentType { get; set; }
    public Guid? DoctorId { get; set; }
}
