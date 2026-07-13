using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AqlanDentalPro.Application.Services;

public class CephService(AppDbContext db, ICurrentUserService currentUser, ILogger<CephService> logger)
{
    public const string VtoDisclaimerAr =
        "هذا هدف علاجي بصري من حركات أدخلها الطبيب، وليس تنبؤاً بالاستجابة الحيوية أو الأنسجة الرخوة.";
    // ──────────────────────────────────────────────────────────────────────────
    //  LIST
    // ──────────────────────────────────────────────────────────────────────────
    public async Task<List<CephAnalysisListDto>> ListAsync(
        Guid? orthoCaseId,
        HashSet<Guid>? accessiblePatientIds = null)
    {
        var rows = await db.CephAnalyses
            .Where(a => orthoCaseId == null || a.OrthoCaseId == orthoCaseId)
            .Where(a => accessiblePatientIds == null ||
                        accessiblePatientIds.Contains(a.OrthoCase.PatientId))
            // Same ordering the deck generator uses to pick the "latest" analysis
            // (OrthoCasePresentationService): AnalysisDate DESC, then CreatedAt DESC.
            // Without the CreatedAt tiebreak, same-day analyses ordered arbitrarily.
            .OrderByDescending(a => a.AnalysisDate)
            .ThenByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.Id,
                a.OrthoCaseId,
                a.OrthoCase.CaseNumber,
                PatientName = a.OrthoCase.Patient.FirstName + " " + a.OrthoCase.Patient.LastName,
                a.AnalysisType,
                AnalysisDate = a.AnalysisDate.ToString("yyyy-MM-dd"),
                a.XrayFileUrl,
                a.AiAssisted,
                LandmarkCount = a.Landmarks.Count(l => l.IsActive),
                HasMeasurements = a.Measurements.Any(m => m.IsActive),
                a.IsApproved,
                a.Notes,
                a.CreatedAt,
                DiagnosisApproved = a.Diagnosis != null && a.Diagnosis.IsActive && a.Diagnosis.DoctorApproved,
                SkeletalClass = a.Diagnosis == null ? null : a.Diagnosis.SkeletalClass,
                VerticalPattern = a.Diagnosis == null ? null : a.Diagnosis.VerticalPattern,
                IncisorInclination = a.Diagnosis == null ? null : a.Diagnosis.IncisorInclination,
            })
            .ToListAsync();

        return rows.Select(a => new CephAnalysisListDto
        {
            Id = a.Id,
            OrthoCaseId = a.OrthoCaseId,
            CaseNumber = a.CaseNumber,
            PatientName = a.PatientName,
            AnalysisType = a.AnalysisType,
            AnalysisDate = a.AnalysisDate,
            XrayFileUrl = a.XrayFileUrl,
            AiAssisted = a.AiAssisted,
            LandmarkCount = a.LandmarkCount,
            HasMeasurements = a.HasMeasurements,
            IsApproved = a.IsApproved,
            Notes = a.Notes,
            CreatedAt = a.CreatedAt,
            ClinicalTags = CephClinicalTagCatalog.Build(
                a.IsApproved,
                a.DiagnosisApproved,
                a.SkeletalClass,
                a.VerticalPattern,
                a.IncisorInclination),
        }).ToList();
    }

    public async Task<CephCohortResultDto> BuildCohortAsync(
        HashSet<Guid>? accessiblePatientIds,
        DateOnly? from,
        DateOnly? to,
        string? analysisType,
        string? tag)
    {
        var query = db.CephAnalyses
            .AsNoTracking()
            .Include(a => a.OrthoCase)
            .Include(a => a.Diagnosis)
            .Include(a => a.Measurements)
            .Where(a => a.IsApproved)
            .Where(a => a.Diagnosis != null && a.Diagnosis.IsActive && a.Diagnosis.DoctorApproved)
            .Where(a => accessiblePatientIds == null ||
                        accessiblePatientIds.Contains(a.OrthoCase.PatientId));

        if (from.HasValue) query = query.Where(a => a.AnalysisDate >= from.Value);
        if (to.HasValue) query = query.Where(a => a.AnalysisDate <= to.Value);
        if (!string.IsNullOrWhiteSpace(analysisType))
            query = query.Where(a => a.AnalysisType == analysisType);

        var records = await query.ToListAsync();
        var latestPerPatient = records
            .GroupBy(a => a.OrthoCase.PatientId)
            .Select(group => group
                .OrderByDescending(a => a.AnalysisDate)
                .ThenByDescending(a => a.CreatedAt)
                .First())
            .ToList();

        if (!string.IsNullOrWhiteSpace(tag))
        {
            latestPerPatient = latestPerPatient
                .Where(a => CephClinicalTagCatalog.Build(
                    a.IsApproved,
                    a.Diagnosis?.DoctorApproved == true,
                    a.Diagnosis?.SkeletalClass,
                    a.Diagnosis?.VerticalPattern,
                    a.Diagnosis?.IncisorInclination)
                    .Any(item => item.Key == tag))
                .ToList();
        }

        var result = new CephCohortResultDto
        {
            Suppressed = latestPerPatient.Count < CephCohortResultDto.PrivacyMinimum,
            CohortSize = latestPerPatient.Count < CephCohortResultDto.PrivacyMinimum
                ? null
                : latestPerPatient.Count,
            FromDate = from?.ToString("yyyy-MM-dd"),
            ToDate = to?.ToString("yyyy-MM-dd"),
            AnalysisType = analysisType,
            Tag = tag,
            AvailableTags = CephClinicalTagCatalog.All(),
        };

        if (result.Suppressed) return result;

        var tags = latestPerPatient
            .SelectMany(a => CephClinicalTagCatalog.Build(
                a.IsApproved,
                a.Diagnosis?.DoctorApproved == true,
                a.Diagnosis?.SkeletalClass,
                a.Diagnosis?.VerticalPattern,
                a.Diagnosis?.IncisorInclination))
            .ToList();

        result.SkeletalDistribution = BuildDistribution(tags, "skeletal", latestPerPatient.Count);
        result.VerticalDistribution = BuildDistribution(tags, "vertical", latestPerPatient.Count);
        result.IncisorDistribution = BuildDistribution(tags, "incisor", latestPerPatient.Count);
        result.Measurements = BuildMeasurementSummaries(latestPerPatient);
        return result;
    }

    private static List<CephCohortBucketDto> BuildDistribution(
        IEnumerable<CephClinicalTagDto> tags,
        string group,
        int cohortSize) =>
        tags.Where(tag => tag.Group == group)
            .GroupBy(tag => new { tag.Key, tag.Label })
            .Select(bucket => new CephCohortBucketDto
            {
                Key = bucket.Key.Key,
                Label = bucket.Key.Label,
                Count = bucket.Count(),
                Percentage = decimal.Round(bucket.Count() * 100m / cohortSize, 1),
            })
            .OrderByDescending(bucket => bucket.Count)
            .ThenBy(bucket => bucket.Label)
            .ToList();

    private static List<CephCohortMeasurementDto> BuildMeasurementSummaries(
        IReadOnlyCollection<CephAnalysis> analyses)
    {
        var grouped = analyses.SelectMany(a => a.Measurements
                .Where(m => m.IsActive && m.MeasurementValue.HasValue)
                .Select(m => new
                {
                    AnalysisId = a.Id,
                    m.MeasurementName,
                    Unit = m.Unit ?? string.Empty,
                    Value = m.MeasurementValue!.Value,
                }))
            .GroupBy(item => new { item.MeasurementName, item.Unit });

        var result = new List<CephCohortMeasurementDto>();
        foreach (var group in grouped)
        {
            // One value per latest patient record. Duplicate measurement rows
            // must never satisfy the five-patient privacy threshold.
            var values = group.GroupBy(item => item.AnalysisId)
                .Select(rows => rows.First().Value)
                .ToArray();
            if (values.Length < CephCohortResultDto.PrivacyMinimum) continue;

            var mean = values.Average();
            var variance = values.Average(value => (value - mean) * (value - mean));
            result.Add(new CephCohortMeasurementDto
            {
                MeasurementName = group.Key.MeasurementName,
                Unit = group.Key.Unit,
                Count = values.Length,
                Mean = decimal.Round(mean, 2),
                Minimum = decimal.Round(values.Min(), 2),
                Maximum = decimal.Round(values.Max(), 2),
                StandardDeviation = decimal.Round((decimal)Math.Sqrt((double)variance), 2),
            });
        }

        return result.OrderBy(item => item.MeasurementName).Take(40).ToList();
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
        var detail = MapDetail(a);

        // Resolve the approver's display name for the UI (best-effort, non-fatal).
        if (a.ApprovedByUserId is Guid approverId)
        {
            detail.ApprovedByName = await db.Users
                .Where(u => u.Id == approverId)
                .Select(u => u.Doctor != null ? u.Doctor.Name : u.Username)
                .FirstOrDefaultAsync();
        }

        return detail;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  CREATE
    // ──────────────────────────────────────────────────────────────────────────
    public async Task<CephAnalysisDetailDto> CreateAsync(CreateCephAnalysisRequest req)
    {
        // CephAnalysis.DoctorId references Doctors.Id, NOT Users.Id — resolve
        // the current user's Doctor row (null for non-doctor users like Admin).
        Guid? doctorId = null;
        if (currentUser.UserId is Guid userId)
            doctorId = await db.Doctors
                .Where(d => d.UserId == userId && d.IsActive)
                .Select(d => (Guid?)d.Id)
                .FirstOrDefaultAsync();

        var analysis = new CephAnalysis
        {
            OrthoCaseId  = req.OrthoCaseId,
            AnalysisType = req.AnalysisType,
            XrayFileUrl  = req.XrayFileUrl,
            AiAssisted   = req.AiAssisted,
            DoctorId     = doctorId,
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

        // CLIN-10: Load patient separately (avoid ThenInclude on InMemory which can fail
        // when the OrthoCase navigation is null in test data).
        Patient? patient = null;
        if (analysis.OrthoCaseId != Guid.Empty)
        {
            var orthoCase = await db.OrthoCases
                .FirstOrDefaultAsync(o => o.Id == analysis.OrthoCaseId);
            if (orthoCase != null)
            {
                patient = await db.Patients
                    .FirstOrDefaultAsync(p => p.Id == orthoCase.PatientId);
            }
        }
        int? patientAge = null;
        if (patient?.DateOfBirth is { } dob)
        {
            // CLIN-10: ClinicTimeProvider.ClinicToday respects the configured
            // clinic timezone (Asia/Aden, UTC+3) — DateTime.Today on Railway
            // (UTC) was returning the wrong calendar day for evening clinics.
            var today = ClinicTimeProvider.ClinicToday();
            patientAge = today.Year - dob.Year;
            if (dob > today.AddYears(-patientAge.Value)) patientAge--;
        }
        // CLIN-10: map Patient.Gender enum → "M"/"F"/null string for the
        // stratified norm lookup. Sex-specific DB norms (Sex="M" or "F") win
        // over sex-null norms for the same age band when the patient sex is
        // known; sex-null norms are the fallback for unknown-sex patients.
        var patientSex = patient?.Gender switch
        {
            Gender.Male   => "M",
            Gender.Female => "F",
            _             => null,
        };
        var isFemale = patient?.Gender == Gender.Female;

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
            catch (Exception ex)
            {
                logger.LogDebug(ex, "[CephService] Failed to parse CephNotesData pixelsPerMm");
            }
        bool cal = pixelsPerMm > 0;
        bool Has(params string[] keys) => keys.All(lm.ContainsKey);
        (double x, double y) G(string k) => lm[k];
        double R1(double v) => Math.Round(v, 1);

        // Load configurable norms once per call. The hardcoded values passed to
        // Add(...) below remain as FALLBACK when a norm row is missing — the
        // computation must never break because of missing configuration.
        // CLIN-10: keep the full list (multiple stratified rows may exist per
        // measurement/group now) and pick the best match per Add() call via
        // FindBestCephNorm — sex-specific+age-matched > sex-null+age-matched >
        // un-stratified fallback > null (caller uses hardcoded default).
        var dbNorms = await db.CephNorms.ToListAsync();

        var results = new List<(string name, string group, double value, double? normal, double? sd, string unit, decimal? min, decimal? max)>();
        void Add(string name, string group, double value, double normal, double sd, string unit)
        {
            decimal? min = null, max = null;
            // CLIN-10: Apply age/sex adjustments to the FALLBACK norms (before DB lookup).
            if (patientAge.HasValue && patientAge <= 12)
            {
                if (name == "ANB") normal += 1.0;
                if (name == "FMA" || name == "MP-SN") normal += 2.0;
            }
            if (isFemale)
            {
                if (name == "SNA" || name == "SNB") normal -= 1.0;
                if (name == "Wits") normal -= 1.0;
            }
            // CLIN-10: best-match lookup by patient age + sex. Returns null
            // when no stratified row matches the patient's age band AND no
            // un-stratified fallback exists — in that case the age/sex-adjusted
            // hardcoded fallback above is used (±2SD range).
            var norm = FindBestCephNorm(dbNorms, name, group, patientAge, patientSex);
            if (norm is not null)
            {
                normal = (double)norm.NormalValue;
                sd     = (double)norm.StdDeviation;
                min    = norm.MinNormal;
                max    = norm.MaxNormal;
            }
            else
            {
                // No DB norm — use the age/sex-adjusted fallback with ±2SD range.
                min = (decimal)(normal - 2 * sd);
                max = (decimal)(normal + 2 * sd);
            }
            results.Add((name, group, R1(value), normal, sd, unit, min, max));
        }
        void AddObserved(string name, double value, string unit) =>
            results.Add((name, "pa", R1(value), null, null, unit, null, null));

        var groups = GetAnalysisGroups(analysis.AnalysisType ?? "full");

        // ── Posteroanterior (PA / Grummons-style asymmetry) ────────────────
        // ZR→ZL is the patient's anatomical right-to-left horizontal axis.
        // Positive lateral values point to the patient's left. The perpendicular
        // axis points inferiorly, so positive cant/asymmetry means left side lower.
        if (groups.Contains("pa") && Has("ZR", "ZL"))
        {
            var zr = G("ZR");
            var zl = G("ZL");
            double hdx = zl.x - zr.x, hdy = zl.y - zr.y;
            double hlen = Math.Sqrt(hdx * hdx + hdy * hdy);
            if (hlen > 1e-9)
            {
                double hx = hdx / hlen, hy = hdy / hlen;
                double vx = -hy, vy = hx;
                double AlongH((double x, double y) p, (double x, double y) origin) =>
                    (p.x - origin.x) * hx + (p.y - origin.y) * hy;
                double AlongV((double x, double y) p, (double x, double y) origin) =>
                    (p.x - origin.x) * vx + (p.y - origin.y) * vy;
                double Cant((double x, double y) right, (double x, double y) left) =>
                    Math.Atan2(AlongV(left, right), AlongH(left, right)) * 180 / Math.PI;

                if (cal && Has("JR", "JL"))
                    AddObserved("PA-JJ-Width", DistT(G("JR"), G("JL")) / pixelsPerMm, "mm");
                if (cal && Has("AgR", "AgL"))
                    AddObserved("PA-AgAg-Width", DistT(G("AgR"), G("AgL")) / pixelsPerMm, "mm");
                if (cal && Has("JR", "JL", "AgR", "AgL"))
                    AddObserved("PA-Width-Differential",
                        (DistT(G("AgR"), G("AgL")) - DistT(G("JR"), G("JL"))) / pixelsPerMm,
                        "mm");

                if (cal && Has("Cg"))
                {
                    foreach (var key in new[] { "ANS", "UDM", "LDM", "Me" })
                        if (Has(key)) AddObserved($"PA-{key}-MSR", AlongH(G(key), G("Cg")) / pixelsPerMm, "mm");
                }

                if (Has("U6R", "U6L")) AddObserved("PA-Maxillary-Cant", Cant(G("U6R"), G("U6L")), "°");
                if (Has("L6R", "L6L")) AddObserved("PA-Mandibular-Cant", Cant(G("L6R"), G("L6L")), "°");
                if (cal && Has("JR", "JL")) AddObserved("PA-J-Vertical-Asymmetry", AlongV(G("JL"), G("JR")) / pixelsPerMm, "mm");
                if (cal && Has("AgR", "AgL")) AddObserved("PA-Ag-Vertical-Asymmetry", AlongV(G("AgL"), G("AgR")) / pixelsPerMm, "mm");
            }
        }

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

            // S-line soft tissue: soft-tissue Pog′ → midpoint(Cm, Pn).
            // SEQ-40: use the SPog landmark when placed; analyses without it keep
            // the historic hard-Pog approximation so saved values don't shift.
            if (Has("Cm","Pn"))
            {
                var sPogKey = Has("SPog") ? "SPog" : "Pog";
                var sLineMid = ((G("Cm").x + G("Pn").x) / 2, (G("Cm").y + G("Pn").y) / 2);
                if (cal && Has("LS", sPogKey))
                    Add("UL-SLine", "steiner",
                        SignedPerp(G("LS").x, G("LS").y,
                                   G(sPogKey).x, G(sPogKey).y, sLineMid.Item1, sLineMid.Item2) / pixelsPerMm,
                        0, 1, "mm");
                if (cal && Has("LI", sPogKey))
                    Add("LL-SLine", "steiner",
                        SignedPerp(G("LI").x, G("LI").y,
                                   G(sPogKey).x, G(sPogKey).y, sLineMid.Item1, sLineMid.Item2) / pixelsPerMm,
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
            // SEQ-40: Ricketts E-plane properly runs Pn → soft-tissue Pogonion —
            // use SPog when placed, legacy hard-Pog fallback otherwise.
            var ePogKey = Has("SPog") ? "SPog" : "Pog";
            if (cal && Has("LS","Pn",ePogKey))
                Add("Upper-Lip-ELine",  "ricketts",
                    SignedPerpT(G("LS"),G("Pn"),G(ePogKey)) / pixelsPerMm, -2, 2, "mm");
            if (cal && Has("LI","Pn",ePogKey))
                Add("Lower-Lip-ELine",  "ricketts",
                    SignedPerpT(G("LI"),G("Pn"),G(ePogKey)) / pixelsPerMm, -2, 2, "mm");
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

        // ── Jarabak (Björk polygon + facial-height ratio) ─────────────────────
        if (groups.Contains("jarabak"))
        {
            double? saddle    = Has("N","S","Ar")   ? R1(Angle3T(G("N"),  G("S"),  G("Ar"))) : null;
            double? articular = Has("S","Ar","Go")  ? R1(Angle3T(G("S"),  G("Ar"), G("Go"))) : null;
            double? gonial    = Has("Ar","Go","Me") ? R1(Angle3T(G("Ar"), G("Go"), G("Me"))) : null;

            if (saddle.HasValue)    Add("Saddle-Angle",    "jarabak", saddle.Value,    123, 5, "°");
            if (articular.HasValue) Add("Articular-Angle", "jarabak", articular.Value, 143, 6, "°");
            if (gonial.HasValue)    Add("Gonial-Angle",    "jarabak", gonial.Value,    130, 7, "°");
            if (saddle.HasValue && articular.HasValue && gonial.HasValue)
                Add("Bjork-Sum", "jarabak", saddle.Value + articular.Value + gonial.Value, 396, 6, "°");

            // Posterior/anterior facial-height ratio (S-Go / N-Me) — a pure length
            // ratio, valid even when the image is not calibrated.
            if (Has("S","Go","N","Me"))
            {
                double afh = DistT(G("N"), G("Me"));
                if (afh > 1e-9)
                    Add("Jarabak-Ratio", "jarabak", DistT(G("S"), G("Go")) / afh * 100, 64, 4, "%");
            }
        }

        // ── Wits ─────────────────────────────────────────────────────────────
        // SEQ-40: with the first-molar landmarks (U6, L6) placed, Wits uses the
        // TRUE functional occlusal plane — incisal overbite midpoint → molar
        // occlusion midpoint (classic definition). Analyses without molars keep
        // the historic mid(U1T,L1T)→mid(Go,Me) approximation (values unchanged).
        var witsHasMolars = Has("U6","L6");
        if (groups.Contains("wits") && cal && Has("U1T","L1T","A","B")
            && (witsHasMolars || Has("Go","Me")))
        {
            var occP1 = ((G("U1T").x + G("L1T").x) / 2, (G("U1T").y + G("L1T").y) / 2);
            var occP2 = witsHasMolars
                ? ((G("U6").x + G("L6").x) / 2, (G("U6").y + G("L6").y) / 2)
                : ((G("Go").x + G("Me").x) / 2, (G("Go").y + G("Me").y) / 2);
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

        foreach (var (name, group, value, normal, sd, unit, min, max) in results)
        {
            double? rawDev = normal.HasValue ? value - normal.Value : null;
            string? classification = normal.HasValue && sd.HasValue
                ? ClassifyMeasurement(value, normal.Value, sd.Value, min, max)
                : "unclassified";

            db.CephMeasurements.Add(new CephMeasurement
            {
                AnalysisId       = id,
                MeasurementName  = name,
                MeasurementValue = (decimal)value,
                NormalValue      = normal.HasValue ? (decimal)normal.Value : null,
                StdDeviation     = sd.HasValue ? (decimal)sd.Value : null,
                Unit             = unit,
                Deviation        = rawDev.HasValue ? (decimal)Math.Round(rawDev.Value, 2) : null,
                Classification   = classification
            });
        }

        await db.SaveChangesAsync();
        await GenerateDiagnosisAsync(id);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  GENERATE DIAGNOSIS  (rule-based Arabic text from measurements)
    // ──────────────────────────────────────────────────────────────────────────

    // C-B rule-engine thresholds (orthodontic literature):
    // ANB skeletal cutoffs per Steiner (Class II > 4°, Class III < 0°).
    // Wits appraisal (Jacobson): Class II > +2mm, Class III < -2mm — used to
    // corroborate ANB, since ANB is distorted by jaw rotation / N position.
    private const double WitsClassIiMm = 2.0;
    private const double WitsClassIiiMm = -2.0;
    // Severe sagittal discrepancy where camouflage alone is often insufficient
    // (Proffit: surgical envelope ~ ANB beyond ±7-8°).
    private const double SurgicalAnbHigh = 8.0;
    private const double SurgicalAnbLow = -3.0;
    // Dentoalveolar protrusion suggesting extraction analysis (Steiner mm norms
    // are 4mm; >7mm is marked protrusion; interincisal < 120° supports it).
    private const double ProtrusionU1NaMm = 7.0;
    private const double ProtrusionL1NbMm = 7.0;
    private const double InterincisalProclined = 120.0;

    private async Task GenerateDiagnosisAsync(Guid id)
    {
        var measurements = await db.CephMeasurements
            .Where(m => m.AnalysisId == id && m.IsActive)
            .ToListAsync();

        var analysisType = await db.CephAnalyses
            .Where(a => a.Id == id)
            .Select(a => a.AnalysisType)
            .FirstOrDefaultAsync();

        if (string.Equals(analysisType, "pa", StringComparison.OrdinalIgnoreCase))
        {
            string Display(string name, string label)
            {
                var measurement = measurements.FirstOrDefault(m => m.MeasurementName == name);
                if (measurement?.MeasurementValue is not decimal value) return $"{label}: —";
                var sign = value > 0 ? "+" : string.Empty;
                return $"{label}: {sign}{value:0.0} {measurement.Unit}";
            }

            var summary = string.Join(Environment.NewLine,
            [
                "ملخص التحليل الأمامي PA (وصفي):",
                Display("PA-ANS-MSR", "انحراف ANS عن الخط الناصف"),
                Display("PA-UDM-MSR", "منتصف الأسنان العلوية"),
                Display("PA-LDM-MSR", "منتصف الأسنان السفلية"),
                Display("PA-Me-MSR", "انحراف الذقن"),
                Display("PA-Maxillary-Cant", "ميل المستوى العلوي"),
                Display("PA-Mandibular-Cant", "ميل المستوى السفلي"),
                "الإشارة الموجبة نحو يسار المريض؛ والميل الموجب يعني أن الجانب الأيسر أخفض.",
                "هذه القياسات وصفية وتحتاج تفسيرًا حسب العمر والجنس والفحص السريري؛ لا تُنتج تصنيفًا هيكليًا جانبيًا.",
            ]);

            var paDiagnosis = await db.CephDiagnoses
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(d => d.AnalysisId == id);
            if (paDiagnosis is null)
            {
                paDiagnosis = new CephDiagnosis { AnalysisId = id };
                db.CephDiagnoses.Add(paDiagnosis);
            }
            paDiagnosis.IsActive = true;
            paDiagnosis.SkeletalClass = null;
            paDiagnosis.VerticalPattern = null;
            paDiagnosis.IncisorInclination = null;
            paDiagnosis.SoftTissueSummary = null;
            paDiagnosis.AiRecommendation = summary;
            await db.SaveChangesAsync();
            return;
        }

        double? GetValue(string name) =>
            measurements.FirstOrDefault(m => m.MeasurementName == name)?.MeasurementValue is decimal d
                ? (double)d : null;

        double? anb    = GetValue("ANB");
        double? wits   = GetValue("Wits");
        double? goGnSn = GetValue("GoGn-SN");
        double? fma    = GetValue("FMA");

        // ── Skeletal Class (ANB corroborated by Wits when available) ──────
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

        string? witsNote = null;
        if (anb.HasValue && wits.HasValue)
        {
            var witsClass = wits.Value > WitsClassIiMm ? "Class II"
                          : wits.Value < WitsClassIiiMm ? "Class III"
                          : "Class I";
            witsNote = witsClass == skeletalClass
                ? $"التصنيف الهيكلي مؤكد بمؤشر Wits ({wits.Value:0.0} مم)."
                : $"تنبيه: نتيجة Wits ({wits.Value:0.0} مم → {ClassAr(witsClass)}) غير متوافقة مع ANB — يُنصح بمراجعة المعايرة ومواضع المعالم والاعتماد على التقييم السريري.";
        }

        // ── Vertical Pattern (GoGn-SN and FMA agreement when both exist) ──
        string verticalPattern = "Normodivergent";
        string verticalAr      = "نمط رأسي طبيعي";
        string? verticalNote   = null;

        string PatternOf(double value, bool isFma)
        {
            double hi = isFma ? 29 : 38;
            double lo = isFma ? 21 : 26;
            return value > hi ? "Hyperdivergent" : value < lo ? "Hypodivergent" : "Normodivergent";
        }

        if (goGnSn.HasValue || fma.HasValue)
        {
            var primary = goGnSn.HasValue
                ? PatternOf(goGnSn.Value, isFma: false)
                : PatternOf(fma!.Value, isFma: true);

            verticalPattern = primary;
            verticalAr = primary switch
            {
                "Hyperdivergent" => "نمط رأسي مرتفع (وجه طويل)",
                "Hypodivergent"  => "نمط رأسي منخفض (وجه قصير)",
                _                 => "نمط رأسي طبيعي",
            };

            if (goGnSn.HasValue && fma.HasValue)
            {
                var secondary = PatternOf(fma.Value, isFma: true);
                verticalNote = secondary == primary
                    ? "النمط الرأسي متوافق بين GoGn-SN وFMA."
                    : "تنبيه: GoGn-SN وFMA يشيران لنمطين رأسيين مختلفين — يُنصح بمراجعة معالم Go/Gn/Po/Or.";
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

        // ── Rule-based recommendation text ────────────────────────────────
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"التشخيص الهيكلي: {skeletalAr}");
        if (witsNote is not null) sb.AppendLine(witsNote);
        sb.AppendLine($"النمط الرأسي: {verticalAr}");
        if (verticalNote is not null) sb.AppendLine(verticalNote);
        sb.AppendLine($"ميلان القواطع: {incisorInclination}");

        if (skeletalClass == "Class II")
            sb.AppendLine("التوصية: يُنصح بتقييم إمكانية العلاج بتعديل النمو أو استخدام أجهزة تقويمية ثابتة مع نوابض Forsus أو جهاز Herbst لتصحيح الصنف الثاني.");
        else if (skeletalClass == "Class III")
            sb.AppendLine("التوصية: يُنصح بتقييم العلاج الجراحي أو استخدام قوة وجهية عكسية في حالات النمو لتصحيح الصنف الثالث.");
        else
            sb.AppendLine("التوصية: يمكن إجراء العلاج التقويمي التقليدي لتصحيح العلاقات السنية.");

        if (verticalPattern == "Hyperdivergent")
            sb.AppendLine("ملاحظة: النمط الرأسي المرتفع يستوجب الحذر في الميكانيكا الباسطة، مع ميل لعضة أمامية مفتوحة.");
        else if (verticalPattern == "Hypodivergent")
            sb.AppendLine("ملاحظة: النمط الرأسي المنخفض يترافق غالبًا مع عضة عميقة ويسمح بحركة أمامية أكبر للأسنان.");

        // ── مؤشرات قاعدية آلية (C-B) — ليست قرارًا علاجيًا ────────────────
        double? u1naMm = GetValue("U1-NA_mm");
        double? l1nbMm = GetValue("L1-NB_mm");
        double? interincisal = GetValue("U1-L1");

        var indicators = new List<string>();

        bool dentalProtrusion =
            (u1naMm.HasValue && u1naMm.Value > ProtrusionU1NaMm) ||
            (l1nbMm.HasValue && l1nbMm.Value > ProtrusionL1NbMm);
        if (dentalProtrusion && interincisal.HasValue && interincisal.Value < InterincisalProclined)
            indicators.Add("بروز سني واضح مع زاوية بين-قاطعية منخفضة — قد يستدعي دراسة القلع ضمن خطة العلاج.");
        else if (dentalProtrusion)
            indicators.Add("بروز سني أمامي ملحوظ — يُدرس ضمن تحليل المسافات وخطة العلاج.");

        if (anb.HasValue && (anb.Value >= SurgicalAnbHigh || anb.Value <= SurgicalAnbLow))
            indicators.Add("تفاوت هيكلي شديد قد يتجاوز حدود المعالجة التقويمية وحدها — يُنصح بتقييم الخيار الجراحي التقويمي.");

        if (indicators.Count > 0)
        {
            sb.AppendLine("مؤشرات قاعدية آلية — تتطلب تقييم الأخصائي:");
            foreach (var ind in indicators) sb.AppendLine($"• {ind}");
        }

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

        await SyncOrthoDiagnosisFromCephAsync(
            id,
            skeletalClass,
            verticalPattern,
            softTissueSummary,
            GetValue);

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Keeps the orthodontic case diagnosis aligned with the latest calculated
    /// cephalometric measurements. Approved diagnoses are immutable: a newer
    /// analysis remains visible in the case workspace but never overwrites a
    /// doctor's approved clinical conclusion.
    /// </summary>
    private async Task SyncOrthoDiagnosisFromCephAsync(
        Guid analysisId,
        string skeletalClass,
        string verticalPattern,
        string softTissueSummary,
        Func<string, double?> getValue)
    {
        var orthoCaseId = await db.CephAnalyses
            .Where(a => a.Id == analysisId)
            .Select(a => (Guid?)a.OrthoCaseId)
            .FirstOrDefaultAsync();

        if (!orthoCaseId.HasValue) return;

        var orthoDiagnosis = await db.OrthoDiagnoses
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.OrthoCaseId == orthoCaseId.Value);

        if (orthoDiagnosis?.ApprovedAt is not null) return;

        if (orthoDiagnosis is null)
        {
            orthoDiagnosis = new OrthoDiagnosis
            {
                OrthoCaseId = orthoCaseId.Value,
            };
            db.OrthoDiagnoses.Add(orthoDiagnosis);
        }

        static decimal? ToDecimal(double? value) =>
            value.HasValue ? (decimal)value.Value : null;

        orthoDiagnosis.IsActive = true;
        orthoDiagnosis.SkeletalClassification = skeletalClass;
        orthoDiagnosis.FacialPattern = verticalPattern;
        orthoDiagnosis.ANB = ToDecimal(getValue("ANB"));
        orthoDiagnosis.Wits = ToDecimal(getValue("Wits"));
        orthoDiagnosis.FMA = ToDecimal(getValue("FMA"));
        orthoDiagnosis.SNA = ToDecimal(getValue("SNA"));
        orthoDiagnosis.SNB = ToDecimal(getValue("SNB"));
        orthoDiagnosis.IMPA = ToDecimal(getValue("IMPA"));
        orthoDiagnosis.SoftTissueDiagnosis ??= softTissueSummary;
        orthoDiagnosis.CephSourceAnalysisId = analysisId;
        orthoDiagnosis.CephSyncedAt = DateTime.UtcNow;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  TEMPLATE-BASED LANDMARK SIMULATION  (NOT AI — honest labeling)
    //
    //  Places landmarks at average template positions with random jitter.
    //  This is a training/demo aid only: it is NOT artificial intelligence and
    //  must never be presented as such. Landmarks are returned with
    //  IsAiPlaced = false and Confidence = null so reports never claim AI
    //  placement. Gated behind the Settings key "ceph.simulation_enabled"
    //  (default: disabled). Real AI auto-tracing has a separate placeholder
    //  endpoint (POST /api/ceph/{id}/ai/auto-trace → 501).
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Settings key that enables the template-based landmark simulation.
    /// Missing or anything other than "true"/"1" means DISABLED (default).
    /// </summary>
    public const string SimulationEnabledSettingKey = "ceph.simulation_enabled";

    public const string SimulationNoticeAr =
        "هذه محاكاة تجريبية لمواضع المعالم وليست ذكاءً اصطناعيًا حقيقيًا — تُستخدم للتدريب فقط ويجب ضبط كل معلم يدويًا.";

    public async Task<bool> IsSimulationEnabledAsync()
    {
        var value = await db.Settings
            .Where(s => s.Key == SimulationEnabledSettingKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";
    }

    public async Task<CephSimulationResultDto> SimulateTemplateAsync(Guid id, AiSimulateRequest req)
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
            analysis.Notes = JsonSerializer.Serialize(notesData);
            // Honest labeling: a template simulation is NOT AI assistance and
            // NOT auto-tracing — never mark the analysis as such.
            analysis.AiAssisted   = false;
            analysis.IsAutoTraced = false;
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
            ("U6",  "الرحى الأولى العلوية",         0.58, 0.55),
            ("L6",  "الرحى الأولى السفلية",         0.57, 0.58),
            ("LS",  "الشفة العلوية",                0.82, 0.56),
            ("LI",  "الشفة السفلية",                0.79, 0.61),
            ("Pn",  "طرف الأنف",                    0.87, 0.44),
            ("Cm",  "قاعدة الأنف",                  0.83, 0.49),
            ("SPog","الذقن الرخو",                  0.70, 0.70),
        };

        var landmarks = new List<CephLandmarkDto>(templates.Length);
        foreach (var (key, nameAr, fx, fy) in templates)
        {
            double jitterX = (rng.NextDouble() - 0.5) * 2 * 0.015;
            double jitterY = (rng.NextDouble() - 0.5) * 2 * 0.015;

            landmarks.Add(new CephLandmarkDto
            {
                Id         = Guid.NewGuid(),
                Key        = key,
                Name       = nameAr,
                X          = Math.Round((fx + jitterX) * W, 1),
                Y          = Math.Round((fy + jitterY) * H, 1),
                // Template positions are NOT AI placements — no fake confidence.
                IsAiPlaced = false,
                Confidence = null
            });
        }

        return new CephSimulationResultDto
        {
            IsSimulation     = true,
            SimulationNotice = SimulationNoticeAr,
            Landmarks        = landmarks
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  SOFT DELETE
    // ──────────────────────────────────────────────────────────────────────────
    // ──────────────────────────────────────────────────────────────────────────
    //  C-B: COMPARE two analyses of the SAME ortho case (pre/post)
    // ──────────────────────────────────────────────────────────────────────────
    public async Task<(CephCompareResultDto? Result, string? Error)> CompareAsync(Guid baseId, Guid targetId)
    {
        var both = await db.CephAnalyses
            .Where(a => (a.Id == baseId || a.Id == targetId) && a.IsActive)
            .ToListAsync();

        var baseA = both.FirstOrDefault(a => a.Id == baseId);
        var targetA = both.FirstOrDefault(a => a.Id == targetId);
        if (baseA is null || targetA is null)
            return (null, "التحليل غير موجود");
        if (baseA.OrthoCaseId != targetA.OrthoCaseId)
            return (null, "التحليلان لا يخصان نفس الحالة");

        var patientName = await db.OrthoCases
            .Where(c => c.Id == baseA.OrthoCaseId)
            .Join(db.Patients, c => c.PatientId, p => p.Id,
                (c, p) => (p.FirstName + " " + p.LastName).Trim())
            .FirstOrDefaultAsync();

        // CLIN-10: load the patient (DOB + gender) for stratified norm lookup.
        // Both analyses belong to the same ortho case (verified above) so a
        // single patient lookup is sufficient. Age is computed via
        // ClinicTimeProvider.ClinicToday (Asia/Aden) so Railway's UTC clock
        // doesn't drift the calendar day.
        var patient = await db.OrthoCases
            .Where(c => c.Id == baseA.OrthoCaseId)
            .Join(db.Patients, c => c.PatientId, p => p.Id, (c, p) => p)
            .FirstOrDefaultAsync();
        int? patientAge = null;
        if (patient?.DateOfBirth is { } dob)
        {
            var today = ClinicTimeProvider.ClinicToday();
            patientAge = today.Year - dob.Year;
            if (dob > today.AddYears(-patientAge.Value)) patientAge--;
        }
        var patientSex = patient?.Gender switch
        {
            Gender.Male   => "M",
            Gender.Female => "F",
            _             => null,
        };

        var measurements = await db.CephMeasurements
            .Where(m => (m.AnalysisId == baseId || m.AnalysisId == targetId) && m.IsActive)
            .ToListAsync();

        // CLIN-10: keep the full norm list (multiple stratified rows per
        // measurement/group now) and pick the best match per row via
        // FindBestCephNorm. The measurement-name → analysis-group mapping uses
        // GetMeasurementGroup (the canonical fallback the rest of the engine
        // uses for norm grouping).
        var norms = await db.CephNorms.ToListAsync();

        var baseMap = measurements.Where(m => m.AnalysisId == baseId)
            .GroupBy(m => m.MeasurementName).ToDictionary(g => g.Key, g => g.First());
        var targetMap = measurements.Where(m => m.AnalysisId == targetId)
            .GroupBy(m => m.MeasurementName).ToDictionary(g => g.Key, g => g.First());

        var rows = baseMap.Keys.Union(targetMap.Keys)
            .Select(name =>
            {
                baseMap.TryGetValue(name, out var b);
                targetMap.TryGetValue(name, out var t);
                var norm = FindBestCephNorm(norms, name, GetMeasurementGroup(name), patientAge, patientSex);
                var normal = norm?.NormalValue ?? b?.NormalValue ?? t?.NormalValue;

                bool? improved = null;
                if (b?.MeasurementValue is decimal bv && t?.MeasurementValue is decimal tv && normal is decimal nv)
                    improved = Math.Abs(tv - nv) < Math.Abs(bv - nv) ? true
                             : Math.Abs(tv - nv) > Math.Abs(bv - nv) ? false
                             : (bool?)null;

                return new CephCompareRowDto
                {
                    MeasurementName = name,
                    NameAr = norm?.NameAr ?? GetMeasurementNameAr(name),
                    AnalysisGroup = norm?.AnalysisGroup,
                    Unit = b?.Unit ?? t?.Unit ?? norm?.Unit ?? "°",
                    BaseValue = b?.MeasurementValue,
                    TargetValue = t?.MeasurementValue,
                    Delta = b?.MeasurementValue is decimal b2 && t?.MeasurementValue is decimal t2
                        ? Math.Round(t2 - b2, 2) : null,
                    NormalValue = normal,
                    StdDeviation = norm?.StdDeviation ?? b?.StdDeviation ?? t?.StdDeviation,
                    BaseClassification = b?.Classification,
                    TargetClassification = t?.Classification,
                    Improved = improved,
                };
            })
            .OrderBy(r => r.AnalysisGroup ?? "zz")
            .ThenBy(r => r.MeasurementName)
            .ToList();

        var result = new CephCompareResultDto
        {
            Base = new CephCompareSideDto { Id = baseA.Id, AnalysisDate = baseA.AnalysisDate.ToString("yyyy-MM-dd"), AnalysisType = baseA.AnalysisType },
            Target = new CephCompareSideDto { Id = targetA.Id, AnalysisDate = targetA.AnalysisDate.ToString("yyyy-MM-dd"), AnalysisType = targetA.AnalysisType },
            PatientName = patientName,
            Rows = rows,
        };
        return (result, null);
    }

    public async Task SoftDeleteAsync(Guid id)
    {
        var analysis = await db.CephAnalyses.FindAsync(id);
        if (analysis is null) return;
        analysis.IsActive = false;
        await db.SaveChangesAsync();
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  CEPH-EPIC batch C-B — ANALYSIS VERSIONS
    //  Named snapshots of (landmarks + measurements + diagnosis) so the
    //  orthodontist can track progress over time (e.g. "Pre-treatment" /
    //  "6 months" / "12 months") and compare a snapshot against the live
    //  analysis or another snapshot. The snapshot is IMMUTABLE — stored as
    //  JSON, decoupled from the live rows.
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Saves the current state of the analysis (landmarks + measurements +
    /// diagnosis) as a named snapshot. Returns null when the analysis does not
    /// exist (the controller maps that to a 404 with an Arabic message).
    /// </summary>
    public async Task<CephVersionListDto?> SaveVersionAsync(Guid analysisId, CreateCephVersionRequest req)
    {
        // Reuse MapDetail to materialize the live DTOs (landmarks + measurements
        // + diagnosis) — single source of truth for the snapshot payload.
        var detail = await GetByIdAsync(analysisId);
        if (detail is null) return null;

        // Validate + normalize the label (trim, collapse whitespace, cap length).
        var label = (req.Label ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("اسم النسخة مطلوب");
        if (label.Length > 100)
            label = label[..100];

        var version = new CephAnalysisVersion
        {
            CephAnalysisId   = analysisId,
            Label            = label,
            LandmarksJson    = JsonSerializer.Serialize(detail.Landmarks),
            MeasurementsJson = JsonSerializer.Serialize(detail.Measurements),
            DiagnosisJson    = detail.Diagnosis is null ? null : JsonSerializer.Serialize(detail.Diagnosis),
            SnapshotDate     = ClinicTimeProvider.ClinicToday(),
            CreatedByUserId  = currentUser.UserId,
        };
        db.CephAnalysisVersions.Add(version);
        await db.SaveChangesAsync();

        return new CephVersionListDto
        {
            Id              = version.Id,
            CephAnalysisId  = version.CephAnalysisId,
            Label           = version.Label,
            SnapshotDate    = version.SnapshotDate.ToString("yyyy-MM-dd"),
            CreatedAt       = version.CreatedAt,
        };
    }

    /// <summary>Lists all snapshots of an analysis, newest first.</summary>
    public async Task<List<CephVersionListDto>> ListVersionsAsync(Guid analysisId)
    {
        return await db.CephAnalysisVersions
            .AsNoTracking()
            .Where(v => v.CephAnalysisId == analysisId && v.IsActive)
            .OrderByDescending(v => v.SnapshotDate)
            .ThenByDescending(v => v.CreatedAt)
            .Select(v => new CephVersionListDto
            {
                Id              = v.Id,
                CephAnalysisId  = v.CephAnalysisId,
                Label           = v.Label,
                SnapshotDate    = v.SnapshotDate.ToString("yyyy-MM-dd"),
                CreatedAt       = v.CreatedAt,
            })
            .ToListAsync();
    }

    /// <summary>
    /// Loads a single snapshot with deserialized landmarks/measurements/
    /// diagnosis. Returns null when not found. The caller (controller) is
    /// responsible for access checks — this method does NOT re-check
    /// PatientAccessFilter, the controller's GetAnalysisAccessErrorAsync
    /// already verified the parent analysis belongs to a patient the user can
    /// access.
    /// </summary>
    public async Task<CephVersionDetailDto?> GetVersionAsync(Guid analysisId, Guid versionId)
    {
        var version = await db.CephAnalysisVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == versionId
                && v.CephAnalysisId == analysisId
                && v.IsActive);

        if (version is null) return null;

        var landmarks = TryDeserialize<List<CephLandmarkDto>>(version.LandmarksJson) ?? [];
        var measurements = TryDeserialize<List<CephMeasurementDto>>(version.MeasurementsJson) ?? [];
        var diagnosis = TryDeserialize<CephDiagnosisDto>(version.DiagnosisJson);

        return new CephVersionDetailDto
        {
            Id              = version.Id,
            CephAnalysisId  = version.CephAnalysisId,
            Label           = version.Label,
            SnapshotDate    = version.SnapshotDate.ToString("yyyy-MM-dd"),
            CreatedAt       = version.CreatedAt,
            Landmarks       = landmarks,
            Measurements    = measurements,
            Diagnosis       = diagnosis,
        };
    }

    private static T? TryDeserialize<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            // A malformed JSON blob in a snapshot is non-fatal — return null
            // (or an empty list via the caller's ?? []). Snapshots are
            // immutable; we never rewrite them. The compare page falls back
            // to an empty list when the payload is unreadable.
            return null;
        }
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

    public async Task<CephAssessmentProblemImportResult> ImportAssessmentProblemsAsync(
        Guid id,
        ImportCephAssessmentProblemsRequest req)
    {
        var analysis = await db.CephAnalyses
            .Include(a => a.Diagnosis)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (analysis is null)
            return new CephAssessmentProblemImportResult { Status = "not_found" };
        if (!analysis.IsApproved)
            return new CephAssessmentProblemImportResult { Status = "analysis_not_approved" };
        if (analysis.Diagnosis?.DoctorApproved != true)
            return new CephAssessmentProblemImportResult { Status = "diagnosis_not_approved" };

        var existingDescriptions = await db.ProblemListItems
            .Where(item => item.OrthoCaseId == analysis.OrthoCaseId)
            .Select(item => item.Description)
            .ToListAsync();
        var knownDescriptions = new HashSet<string>(
            existingDescriptions.Select(description => description.Trim()),
            StringComparer.OrdinalIgnoreCase);
        var maxOrder = await db.ProblemListItems
            .Where(item => item.OrthoCaseId == analysis.OrthoCaseId)
            .MaxAsync(item => (int?)item.SortOrder) ?? 0;

        var result = new CephAssessmentProblemImportResult();
        foreach (var input in req.Items)
        {
            var description = input.Description.Trim();
            if (!knownDescriptions.Add(description))
            {
                result.SkippedExisting++;
                continue;
            }

            var item = new ProblemListItem
            {
                OrthoCaseId = analysis.OrthoCaseId,
                Category = input.Category,
                Description = description,
                Severity = input.Severity,
                SortOrder = ++maxOrder,
            };
            db.ProblemListItems.Add(item);
            result.Items.Add(new CephAssessmentProblemImportItem
            {
                Id = item.Id,
                Category = item.Category,
                Description = item.Description,
                Severity = item.Severity,
                SortOrder = item.SortOrder,
            });
        }

        if (result.Items.Count > 0)
            await db.SaveChangesAsync();
        result.Added = result.Items.Count;
        return result;
    }

    public async Task<List<CephVtoScenarioDto>> ListVtoScenariosAsync(Guid analysisId)
    {
        return await db.CephVtoScenarios
            .Where(x => x.CephAnalysisId == analysisId)
            .OrderBy(x => x.Name)
            .ThenByDescending(x => x.VersionNumber)
            .Select(x => new CephVtoScenarioDto
            {
                Id = x.Id,
                CephAnalysisId = x.CephAnalysisId,
                ScenarioGroupId = x.ScenarioGroupId,
                VersionNumber = x.VersionNumber,
                Name = x.Name,
                UpperIncisorMoveMm = x.UpperIncisorMoveMm,
                LowerIncisorMoveMm = x.LowerIncisorMoveMm,
                OverjetBeforeMm = x.OverjetBeforeMm,
                OverjetAfterMm = x.OverjetAfterMm,
                Notes = x.Notes,
                CreatedByUserId = x.CreatedByUserId,
                CreatedAt = x.CreatedAt,
                Disclaimer = VtoDisclaimerAr,
            })
            .ToListAsync();
    }

    public async Task<SaveCephVtoScenarioResult> SaveVtoScenarioAsync(
        Guid analysisId,
        SaveCephVtoScenarioRequest req)
    {
        var analysis = await db.CephAnalyses
            .Include(x => x.Landmarks)
            .FirstOrDefaultAsync(x => x.Id == analysisId);
        if (analysis is null)
            return new SaveCephVtoScenarioResult { Status = "not_found" };
        if (!analysis.IsApproved)
            return new SaveCephVtoScenarioResult { Status = "analysis_not_approved" };

        CephNotesData? notes = null;
        if (!string.IsNullOrWhiteSpace(analysis.Notes))
        {
            try { notes = JsonSerializer.Deserialize<CephNotesData>(analysis.Notes); }
            catch (JsonException) { }
        }
        if (notes is null || notes.PixelsPerMm <= 0)
            return new SaveCephVtoScenarioResult { Status = "calibration_missing" };

        var landmarkMap = analysis.Landmarks
            .Where(x => x.IsActive && x.XCoord.HasValue && x.YCoord.HasValue)
            .ToDictionary(x => x.LandmarkKey, StringComparer.OrdinalIgnoreCase);
        if (!landmarkMap.ContainsKey("U1T") || !landmarkMap.ContainsKey("U1A") ||
            !landmarkMap.ContainsKey("L1T") || !landmarkMap.ContainsKey("L1A"))
            return new SaveCephVtoScenarioResult { Status = "incisors_missing" };

        Guid groupId;
        int versionNumber;
        if (req.ScenarioGroupId.HasValue)
        {
            groupId = req.ScenarioGroupId.Value;
            var previousVersion = await db.CephVtoScenarios
                .Where(x => x.CephAnalysisId == analysisId && x.ScenarioGroupId == groupId)
                .MaxAsync(x => (int?)x.VersionNumber);
            if (!previousVersion.HasValue)
                return new SaveCephVtoScenarioResult { Status = "scenario_not_found" };
            versionNumber = previousVersion.Value + 1;
        }
        else
        {
            groupId = Guid.NewGuid();
            versionNumber = 1;
        }

        var overjetBefore =
            (landmarkMap["U1T"].XCoord!.Value - landmarkMap["L1T"].XCoord!.Value) /
            (decimal)notes.PixelsPerMm;
        var overjetAfter = overjetBefore + req.UpperIncisorMoveMm - req.LowerIncisorMoveMm;
        var scenario = new CephVtoScenario
        {
            CephAnalysisId = analysisId,
            ScenarioGroupId = groupId,
            VersionNumber = versionNumber,
            Name = req.Name.Trim(),
            UpperIncisorMoveMm = req.UpperIncisorMoveMm,
            LowerIncisorMoveMm = req.LowerIncisorMoveMm,
            OverjetBeforeMm = decimal.Round(overjetBefore, 2),
            OverjetAfterMm = decimal.Round(overjetAfter, 2),
            Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim(),
            CreatedByUserId = currentUser.UserId,
        };
        db.CephVtoScenarios.Add(scenario);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is NpgsqlException { SqlState: "23505" })
        {
            db.Entry(scenario).State = EntityState.Detached;
            logger.LogWarning(ex,
                "[CephService] Concurrent VTO scenario version conflict for analysis {AnalysisId}, group {GroupId}",
                analysisId, groupId);
            return new SaveCephVtoScenarioResult { Status = "version_conflict" };
        }

        return new SaveCephVtoScenarioResult
        {
            Scenario = new CephVtoScenarioDto
            {
                Id = scenario.Id,
                CephAnalysisId = scenario.CephAnalysisId,
                ScenarioGroupId = scenario.ScenarioGroupId,
                VersionNumber = scenario.VersionNumber,
                Name = scenario.Name,
                UpperIncisorMoveMm = scenario.UpperIncisorMoveMm,
                LowerIncisorMoveMm = scenario.LowerIncisorMoveMm,
                OverjetBeforeMm = scenario.OverjetBeforeMm,
                OverjetAfterMm = scenario.OverjetAfterMm,
                Notes = scenario.Notes,
                CreatedByUserId = scenario.CreatedByUserId,
                CreatedAt = scenario.CreatedAt,
                Disclaimer = VtoDisclaimerAr,
            },
        };
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
            catch
            {
                // CephNotesData parse failed (static method, no logger)
            }

        return new CephAnalysisDetailDto
        {
            Id           = a.Id,
            OrthoCaseId  = a.OrthoCaseId,
            PatientId    = a.OrthoCase.PatientId,
            CaseNumber   = a.OrthoCase.CaseNumber,
            PatientName  = a.OrthoCase.Patient.FirstName + " " + a.OrthoCase.Patient.LastName,
            AnalysisType = a.AnalysisType,
            AnalysisDate = a.AnalysisDate.ToString("yyyy-MM-dd"),
            XrayFileUrl  = a.XrayFileUrl,
            IsAutoTraced = a.IsAutoTraced,
            AiAssisted   = a.AiAssisted,
            DoctorId     = a.DoctorId,
            Notes        = a.Notes,
            IsApproved       = a.IsApproved,
            ApprovedByUserId = a.ApprovedByUserId,
            ApprovedAt       = a.ApprovedAt?.ToString("yyyy-MM-dd HH:mm"),
            ApprovalNotes    = a.ApprovalNotes,
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
                    string severity = m.Classification
                        ?? (m.NormalValue.HasValue ? (sdNorm.HasValue ? ClassifyDeviation(sdNorm.Value) : "normal") : "unclassified");
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
                            : GetObservedMeasurementInterpretationAr(m.MeasurementName)
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

    // When an explicit normal range (MinNormal/MaxNormal) is configured on the
    // CephNorm row it takes precedence over the ±1SD rule for the "normal"
    // boundary; severity outside the range still follows the SD thresholds.
    private static string ClassifyMeasurement(double value, double normal, double sd, decimal? min, decimal? max)
    {
        double sdNorm = sd > 0 ? (value - normal) / sd : 0;
        if (min.HasValue || max.HasValue)
        {
            bool within = (!min.HasValue || value >= (double)min.Value)
                       && (!max.HasValue || value <= (double)max.Value);
            if (within) return "normal";
            return Math.Abs(sdNorm) <= 2.0 ? "mild" : "severe";
        }
        return ClassifyDeviation(sdNorm);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  CLIN-10 — STRATIFIED NORM LOOKUP
    // ──────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Picks the best-matching <see cref="CephNorm"/> row for a given
    /// measurement, analysis group, and patient age/sex. Match priority
    /// (highest first):
    /// <list type="number">
    ///   <item><b>Sex-specific + age-matched</b> — Sex equals patientSex (non-null)
    ///     AND AgeMin/AgeMax form a band containing patientAge.</item>
    ///   <item><b>Sex-null + age-matched</b> — Sex is null AND AgeMin/AgeMax form
    ///     a band containing patientAge. Used when patientSex is null OR when no
    ///     sex-specific row exists for the matched band.</item>
    ///   <item><b>Un-stratified fallback</b> — AgeMin, AgeMax, and Sex all null.
    ///     Applies to any patient. Backward-compatible with pre-CLIN-10 rows.</item>
    ///   <item><b>null</b> — caller uses the hardcoded built-in default. This is
    ///     the patient-safety escape hatch: a child norm is NEVER returned for an
    ///     adult and vice versa.</item>
    /// </list>
    /// Patient-safety invariant: a banded row (AgeMin or AgeMax set) NEVER
    /// matches a patient outside its band. A 30-year-old cannot match a 6–12
    /// child row even when it's the only row present — <c>null</c> is returned
    /// and the caller falls back to the hardcoded default rather than a
    /// wrong-age norm.
    /// </summary>
    /// <param name="norms">All norm rows for the call site (caller pre-filters
    /// soft-deleted via the global query filter; this method does not re-filter
    /// by IsActive).</param>
    /// <param name="measurementName">Canonical measurement key (e.g. "SNA").</param>
    /// <param name="analysisGroup">Analysis group (e.g. "steiner").</param>
    /// <param name="patientAge">Patient age in whole years, or null when DOB
    /// missing. When null, banded rows are skipped (can't safely match without
    /// the age) and only the un-stratified fallback is considered.</param>
    /// <param name="patientSex">"M" or "F", or null when sex unknown. When null,
    /// sex-specific rows are skipped and sex-null rows win.</param>
    /// <returns>Best-matching norm, or null when no row safely applies.</returns>
    public static CephNorm? FindBestCephNorm(
        IEnumerable<CephNorm> norms,
        string measurementName,
        string analysisGroup,
        int? patientAge,
        string? patientSex)
    {
        if (string.IsNullOrEmpty(measurementName) || string.IsNullOrEmpty(analysisGroup))
            return null;

        // Filter to rows for this measurement + group once. Order doesn't
        // matter here — the priority tiers below pick the winner deterministically.
        var candidates = norms
            .Where(n => n.MeasurementName == measurementName && n.AnalysisGroup == analysisGroup)
            .ToList();
        if (candidates.Count == 0) return null;

        // Tier 1 + 2: age-matched rows. A row qualifies as "age-matched" when
        // it has a SPECIFIC age band (AgeMin or AgeMax set) AND the patient
        // age falls inside [AgeMin, AgeMax] (inclusive bounds, null = open).
        // Patient-safety: when patientAge is null, NO banded row qualifies —
        // we can't safely match without knowing the patient's age.
        List<CephNorm> ageMatched = new();
        if (patientAge.HasValue)
        {
            int age = patientAge.Value;
            foreach (var n in candidates)
            {
                bool hasBand = n.AgeMin.HasValue || n.AgeMax.HasValue;
                if (!hasBand) continue;
                bool lowerOk = !n.AgeMin.HasValue || n.AgeMin.Value <= age;
                bool upperOk = !n.AgeMax.HasValue || n.AgeMax.Value >= age;
                if (lowerOk && upperOk) ageMatched.Add(n);
            }
        }

        // Tier 1: sex-specific age-matched. Wins only when patientSex is known.
        if (patientSex is not null)
        {
            var sexSpecific = ageMatched.FirstOrDefault(n => n.Sex == patientSex);
            if (sexSpecific is not null) return sexSpecific;
        }

        // Tier 2: sex-null age-matched. The default age-band row for the
        // matched band (applies to both sexes when no sex-specific row exists,
        // or always when patientSex is null).
        var sexNullAgeMatched = ageMatched.FirstOrDefault(n => n.Sex is null);
        if (sexNullAgeMatched is not null) return sexNullAgeMatched;

        // Tier 3: un-stratified fallback — AgeMin, AgeMax, Sex all null. This
        // is the pre-CLIN-10 row shape; applies to any patient. Returned when
        // no stratified row matches (e.g. measurement has only an un-stratified
        // row, or patient is outside every banded row).
        var unstratified = candidates.FirstOrDefault(n =>
            !n.AgeMin.HasValue && !n.AgeMax.HasValue && n.Sex is null);
        if (unstratified is not null) return unstratified;

        // Tier 4: no safe match — caller uses the hardcoded built-in default.
        // Patient-safety escape hatch: we would rather return null (and let the
        // caller use the age/sex-adjusted hardcoded fallback) than return a
        // wrong-age or wrong-sex norm.
        return null;
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
        "jarabak"  => ["jarabak"],
        "wits"     => ["wits"],
        "pa"       => ["pa"],
        _          => ["steiner", "tweed", "mcnamara", "ricketts", "downs", "jarabak", "wits"]
    };

    // Public: also used by CephReportPdfGenerator as the fallback when a
    // CephNorm row is missing for a measurement.
    public static string GetMeasurementGroup(string name) => name switch
    {
        "FMA" or "FMIA" or "IMPA"                                      => "tweed",
        "Co-A" or "Co-Gn" or "ANS-Me"                                  => "mcnamara",
        "Facial-Depth" or "Facial-Axis" or "Mandibular-Plane"
            or "Convexity-A" or "L1-APog_mm" or "L1-APog_angle"
            or "Upper-Lip-ELine" or "Lower-Lip-ELine" or "Nasolabial"  => "ricketts",
        "Convexity" or "AB-FacialPlane" or "Y-Axis"
            or "Facial-Plane-FH" or "Mandibular-FH"                    => "downs",
        "Saddle-Angle" or "Articular-Angle" or "Gonial-Angle"
            or "Bjork-Sum" or "Jarabak-Ratio"                          => "jarabak",
        "Wits"                                                          => "wits",
        "PA-JJ-Width" or "PA-AgAg-Width" or "PA-Width-Differential"
            or "PA-ANS-MSR" or "PA-UDM-MSR" or "PA-LDM-MSR" or "PA-Me-MSR"
            or "PA-Maxillary-Cant" or "PA-Mandibular-Cant"
            or "PA-J-Vertical-Asymmetry" or "PA-Ag-Vertical-Asymmetry" => "pa",
        _                                                               => "steiner"
    };

    // Public: also used by CephReportPdfGenerator as the fallback when a
    // CephNorm row has no NameAr.
    public static string GetMeasurementNameAr(string name) => name switch
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
        // Jarabak
        "Saddle-Angle"     => "الزاوية السرجية (N-S-Ar)",
        "Articular-Angle"  => "الزاوية المفصلية (S-Ar-Go)",
        "Gonial-Angle"     => "الزاوية الفكية (Ar-Go-Me)",
        "Bjork-Sum"        => "مجموع زوايا بيورك",
        "Jarabak-Ratio"    => "نسبة جاراباك (الارتفاع الخلفي/الأمامي)",
        // Wits
        "Wits"             => "مسافة وتس (AO-BO)",
        // PA / frontal
        "PA-JJ-Width"                  => "عرض الفك العلوي (J–J)",
        "PA-AgAg-Width"                => "عرض الفك السفلي (Ag–Ag)",
        "PA-Width-Differential"        => "فرق العرض السفلي − العلوي",
        "PA-ANS-MSR"                   => "انحراف ANS عن الخط الناصف",
        "PA-UDM-MSR"                   => "انحراف منتصف الأسنان العلوية",
        "PA-LDM-MSR"                   => "انحراف منتصف الأسنان السفلية",
        "PA-Me-MSR"                    => "انحراف الذقن (Me)",
        "PA-Maxillary-Cant"            => "ميل المستوى الإطباقي العلوي",
        "PA-Mandibular-Cant"           => "ميل المستوى الإطباقي السفلي",
        "PA-J-Vertical-Asymmetry"      => "عدم التناظر الرأسي عند J",
        "PA-Ag-Vertical-Asymmetry"     => "عدم التناظر الرأسي عند Ag",
        _                  => name
    };

    public static string GetObservedMeasurementInterpretationAr(string name) => name switch
    {
        "PA-JJ-Width" or "PA-AgAg-Width" =>
            "قياس عرض وصفي — يُقارن بمرجع مناسب لعمر المريض وجنسه وطريقة التصوير.",
        "PA-Width-Differential" =>
            "القيمة الموجبة تعني أن عرض الفك السفلي أكبر من عرض الفك العلوي.",
        "PA-Maxillary-Cant" or "PA-Mandibular-Cant"
            or "PA-J-Vertical-Asymmetry" or "PA-Ag-Vertical-Asymmetry" =>
            "القيمة الموجبة تعني أن الجانب الأيسر للمريض أخفض من الأيمن.",
        "PA-ANS-MSR" or "PA-UDM-MSR" or "PA-LDM-MSR" or "PA-Me-MSR" =>
            "القيمة الموجبة تعني انحرافًا نحو يسار المريض، والسالبة نحو يمينه.",
        _ => "قياس وصفي — لا توجد قيمة مرجعية عامة آمنة؛ فسّره حسب السياق السريري.",
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
            "Saddle-Angle"    => above ? $"الزاوية السرجية مفتوحة (وضع خلفي للحفرة الفكية) {s}" : $"الزاوية السرجية ضيقة (وضع أمامي للحفرة الفكية) {s}",
            "Articular-Angle" => above ? $"الزاوية المفصلية مفتوحة — ميل لنمط عمودي {s}" : $"الزاوية المفصلية ضيقة — ميل لنمط أفقي {s}",
            "Gonial-Angle"    => above ? $"زاوية الفك مفتوحة — نمط نمو عمودي {s}" : $"زاوية الفك ضيقة — نمط نمو أفقي {s}",
            "Bjork-Sum"       => above ? $"مجموع الزوايا مرتفع — نمط نمو عمودي (دوران خلفي) {s}" : $"مجموع الزوايا منخفض — نمط نمو أفقي (دوران أمامي) {s}",
            "Jarabak-Ratio"   => above ? $"نسبة عالية — اتجاه نمو أفقي {s}" : $"نسبة منخفضة — اتجاه نمو عمودي {s}",
            "Wits"          => above ? $"تنافر هيكلي من الصنف الثاني (AO أمام BO) {s}" : $"تنافر هيكلي من الصنف الثالث (BO أمام AO) {s}",
            _               => above ? $"أعلى من المعدل {s}" : $"أقل من المعدل {s}"
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  NOTES JSON HELPER
    // ──────────────────────────────────────────────────────────────────────────
    private static string ClassAr(string skeletalClass) => skeletalClass switch
    {
        "Class II"  => "الصنف الثاني",
        "Class III" => "الصنف الثالث",
        _            => "الصنف الأول",
    };

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
