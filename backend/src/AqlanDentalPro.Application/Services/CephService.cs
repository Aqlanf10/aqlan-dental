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
    public async Task<List<CephAnalysisListDto>> ListAsync(Guid orthoCaseId)
    {
        return await db.CephAnalyses
            .Where(a => a.OrthoCaseId == orthoCaseId)
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

        // Build landmark dictionary.
        var lm = analysis.Landmarks
            .Where(l => l.IsActive)
            .ToDictionary(
                l => l.LandmarkKey,
                l => (x: (double)(l.XCoord ?? 0), y: (double)(l.YCoord ?? 0)));

        // Parse calibration from Notes.
        double pixelsPerMm = 1.0;
        if (!string.IsNullOrWhiteSpace(analysis.Notes))
        {
            try
            {
                var nd = JsonSerializer.Deserialize<CephNotesData>(analysis.Notes);
                if (nd is not null && nd.PixelsPerMm > 0)
                    pixelsPerMm = nd.PixelsPerMm;
            }
            catch { /* Notes may be plain text on older records; keep default. */ }
        }

        var results = new List<(string name, string group, double value, double normal, double sd, string unit)>();

        // ── Steiner Analysis ────────────────────────────────────────────────
        if (Has(lm, "S", "N", "A"))
            results.Add(("SNA", "steiner",
                Angle3(lm["S"].x, lm["S"].y, lm["N"].x, lm["N"].y, lm["A"].x, lm["A"].y),
                82, 2, "°"));

        if (Has(lm, "S", "N", "B"))
            results.Add(("SNB", "steiner",
                Angle3(lm["S"].x, lm["S"].y, lm["N"].x, lm["N"].y, lm["B"].x, lm["B"].y),
                80, 2, "°"));

        if (Has(lm, "S", "N", "A", "B"))
        {
            double sna = Angle3(lm["S"].x, lm["S"].y, lm["N"].x, lm["N"].y, lm["A"].x, lm["A"].y);
            double snb = Angle3(lm["S"].x, lm["S"].y, lm["N"].x, lm["N"].y, lm["B"].x, lm["B"].y);
            results.Add(("ANB", "steiner", sna - snb, 2, 1, "°"));
        }

        if (Has(lm, "S", "N", "D"))
            results.Add(("SND", "steiner",
                Angle3(lm["S"].x, lm["S"].y, lm["N"].x, lm["N"].y, lm["D"].x, lm["D"].y),
                76, 2, "°"));

        if (Has(lm, "N", "A", "U1T", "U1A"))
        {
            // U1-NA angle: angle of U1 axis to N-A line.
            double u1naAngle = AngleBetweenLines(
                lm["N"].x, lm["N"].y, lm["A"].x, lm["A"].y,
                lm["U1A"].x, lm["U1A"].y, lm["U1T"].x, lm["U1T"].y);
            results.Add(("U1-NA°", "steiner", u1naAngle, 22, 2, "°"));

            // U1-NA mm: perpendicular distance from U1T to N-A line.
            double u1naMm = Math.Abs(PerpDist(lm["U1T"].x, lm["U1T"].y,
                lm["N"].x, lm["N"].y, lm["A"].x, lm["A"].y)) / pixelsPerMm;
            results.Add(("U1-NA mm", "steiner", u1naMm, 4, 2, "mm"));
        }

        if (Has(lm, "N", "B", "L1T", "L1A"))
        {
            double l1nbAngle = AngleBetweenLines(
                lm["N"].x, lm["N"].y, lm["B"].x, lm["B"].y,
                lm["L1A"].x, lm["L1A"].y, lm["L1T"].x, lm["L1T"].y);
            results.Add(("L1-NB°", "steiner", l1nbAngle, 25, 2, "°"));

            double l1nbMm = Math.Abs(PerpDist(lm["L1T"].x, lm["L1T"].y,
                lm["N"].x, lm["N"].y, lm["B"].x, lm["B"].y)) / pixelsPerMm;
            results.Add(("L1-NB mm", "steiner", l1nbMm, 4, 2, "mm"));
        }

        if (Has(lm, "U1T", "U1A", "L1T", "L1A"))
        {
            // U1-L1 interincisal angle: angle between the two incisor axes.
            double interIncisal = 180.0 - AngleBetweenLines(
                lm["U1A"].x, lm["U1A"].y, lm["U1T"].x, lm["U1T"].y,
                lm["L1A"].x, lm["L1A"].y, lm["L1T"].x, lm["L1T"].y);
            results.Add(("U1-L1", "steiner", interIncisal, 131, 6, "°"));
        }

        if (Has(lm, "Go", "Gn", "S", "N"))
        {
            double goGnSn = AngleBetweenLines(
                lm["S"].x, lm["S"].y, lm["N"].x, lm["N"].y,
                lm["Go"].x, lm["Go"].y, lm["Gn"].x, lm["Gn"].y);
            results.Add(("GoGn-SN", "steiner", goGnSn, 32, 6, "°"));
        }

        // ── Tweed Analysis ──────────────────────────────────────────────────
        // FH plane defined by Or (Orbitale) and Po (Porion).
        // Mandibular plane defined by Go and Me.
        if (Has(lm, "Or", "Po", "Go", "Me"))
        {
            double fma = AngleBetweenLines(
                lm["Or"].x, lm["Or"].y, lm["Po"].x, lm["Po"].y,
                lm["Go"].x, lm["Go"].y, lm["Me"].x, lm["Me"].y);
            results.Add(("FMA", "tweed", fma, 25, 4, "°"));
        }

        if (Has(lm, "Or", "Po", "L1A", "L1T"))
        {
            // FMIA: FH plane to L1 long axis.
            double fmia = AngleBetweenLines(
                lm["Or"].x, lm["Or"].y, lm["Po"].x, lm["Po"].y,
                lm["L1A"].x, lm["L1A"].y, lm["L1T"].x, lm["L1T"].y);
            results.Add(("FMIA", "tweed", fmia, 65, 5, "°"));
        }

        if (Has(lm, "Go", "Me", "L1A", "L1T"))
        {
            // IMPA: mandibular plane to L1 long axis.
            double impa = AngleBetweenLines(
                lm["Go"].x, lm["Go"].y, lm["Me"].x, lm["Me"].y,
                lm["L1A"].x, lm["L1A"].y, lm["L1T"].x, lm["L1T"].y);
            results.Add(("IMPA", "tweed", impa, 90, 4, "°"));
        }

        // ── McNamara Analysis (mm) ──────────────────────────────────────────
        if (Has(lm, "Co", "A"))
        {
            double coA = Distance(lm["Co"].x, lm["Co"].y, lm["A"].x, lm["A"].y) / pixelsPerMm;
            results.Add(("Co-A", "mcnamara", coA, 90, 5, "mm"));
        }

        if (Has(lm, "Co", "Gn"))
        {
            double coGn = Distance(lm["Co"].x, lm["Co"].y, lm["Gn"].x, lm["Gn"].y) / pixelsPerMm;
            results.Add(("Co-Gn", "mcnamara", coGn, 120, 6, "mm"));
        }

        // ── Persist results ─────────────────────────────────────────────────
        var oldMeasurements = await db.CephMeasurements
            .Where(m => m.AnalysisId == id)
            .ToListAsync();
        foreach (var m in oldMeasurements) m.IsActive = false;

        foreach (var (name, group, value, normal, sd, unit) in results)
        {
            double deviation = sd > 0 ? (value - normal) / sd : 0;
            string classification = ClassifyDeviation(deviation);

            db.CephMeasurements.Add(new CephMeasurement
            {
                AnalysisId      = id,
                MeasurementName = name,
                MeasurementValue = (decimal)Math.Round(value, 2),
                NormalValue     = (decimal)normal,
                StdDeviation    = (decimal)sd,
                Unit            = unit,
                Deviation       = (decimal)Math.Round(deviation, 2),
                Classification  = classification
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

        // ── Incisor Inclination (from U1-NA° and L1-NB°) ─────────────────
        double? u1na = GetValue("U1-NA°");
        double? l1nb = GetValue("L1-NB°");
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
                .Select(m => new CephMeasurementDto
                {
                    Name          = m.MeasurementName,
                    NameAr        = GetMeasurementNameAr(m.MeasurementName),
                    Value         = m.MeasurementValue.HasValue ? (double)m.MeasurementValue.Value : null,
                    Normal        = m.NormalValue.HasValue ? (double)m.NormalValue.Value : null,
                    StdDev        = m.StdDeviation.HasValue ? (double)m.StdDeviation.Value : null,
                    Unit          = m.Unit,
                    Deviation     = m.Deviation.HasValue ? (double)m.Deviation.Value : null,
                    Status        = m.Classification ?? "normal",
                    AnalysisGroup = GetMeasurementGroup(m.MeasurementName),
                    InterpretationAr = GetInterpretationAr(m.MeasurementName,
                        m.MeasurementValue.HasValue ? (double)m.MeasurementValue.Value : null,
                        m.Deviation.HasValue ? (double)m.Deviation.Value : null)
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
    //  STATIC GEOMETRY HELPERS
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Angle at vertex (x2,y2) in the triangle formed by points 1-2-3, in degrees.
    /// </summary>
    private static double Angle3(
        double x1, double y1,
        double x2, double y2,
        double x3, double y3)
    {
        double ax = x1 - x2, ay = y1 - y2;
        double bx = x3 - x2, by = y3 - y2;
        double dot  = ax * bx + ay * by;
        double magA = Math.Sqrt(ax * ax + ay * ay);
        double magB = Math.Sqrt(bx * bx + by * by);
        if (magA < 1e-9 || magB < 1e-9) return 0;
        double cosA = Math.Clamp(dot / (magA * magB), -1, 1);
        return Math.Acos(cosA) * 180.0 / Math.PI;
    }

    /// <summary>
    /// Acute angle (0-90°) between two line segments, in degrees.
    /// </summary>
    private static double AngleBetweenLines(
        double l1x1, double l1y1, double l1x2, double l1y2,
        double l2x1, double l2y1, double l2x2, double l2y2)
    {
        double d1x = l1x2 - l1x1, d1y = l1y2 - l1y1;
        double d2x = l2x2 - l2x1, d2y = l2y2 - l2y1;
        double dot  = d1x * d2x + d1y * d2y;
        double mag1 = Math.Sqrt(d1x * d1x + d1y * d1y);
        double mag2 = Math.Sqrt(d2x * d2x + d2y * d2y);
        if (mag1 < 1e-9 || mag2 < 1e-9) return 0;
        double cosA = Math.Clamp(Math.Abs(dot) / (mag1 * mag2), 0, 1);
        return Math.Acos(cosA) * 180.0 / Math.PI;
    }

    /// <summary>
    /// Perpendicular distance from point (px,py) to line defined by (lx1,ly1)-(lx2,ly2).
    /// </summary>
    private static double PerpDist(
        double px, double py,
        double lx1, double ly1, double lx2, double ly2)
    {
        double dx = lx2 - lx1, dy = ly2 - ly1;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-9) return 0;
        return (dy * px - dx * py + lx2 * ly1 - ly2 * lx1) / len;
    }

    /// <summary>
    /// Euclidean distance between two points.
    /// </summary>
    private static double Distance(double x1, double y1, double x2, double y2)
        => Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));

    private static bool Has(Dictionary<string, (double x, double y)> lm, params string[] keys)
        => keys.All(lm.ContainsKey);

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
    private static string GetMeasurementGroup(string name) => name switch
    {
        "FMA" or "FMIA" or "IMPA"                        => "tweed",
        "Co-A" or "Co-Gn"                                => "mcnamara",
        _                                                 => "steiner"
    };

    private static string GetMeasurementNameAr(string name) => name switch
    {
        "SNA"       => "زاوية SNA",
        "SNB"       => "زاوية SNB",
        "ANB"       => "زاوية ANB",
        "SND"       => "زاوية SND",
        "U1-NA°"    => "زاوية القاطعة العلوية-NA",
        "U1-NA mm"  => "بعد القاطعة العلوية عن NA",
        "L1-NB°"    => "زاوية القاطعة السفلية-NB",
        "L1-NB mm"  => "بعد القاطعة السفلية عن NB",
        "U1-L1"     => "الزاوية بين القاطعتين",
        "GoGn-SN"   => "زاوية قاعدة الفك-SN",
        "FMA"       => "زاوية فرانكفورت-فك سفلي",
        "FMIA"      => "زاوية فرانكفورت-قاطعة سفلية",
        "IMPA"      => "زاوية فك سفلي-قاطعة سفلية",
        "Co-A"      => "مسافة Co-A",
        "Co-Gn"     => "مسافة Co-Gn",
        _           => name
    };

    private static string GetInterpretationAr(string name, double? value, double? deviationSd)
    {
        if (value is null || deviationSd is null) return string.Empty;

        string status = ClassifyDeviation(deviationSd.Value);
        bool high = deviationSd.Value > 0;

        return name switch
        {
            "SNA" => status == "normal"
                ? "موضع الفك العلوي طبيعي"
                : high ? "بروز الفك العلوي للأمام" : "تراجع الفك العلوي للخلف",

            "SNB" => status == "normal"
                ? "موضع الفك السفلي طبيعي"
                : high ? "بروز الفك السفلي للأمام" : "تراجع الفك السفلي للخلف",

            "ANB" => status == "normal"
                ? "علاقة هيكلية من الصنف الأول"
                : high ? "علاقة هيكلية من الصنف الثاني" : "علاقة هيكلية من الصنف الثالث",

            "GoGn-SN" or "FMA" => status == "normal"
                ? "نمط رأسي طبيعي"
                : high ? "نمط مرتفع (وجه طويل)" : "نمط منخفض (وجه قصير)",

            "U1-NA°" or "U1-NA mm" => status == "normal"
                ? "ميلان القاطعة العلوية طبيعي"
                : high ? "بروز القاطعة العلوية" : "ارتداد القاطعة العلوية",

            "L1-NB°" or "L1-NB mm" => status == "normal"
                ? "ميلان القاطعة السفلية طبيعي"
                : high ? "بروز القاطعة السفلية" : "ارتداد القاطعة السفلية",

            "U1-L1" => status == "normal"
                ? "الزاوية بين القاطعتين طبيعية"
                : high ? "زاوية القاطعتين مفتوحة (الأسنان مائلة للخلف)" : "زاوية القاطعتين ضيقة (بروز سني)",

            "IMPA" => status == "normal"
                ? "ميلان القاطعة السفلية نسبة للفك طبيعي"
                : high ? "بروز القاطعة السفلية" : "ارتداد القاطعة السفلية",

            _ => string.Empty
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
