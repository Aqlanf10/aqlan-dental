using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Infrastructure.Data.Seed;

/// <summary>
/// Factory cephalometric norms (international standard values from published
/// orthodontic literature — Steiner, Tweed, McNamara, Ricketts, Downs, Wits).
/// Single source of truth shared by:
/// - startup seeding (StartupDatabaseMaintenance — only inserts when table empty),
/// - POST /api/ceph-norms/reset-defaults (re-seeds factory values),
/// - CephService fallback values mirror these (norms must stay in sync).
/// </summary>
public static class CephNormSeeder
{
    /// <summary>
    /// Idempotent: inserts factory defaults only when the CephNorms table is empty.
    /// Returns the number of rows inserted (0 when already seeded).
    /// </summary>
    public static async Task<int> SeedIfEmptyAsync(AppDbContext db, CancellationToken ct = default)
    {
        // IgnoreQueryFilters: soft-deleted rows still count as "seeded" so we
        // never duplicate rows for a clinic that deactivated some norms.
        if (await db.CephNorms.IgnoreQueryFilters().AnyAsync(ct)) return 0;

        var defaults = GetFactoryDefaults();
        db.CephNorms.AddRange(defaults);
        await db.SaveChangesAsync(ct);
        return defaults.Count;
    }

    /// <summary>
    /// Restores every norm row to factory values: existing rows (matched by
    /// MeasurementName + AnalysisGroup, including soft-deleted ones) are
    /// overwritten and re-activated; missing rows are inserted.
    /// </summary>
    public static async Task<int> ResetToFactoryDefaultsAsync(AppDbContext db, CancellationToken ct = default)
    {
        var existing = await db.CephNorms.IgnoreQueryFilters().ToListAsync(ct);
        var byKey = existing
            .GroupBy(n => (n.MeasurementName, n.AnalysisGroup))
            .ToDictionary(g => g.Key, g => g.First());

        var defaults = GetFactoryDefaults();
        foreach (var d in defaults)
        {
            if (byKey.TryGetValue((d.MeasurementName, d.AnalysisGroup), out var row))
            {
                row.NameAr               = d.NameAr;
                row.NormalValue          = d.NormalValue;
                row.StdDeviation         = d.StdDeviation;
                row.MinNormal            = d.MinNormal;
                row.MaxNormal            = d.MaxNormal;
                row.Unit                 = d.Unit;
                row.Category             = d.Category;
                row.InterpretationBelow  = d.InterpretationBelow;
                row.InterpretationNormal = d.InterpretationNormal;
                row.InterpretationAbove  = d.InterpretationAbove;
                row.SortOrder            = d.SortOrder;
                row.IsActive             = true;
                row.DeletedAt            = null;
                row.DeletedBy            = null;
            }
            else
            {
                db.CephNorms.Add(d);
            }
        }

        await db.SaveChangesAsync(ct);
        return defaults.Count;
    }

    private const string WithinNormalAr = "ضمن الحدود الطبيعية";

    /// <summary>
    /// The authoritative factory list — values match the hardcoded fallbacks in
    /// CephService.ComputeMeasurementsAsync and frontend cephMath.ts norm tables.
    /// </summary>
    public static List<CephNorm> GetFactoryDefaults() =>
    [
        // ── Steiner ─────────────────────────────────────────────────────────
        Norm("SNA", "زاوية SNA", "steiner", 82, 2, "°", "Skeletal", 10,
            below: "رجوع الفك العلوي", above: "بروز الفك العلوي"),
        Norm("SNB", "زاوية SNB", "steiner", 80, 2, "°", "Skeletal", 20,
            below: "رجوع الفك السفلي", above: "بروز الفك السفلي"),
        Norm("ANB", "زاوية ANB", "steiner", 2, 1, "°", "Sagittal", 30,
            below: "ميل لصنف ثالث هيكلي", above: "ميل لصنف ثانٍ هيكلي"),
        Norm("SND", "زاوية SND", "steiner", 76, 2, "°", "Skeletal", 40),
        Norm("U1-NA_angle", "U1/NA (°)", "steiner", 22, 2, "°", "Dental", 50,
            below: "ميلان حنكي", above: "بروز قواطع علوية"),
        Norm("U1-NA_mm", "U1/NA (mm)", "steiner", 4, 2, "mm", "Dental", 60,
            below: "ميلان حنكي", above: "بروز قواطع علوية"),
        Norm("L1-NB_angle", "L1/NB (°)", "steiner", 25, 2, "°", "Dental", 70,
            above: "بروز قواطع سفلية"),
        Norm("L1-NB_mm", "L1/NB (mm)", "steiner", 4, 2, "mm", "Dental", 80,
            above: "بروز قواطع سفلية"),
        Norm("U1-L1", "زاوية القاطعين", "steiner", 131, 6, "°", "Dental", 90),
        Norm("GoGn-SN", "GoGn / SN", "steiner", 32, 6, "°", "Vertical", 100,
            below: "نمط نمو أفقي", above: "نمط نمو عمودي"),
        Norm("UL-SLine", "الشفة العلوية — خط S", "steiner", 0, 1, "mm", "SoftTissue", 110),
        Norm("LL-SLine", "الشفة السفلية — خط S", "steiner", 0, 1, "mm", "SoftTissue", 120),

        // ── Tweed ───────────────────────────────────────────────────────────
        Norm("FMA", "FMA (فرانكفورت-فك سفلي)", "tweed", 25, 4, "°", "Vertical", 10,
            below: "نمط نمو أفقي", above: "نمط نمو عمودي"),
        Norm("FMIA", "FMIA (فرانكفورت-قاطعة سفلية)", "tweed", 65, 5, "°", "Dental", 20),
        Norm("IMPA", "IMPA (فك سفلي-قاطعة سفلية)", "tweed", 90, 4, "°", "Dental", 30),

        // ── McNamara ────────────────────────────────────────────────────────
        Norm("Co-A", "Co-A (طول الفك العلوي)", "mcnamara", 91, 6, "mm", "Skeletal", 10),
        Norm("Co-Gn", "Co-Gn (طول الفك السفلي)", "mcnamara", 120, 7, "mm", "Skeletal", 20),
        Norm("ANS-Me", "ANS-Me (ارتفاع الوجه السفلي)", "mcnamara", 65, 5, "mm", "Vertical", 30),

        // ── Ricketts ────────────────────────────────────────────────────────
        Norm("Facial-Depth", "عمق الوجه (FH-N-Pog)", "ricketts", 87, 3, "°", "Skeletal", 10),
        Norm("Facial-Axis", "محور الوجه (Pt-Gn / BaN)", "ricketts", 90, 3, "°", "Vertical", 20),
        Norm("Mandibular-Plane", "ميل مستوى الفك (FH-GoMe)", "ricketts", 26, 4, "°", "Vertical", 30),
        Norm("Convexity-A", "انحناء النقطة A (N-Pog)", "ricketts", 2, 2, "mm", "Skeletal", 40),
        Norm("L1-APog_mm", "L1 إلى خط A-Pog (mm)", "ricketts", 1, 2, "mm", "Dental", 50),
        Norm("L1-APog_angle", "L1 إلى خط A-Pog (°)", "ricketts", 22, 4, "°", "Dental", 60),
        Norm("Upper-Lip-ELine", "الشفة العلوية إلى خط E", "ricketts", -2, 2, "mm", "SoftTissue", 70),
        Norm("Lower-Lip-ELine", "الشفة السفلية إلى خط E", "ricketts", -2, 2, "mm", "SoftTissue", 80),
        Norm("Nasolabial", "الزاوية الأنفية-الشفوية", "ricketts", 102, 8, "°", "SoftTissue", 90),

        // ── Downs ───────────────────────────────────────────────────────────
        Norm("Convexity", "انحناء الوجه (N-A-Pog)", "downs", 0, 5, "°", "Skeletal", 10),
        Norm("AB-FacialPlane", "خط A-B إلى مستوى الوجه", "downs", -4.6m, 3, "°", "Sagittal", 20),
        Norm("Y-Axis", "محور Y (S-Gn/FH)", "downs", 59.4m, 4, "°", "Vertical", 30),
        Norm("Facial-Plane-FH", "مستوى الوجه (N-Pog) / FH", "downs", 87.8m, 3, "°", "Skeletal", 40),
        Norm("Mandibular-FH", "مستوى الفك السفلي / FH", "downs", 21.9m, 4, "°", "Vertical", 50),

        // ── Jarabak (Björk polygon + facial-height ratio) ─────────────────────
        Norm("Saddle-Angle", "الزاوية السرجية (N-S-Ar)", "jarabak", 123, 5, "°", "Skeletal", 10,
            below: "وضع أمامي للحفرة الفكية", above: "وضع خلفي للحفرة الفكية"),
        Norm("Articular-Angle", "الزاوية المفصلية (S-Ar-Go)", "jarabak", 143, 6, "°", "Skeletal", 20,
            below: "ميل لنمط أفقي", above: "ميل لنمط عمودي"),
        Norm("Gonial-Angle", "الزاوية الفكية (Ar-Go-Me)", "jarabak", 130, 7, "°", "Vertical", 30,
            below: "نمط نمو أفقي (عضة عميقة)", above: "نمط نمو عمودي (عضة مفتوحة)"),
        Norm("Bjork-Sum", "مجموع زوايا بيورك", "jarabak", 396, 6, "°", "Vertical", 40,
            below: "نمط نمو أفقي (دوران أمامي)", above: "نمط نمو عمودي (دوران خلفي)"),
        Norm("Jarabak-Ratio", "نسبة جاراباك (الارتفاع الخلفي/الأمامي)", "jarabak", 64, 4, "%", "Vertical", 50,
            below: "اتجاه نمو عمودي", above: "اتجاه نمو أفقي"),

        // ── Wits ────────────────────────────────────────────────────────────
        Norm("Wits", "مسافة وتس (AO-BO)", "wits", 0, 1.5m, "mm", "Sagittal", 10,
            below: "صنف ثالث", above: "صنف ثانٍ"),
    ];

    private static CephNorm Norm(
        string name, string nameAr, string group,
        decimal normal, decimal sd, string unit, string category, int sortOrder,
        string? below = null, string? above = null)
        => new()
        {
            MeasurementName      = name,
            NameAr               = nameAr,
            AnalysisGroup        = group,
            NormalValue          = normal,
            StdDeviation         = sd,
            MinNormal            = null,
            MaxNormal            = null,
            Unit                 = unit,
            Category             = category,
            SortOrder            = sortOrder,
            InterpretationBelow  = below,
            InterpretationAbove  = above,
            InterpretationNormal = (below is not null || above is not null) ? WithinNormalAr : null,
        };
}
