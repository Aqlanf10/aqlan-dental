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

    /// <summary>
    /// YOLO-S2: Services included in this package. Populated only on GetById
    /// (the list endpoint keeps the payload light). Defaulted to empty so list
    /// consumers don't crash on missing field.
    /// </summary>
    public List<TreatmentPackageServiceDto> Services { get; set; } = [];

    /// <summary>
    /// YOLO-S2: Computed catalog total = sum(service.DefaultPrice * Quantity,
    /// or OverridePrice * Quantity when OverridePrice is set). Returned for
    /// display only — the canonical price on a Contract is still TotalAmount.
    /// </summary>
    public decimal? ComputedTotal { get; set; }
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

// ─── YOLO-S2: Package ↔ Service link DTOs ─────────────────────────────────────

/// <summary>DTO for a single TreatmentPackageService row (package ↔ service link).</summary>
public class TreatmentPackageServiceDto
{
    public Guid Id { get; set; }
    public Guid PackageId { get; set; }
    public Guid ClinicServiceId { get; set; }
    public string ServiceArabicName { get; set; } = string.Empty;
    public string? ServiceEnglishName { get; set; }
    public string? ServiceCode { get; set; }
    public string? ServiceColor { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal? OverridePrice { get; set; }
    /// <summary>Effective unit price = OverridePrice ?? Service.DefaultPrice. Computed on read.</summary>
    public decimal EffectiveUnitPrice { get; set; }
    /// <summary>Effective line total = EffectiveUnitPrice * Quantity. Computed on read.</summary>
    public decimal LineTotal { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Request body for adding a service to a package (or updating an existing link).</summary>
public class UpsertPackageServiceRequest
{
    public Guid ClinicServiceId { get; set; }
    public int Quantity { get; set; } = 1;
    /// <summary>Optional fixed unit price that overrides ClinicService.DefaultPrice.</summary>
    public decimal? OverridePrice { get; set; }
}
