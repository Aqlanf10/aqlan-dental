namespace AqlanDentalPro.Application.DTOs.Ortho;

public class CreateOrthoCaseRequest
{
    public Guid PatientId { get; set; }
    public Guid? DoctorId { get; set; }
    public string? ApplianceType { get; set; }
    public string? StartDate { get; set; }
    public int? ExpectedDurationMonths { get; set; }
    public decimal? TotalFee { get; set; }
    public string? Notes { get; set; }
}
