namespace AqlanDentalPro.Domain.Entities;

public class RolePermission : BaseEntity
{
    public string Role { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public bool CanView { get; set; } = false;
    public bool CanCreate { get; set; } = false;
    public bool CanEdit { get; set; } = false;
    public bool CanDelete { get; set; } = false;
    public bool CanExport { get; set; } = false;
    public bool CanApprove { get; set; } = false;
}
