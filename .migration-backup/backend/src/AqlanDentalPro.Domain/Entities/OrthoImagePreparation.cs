namespace AqlanDentalPro.Domain.Entities;

public class OrthoImagePreparation : BaseEntity
{
    public Guid OrthoClinicalPhotoId { get; set; }
    public decimal CropX { get; set; }
    public decimal CropY { get; set; }
    public decimal CropWidth { get; set; } = 1m;
    public decimal CropHeight { get; set; } = 1m;
    public decimal Zoom { get; set; } = 1m;
    public int RotationDegrees { get; set; }
    public int Brightness { get; set; }
    public int Contrast { get; set; }
    public bool FlipHorizontal { get; set; }
    public bool FlipVertical { get; set; }
    public string AspectRatio { get; set; } = "Original";
    public string? Preset { get; set; }
    public string Status { get; set; } = "PreparedForReport";
    public string? PreparedImageUrl { get; set; }
    public DateTime? PreparedAt { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public OrthoClinicalPhoto OrthoClinicalPhoto { get; set; } = null!;
}
