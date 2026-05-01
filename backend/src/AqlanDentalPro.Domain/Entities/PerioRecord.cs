namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Per-tooth periodontal charting record.
/// </summary>
public class PerioRecord : BaseEntity
{
    public Guid PatientId { get; set; }
    public int ToothNumber { get; set; }
    public decimal ProbingDepth { get; set; }       // mm
    public decimal ClinicalAttachment { get; set; }  // mm
    public bool BleedingOnProbing { get; set; }
    public int PlaqueIndex { get; set; }    // 0-3
    public int GingivalIndex { get; set; }  // 0-3
    public int Furcation { get; set; }      // 0-3
    public int Mobility { get; set; }       // 0-3
    public string? Notes { get; set; }
    public Guid? DoctorId { get; set; }

    public Patient Patient { get; set; } = null!;
    public Doctor? Doctor { get; set; }
}
