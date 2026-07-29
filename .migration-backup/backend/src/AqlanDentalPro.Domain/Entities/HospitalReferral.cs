namespace AqlanDentalPro.Domain.Entities;

public class HospitalReferral : BaseEntity
{
    public Guid SurgeryCaseId { get; set; }
    public string? HospitalName { get; set; }
    public string? Reason { get; set; }
    public DateOnly? ReferralDate { get; set; }
    public string Status { get; set; } = "pending";
    public string? Notes { get; set; }

    public SurgeryCase SurgeryCase { get; set; } = null!;
}
