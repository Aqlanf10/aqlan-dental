namespace AqlanDentalPro.Application.DTOs.Ceph;

/// <summary>Create/replace a saved facial photo analysis on a case.</summary>
public class SavePhotoAnalysisRequest
{
    public Guid OrthoCaseId { get; set; }
    public string ViewType { get; set; } = "profile"; // profile | frontal
    public string ImageFileUrl { get; set; } = string.Empty;
    /// <summary>JSON map of placed landmarks.</summary>
    public string? LandmarksJson { get; set; }
    /// <summary>JSON array of computed measurements.</summary>
    public string? MeasurementsJson { get; set; }
    public string? Notes { get; set; }
}

public class PhotoAnalysisListItemDto
{
    public Guid Id { get; set; }
    public Guid OrthoCaseId { get; set; }
    public string ViewType { get; set; } = string.Empty;
    public string ImageFileUrl { get; set; } = string.Empty;
    public string AnalysisDate { get; set; } = string.Empty;
}

public class PhotoAnalysisDetailDto
{
    public Guid Id { get; set; }
    public Guid OrthoCaseId { get; set; }
    public string ViewType { get; set; } = string.Empty;
    public string ImageFileUrl { get; set; } = string.Empty;
    public string? LandmarksJson { get; set; }
    public string? MeasurementsJson { get; set; }
    public string? Notes { get; set; }
    public string AnalysisDate { get; set; } = string.Empty;
}
