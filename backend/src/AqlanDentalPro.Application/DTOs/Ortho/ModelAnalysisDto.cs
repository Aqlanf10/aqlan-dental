namespace AqlanDentalPro.Application.DTOs.Ortho;

public class ModelAnalysisDto
{
    public Guid Id { get; set; }
    public Guid OrthoCaseId { get; set; }
    public string? AnalysisDate { get; set; }
    public decimal? BoltonOverall { get; set; }
    public decimal? BoltonAnterior { get; set; }
    public decimal? UpperSum12 { get; set; }
    public decimal? LowerSum12 { get; set; }
    public decimal? UpperArchLength { get; set; }
    public decimal? LowerArchLength { get; set; }
    public decimal? UpperAld { get; set; }
    public decimal? LowerAld { get; set; }
    public decimal? PontIndex { get; set; }
    public string? Notes { get; set; }
}

public class SaveModelAnalysisRequest
{
    public string? AnalysisDate { get; set; }
    public decimal? BoltonOverall { get; set; }
    public decimal? BoltonAnterior { get; set; }
    public decimal? UpperSum12 { get; set; }
    public decimal? LowerSum12 { get; set; }
    public decimal? UpperArchLength { get; set; }
    public decimal? LowerArchLength { get; set; }
    public decimal? UpperAld { get; set; }
    public decimal? LowerAld { get; set; }
    public decimal? PontIndex { get; set; }
    public string? Notes { get; set; }
}

public class BoltonCalculationRequest
{
    /// <summary>Mesiodistal widths of upper teeth (11-17, 21-27) in mm</summary>
    public List<decimal> UpperWidths { get; set; } = [];
    /// <summary>Mesiodistal widths of lower teeth (31-37, 41-47) in mm</summary>
    public List<decimal> LowerWidths { get; set; } = [];
    /// <summary>Mesiodistal widths of upper anterior teeth (13-23) in mm</summary>
    public List<decimal> UpperAnteriorWidths { get; set; } = [];
    /// <summary>Mesiodistal widths of lower anterior teeth (33-43) in mm</summary>
    public List<decimal> LowerAnteriorWidths { get; set; } = [];
}

public class BoltonResult
{
    /// <summary>Overall Bolton ratio: lower sum / upper sum × 100</summary>
    public decimal OverallRatio { get; set; }
    /// <summary>Normal overall ratio: 91.3%</summary>
    public decimal NormalOverall { get; set; } = 91.3m;
    /// <summary>Overall discrepancy in mm (positive = mandibular excess)</summary>
    public decimal OverallDiscrepancy { get; set; }
    /// <summary>Anterior Bolton ratio: lower anterior / upper anterior × 100</summary>
    public decimal? AnteriorRatio { get; set; }
    /// <summary>Normal anterior ratio: 77.2%</summary>
    public decimal NormalAnterior { get; set; } = 77.2m;
    /// <summary>Anterior discrepancy in mm</summary>
    public decimal? AnteriorDiscrepancy { get; set; }
    /// <summary>Arabic interpretation</summary>
    public string InterpretationAr { get; set; } = "";
}
