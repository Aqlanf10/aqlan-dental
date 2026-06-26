namespace AqlanDentalPro.Application.DTOs.Appointments;

/// <summary>
/// DTO for a TreatmentPackage row as returned by the API.
/// Used by the settings/packages page and the appointment-form package dropdown.
/// </summary>
public class TreatmentPackageDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal TotalPrice { get; set; }
    public int SessionCount { get; set; } = 1;
    public string? Color { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Request body for creating a new TreatmentPackage.</summary>
public class CreateTreatmentPackageRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal TotalPrice { get; set; }
    public int SessionCount { get; set; } = 1;
    public string? Color { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Request body for updating an existing TreatmentPackage.</summary>
public class UpdateTreatmentPackageRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal TotalPrice { get; set; }
    public int SessionCount { get; set; } = 1;
    public string? Color { get; set; }
    public bool? IsActive { get; set; }
}
