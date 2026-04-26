using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AqlanDentalPro.Application.Services;

public class CephService(AppDbContext db, ICurrentUserService currentUser)
{
    // ──────────────────────────────────────────────────────────────────────────
    //  LIST
    // ──────────────────────────────────────────────────────────────────────────
    public async Task<List<CephAnalysisListDto>> ListAsync(Guid? orthoCaseId)
    {
        return await db.CephAnalyses
            .Where(a => orthoCaseId == null || a.OrthoCaseId == orthoCaseId)
            .OrderByDescending(a => a.AnalysisDate)
            .Select(a => new CephAnalysisListDto
            {
                Id              = a.Id,
                OrthoCaseId     = a.OrthoCaseId,
                CaseNumber      = a.OrthoCase.CaseNumber,
                PatientName     = a.OrthoCase.Patient.FirstName + " " + a.OrthoCase.Patient.LastName,
                AnalysisType    = a.AnalysisType,
                AnalysisDate    = a.AnalysisDate.ToString("yyyy-MM-dd"),
                XrayFileUrl     = a.XrayFileUrl,
                AiAssisted      = a.AiAssisted,
                LandmarkCount   = a.Landmarks.Count(l => l.IsActive),
                HasMeasurements = a.Measurements.Any(m => m.IsActive),
                Notes           = a.Notes
            })
            .ToListAsync();
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  GET BY ID
    // ──────────────────────────────────────────────────────────────────────────
    public async Task<CephAnalysisDetailDto?> GetByIdAsync(Guid id)
    {
        var a = await db.CephAnalyses
            .Include(x => x.OrthoCase).ThenInclude(o => o.Patient)
            .Include(x => x.Landmarks)
            .Include(x => x.Measurements)
            .Include(x => x.Diagnosis)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (a is null) return null;
        return MapDetail(a);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  CREATE
    // ──────────────────────────────────────────────────────────────────────────
    public async Task<CephAnalysisDetailDto> CreateAsync(CreateCephAnalysisRequest req)
    {
        var analysis = new CephAnalysis
        {
            OrthoCaseId  = req.OrthoCaseId,
            AnalysisType = req.AnalysisType,
            XrayFileUrl  = req.XrayFileUrl,
            AiAssisted   = req.AiAssisted,
            DoctorId     = currentUser.UserId,
            Notes        = req.Notes
        };

        db.CephAnalyses.Add(analysis);
        await db.SaveChangesAsync();

        return await GetByIdAsync(analysis.Id)
               ?? throw new InvalidOperationException("Failed to load created analysis.");
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  SAVE LANDMARKS  →  triggers measurement computation
    // ──────────────────────────────────────────────────────────────────────────
    public async Task<bool> SaveLandmarksAsync(Guid id, SaveLandmarksRequest req)
    {
        var analysis = await db.CephAnalyses.FindAsync(id);
        if (analysis is null) return false;

        // Store calibration data in Notes as JSON so ComputeMeasurements can use it.
        var notesData = new CephNotesData
        {
            PixelsPerMm = req.PixelsPerMm,
            ImageWidth  = req.ImageWidth,
            ImageHeight = req.ImageHeight,
            UserNotes   = ExtractUserNotes(analysis.Notes)
        };
        analysis.Notes = JsonSerializer.Serialize(notesData);

        // Replace landmarks: soft-delete old, insert new.
        var existing = await db.CephLandmarks
            .Where(l => l.AnalysisId == id)
            .ToListAsync();

        foreach (var lm in existing) lm.IsActive = false;

        foreach (var input in req.Landmarks)
        {
            db.CephLandmarks.Add(new CephLandmark
            {
                AnalysisId    = id,
                LandmarkKey   = input.Key,
                LandmarkName  = input.Name,
                XCoord        = (decimal)input.X,
                YCoord        = (decimal)input.Y,
                IsAiPlaced    = input.IsAiPlaced,
                Confidence    = input.Confidence.HasValue ? (decimal)input.Confidence.Value : null
            });
        }

        await db.SaveChangesAsync();
        await ComputeMeasurementsAsync(id);
        return true;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  COMPUTE MEASUREMENTS
    // ──────────────────────────────────────────────────────────────────────────
    public async Task ComputeMeasurementsAsync(Guid id)
    {
        var analysis = await db.CephAnalyses
            .Include(a => a.Landmarks)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (analysis is null) return;

        var lm = analysis.Landmarks
            .Where(l => l.IsActive)
            .ToDictionary(
                l => l.LandmarkKey,
                l => (x: (double)(l.XCoord ?? 0), y: (double)(l.YCoord ?? 0)));

        double pixelsPerMm = 0;
        if (!string.IsNullOrWhiteSpace(analysis.Notes))
            try
            {
                var nd = JsonSerializer.Deserialize<CephNotesData>(analysis.Notes);
                if (nd is not null) pixelsPerMm = nd.PixelsPerMm;
            }
            catch { }

        bool cal = pixelsPerMm > 0;
        bool Has(params string[] keys) => keys.All(lm.ContainsKey);
        (double x, double y) G(string k) => lm[k];
        double R1(double v) => Math.Round(v, 1);

        var results = new List<(string name, string group, double value, double normal, double sd, string unit)>();
        void Add(string name, string group, double value, double normal, double sd, string unit)
            => results.Add((name, group, R1(value), normal, sd, unit));

        var groups = GetAnalysisGroups(analysis.AnalysisType ?? "full");

        // ── Steiner ─────────────────────────────────────────────────────────
        if (groups.Contains("steiner"))
        {
            if (Has("S","N","A"))         Add("SNA",        "steiner", Angle3T(G("S"),G("N"),G("A")), 82, 2, "°");
            if (Has("S","N","B"))         Add("SNB",        "steiner", Angle3T(G("S"),G("N"),G("B")), 80, 2, "°");

            double? snAng = results.Find(r => r.name == "SNA") is var rA && rA.name != null ? rA.value : null;
            double? snBAng = results.Find(r => r.name == "SNB") is var rB && rB.name != null ? rB.value : null;
            if (snAng.HasValue && snBAng.HasValue)
                Add("ANB",        "steiner", snAng.Value - snBAng.Value, 2, 1, "°");

            if (Has("S","N","D"))         Add("SND",        "steiner", Angle3T(G("S"),G("N"),G("D")), 76, 2, "°");
            if (Has("U1A","U1T","N","A")) Add("U1-NA_angle","steiner", ABLines(G("U1A"),G("U1T"),G("N"),G("A")), 22, 2, "°");
            if (cal && Has("U1T","N","A")) Add("U1-NA_mm",  "steiner", SignedPerpT(G("U1T"),G("N"),G("A")) / pixelsPerMm, 4, 2, "mm");
            if (Has("L1A","L1T","N","B")) Add("L1-NB_angle","steiner", ABLines(G("L1A"),G("L1T"),G("N"),G("B")), 25, 2, "°");
            if (cal && Has("L1T","N","B")) Add("L1-NB_mm",  "steiner", SignedPerpT(G("L1T"),G("N"),G("B")) / pixelsPerMm, 4, 2, "mm");
            if (Has("U1A","U1T","L1A","L1T"))
                Add("U1-L1",      "steiner", 180 - ABLines(G("U1A"),G("U1T"),G("L1A"),G("L1T")), 131, 6, "°");
            if (Has("Go","Me","S","N"))   Add("GoGn-SN",    "steiner", ABLines(G("Go"),G("Me"),G("S"),G("N")), 32, 6, "°");

            // S-line soft tissue: Pog → midpoint(Cm, Pn)
            if (Has("Cm","Pn"))
            {
                var sLineMid = ((G("Cm").x + G("Pn").x) / 2, (G("Cm").y + G("Pn").y) / 2);
                if (cal && Has("LS","Pog"))
                    Add("UL-SLine", "steiner",
                        SignedPerp(G("LS").x, G("LS").y,
                                   G("Pog").x, G("Pog").y, sLineMid.Item1, sLineMid.Item2) / pixelsPerMm,
                        0, 1, "mm");
                if (cal && Has("LI","Pog"))
                    Add("LL-SLine", "steiner",
                        SignedPerp(G("LI").x, G("LI").y,
                                   G("Pog").x, G("Pog").y, sLineMid.Item1, sLineMid.Item2) / pixelsPerMm,
                        0, 1, "mm");
            }
        }

        // ── Tweed ───────────────────────────────────────────────────────────
        if (groups.Contains("tweed"))
        {
            if (Has("Po","Or","Go","Me"))    Add("FMA",  "tweed", ABLines(G("Po"),G("Or"),G("Go"),G("Me")), 25, 4, "°");
            if (Has("Po","Or","L1A","L1T"))  Add("FMIA", "tweed", ABLines(G("Po"),G("Or"),G("L1A"),G("L1T")), 65, 5, "°");
            if (Has("Go","Me","L1A","L1T"))  Add("IMPA", "tweed", ABLines(G("Go"),G("Me"),G("L1A"),G("L1T")), 90, 4, "°");
        }

        // ── McNamara ────────────────────────────────────────────────────────
        if (groups.Contains("mcnamara"))
        {
            if (cal && Has("Co","A"))   Add("Co-A",   "mcnamara", DistT(G("Co"),G("A")) / pixelsPerMm, 91, 6, "mm");
            if (cal && Has("Co","Gn"))  Add("Co-Gn",  "mcnamara", DistT(G("Co"),G("Gn")) / pixelsPerMm, 120, 7, "mm");
            if (cal && Has("ANS","Me")) Add("ANS-Me", "mcnamara", DistT(G("ANS"),G("Me")) / pixelsPerMm, 65, 5, "mm");
        }

        // ── Ricketts ────────────────────────────────────────────────────────
        if (groups.Contains("ricketts"))
        {
            if (Has("Po","Or","N","Pog"))
                Add("Facial-Depth",     "ricketts", ABLines(G("Po"),G("Or"),G("N"),G("Pog")), 87, 3, "°");
            if (Has("S","N","Gn"))
                Add("Facial-Axis",      "ricketts", ABLines(G("S"),G("N"),G("N"),G("Gn")), 90, 3, "°");
            if (Has("Po","Or","Go","Me"))
                Add("Mandibular-Plane", "ricketts", ABLines(G("Po"),G("Or"),G("Go"),G("Me")), 26, 4, "°");
            if (cal && Has("N","A","Pog"))
                Add("Convexity-A",      "ricketts",
                    SignedPerpT(G("A"),G("N"),G("Pog")) / pixelsPerMm, 2, 2, "mm");
            if (cal && Has("L1T","A","Pog"))
                Add("L1-APog_mm",       "ricketts",
                    SignedPerpT(G("L1T"),G("A"),G("Pog")) / pixelsPerMm, 1, 2, "mm");
            if (Has("L1A","L1T","A","Pog"))
                Add("L1-APog_angle",    "ricketts", ABLines(G("L1A"),G("L1T"),G("A"),G("Pog")), 22, 4, "°");
            if (cal && Has("LS","Pn","Pog"))
                Add("Upper-Lip-ELine",  "ricketts",
                    SignedPerpT(G("LS"),G("Pn"),G("Pog")) / pixelsPerMm, -2, 2, "mm");
            if (cal && Has("LI","Pn","Pog"))
                Add("Lower-Lip-ELine",  "ricketts",
                    SignedPerpT(G("LI"),G("Pn"),G("Pog")) / pixelsPerMm, -2, 2, "mm");
            if (Has("Pn","Cm","LS"))
                Add("Nasolabial",       "ricketts", Angle3T(G("Pn"),G("Cm"),G("LS")), 102, 8, "°");
        }

        // ── Downs ───────────────────────────────────────────────────────────
        if (groups.Contains("downs"))
        {
            if (Has("N","A","Pog"))
                Add("Convexity",        "downs", Angle3T(G("N"),G("A"),G("Pog")) - 180, 0, 5, "°");
            if (Has("A","B","N","Pog"))
                Add("AB-FacialPlane",   "downs", -ABLines(G("A"),G("B"),G("N"),G("Pog")), -4.6, 3, "°");
            if (Has("S","Gn","Po","Or"))
                Add("Y-Axis",           "downs", ABLines(G("S"),G("Gn"),G("Po"),G("Or")), 59.4, 4, "°");
            if (Has("N","Pog","Po","Or"))
                Add("Facial-Plane-FH",  "downs", ABLines(G("N"),G("Pog"),G("Po"),G("Or")), 87.8, 3, "°");
            if (Has("Go","Me","Po","Or"))
                Add("Mandibular-FH",    "downs", ABLines(G("Go"),G("Me"),G("Po"),G("Or")), 21.9, 4, "°");
        }

        // ── Wits ─────────────────────────────────────────────────────────────
        if (groups.Contains("wits") && cal && Has("U1T","L1T","Go","Me","A","B"))
        {
            // Occlusal plane: midpoint(U1T,L1T) → midpoint(Go,Me)
            var occP1 = ((G("U1T").x + G("L1T").x) / 2, (G("U1T").y + G("L1T").y) / 2);
            var occP2 = ((G("Go").x  + G("Me").x)  / 2, (G("Go").y  + G("Me").y)  / 2);
            double dx = occP2.Item1 - occP1.Item1, dy = occP2.Item2 - occP1.Item2;
            double len2 = dx * dx + dy * dy;
            if (len2 > 1e-9)
            {
                (double px, double py) ProjectOcc((double x, double y) p)
                {
                    double t = ((p.x - occP1.Item1) * dx + (p.y - occP1.Item2) * dy) / len2;
                    return (occP1.Item1 + t * dx, occP1.Item2 + t * dy);
                }
                var ao = ProjectOcc(G("A"));
                var bo = ProjectOcc(G("B"));
                double dLen = Math.Sqrt(len2);
                double ux = dx / dLen, uy = dy / dLen;
                double wits = ((ao.px - bo.px) * ux + (ao.py - bo.py) * uy) / pixelsPerMm;
                Add("Wits", "wits", wits, 0, 1.5, "mm");
            }
        }
        else if (groups.Contains("wits") && !results.Exists(r => r.group == "wits"))
        {
            // No landmarks available — skip (frontend shows "—" via real-time compute)
        }

        // ── Persist ──────────────────────────────────────────────────────────
        var oldMeasurements = await db.CephMeasurements.Where(m => m.AnalysisId == id).ToListAsync();
        foreach (var m in oldMeasurements) m.IsActive = false;

        foreach (var (name, group, value, normal, sd, unit) in results)
        {
            double rawDev = value - normal;
            double sdNorm = sd > 0 ? rawDev / sd : 0;
            string classification = ClassifyDeviation(sdNorm);

            db.CephMeasurements.Add(new CephMeasurement
            {
                AnalysisId       = id,
                MeasurementName  = name,
                MeasurementValue = (decimal)value,
                NormalValue      = (decimal)normal,
                StdDeviation     = (decimal)sd,
                Unit             = unit,
                Deviation        = (decimal)Math.Round(rawDev, 2),
                Classification   = classification
            });
        }

        await db.SaveChangesAsync();
        await GenerateDiagnosisAsync(id);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  GENERATE DIAGNOSIS  (AI-style Arabic text from measurements)
    // ──────────────────────────────────────────────────────────────────────────
    private async Task GenerateDiagnosisAsync(Guid id)
    {
        var measurements = await db.CephMeasurements
            .Where(m => m.AnalysisId == id && m.IsActive)
            .ToListAsync();

        double? GetValue(string name) =>
            measurements.FirstOrDefault(m => m.MeasurementName == name)?.MeasurementValue is decimal d
                ? (double)d : null;

        double? anb    = GetValue("ANB");
        double? goGnSn = GetValue("GoGn-SN");
        double? fma    = GetValue("FMA");

        // ── Skeletal Class ────────────────────────────────────────────────
        string skeletalClass = "Class I";
        string skeletalAr    = "الصنف الأول الهيكلي";
        if (anb.HasValue)
        {
            if (anb.Value > 4)
            {
                skeletalClass = "Class II";
                skeletalAr    = "الصنف الثاني الهيكلي";
            }
            else if (anb.Value < 0)
            {
                skeletalClass = "Class III";
                skeletalAr    = "الصنف الثالث الهيكلي";
            }
        }

        // ── Vertical Pattern ──────────────────────────────────────────────
        string verticalPattern = "Normodivergent";
        string verticalAr      = "نمط رأسي طبيعي";
        double verticalRef     = goGnSn ?? fma ?? double.NaN;

        if (!double.IsNaN(verticalRef))
        {
            bool usingFma = !goGnSn.HasValue && fma.HasValue;
            double hi = usingFma ? 29 : 38;
            double lo = usingFma ? 21 : 26;

            if (verticalRef > hi)
            {
                verticalPattern = "Hyperdivergent";
                verticalAr      = "نمط رأسي مرتفع (وجه طويل)";
            }
            else if (verticalRef < lo)
            {
                verticalPattern = "Hypodivergent";
                verticalAr      = "نمط رأسي منخفض (وجه قصير)";
            }
        }

        // ── Incisor Inclination (from U1-NA_angle and L1-NB_angle) ──────────
        double? u1na = GetValue("U1-NA_angle");
        double? l1nb = GetValue("L1-NB_angle");
        string incisorInclination;

        bool u1Protrusive = u1na.HasValue && u1na.Value > 24;
        bool u1Retrusive  = u1na.HasValue && u1na.Value < 20;
        bool l1Protrusive = l1nb.HasValue && l1nb.Value > 27;
        bool l1Retrusive  = l1nb.HasValue && l1nb.Value < 23;

        if (u1Protrusive && l1Protrusive)
            incisorInclination = "بروز للقواطع العلوية والسفلية";
        else if (u1Protrusive)
            incisorInclination = "بروز في القاطعة العلوية";
        else if (l1Protrusive)
            incisorInclination = "بروز في القاطعة السفلية";
        else if (u1Retrusive && l1Retrusive)
            incisorInclination = "ارتداد للقواطع العلوية والسفلية";
        else if (u1Retrusive)
            incisorInclination = "ارتداد في القاطعة العلوية";
        else if (l1Retrusive)
            incisorInclination = "ارتداد في القاطعة السفلية";
        else
            incisorInclination = "ميلان القواطع ضمن الحدود الطبيعية";

        // ── Soft Tissue Summary ───────────────────────────────────────────
        string softTissueSummary = "الأنسجة الرخوة في حدود طبيعية";

        // ── AI Recommendation ─────────────────────────────────────────────
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"التشخيص الهيكلي: {skeletalAr}");
        sb.AppendLine($"النمط الرأسي: {verticalAr}");
        sb.AppendLine($"ميلان القواطع: {incisorInclination}");

        if (skeletalClass == "Class II")
            sb.AppendLine("التوصية: يُنصح بتقييم إمكانية العلاج بتعديل النمو أو استخدام أجهزة تقويمية ثابتة مع نوابض Forsus أو جهاز Herbst لتصحيح الصنف الثاني.");
        else if (skeletalClass == "Class III")
            sb.AppendLine("التوصية: يُنصح بتقييم العلاج الجراحي أو استخدام قوة وجهية عكسية في حالات النمو لتصحيح الصنف الثالث.");
        else
            sb.AppendLine("التوصية: يمكن إجراء العلاج التقويمي التقليدي لتصحيح العلاقات السنية.");

        if (verticalPattern == "Hyperdivergent")
            sb.AppendLine("ملاحظة: النمط الرأسي المرتفع يستوجب الحذر في استخدام مستخلصات الأسنان لتجنب مزيد من الفتح الرأسي.");
        else if (verticalPattern == "Hypodivergent")
            sb.AppendLine("ملاحظة: النمط الرأسي المنخفض يسمح عادةً بحركة أمامية للأسنان بدرجة أكبر.");

        string aiRecommendation = sb.ToString().Trim();

        // ── Save / update diagnosis ───────────────────────────────────────
        var diagnosis = await db.CephDiagnoses
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.AnalysisId == id);

        if (diagnosis is null)
        {
            diagnosis = new CephDiagnosis { AnalysisId = id };
            db.CephDiagnoses.Add(diagnosis);
        }

        diagnosis.IsActive           = true;
        diagnosis.SkeletalClass      = skeletalClass;
        diagnosis.VerticalPattern    = verticalPattern;
        diagnosis.IncisorInclination = incisorInclination;
        diagnosis.SoftTissueSummary  = softTissueSummary;
        diagnosis.AiRecommendation   = aiRecommendation;
        // Do not overwrite DoctorApproved/FinalDiagnosis if already set by doctor.

        await db.SaveChangesAsync();
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  SIMULATE AI LANDMARKS
    // ──────────────────────────────────────────────────────────────────────────
    public async Task<List<CephLandmarkDto>> SimulateAiAsync(Guid id, AiSimulateRequest req)
    {
        // Store calibration so subsequent compute has it.
        var analysis = await db.CephAnalyses.FindAsync(id);
        if (analysis is not null)
        {
            var notesData = new CephNotesData
            {
                PixelsPerMm = req.PixelsPerMm,
                ImageWidth  = req.ImageWidth,
                ImageHeight = req.ImageHeight,
                UserNotes   = ExtractUserNotes(analysis.Notes)
            };
            analysis.Notes      = JsonSerializer.Serialize(notesData);
            analysis.AiAssisted = true;
            analysis.IsAutoTraced = true;
            await db.SaveChangesAsync();
        }

        var rng = new Random();
        double W = req.ImageWidth  > 0 ? req.ImageWidth  : 800;
        double H = req.ImageHeight > 0 ? req.ImageHeight : 600;

        // (key, arabicName, fracX, fracY)
        var templates = new (string key, string nameAr, double fx, double fy)[]
        {
            ("S",   "السرج",                        0.45, 0.32),
            ("N",   "الناسيون",                     0.65, 0.24),
            ("Or",  "قاع المدار",                   0.72, 0.30),
            ("Po",  "قمة المسمع",                   0.40, 0.30),
            ("ANS", "الشوكة الأنفية الأمامية",      0.74, 0.48),
            ("PNS", "الشوكة الأنفية الخلفية",       0.50, 0.46),
            ("A",   "النقطة A",                     0.73, 0.52),
            ("B",   "النقطة B",                     0.68, 0.63),
            ("Pog", "الذقن البارز",                 0.66, 0.69),
            ("Gn",  "الذقن",                        0.64, 0.73),
            ("Me",  "الأسفل",                       0.61, 0.76),
            ("Go",  "زاوية الفك",                   0.37, 0.69),
            ("Co",  "رأس اللقمة",                   0.34, 0.27),
            ("Ar",  "المفصل",                       0.36, 0.31),
            ("D",   "النقطة D",                     0.65, 0.66),
            ("Pm",  "بروز الذقن",                   0.63, 0.65),
            ("U1T", "طرف القاطع العلوي",            0.77, 0.55),
            ("U1A", "قمة القاطع العلوي",            0.70, 0.61),
            ("L1T", "طرف القاطع السفلي",            0.75, 0.57),
            ("L1A", "قمة القاطع السفلي",            0.67, 0.64),
            ("LS",  "الشفة العلوية",                0.82, 0.56),
            ("LI",  "الشفة السفلية",                0.79, 0.61),
            ("Pn",  "طرف الأنف",                    0.87, 0.44),
            ("Cm",  "قاعدة الأنف",                  0.83, 0.49),
        };

        var result = new List<CephLandmarkDto>(templates.Length);
        foreach (var (key, nameAr, fx, fy) in templates)
        {
            double jitterX = (rng.NextDouble() - 0.5) * 2 * 0.015;
            double jitterY = (rng.NextDouble() - 0.5) * 2 * 0.015;
            double confidence = 0.75 + rng.NextDouble() * 0.20;

            result.Add(new CephLandmarkDto
            {
                Id         = Guid.NewGuid(),
                Key        = key,
                Name       = nameAr,
                X          = Math.Round((fx + jitterX) * W, 1),
                Y          = Math.Round((fy + jitterY) * H, 1),
                IsAiPlaced = true,
                Confidence = Math.Round(confidence, 2)
            });
        }

        return result;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  SOFT DELETE
    // ──────────────────────────────────────────────────────────────────────────
    public async Task SoftDeleteAsync(Guid id)
    {
        var analysis = await db.CephAnalyses.FindAsync(id);
        if (analysis is null) return;
        analysis.IsActive = false;
        await db.SaveChangesAsync();
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  SAVE DIAGNOSIS
    // ──────────────────────────────────────────────────────────────────────────
    public async Task<bool> SaveDiagnosisAsync(Guid id, SaveDiagnosisRequest req)
    {
        var analysis = await db.CephAnalyses.FindAsync(id);
        if (analysis is null) return false;

        var diagnosis = await db.CephDiagnoses
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.AnalysisId == id);

        if (diagnosis is null)
        {
            diagnosis = new CephDiagnosis { AnalysisId = id };
            db.CephDiagnoses.Add(diagnosis);
        }

        diagnosis.IsActive           = true;
        diagnosis.SkeletalClass      = req.SkeletalClass;
        diagnosis.VerticalPattern    = req.VerticalPattern;
        diagnosis.IncisorInclination = req.IncisorInclination;
        diagnosis.SoftTissueSummary  = req.SoftTissueSummary;
        diagnosis.FinalDiagnosis     = req.FinalDiagnosis;
        diagnosis.DoctorApproved     = req.DoctorApproved;

        await db.SaveChangesAsync();
        return true;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  MAPPING HELPERS
    // ──────────────────────────────────────────────────────────────────────────
    private static CephAnalysisDetailDto MapDetail(CephAnalysis a)
    {
        // Extract calibration data stored in Notes JSON.
        double? pixelsPerMm = null;
        int imageWidth = 0, imageHeight = 0;
        if (!string.IsNullOrWhiteSpace(a.Notes))
            try
            {
                var nd = JsonSerializer.Deserialize<CephNotesData>(a.Notes);
                if (nd is not null)
                {
                    pixelsPerMm = nd.PixelsPerMm > 0 ? nd.PixelsPerMm : null;
                    imageWidth  = nd.ImageWidth;
                    imageHeight = nd.ImageHeight;
                }
            }
            catch { }

        return new CephAnalysisDetailDto
        {
            Id           = a.Id,
            OrthoCaseId  = a.OrthoCaseId,
            CaseNumber   = a.OrthoCase.CaseNumber,
            PatientName  = a.OrthoCase.Patient.FirstName + " " + a.OrthoCase.Patient.LastName,
            AnalysisType = a.AnalysisType,
            AnalysisDate = a.AnalysisDate.ToString("yyyy-MM-dd"),
            XrayFileUrl  = a.XrayFileUrl,
            IsAutoTraced = a.IsAutoTraced,
            AiAssisted   = a.AiAssisted,
            DoctorId     = a.DoctorId,
            Notes        = a.Notes,
            PixelsPerMm  = pixelsPerMm,
            ImageWidth   = imageWidth,
            ImageHeight  = imageHeight,
            Landmarks    = a.Landmarks
                .Where(l => l.IsActive)
                .Select(l => new CephLandmarkDto
                {
                    Id         = l.Id,
                    Key        = l.LandmarkKey,
                    Name       = l.LandmarkName,
                    X          = (double)(l.XCoord ?? 0),
                    Y          = (double)(l.YCoord ?? 0),
                    IsAiPlaced = l.IsAiPlaced,
                    Confidence = l.Confidence.HasValue ? (double)l.Confidence.Value : null
                })
                .ToList(),
            Measurements = a.Measurements
                .Where(m => m.IsActive)
                .Select(m =>
                {
                    // Compute raw deviation from stored value/normal (more reliable than stored Deviation column).
                    double? rawDev = (m.MeasurementValue.HasValue && m.NormalValue.HasValue)
                        ? (double)(m.MeasurementValue.Value - m.NormalValue.Value)
                        : null;
                    double sd = m.StdDeviation.HasValue ? (double)m.StdDeviation.Value : 1.0;
                    double? sdNorm = rawDev.HasValue && sd > 0 ? rawDev.Value / sd : null;
                    string severity = m.Classification ?? (sdNorm.HasValue ? ClassifyDeviation(sdNorm.Value) : "normal");
                    string direction = rawDev.HasValue
                        ? (rawDev.Value > 0.001 ? "above" : rawDev.Value < -0.001 ? "below" : "within")
                        : "within";

                    return new CephMeasurementDto
                    {
                        Name          = m.MeasurementName,
                        NameAr        = GetMeasurementNameAr(m.MeasurementName),
                        Value         = m.MeasurementValue.HasValue ? (double)m.MeasurementValue.Value : null,
                        Normal        = m.NormalValue.HasValue ? (double)m.NormalValue.Value : null,
                        StdDev        = sd,
                        Unit          = m.Unit,
                        Deviation     = rawDev,
                        Severity      = severity,
                        Direction     = direction,
                        AnalysisGroup = GetMeasurementGroup(m.MeasurementName),
                        InterpretationAr = rawDev.HasValue
                            ? GetInterpretationAr(m.MeasurementName, rawDev.Value, severity)
                            : null
                    };
                })
                .ToList(),
            Diagnosis = a.Diagnosis is null ? null : new CephDiagnosisDto
            {
                SkeletalClass      = a.Diagnosis.SkeletalClass,
                VerticalPattern    = a.Diagnosis.VerticalPattern,
                IncisorInclination = a.Diagnosis.IncisorInclination,
                SoftTissueSummary  = a.Diagnosis.SoftTissueSummary,
                AiRecommendation   = a.Diagnosis.AiRecommendation,
                DoctorApproved     = a.Diagnosis.DoctorApproved,
                FinalDiagnosis     = a.Diagnosis.FinalDiagnosis
            }
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  STATIC GEOMETRY HELPERS  (tuple-accepting overloads for cleaner code)
    // ──────────────────────────────────────────────────────────────────────────

    // Angle at vertex between rays vertex→p1 and vertex→p3, range [0,180]°.
    private static double Angle3T(
        (double x, double y) p1,
        (double x, double y) vertex,
        (double x, double y) p3)
    {
        double ax = p1.x - vertex.x, ay = p1.y - vertex.y;
        double bx = p3.x - vertex.x, by = p3.y - vertex.y;
        double dot  = ax * bx + ay * by;
        double magA = Math.Sqrt(ax * ax + ay * ay);
        double magB = Math.Sqrt(bx * bx + by * by);
        if (magA < 1e-9 || magB < 1e-9) return 0;
        return Math.Acos(Math.Clamp(dot / (magA * magB), -1, 1)) * 180.0 / Math.PI;
    }

    // Acute angle (0–90°) between two line segments.
    private static double ABLines(
        (double x, double y) l1p1, (double x, double y) l1p2,
        (double x, double y) l2p1, (double x, double y) l2p2)
    {
        double d1x = l1p2.x - l1p1.x, d1y = l1p2.y - l1p1.y;
        double d2x = l2p2.x - l2p1.x, d2y = l2p2.y - l2p1.y;
        double mag1 = Math.Sqrt(d1x * d1x + d1y * d1y);
        double mag2 = Math.Sqrt(d2x * d2x + d2y * d2y);
        if (mag1 < 1e-9 || mag2 < 1e-9) return 0;
        double cosA = Math.Clamp(Math.Abs(d1x * d2x + d1y * d2y) / (mag1 * mag2), 0, 1);
        return Math.Acos(cosA) * 180.0 / Math.PI;
    }

    // Signed perpendicular distance (positive = point is to the right of lp1→lp2).
    private static double SignedPerp(
        double px, double py,
        double lx1, double ly1, double lx2, double ly2)
    {
        double dx = lx2 - lx1, dy = ly2 - ly1;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-9) return 0;
        return ((px - lx1) * dy - (py - ly1) * dx) / len;
    }

    private static double SignedPerpT(
        (double x, double y) pt,
        (double x, double y) lp1,
        (double x, double y) lp2)
        => SignedPerp(pt.x, pt.y, lp1.x, lp1.y, lp2.x, lp2.y);

    // Euclidean distance.
    private static double DistT((double x, double y) a, (double x, double y) b)
        => Math.Sqrt((b.x - a.x) * (b.x - a.x) + (b.y - a.y) * (b.y - a.y));

    private static string ClassifyDeviation(double deviationInSd)
    {
        double abs = Math.Abs(deviationInSd);
        if (abs <= 1.0) return "normal";
        if (abs <= 2.0) return "mild";
        return "severe";
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  LOOKUP TABLES
    // ──────────────────────────────────────────────────────────────────────────
    private static HashSet<string> GetAnalysisGroups(string analysisType) => analysisType switch
    {
        "steiner"  => ["steiner"],
        "tweed"    => ["tweed"],
        "mcnamara" => ["mcnamara"],
        "ricketts" => ["ricketts"],
        "downs"    => ["downs"],
        "wits"     => ["wits"],
        _          => ["steiner", "tweed", "mcnamara", "ricketts", "downs", "wits"]
    };

    private static string GetMeasurementGroup(string name) => name switch
    {
        "FMA" or "FMIA" or "IMPA"                                      => "tweed",
        "Co-A" or "Co-Gn" or "ANS-Me"                                  => "mcnamara",
        "Facial-Depth" or "Facial-Axis" or "Mandibular-Plane"
            or "Convexity-A" or "L1-APog_mm" or "L1-APog_angle"
            or "Upper-Lip-ELine" or "Lower-Lip-ELine" or "Nasolabial"  => "ricketts",
        "Convexity" or "AB-FacialPlane" or "Y-Axis"
            or "Facial-Plane-FH" or "Mandibular-FH"                    => "downs",
        "Wits"                                                          => "wits",
        _                                                               => "steiner"
    };

    private static string GetMeasurementNameAr(string name) => name switch
    {
        // Steiner
        "SNA"           => "زاوية SNA",
        "SNB"           => "زاوية SNB",
        "ANB"           => "زاوية ANB",
        "SND"           => "زاوية SND",
        "U1-NA_angle"   => "U1/NA (°)",
        "U1-NA_mm"      => "U1/NA (mm)",
        "L1-NB_angle"   => "L1/NB (°)",
        "L1-NB_mm"      => "L1/NB (mm)",
        "U1-L1"         => "زاوية القاطعين",
        "GoGn-SN"       => "GoGn / SN",
        "UL-SLine"      => "الشفة العلوية — خط S",
        "LL-SLine"      => "الشفة السفلية — خط S",
        // Tweed
        "FMA"           => "FMA (فرانكفورت-فك سفلي)",
        "FMIA"          => "FMIA (فرانكفورت-قاطعة سفلية)",
        "IMPA"          => "IMPA (فك سفلي-قاطعة سفلية)",
        // McNamara
        "Co-A"          => "Co-A (طول الفك العلوي)",
        "Co-Gn"         => "Co-Gn (طول الفك السفلي)",
        "ANS-Me"        => "ANS-Me (ارتفاع الوجه السفلي)",
        // Ricketts
        "Facial-Depth"     => "عمق الوجه (FH-N-Pog)",
        "Facial-Axis"      => "محور الوجه (Pt-Gn / BaN)",
        "Mandibular-Plane" => "ميل مستوى الفك (FH-GoMe)",
        "Convexity-A"      => "انحناء النقطة A (N-Pog)",
        "L1-APog_mm"       => "L1 إلى خط A-Pog (mm)",
        "L1-APog_angle"    => "L1 إلى خط A-Pog (°)",
        "Upper-Lip-ELine"  => "الشفة العلوية إلى خط E",
        "Lower-Lip-ELine"  => "الشفة السفلية إلى خط E",
        "Nasolabial"       => "الزاوية الأنفية-الشفوية",
        // Downs
        "Convexity"        => "انحناء الوجه (N-A-Pog)",
        "AB-FacialPlane"   => "خط A-B إلى مستوى الوجه",
        "Y-Axis"           => "محور Y (S-Gn/FH)",
        "Facial-Plane-FH"  => "مستوى الوجه (N-Pog) / FH",
        "Mandibular-FH"    => "مستوى الفك السفلي / FH",
        // Wits
        "Wits"             => "مسافة وتس (AO-BO)",
        _                  => name
    };

    private static string GetInterpretationAr(string name, double rawDev, string severity)
    {
        if (severity == "normal") return "ضمن الحدود الطبيعية";
        string s = severity == "severe" ? "بشكل واضح" : "بشكل طفيف";
        bool above = rawDev > 0;

        return name switch
        {
            "SNA"           => above ? $"الفك العلوي بارز للأمام {s}" : $"الفك العلوي متراجع للخلف {s}",
            "SNB"           => above ? $"الفك السفلي بارز للأمام {s}" : $"الفك السفلي متراجع للخلف {s}",
            "ANB"           => above ? $"علاقة هيكلية من الدرجة الثانية {s}" : $"علاقة هيكلية من الدرجة الثالثة {s}",
            "SND"           => above ? $"SND مرتفع {s}" : $"SND منخفض {s}",
            "U1-NA_angle"   => above ? $"القاطع العلوي مائل للأمام {s}" : $"القاطع العلوي مائل للخلف {s}",
            "U1-NA_mm"      => above ? $"القاطع العلوي بارز {s}" : $"القاطع العلوي مرتد {s}",
            "L1-NB_angle"   => above ? $"القاطع السفلي مائل للأمام {s}" : $"القاطع السفلي مائل للخلف {s}",
            "L1-NB_mm"      => above ? $"القاطع السفلي بارز {s}" : $"القاطع السفلي مرتد {s}",
            "U1-L1"         => above ? $"زاوية القاطعين مفتوحة (قواطع مرتدة) {s}" : $"زاوية القاطعين ضيقة (بروز سني) {s}",
            "GoGn-SN"       => above ? $"نمط رأسي مرتفع (وجه طويل) {s}" : $"نمط رأسي منخفض (وجه قصير) {s}",
            "UL-SLine"      => above ? $"الشفة العلوية بارزة أمام خط S {s}" : $"الشفة العلوية مرتدة خلف خط S {s}",
            "LL-SLine"      => above ? $"الشفة السفلية بارزة أمام خط S {s}" : $"الشفة السفلية مرتدة خلف خط S {s}",
            "FMA"           => above ? $"ميل الفك السفلي مرتفع — نمط رأسي {s}" : $"ميل الفك السفلي منخفض — نمط أفقي {s}",
            "FMIA"          => above ? $"القاطع السفلي مائل للخلف نسبة لـFH {s}" : $"القاطع السفلي بارز للأمام نسبة لـFH {s}",
            "IMPA"          => above ? $"القاطع السفلي منتصب بشكل زائد {s}" : $"القاطع السفلي مائل للخلف {s}",
            "Co-A"          => above ? $"طول الفك العلوي أكبر من المعدل {s}" : $"طول الفك العلوي أقل من المعدل {s}",
            "Co-Gn"         => above ? $"طول الفك السفلي أكبر من المعدل {s}" : $"طول الفك السفلي أقل من المعدل {s}",
            "ANS-Me"        => above ? $"ارتفاع الوجه السفلي مرتفع {s}" : $"ارتفاع الوجه السفلي منخفض {s}",
            "Facial-Depth"  => above ? $"الوجه أكثر بروزاً من المعدل {s}" : $"الوجه أكثر تراجعاً من المعدل {s}",
            "Facial-Axis"   => above ? $"محور نمو أمامي متزايد {s}" : $"محور نمو خلفي متزايد {s}",
            "Mandibular-Plane" => above ? $"مستوى الفك السفلي مائل بشكل مرتفع {s}" : $"مستوى الفك السفلي مسطّح {s}",
            "Convexity-A"   => above ? $"بروز عظمي واضح (Class II هيكلي) {s}" : $"تراجع عظمي (Class III هيكلي) {s}",
            "L1-APog_mm"    => above ? $"القاطع السفلي بارز عن خط A-Pog {s}" : $"القاطع السفلي مرتد عن خط A-Pog {s}",
            "L1-APog_angle" => above ? $"القاطع السفلي مائل للأمام {s}" : $"القاطع السفلي مائل للخلف {s}",
            "Upper-Lip-ELine" => above ? $"الشفة العلوية بارزة للأمام {s}" : $"الشفة العلوية مرتدة للخلف {s}",
            "Lower-Lip-ELine" => above ? $"الشفة السفلية بارزة للأمام {s}" : $"الشفة السفلية مرتدة للخلف {s}",
            "Nasolabial"    => above ? $"الزاوية الأنفية مفتوحة (ميل شفوي) {s}" : $"الزاوية الأنفية ضيقة (بروز شفوي) {s}",
            "Convexity"     => above ? $"بروز هيكلي للوجه {s}" : $"تراجع هيكلي للوجه {s}",
            "AB-FacialPlane" => above ? $"الفك السفلي بارز {s}" : $"الفك السفلي متراجع {s}",
            "Y-Axis"        => above ? $"نمط نمو رأسي {s}" : $"نمط نمو أفقي {s}",
            "Facial-Plane-FH" => above ? $"مستوى الوجه بزاوية أعلى {s}" : $"مستوى الوجه بزاوية أقل {s}",
            "Mandibular-FH" => above ? $"مستوى الفك السفلي مرتفع {s}" : $"مستوى الفك السفلي منخفض {s}",
            "Wits"          => above ? $"تنافر هيكلي من الصنف الثاني (AO أمام BO) {s}" : $"تنافر هيكلي من الصنف الثالث (BO أمام AO) {s}",
            _               => above ? $"أعلى من المعدل {s}" : $"أقل من المعدل {s}"
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  NOTES JSON HELPER
    // ──────────────────────────────────────────────────────────────────────────
    private static string? ExtractUserNotes(string? notesJson)
    {
        if (string.IsNullOrWhiteSpace(notesJson)) return null;
        try
        {
            var nd = JsonSerializer.Deserialize<CephNotesData>(notesJson);
            return nd?.UserNotes;
        }
        catch
        {
            // Plain-text notes from records created before the JSON format.
            return notesJson;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  INNER DTO FOR NOTES JSON
    // ──────────────────────────────────────────────────────────────────────────
    private sealed class CephNotesData
    {
        public double PixelsPerMm { get; set; } = 1.0;
        public int    ImageWidth  { get; set; }
        public int    ImageHeight { get; set; }
        public string? UserNotes  { get; set; }
    }
}
