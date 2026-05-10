namespace AqlanDentalPro.Application.DTOs.General;

public class PerioRecordDto
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public int ToothNumber { get; set; }
    public decimal ProbingDepth { get; set; }
    public decimal ClinicalAttachment { get; set; }
    public bool BleedingOnProbing { get; set; }
    public int PlaqueIndex { get; set; }
    public int GingivalIndex { get; set; }
    public int Furcation { get; set; }
    public int Mobility { get; set; }
    public string? Notes { get; set; }
    public string? DoctorName { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}

public class CreatePerioRecordRequest
{
    public Guid PatientId { get; set; }
    public int ToothNumber { get; set; }
    public decimal ProbingDepth { get; set; }
    public decimal ClinicalAttachment { get; set; }
    public bool BleedingOnProbing { get; set; }
    public int PlaqueIndex { get; set; }
    public int GingivalIndex { get; set; }
    public int Furcation { get; set; }
    public int Mobility { get; set; }
    public string? Notes { get; set; }
    public Guid? DoctorId { get; set; }
}
