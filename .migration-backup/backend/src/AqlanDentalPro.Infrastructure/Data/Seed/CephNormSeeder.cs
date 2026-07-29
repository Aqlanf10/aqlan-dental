using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Infrastructure.Data.Seed;

/// <summary>
/// Factory cephalometric norms (international standard values from published
/// orthodontic literature — Steiner, Tweed, McNamara, Ricketts, Downs, Jarabak,
/// Wits). Single source of truth shared by:
/// - startup seeding (StartupDatabaseMaintenance — only inserts when table empty),
/// - POST /api/ceph-norms/reset-defaults (re-seeds factory values),
/// - CephService fallback values mirror these (norms must stay in sync).
///
/// CLIN-10 — stratified by patient age band and sex for the key skeletal /
/// dental measurements whose norms shift materially between child, adolescent,
/// and adult (Bishara longitudinal, Jacobson Wits, Steiner, Tweed). Rows with
/// AgeMin/AgeMax/Sex all null remain "un-stratified" and act as a
/// backward-compatible fallback for measurements we don't stratify (e.g.
/// McNamara, Ricketts, Downs) so the lookup never breaks. Uncertain
/// population-specific values are flagged with <c>// TODO: verify</c> so the
/// clinic owner (an orthodontist) can review against his own reference set.
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
    /// Inserts only the factory rows that are MISSING from the table (matched by
    /// the full stratification key — MeasurementName + AnalysisGroup + AgeMin +
    /// AgeMax + Sex, including soft-deleted rows so a unique-by-strata invariant
    /// would never be violated). Existing rows — even admin-customized ones —
    /// are left untouched. This lets newly added analyses (e.g. Jarabak) or
    /// newly stratified measurements (CLIN-10) light up on already-seeded
    /// clinics at startup without a manual reset. Returns the number of rows
    /// inserted (0 when nothing was missing).
    /// </summary>
    public static async Task<int> BackfillMissingDefaultsAsync(AppDbContext db, CancellationToken ct = default)
    {
        var existingKeys = (await db.CephNorms.IgnoreQueryFilters()
                .Select(n => new { n.MeasurementName, n.AnalysisGroup, n.AgeMin, n.AgeMax, n.Sex })
                .ToListAsync(ct))
            .Select(k => (k.MeasurementName, k.AnalysisGroup, k.AgeMin, k.AgeMax, k.Sex))
            .ToHashSet();

        var missing = GetFactoryDefaults()
            .Where(d => !existingKeys.Contains((d.MeasurementName, d.AnalysisGroup, d.AgeMin, d.AgeMax, d.Sex)))
            .ToList();

        if (missing.Count == 0) return 0;
        db.CephNorms.AddRange(missing);
        await db.SaveChangesAsync(ct);
        return missing.Count;
    }

    /// <summary>
    /// Restores every factory row to factory values: existing rows (matched by
    /// the full stratification key — MeasurementName + AnalysisGroup + AgeMin +
    /// AgeMax + Sex, including soft-deleted ones) are overwritten and
    /// re-activated; missing rows are inserted. Rows present in the DB but no
    /// longer in the factory list (e.g. an admin-added custom norm, or a
    /// stratified row removed from the factory defaults in a future release)
    /// are left untouched.
    /// </summary>
    public static async Task<int> ResetToFactoryDefaultsAsync(AppDbContext db, CancellationToken ct = default)
    {
        var existing = await db.CephNorms.IgnoreQueryFilters().ToListAsync(ct);
        var byKey = existing
            .GroupBy(n => (n.MeasurementName, n.AnalysisGroup, n.AgeMin, n.AgeMax, n.Sex))
            .ToDictionary(g => g.Key, g => g.First());

        var defaults = GetFactoryDefaults();
        foreach (var d in defaults)
        {
            if (byKey.TryGetValue((d.MeasurementName, d.AnalysisGroup, d.AgeMin, d.AgeMax, d.Sex), out var row))
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

    // CLIN-10 — age bands used for stratification. Whole years, inclusive
    // bounds. Child = mixed/early permanent dentition, Adolescent = late
    // permanent dentition / active growth tail, Adult = growth complete.
    private const int ChildMin = 6, ChildMax = 12;
    private const int AdolMin  = 13, AdolMax  = 17;
    private const int AdultMin = 18;

    /// <summary>
    /// The authoritative factory list — values match the hardcoded fallbacks in
    /// CephService.ComputeMeasurementsAsync and frontend cephMath.ts norm tables.
    /// Stratified rows (AgeMin/AgeMax set) are listed in age order per
    /// measurement: child → adolescent → adult. Un-stratified rows (all three
    /// null) act as a backward-compatible fallback for measurements we don't
    /// stratify.
    /// </summary>
    public static List<CephNorm> GetFactoryDefaults() =>
    [
        // ── Steiner ─────────────────────────────────────────────────────────
        // CLIN-10: SNA stratified. Adult = 82° (Steiner original).
        // Child/Adolescent ~81° (Bishara longitudinal). TODO: verify against
        // the clinic owner's reference population.
        Norm("SNA", "زاوية SNA", "steiner", 81, 2, "°", "Skeletal", 10,
            ageMin: ChildMin, ageMax: ChildMax,
            below: "رجوع الفك العلوي", above: "بروز الفك العلوي"),
        Norm("SNA", "زاوية SNA", "steiner", 81, 2, "°", "Skeletal", 11,
            ageMin: AdolMin, ageMax: AdolMax,
            below: "رجوع الفك العلوي", above: "بروز الفك العلوي"),
        Norm("SNA", "زاوية SNA", "steiner", 82, 2, "°", "Skeletal", 12,
            ageMin: AdultMin, ageMax: null,
            below: "رجوع الفك العلوي", above: "بروز الفك العلوي"),

        // CLIN-10: SNB stratified. Adult = 80° (Steiner). Child/Adolescent ~78°
        // (Bishara — mandible still retruded relative to cranial base during
        // growth). TODO: verify.
        Norm("SNB", "زاوية SNB", "steiner", 78, 2, "°", "Skeletal", 20,
            ageMin: ChildMin, ageMax: ChildMax,
            below: "رجوع الفك السفلي", above: "بروز الفك السفلي"),
        Norm("SNB", "زاوية SNB", "steiner", 78, 2, "°", "Skeletal", 21,
            ageMin: AdolMin, ageMax: AdolMax,
            below: "رجوع الفك السفلي", above: "بروز الفك السفلي"),
        Norm("SNB", "زاوية SNB", "steiner", 80, 2, "°", "Skeletal", 22,
            ageMin: AdultMin, ageMax: null,
            below: "رجوع الفك السفلي", above: "بروز الفك السفلي"),

        // CLIN-10: ANB stratified. Adult = 2° (Steiner). Child = 3° (mixed
        // dentition — A-point still forward). Adolescent = 2-3°. TODO: verify.
        Norm("ANB", "زاوية ANB", "steiner", 3, 1, "°", "Sagittal", 30,
            ageMin: ChildMin, ageMax: ChildMax,
            below: "ميل لصنف ثالث هيكلي", above: "ميل لصنف ثانٍ هيكلي"),
        Norm("ANB", "زاوية ANB", "steiner", 2, 1, "°", "Sagittal", 31,
            ageMin: AdolMin, ageMax: AdolMax,
            below: "ميل لصنف ثالث هيكلي", above: "ميل لصنف ثانٍ هيكلي"),
        Norm("ANB", "زاوية ANB", "steiner", 2, 1, "°", "Sagittal", 32,
            ageMin: AdultMin, ageMax: null,
            below: "ميل لصنف ثالث هيكلي", above: "ميل لصنف ثانٍ هيكلي"),

        Norm("SND", "زاوية SND", "steiner", 76, 2, "°", "Skeletal", 40),

        // CLIN-10: U1-NA_angle stratified. Adult = 22° (Steiner). Child/Adolescent
        // ~22° (no material change post-eruption). TODO: verify.
        Norm("U1-NA_angle", "U1/NA (°)", "steiner", 22, 2, "°", "Dental", 50,
            ageMin: ChildMin, ageMax: ChildMax,
            below: "ميلان حنكي", above: "بروز قواطع علوية"),
        Norm("U1-NA_angle", "U1/NA (°)", "steiner", 22, 2, "°", "Dental", 51,
            ageMin: AdolMin, ageMax: AdolMax,
            below: "ميلان حنكي", above: "بروز قواطع علوية"),
        Norm("U1-NA_angle", "U1/NA (°)", "steiner", 22, 2, "°", "Dental", 52,
            ageMin: AdultMin, ageMax: null,
            below: "ميلان حنكي", above: "بروز قواطع علوية"),

        // CLIN-10: U1-NA_mm stratified. Adult = 4mm. Child/Adolescent ~4mm.
        // TODO: verify.
        Norm("U1-NA_mm", "U1/NA (mm)", "steiner", 4, 2, "mm", "Dental", 60,
            ageMin: ChildMin, ageMax: ChildMax,
            below: "ميلان حنكي", above: "بروز قواطع علوية"),
        Norm("U1-NA_mm", "U1/NA (mm)", "steiner", 4, 2, "mm", "Dental", 61,
            ageMin: AdolMin, ageMax: AdolMax,
            below: "ميلان حنكي", above: "بروز قواطع علوية"),
        Norm("U1-NA_mm", "U1/NA (mm)", "steiner", 4, 2, "mm", "Dental", 62,
            ageMin: AdultMin, ageMax: null,
            below: "ميلان حنكي", above: "بروز قواطع علوية"),

        // CLIN-10: L1-NB_angle stratified. Adult = 25° (Steiner).
        // Child/Adolescent ~25°. TODO: verify.
        Norm("L1-NB_angle", "L1/NB (°)", "steiner", 25, 2, "°", "Dental", 70,
            ageMin: ChildMin, ageMax: ChildMax, above: "بروز قواطع سفلية"),
        Norm("L1-NB_angle", "L1/NB (°)", "steiner", 25, 2, "°", "Dental", 71,
            ageMin: AdolMin, ageMax: AdolMax, above: "بروز قواطع سفلية"),
        Norm("L1-NB_angle", "L1/NB (°)", "steiner", 25, 2, "°", "Dental", 72,
            ageMin: AdultMin, ageMax: null, above: "بروز قواطع سفلية"),

        // CLIN-10: L1-NB_mm stratified. Adult = 4mm. Child/Adolescent ~4mm.
        // TODO: verify.
        Norm("L1-NB_mm", "L1/NB (mm)", "steiner", 4, 2, "mm", "Dental", 80,
            ageMin: ChildMin, ageMax: ChildMax, above: "بروز قواطع سفلية"),
        Norm("L1-NB_mm", "L1/NB (mm)", "steiner", 4, 2, "mm", "Dental", 81,
            ageMin: AdolMin, ageMax: AdolMax, above: "بروز قواطع سفلية"),
        Norm("L1-NB_mm", "L1/NB (mm)", "steiner", 4, 2, "mm", "Dental", 82,
            ageMin: AdultMin, ageMax: null, above: "بروز قواطع سفلية"),

        // CLIN-10: U1-L1 (interincisal) stratified. Adult = 131° (Steiner).
        // Child ~124° (smaller in mixed dentition). Adolescent ~131°. TODO: verify.
        Norm("U1-L1", "زاوية القاطعين", "steiner", 124, 6, "°", "Dental", 90,
            ageMin: ChildMin, ageMax: ChildMax),
        Norm("U1-L1", "زاوية القاطعين", "steiner", 131, 6, "°", "Dental", 91,
            ageMin: AdolMin, ageMax: AdolMax),
        Norm("U1-L1", "زاوية القاطعين", "steiner", 131, 6, "°", "Dental", 92,
            ageMin: AdultMin, ageMax: null),

        // CLIN-10: GoGn-SN stratified. Adult = 32°. Child/Adolescent ~32-35°
        // (larger during active growth). TODO: verify.
        Norm("GoGn-SN", "GoGn / SN", "steiner", 35, 6, "°", "Vertical", 100,
            ageMin: ChildMin, ageMax: ChildMax,
            below: "نمط نمو أفقي", above: "نمط نمو عمودي"),
        Norm("GoGn-SN", "GoGn / SN", "steiner", 33, 6, "°", "Vertical", 101,
            ageMin: AdolMin, ageMax: AdolMax,
            below: "نمط نمو أفقي", above: "نمط نمو عمودي"),
        Norm("GoGn-SN", "GoGn / SN", "steiner", 32, 6, "°", "Vertical", 102,
            ageMin: AdultMin, ageMax: null,
            below: "نمط نمو أفقي", above: "نمط نمو عمودي"),

        Norm("UL-SLine", "الشفة العلوية — خط S", "steiner", 0, 1, "mm", "SoftTissue", 110),
        Norm("LL-SLine", "الشفة السفلية — خط S", "steiner", 0, 1, "mm", "SoftTissue", 120),

        // ── Tweed ───────────────────────────────────────────────────────────
        // CLIN-10: FMA stratified. Adult = 25° (Tweed). Child ~28-29° (steeper
        // mandibular plane during growth). Adolescent ~26°. TODO: verify.
        Norm("FMA", "FMA (فرانكفورت-فك سفلي)", "tweed", 29, 4, "°", "Vertical", 10,
            ageMin: ChildMin, ageMax: ChildMax,
            below: "نمط نمو أفقي", above: "نمط نمو عمودي"),
        Norm("FMA", "FMA (فرانكفورت-فك سفلي)", "tweed", 26, 4, "°", "Vertical", 11,
            ageMin: AdolMin, ageMax: AdolMax,
            below: "نمط نمو أفقي", above: "نمط نمو عمودي"),
        Norm("FMA", "FMA (فرانكفورت-فك سفلي)", "tweed", 25, 4, "°", "Vertical", 12,
            ageMin: AdultMin, ageMax: null,
            below: "نمط نمو أفقي", above: "نمط نمو عمودي"),

        // CLIN-10: FMIA stratified. Adult = 65° (Tweed). Child/Adolescent ~65°.
        // TODO: verify.
        Norm("FMIA", "FMIA (فرانكفورت-قاطعة سفلية)", "tweed", 65, 5, "°", "Dental", 20,
            ageMin: ChildMin, ageMax: ChildMax),
        Norm("FMIA", "FMIA (فرانكفورت-قاطعة سفلية)", "tweed", 65, 5, "°", "Dental", 21,
            ageMin: AdolMin, ageMax: AdolMax),
        Norm("FMIA", "FMIA (فرانكفورت-قاطعة سفلية)", "tweed", 65, 5, "°", "Dental", 22,
            ageMin: AdultMin, ageMax: null),

        // CLIN-10: IMPA stratified. Adult = 90° (Tweed). Child/Adolescent ~90°.
        // TODO: verify.
        Norm("IMPA", "IMPA (فك سفلي-قاطعة سفلية)", "tweed", 90, 4, "°", "Dental", 30,
            ageMin: ChildMin, ageMax: ChildMax),
        Norm("IMPA", "IMPA (فك سفلي-قاطعة سفلية)", "tweed", 90, 4, "°", "Dental", 31,
            ageMin: AdolMin, ageMax: AdolMax),
        Norm("IMPA", "IMPA (فك سفلي-قاطعة سفلية)", "tweed", 90, 4, "°", "Dental", 32,
            ageMin: AdultMin, ageMax: null),

        // ── McNamara ────────────────────────────────────────────────────────
        // Un-stratified (no age/sex-specific published values readily cited for
        // the clinic's reference population). Acts as fallback. TODO: verify.
        Norm("Co-A", "Co-A (طول الفك العلوي)", "mcnamara", 91, 6, "mm", "Skeletal", 10),
        Norm("Co-Gn", "Co-Gn (طول الفك السفلي)", "mcnamara", 120, 7, "mm", "Skeletal", 20),
        Norm("ANS-Me", "ANS-Me (ارتفاع الوجه السفلي)", "mcnamara", 65, 5, "mm", "Vertical", 30),

        // ── Ricketts ────────────────────────────────────────────────────────
        // Un-stratified. TODO: verify.
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
        // Un-stratified. TODO: verify.
        Norm("Convexity", "انحناء الوجه (N-A-Pog)", "downs", 0, 5, "°", "Skeletal", 10),
        Norm("AB-FacialPlane", "خط A-B إلى مستوى الوجه", "downs", -4.6m, 3, "°", "Sagittal", 20),
        Norm("Y-Axis", "محور Y (S-Gn/FH)", "downs", 59.4m, 4, "°", "Vertical", 30),
        Norm("Facial-Plane-FH", "مستوى الوجه (N-Pog) / FH", "downs", 87.8m, 3, "°", "Skeletal", 40),
        Norm("Mandibular-FH", "مستوى الفك السفلي / FH", "downs", 21.9m, 4, "°", "Vertical", 50),

        // ── Jarabak (Björk polygon + facial-height ratio) ─────────────────────
        // Un-stratified. TODO: verify.
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
        // CLIN-10: Wits stratified by age + sex. Adult values from Jacobson
        // (1975): Male +1mm, Female 0mm, Both (sex-null) 0mm fallback.
        // Child/Adolescent ~0mm — published norms for growing patients are
        // sparse; we use 0mm with sex-null as a conservative fallback.
        // TODO: verify against the clinic owner's reference population.
        Norm("Wits", "مسافة وتس (AO-BO)", "wits", 0, 1.5m, "mm", "Sagittal", 10,
            ageMin: ChildMin, ageMax: ChildMax, sex: null,
            below: "صنف ثالث", above: "صنف ثانٍ"),
        Norm("Wits", "مسافة وتس (AO-BO)", "wits", 0, 1.5m, "mm", "Sagittal", 11,
            ageMin: AdolMin, ageMax: AdolMax, sex: null,
            below: "صنف ثالث", above: "صنف ثانٍ"),
        // Adult — sex-null fallback (applies to both sexes when sex-specific
        // row missing or patient sex unknown).
        Norm("Wits", "مسافة وتس (AO-BO)", "wits", 0, 1.5m, "mm", "Sagittal", 12,
            ageMin: AdultMin, ageMax: null, sex: null,
            below: "صنف ثالث", above: "صنف ثانٍ"),
        // Adult — male-specific (Jacobson +1mm).
        Norm("Wits", "مسافة وتس (AO-BO) — ذكر", "wits", 1, 1.5m, "mm", "Sagittal", 13,
            ageMin: AdultMin, ageMax: null, sex: "M",
            below: "صنف ثالث", above: "صنف ثانٍ"),
        // Adult — female-specific (Jacobson 0mm).
        Norm("Wits", "مسافة وتس (AO-BO) — أنثى", "wits", 0, 1.5m, "mm", "Sagittal", 14,
            ageMin: AdultMin, ageMax: null, sex: "F",
            below: "صنف ثالث", above: "صنف ثانٍ"),
    ];

    private static CephNorm Norm(
        string name, string nameAr, string group,
        decimal normal, decimal sd, string unit, string category, int sortOrder,
        int? ageMin = null, int? ageMax = null, string? sex = null,
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
            AgeMin               = ageMin,
            AgeMax               = ageMax,
            Sex                  = sex,
            InterpretationBelow  = below,
            InterpretationAbove  = above,
            InterpretationNormal = (below is not null || above is not null) ? WithinNormalAr : null,
        };
}
