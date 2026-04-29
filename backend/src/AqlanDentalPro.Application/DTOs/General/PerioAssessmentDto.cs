namespace AqlanDentalPro.Application.DTOs.General;

/// <summary>
/// Helper for pocket depth sites per tooth (6 sites: MB, B, DB, ML, L, DL)
/// </summary>
public class PerioSiteData
{
    public int ToothNumber { get; set; }
    /// <summary>Mesiobuccal probing depth (mm)</summary>
    public decimal? MB { get; set; }
    /// <summary>Buccal probing depth (mm)</summary>
    public decimal? B { get; set; }
    /// <summary>Distobuccal probing depth (mm)</summary>
    public decimal? DB { get; set; }
    /// <summary>Mesiolingual probing depth (mm)</summary>
    public decimal? ML { get; set; }
    /// <summary>Lingual probing depth (mm)</summary>
    public decimal? L { get; set; }
    /// <summary>Distolingual probing depth (mm)</summary>
    public decimal? DL { get; set; }
}

public class PerioAssessmentDto
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public string? AssessmentDate { get; set; }
    public decimal? AvgPocketDepth { get; set; }
    public int? BleedingPoints { get; set; }
    public string? RecessionLevel { get; set; }
    public string? PerioStage { get; set; }
    public string? Recommendation { get; set; }
    public string? DoctorName { get; set; }
    public Guid? DoctorId { get; set; }

    // Detailed periodontal data
    public object? PocketDepths { get; set; }
    public object? BleedingOnProbing { get; set; }
    public object? Mobility { get; set; }
    public object? FurcationInvolvement { get; set; }
    public decimal? PlaqueIndex { get; set; }
    public decimal? GingivalIndex { get; set; }
    public decimal? AttachmentLoss { get; set; }
    public string? BoneLossPattern { get; set; }
    public string? TreatmentPlan { get; set; }

    public string CreatedAt { get; set; } = string.Empty;
}

public class CreatePerioAssessmentRequest
{
    public Guid PatientId { get; set; }
    public string? AssessmentDate { get; set; }
    public decimal? AvgPocketDepth { get; set; }
    public int? BleedingPoints { get; set; }
    public string? RecessionLevel { get; set; }
    public string? PerioStage { get; set; }
    public string? Recommendation { get; set; }
    public Guid? DoctorId { get; set; }

    // Detailed periodontal data
    public List<PerioSiteData>? PocketDepths { get; set; }
    public object? BleedingOnProbing { get; set; }
    public object? Mobility { get; set; }
    public object? FurcationInvolvement { get; set; }
    public decimal? PlaqueIndex { get; set; }
    public decimal? GingivalIndex { get; set; }
    public decimal? AttachmentLoss { get; set; }
    public string? BoneLossPattern { get; set; }
    public string? TreatmentPlan { get; set; }
}

public class UpdatePerioAssessmentRequest
{
    public string? AssessmentDate { get; set; }
    public decimal? AvgPocketDepth { get; set; }
    public int? BleedingPoints { get; set; }
    public string? RecessionLevel { get; set; }
    public string? PerioStage { get; set; }
    public string? Recommendation { get; set; }
    public Guid? DoctorId { get; set; }

    // Detailed periodontal data
    public List<PerioSiteData>? PocketDepths { get; set; }
    public object? BleedingOnProbing { get; set; }
    public object? Mobility { get; set; }
    public object? FurcationInvolvement { get; set; }
    public decimal? PlaqueIndex { get; set; }
    public decimal? GingivalIndex { get; set; }
    public decimal? AttachmentLoss { get; set; }
    public string? BoneLossPattern { get; set; }
    public string? TreatmentPlan { get; set; }
}
