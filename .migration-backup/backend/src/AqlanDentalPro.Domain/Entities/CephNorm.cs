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

    /// <summary>
    /// CLIN-10 — Lower bound (inclusive) of the patient-age band this norm
    /// applies to, in whole years. Null = no lower bound (matches any age
    /// from 0 up to <see cref="AgeMax"/>). Used together with
    /// <see cref="AgeMax"/> and <see cref="Sex"/> to stratify norms by
    /// patient age and gender so a 10-year-old is never compared against an
    /// adult norm. A row with all three null is "un-stratified" and acts as
    /// a backward-compatible fallback for any patient.
    /// </summary>
    public int? AgeMin { get; set; }

    /// <summary>
    /// CLIN-10 — Upper bound (inclusive) of the patient-age band this norm
    /// applies to, in whole years. Null = no upper bound (matches any age
    /// from <see cref="AgeMin"/> upward).
    /// </summary>
    public int? AgeMax { get; set; }

    /// <summary>
    /// CLIN-10 — Sex this norm applies to: "M" or "F". Null = applies to
    /// both sexes. When a patient's sex is known, the lookup prefers a
    /// sex-specific row over a sex-null row for the same age band.
    /// </summary>
    public string? Sex { get; set; }
}
