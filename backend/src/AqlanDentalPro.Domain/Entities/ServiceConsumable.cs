namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// YOLO-S2: Link table between <see cref="ClinicService"/> and <see cref="Inventory"/>
/// (consumable materials used per session of a service — e.g. composite resin, anesthetic
/// carpule, glove pack). Lets the clinic track "what does performing this service cost
/// in materials" and prepare the chair-side kit. YOLO-S2 keeps this as catalog metadata:
/// it does NOT auto-decrement Inventory quantity on appointment completion (that is a
/// later sprint — and the rules forbid auto-generation here). Notes is free-form Arabic
/// for batch/lot or usage instructions.
/// </summary>
public class ServiceConsumable : BaseEntity
{
    public Guid ClinicServiceId { get; set; }
    public ClinicService? Service { get; set; }

    public Guid InventoryItemId { get; set; }
    public Inventory? InventoryItem { get; set; }

    /// <summary>Quantity consumed per single session of the service. Must be ≥ 1.</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>Optional free-form notes (Arabic) — e.g. "يُستخدم فقط في الحالات المعقدة".</summary>
    public string? Notes { get; set; }
}
