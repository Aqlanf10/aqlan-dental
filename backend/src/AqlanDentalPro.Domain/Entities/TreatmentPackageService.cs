namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// YOLO-S2: Link table between <see cref="TreatmentPackage"/> and <see cref="ClinicService"/>.
/// A package can bundle N services, each with its own quantity (e.g. 3× whitening
/// sessions) and an optional override price that wins over the service's DefaultPrice
/// when computing the package total. Quantity defaults to 1 — most packages are
/// one-of-each. OverridePrice is nullable so the catalog can fall back to the
/// service default. Both FKs cascade-restrict: deleting a package or a service
/// must not silently drop historical contract/appointment references that depend
/// on the package shape. YOLO-S2 keeps this as catalog metadata only — pricing
/// is computed on read, no contract auto-generation, no commission change.
/// </summary>
public class TreatmentPackageService : BaseEntity
{
    public Guid PackageId { get; set; }
    public TreatmentPackage? Package { get; set; }

    public Guid ClinicServiceId { get; set; }
    public ClinicService? Service { get; set; }

    /// <summary>How many units of this service are included in the package. Defaults to 1.</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Optional fixed unit price that overrides ClinicService.DefaultPrice for THIS package.
    /// Null = use the service's default price (price follows the service catalog).
    /// </summary>
    public decimal? OverridePrice { get; set; }
}
