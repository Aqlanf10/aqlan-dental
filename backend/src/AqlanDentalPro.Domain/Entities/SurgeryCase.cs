namespace AqlanDentalPro.Domain.Entities;

public class SurgeryCase : BaseEntity
{
    public string CaseNumber { get; set; } = string.Empty;
    public Guid PatientId { get; set; }
    public Guid? DoctorId { get; set; }
    public string SurgeryType { get; set; } = string.Empty;
    public string? TeethInvolved { get; set; }
    public string Status { get; set; } = "scheduled";

    public Patient Patient { get; set; } = null!;
    public Doctor? Doctor { get; set; }
    public PreopReport? PreopReport { get; set; }
    public OperativeReport? OperativeReport { get; set; }
    public PostopRecord? PostopRecord { get; set; }
    public ICollection<HospitalReferral> HospitalReferrals { get; set; } = [];
}
