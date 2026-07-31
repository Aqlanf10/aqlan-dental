namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Security metadata for a file stored under UPLOADS_PATH. The physical filename is
/// never sufficient authorization: every download is evaluated against this record.
/// </summary>
public class UploadedFile : BaseEntity
{
    public string FileName { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long Size { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string Purpose { get; set; } = "general";
    public string? ResourceType { get; set; }
    public Guid? ResourceId { get; set; }
    public Guid? PatientId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid UploadedBy { get; set; }
    public bool IsPublic { get; set; }
}
