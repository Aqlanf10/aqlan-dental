namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Configurable cephalometric norm (normal value ± SD) per measurement per
/// analysis group (steiner/tweed/mcnamara/ricketts/downs/wits).
/// Seeded with international standard values; editable by admins so the
/// clinic can adopt population-specific norms. CephService falls back to its
/// built-in defaults when a row is missing — computation never breaks.
/// </summary>
public class CephNorm : BaseEntity
{
    /// <summary>Canonical measurement key, e.g. "SNA", "U1-NA_angle", "Wits".</summary>
    public string MeasurementName { get; set; } = string.Empty;

    /// <summary>Arabic display name, e.g. "زاوية SNA".</summary>
    public string? NameAr { get; set; }

    /// <summary>Analysis group: steiner | tweed | mcnamara | ricketts | downs | wits.</summary>
    public string AnalysisGroup { get; set; } = string.Empty;

    public decimal NormalValue { get; set; }
    public decimal StdDeviation { get; set; }

    /// <summary>Optional explicit normal range; overrides ±1SD when present.</summary>
    public decimal? MinNormal { get; set; }
    public decimal? MaxNormal { get; set; }

    /// <summary>"°" or "mm".</summary>
    public string Unit { get; set; } = "°";

    /// <summary>Skeletal | Dental | SoftTissue | Vertical | Sagittal.</summary>
    public string? Category { get; set; }

    /// <summary>Arabic interpretation when the value is below the normal range.</summary>
    public string? InterpretationBelow { get; set; }

    /// <summary>Arabic interpretation when the value is within the normal range.</summary>
    public string? InterpretationNormal { get; set; }

    /// <summary>Arabic interpretation when the value is above the normal range.</summary>
    public string? InterpretationAbove { get; set; }

    public int SortOrder { get; set; }
}
