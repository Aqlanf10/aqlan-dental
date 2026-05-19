using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

public class SurgeryCase : BaseEntity
{
    public string CaseNumber { get; set; } = string.Empty;
    public Guid PatientId { get; set; }
    public Guid? DoctorId { get; set; }
    public string SurgeryType { get; set; } = string.Empty;
    public string? TeethInvolved { get; set; }
    /// <summary>M2 FIX: Changed from string to SurgeryCaseStatus enum with HasConversion&lt;string&gt; for DB compatibility.</summary>
    public SurgeryCaseStatus Status { get; set; } = SurgeryCaseStatus.Scheduled;

    public Patient Patient { get; set; } = null!;
    public Doctor? Doctor { get; set; }
    public PreopReport? PreopReport { get; set; }
    public OperativeReport? OperativeReport { get; set; }
    public PostopRecord? PostopRecord { get; set; }
    public ICollection<HospitalReferral> HospitalReferrals { get; set; } = [];
}
