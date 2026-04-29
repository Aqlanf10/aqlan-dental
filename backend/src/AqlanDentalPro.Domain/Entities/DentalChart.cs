namespace AqlanDentalPro.Domain.Entities;

public class DentalChart : BaseEntity
{
    public Guid PatientId { get; set; }
    public DateOnly ChartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public Guid? DoctorId { get; set; }

    public Patient Patient { get; set; } = null!;
    public Doctor? Doctor { get; set; }
    public ICollection<ToothCondition> ToothConditions { get; set; } = [];
}
