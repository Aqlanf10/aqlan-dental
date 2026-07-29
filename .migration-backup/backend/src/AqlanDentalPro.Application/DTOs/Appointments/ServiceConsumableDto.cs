namespace AqlanDentalPro.Application.DTOs.Appointments;

/// <summary>
/// YOLO-S2: DTO for a ServiceConsumable row (service ↔ inventory item with quantity +
/// optional notes). Returned by the ServiceConsumablesController for the settings/services
/// consumables-management UI.
/// </summary>
public class ServiceConsumableDto
{
    public Guid Id { get; set; }
    public Guid ClinicServiceId { get; set; }
    public Guid InventoryItemId { get; set; }
    public string InventoryItemName { get; set; } = string.Empty;
    public string? InventoryItemUnit { get; set; }
    public int Quantity { get; set; } = 1;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Request body for creating a new ServiceConsumable.</summary>
public class CreateServiceConsumableRequest
{
    public Guid ClinicServiceId { get; set; }
    public Guid InventoryItemId { get; set; }
    public int Quantity { get; set; } = 1;
    public string? Notes { get; set; }
}
