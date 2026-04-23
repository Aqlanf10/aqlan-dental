namespace AqlanDentalPro.Domain.Entities;

public class OrthoVisit : BaseEntity
{
    public Guid OrthoCaseId { get; set; }
    public int VisitNumber { get; set; }
    public DateOnly VisitDate { get; set; }
    public string? VisitType { get; set; }
    public string? CurrentStage { get; set; }
    public string? WireUpper { get; set; }
    public string? WireLower { get; set; }
    public string? ElasticsType { get; set; }
    public decimal? CurrentOverjet { get; set; }
    public decimal? CurrentOverbite { get; set; }
    public string? MidlineNotes { get; set; }
    public string? ClinicalNotes { get; set; }
    public string? PatientInstructions { get; set; }
    public DateOnly? NextAppointmentDate { get; set; }
    public string? NextAppointmentType { get; set; }
    public Guid? DoctorId { get; set; }

    public OrthoCase OrthoCase { get; set; } = null!;
    public Doctor? Doctor { get; set; }
}
