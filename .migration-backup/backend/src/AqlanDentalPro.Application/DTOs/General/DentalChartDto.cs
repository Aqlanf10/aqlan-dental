namespace AqlanDentalPro.Application.DTOs.General;

public class DentalChartDto
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public string ChartDate { get; set; } = string.Empty;
    public string? DoctorName { get; set; }
    public List<ToothConditionDto> Teeth { get; set; } = [];
}

public class ToothConditionDto
{
    public Guid Id { get; set; }
    public string ToothNumber { get; set; } = string.Empty;
    public string? Condition { get; set; }
    public string? SurfacesAffected { get; set; }
    public string? TreatmentDone { get; set; }
    public string? Notes { get; set; }
}

public class UpdateToothRequest
{
    public string ToothNumber { get; set; } = string.Empty;
    public string? Condition { get; set; }
    public string? SurfacesAffected { get; set; }
    public string? TreatmentDone { get; set; }
    public string? Notes { get; set; }
}
