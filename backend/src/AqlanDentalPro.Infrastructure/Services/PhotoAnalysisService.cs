using System.Text.Json;
using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Application.Services;

/// <summary>
/// Saves facial photo analyses (profile/frontal) against an orthodontic case.
/// Measurements/landmarks are stored as JSON exactly as computed on the
/// frontend (honest geometry — no AI). The DoctorId stored references
/// <c>Doctors.Id</c>, resolved from the current user via <c>Doctors.UserId</c>.
/// </summary>
public class PhotoAnalysisService(AppDbContext db, ICurrentUserService currentUser)
{
    private static readonly HashSet<string> ViewTypes = new(StringComparer.OrdinalIgnoreCase) { "profile", "frontal" };

    private sealed class StoredMeasurement
    {
        public string? Key { get; set; }
        public string? NameAr { get; set; }
        public double? Value { get; set; }
        public string? Severity { get; set; }
        public string? InterpretationAr { get; set; }
    }

    public async Task<List<PhotoAnalysisListItemDto>> ListAsync(Guid orthoCaseId)
    {
        return await db.PhotoAnalyses
            .Where(p => p.OrthoCaseId == orthoCaseId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PhotoAnalysisListItemDto
            {
                Id = p.Id,
                OrthoCaseId = p.OrthoCaseId,
                ViewType = p.ViewType,
                ImageFileUrl = p.ImageFileUrl,
                AnalysisDate = p.CreatedAt.ToString("yyyy-MM-dd"),
            })
            .ToListAsync();
    }

    public async Task<PhotoAnalysisDetailDto?> GetByIdAsync(Guid id)
    {
        var p = await db.PhotoAnalyses.FirstOrDefaultAsync(x => x.Id == id);
        return p is null ? null : Map(p);
    }

    public async Task<(PhotoAnalysisDetailDto? Result, string? Error)> CreateAsync(SavePhotoAnalysisRequest req)
    {
        if (!ViewTypes.Contains(req.ViewType))
            return (null, "نوع الصورة غير صالح");

        var caseExists = await db.OrthoCases.AnyAsync(c => c.Id == req.OrthoCaseId && c.IsActive);
        if (!caseExists)
            return (null, "حالة التقويم غير موجودة");

        // DoctorId references Doctors.Id, NOT Users.Id — resolve via Doctors.UserId.
        Guid? doctorId = null;
        if (currentUser.UserId is Guid userId)
            doctorId = await db.Doctors
                .Where(d => d.UserId == userId && d.IsActive)
                .Select(d => (Guid?)d.Id)
                .FirstOrDefaultAsync();

        var entity = new PhotoAnalysis
        {
            OrthoCaseId = req.OrthoCaseId,
            ViewType = req.ViewType.ToLowerInvariant(),
            ImageFileUrl = req.ImageFileUrl,
            LandmarksJson = req.LandmarksJson,
            MeasurementsJson = req.MeasurementsJson,
            Notes = req.Notes,
            DoctorId = doctorId,
        };

        db.PhotoAnalyses.Add(entity);
        await SyncOrthoDiagnosisFromPhotoAnalysisAsync(entity);
        await db.SaveChangesAsync();
        return (Map(entity), null);
    }

    public async Task<bool> SoftDeleteAsync(Guid id)
    {
        var entity = await db.PhotoAnalyses.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return false;
        entity.IsActive = false;
        await db.SaveChangesAsync();
        return true;
    }

    private static PhotoAnalysisDetailDto Map(PhotoAnalysis p) => new()
    {
        Id = p.Id,
        OrthoCaseId = p.OrthoCaseId,
        ViewType = p.ViewType,
        ImageFileUrl = p.ImageFileUrl,
        LandmarksJson = p.LandmarksJson,
        MeasurementsJson = p.MeasurementsJson,
        Notes = p.Notes,
        AnalysisDate = p.CreatedAt.ToString("yyyy-MM-dd"),
    };

    /// <summary>
    /// Transfers the latest facial-photo findings into the orthodontic diagnosis
    /// as a clearly separated draft summary. Approved diagnoses and manually
    /// authored soft-tissue text are never overwritten.
    /// </summary>
    private async Task SyncOrthoDiagnosisFromPhotoAnalysisAsync(PhotoAnalysis current)
    {
        var currentSummary = BuildClinicalSummary(current.ViewType, current.MeasurementsJson);
        if (string.IsNullOrWhiteSpace(currentSummary)) return;

        var diagnosis = await db.OrthoDiagnoses
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.OrthoCaseId == current.OrthoCaseId);

        if (diagnosis?.ApprovedAt is not null) return;

        var otherView = current.ViewType.Equals("profile", StringComparison.OrdinalIgnoreCase)
            ? "frontal"
            : "profile";
        var other = await db.PhotoAnalyses
            .Where(p => p.OrthoCaseId == current.OrthoCaseId && p.ViewType == otherView)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();
        var otherSummary = other is null
            ? null
            : BuildClinicalSummary(other.ViewType, other.MeasurementsJson);

        var profileSummary = current.ViewType.Equals("profile", StringComparison.OrdinalIgnoreCase)
            ? currentSummary
            : otherSummary;
        var frontalSummary = current.ViewType.Equals("frontal", StringComparison.OrdinalIgnoreCase)
            ? currentSummary
            : otherSummary;

        var sections = new List<string>();
        if (!string.IsNullOrWhiteSpace(profileSummary))
            sections.Add($"تحليل البروفايل: {profileSummary}");
        if (!string.IsNullOrWhiteSpace(frontalSummary))
            sections.Add($"تحليل الصورة الأمامية: {frontalSummary}");

        if (diagnosis is null)
        {
            diagnosis = new OrthoDiagnosis { OrthoCaseId = current.OrthoCaseId };
            db.OrthoDiagnoses.Add(diagnosis);
        }

        var combinedSummary = string.Join(Environment.NewLine, sections);
        var previousAutoSummary = diagnosis.PhotoAnalysisSummary;
        diagnosis.IsActive = true;
        diagnosis.PhotoAnalysisSummary = combinedSummary;
        if (string.IsNullOrWhiteSpace(diagnosis.SoftTissueDiagnosis)
            || string.Equals(diagnosis.SoftTissueDiagnosis, previousAutoSummary, StringComparison.Ordinal))
        {
            diagnosis.SoftTissueDiagnosis = combinedSummary;
        }
        diagnosis.PhotoAnalysisSyncedAt = DateTime.UtcNow;

        if (current.ViewType.Equals("profile", StringComparison.OrdinalIgnoreCase))
        {
            diagnosis.ProfileSourceAnalysisId = current.Id;
            if (other is not null) diagnosis.FrontalSourceAnalysisId = other.Id;
        }
        else
        {
            diagnosis.FrontalSourceAnalysisId = current.Id;
            if (other is not null) diagnosis.ProfileSourceAnalysisId = other.Id;
        }
    }

    private static string? BuildClinicalSummary(string viewType, string? measurementsJson)
    {
        if (string.IsNullOrWhiteSpace(measurementsJson)) return null;

        List<StoredMeasurement> measurements;
        try
        {
            measurements = JsonSerializer.Deserialize<List<StoredMeasurement>>(
                measurementsJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        catch (JsonException)
        {
            return null;
        }

        var scored = measurements
            .Where(m => m.Value.HasValue && double.IsFinite(m.Value.Value))
            .ToList();
        if (scored.Count == 0) return null;

        var findings = scored
            .Where(m => !string.Equals(m.Severity, "normal", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.InterpretationAr)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct()
            .Take(4)
            .ToList();

        string headline;
        if (viewType.Equals("profile", StringComparison.OrdinalIgnoreCase))
        {
            var convexity = scored.FirstOrDefault(m =>
                string.Equals(m.Key, "FacialConvexity", StringComparison.OrdinalIgnoreCase))?.Value;
            headline = convexity switch
            {
                > 16 => "ملف محدب مع ميل لنمط هيكلي من الصنف الثاني",
                < 8 => "ملف مقعر مع ميل لنمط هيكلي من الصنف الثالث",
                not null => "ملف مستقيم ضمن الحدود المرجعية",
                _ => "تم توثيق قياسات الأنسجة الرخوة الجانبية",
            };
        }
        else
        {
            var abnormalCount = scored.Count(m =>
                !string.Equals(m.Severity, "normal", StringComparison.OrdinalIgnoreCase));
            headline = abnormalCount == 0
                ? "النسب والتناظر ضمن الحدود المرجعية"
                : $"{abnormalCount} قياس خارج النطاق المرجعي ويحتاج مراجعة الأخصائي";
        }

        return findings.Count == 0
            ? headline
            : $"{headline}. {string.Join("؛ ", findings)}";
    }
}
