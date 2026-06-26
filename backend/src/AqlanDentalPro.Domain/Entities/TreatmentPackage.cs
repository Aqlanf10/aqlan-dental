namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// باقة علاج — a saleable bundle of N sessions at a fixed price (e.g.
/// "باقة تبييض كاملة" = 3 sessions). Linked to Appointments via PackageId
/// so the calendar can color-tag and the reception can pre-fill the
/// appointment type from the package name. Sprint: YOLO-S1.
/// Simple catalog only — installments / contract bindings are Sprint 2.
/// </summary>
public class TreatmentPackage : BaseEntity
{
    /// <summary>Arabic display name (e.g. "باقة تبييض كاملة").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional longer Arabic description.</summary>
    public string? Description { get; set; }

    /// <summary>Fixed total price for the whole package (YER unless multi-currency adds a column later).</summary>
    public decimal TotalPrice { get; set; }

    /// <summary>Number of sessions included in the package (default 1).</summary>
    public int SessionCount { get; set; } = 1;

    /// <summary>Hex color (e.g. "#8b5cf6") used for calendar display. Optional.</summary>
    public string? Color { get; set; }

    /// <summary>Soft-active flag (true = available for new appointments).</summary>
    public bool IsActive { get; set; } = true;
}
