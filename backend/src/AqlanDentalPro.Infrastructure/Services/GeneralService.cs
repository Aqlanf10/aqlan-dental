using System.Text.Json;
using AqlanDentalPro.Application.DTOs.General;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Application.Services;

public class GeneralService(AppDbContext db, ICurrentUserService currentUser)
{
    // ── Dental Chart ─────────────────────────────────────────────────────────────

    public async Task<DentalChartDto> GetOrCreateChartAsync(Guid patientId)
    {
        var chart = await db.DentalCharts
            .Include(c => c.Doctor)
            .Include(c => c.ToothConditions)
            .FirstOrDefaultAsync(c => c.PatientId == patientId);

        if (chart == null)
        {
            chart = new DentalChart
            {
                PatientId = patientId,
                ChartDate = DateOnly.FromDateTime(DateTime.Today)
            };
            db.DentalCharts.Add(chart);
            await db.SaveChangesAsync();
        }

        return new DentalChartDto
        {
            Id = chart.Id,
            PatientId = chart.PatientId,
            ChartDate = chart.ChartDate.ToString("yyyy-MM-dd"),
            DoctorName = chart.Doctor?.Name,
            Teeth = chart.ToothConditions.Select(t => new ToothConditionDto
            {
                Id = t.Id,
                ToothNumber = t.ToothNumber,
                Condition = t.Condition,
                SurfacesAffected = t.SurfacesAffected,
                TreatmentDone = t.TreatmentDone,
                Notes = t.Notes
            }).ToList()
        };
    }

    public async Task<ToothConditionDto> UpdateToothAsync(Guid patientId, UpdateToothRequest req)
    {
        var chart = await db.DentalCharts
            .Include(c => c.ToothConditions)
            .FirstOrDefaultAsync(c => c.PatientId == patientId);

        if (chart == null)
        {
            chart = new DentalChart
            {
                PatientId = patientId,
                ChartDate = DateOnly.FromDateTime(DateTime.Today)
            };
            db.DentalCharts.Add(chart);
        }

        var tooth = chart.ToothConditions.FirstOrDefault(t => t.ToothNumber == req.ToothNumber);
        if (tooth == null)
        {
            tooth = new ToothCondition
            {
                ChartId = chart.Id,
                ToothNumber = req.ToothNumber,
                Condition = req.Condition,
                SurfacesAffected = req.SurfacesAffected,
                TreatmentDone = req.TreatmentDone,
                Notes = req.Notes
            };
            db.ToothConditions.Add(tooth);
        }
        else
        {
            tooth.Condition = req.Condition;
            tooth.SurfacesAffected = req.SurfacesAffected;
            tooth.TreatmentDone = req.TreatmentDone;
            tooth.Notes = req.Notes;
        }

        await db.SaveChangesAsync();

        return new ToothConditionDto
        {
            Id = tooth.Id,
            ToothNumber = tooth.ToothNumber,
            Condition = tooth.Condition,
            SurfacesAffected = tooth.SurfacesAffected,
            TreatmentDone = tooth.TreatmentDone,
            Notes = tooth.Notes
        };
    }

    // ── General Treatments ──────────────────────────────────────────────────────

    public async Task<List<GeneralTreatmentDto>> GetTreatmentsAsync(Guid patientId)
    {
        return await db.GeneralTreatments
            .Include(t => t.Doctor)
            .Where(t => t.PatientId == patientId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new GeneralTreatmentDto
            {
                Id = t.Id,
                PatientId = t.PatientId,
                TreatmentType = t.TreatmentType,
                ToothNumber = t.ToothNumber,
                MaterialUsed = t.MaterialUsed,
                AnesthesiaType = t.AnesthesiaType,
                Cost = t.Cost,
                DoctorName = t.Doctor != null ? t.Doctor.Name : null,
                Notes = t.Notes,
                CreatedAt = t.CreatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();
    }

    public async Task<GeneralTreatmentDto> CreateTreatmentAsync(CreateGeneralTreatmentRequest req)
    {
        var treatment = new GeneralTreatment
        {
            PatientId = req.PatientId,
            TreatmentType = req.TreatmentType,
            ToothNumber = req.ToothNumber,
            MaterialUsed = req.MaterialUsed,
            AnesthesiaType = req.AnesthesiaType,
            Cost = req.Cost,
            DoctorId = req.DoctorId,
            Notes = req.Notes
        };

        db.GeneralTreatments.Add(treatment);
        await db.SaveChangesAsync();

        await db.Entry(treatment).Reference(t => t.Doctor).LoadAsync();

        return new GeneralTreatmentDto
        {
            Id = treatment.Id,
            PatientId = treatment.PatientId,
            TreatmentType = treatment.TreatmentType,
            ToothNumber = treatment.ToothNumber,
            MaterialUsed = treatment.MaterialUsed,
            AnesthesiaType = treatment.AnesthesiaType,
            Cost = treatment.Cost,
            DoctorName = treatment.Doctor?.Name,
            Notes = treatment.Notes,
            CreatedAt = treatment.CreatedAt.ToString("yyyy-MM-dd")
        };
    }

    public async Task<GeneralTreatmentDto> UpdateTreatmentAsync(Guid id, UpdateGeneralTreatmentRequest req)
    {
        var treatment = await db.GeneralTreatments
            .Include(t => t.Doctor)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (treatment is null)
            throw new KeyNotFoundException("العلاج غير موجود");

        if (req.TreatmentType is not null) treatment.TreatmentType = req.TreatmentType;
        if (req.ToothNumber is not null) treatment.ToothNumber = req.ToothNumber;
        if (req.MaterialUsed is not null) treatment.MaterialUsed = req.MaterialUsed;
        if (req.AnesthesiaType is not null) treatment.AnesthesiaType = req.AnesthesiaType;
        if (req.Cost.HasValue) treatment.Cost = req.Cost;
        if (req.DoctorId.HasValue) treatment.DoctorId = req.DoctorId;
        if (req.Notes is not null) treatment.Notes = req.Notes;

        await db.SaveChangesAsync();

        await db.Entry(treatment).Reference(t => t.Doctor).LoadAsync();

        return new GeneralTreatmentDto
        {
            Id = treatment.Id,
            PatientId = treatment.PatientId,
            TreatmentType = treatment.TreatmentType,
            ToothNumber = treatment.ToothNumber,
            MaterialUsed = treatment.MaterialUsed,
            AnesthesiaType = treatment.AnesthesiaType,
            Cost = treatment.Cost,
            DoctorName = treatment.Doctor?.Name,
            Notes = treatment.Notes,
            CreatedAt = treatment.CreatedAt.ToString("yyyy-MM-dd")
        };
    }

    public async Task DeleteTreatmentAsync(Guid id)
    {
        var treatment = await db.GeneralTreatments.FindAsync(id);
        if (treatment is null)
            throw new KeyNotFoundException("العلاج غير موجود");

        treatment.IsActive = false;
        await db.SaveChangesAsync();
    }

    public async Task<List<GeneralTreatmentDto>> GetRecentTreatmentsAsync(int limit = 20)
    {
        return await db.GeneralTreatments
            .Include(t => t.Patient)
            .Include(t => t.Doctor)
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .Select(t => new GeneralTreatmentDto
            {
                Id = t.Id,
                PatientId = t.PatientId,
                PatientName = t.Patient.FirstName + " " + t.Patient.LastName,
                TreatmentType = t.TreatmentType,
                ToothNumber = t.ToothNumber,
                MaterialUsed = t.MaterialUsed,
                Cost = t.Cost,
                DoctorName = t.Doctor != null ? t.Doctor.Name : null,
                CreatedAt = t.CreatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();
    }

    // ── Perio Assessment ────────────────────────────────────────────────────────

    public async Task<PerioAssessmentDto?> GetPerioAssessmentAsync(Guid patientId)
    {
        var assessment = await db.PerioAssessments
            .Include(a => a.Doctor)
            .Where(a => a.PatientId == patientId)
            .OrderByDescending(a => a.AssessmentDate)
            .ThenByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync();

        if (assessment is null) return null;

        return MapPerioAssessment(assessment);
    }

    public async Task<List<PerioAssessmentDto>> GetPerioHistoryAsync(Guid patientId)
    {
        var assessments = await db.PerioAssessments
            .Include(a => a.Doctor)
            .Where(a => a.PatientId == patientId)
            .OrderByDescending(a => a.AssessmentDate)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync();

        return assessments.Select(MapPerioAssessment).ToList();
    }

    public async Task<PerioAssessmentDto> CreatePerioAssessmentAsync(CreatePerioAssessmentRequest req)
    {
        var assessment = new PerioAssessment
        {
            PatientId = req.PatientId,
            AssessmentDate = req.AssessmentDate != null ? DateOnly.Parse(req.AssessmentDate) : null,
            AvgPocketDepth = req.AvgPocketDepth,
            BleedingPoints = req.BleedingPoints,
            RecessionLevel = req.RecessionLevel,
            PerioStage = req.PerioStage,
            Recommendation = req.Recommendation,
            DoctorId = req.DoctorId,
            PocketDepths = req.PocketDepths != null
                ? JsonDocument.Parse(JsonSerializer.Serialize(req.PocketDepths))
                : null,
            BleedingOnProbing = req.BleedingOnProbing != null
                ? JsonDocument.Parse(JsonSerializer.Serialize(req.BleedingOnProbing))
                : null,
            Mobility = req.Mobility != null
                ? JsonDocument.Parse(JsonSerializer.Serialize(req.Mobility))
                : null,
            FurcationInvolvement = req.FurcationInvolvement != null
                ? JsonDocument.Parse(JsonSerializer.Serialize(req.FurcationInvolvement))
                : null,
            PlaqueIndex = req.PlaqueIndex,
            GingivalIndex = req.GingivalIndex,
            AttachmentLoss = req.AttachmentLoss,
            BoneLossPattern = req.BoneLossPattern,
            TreatmentPlan = req.TreatmentPlan
        };

        db.PerioAssessments.Add(assessment);
        await db.SaveChangesAsync();

        await db.Entry(assessment).Reference(a => a.Doctor).LoadAsync();

        return MapPerioAssessment(assessment);
    }

    public async Task<PerioAssessmentDto> UpdatePerioAssessmentAsync(Guid id, UpdatePerioAssessmentRequest req)
    {
        var assessment = await db.PerioAssessments
            .Include(a => a.Doctor)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (assessment is null)
            throw new KeyNotFoundException("تقييم اللثة غير موجود");

        if (req.AssessmentDate is not null)
            assessment.AssessmentDate = DateOnly.Parse(req.AssessmentDate);
        if (req.AvgPocketDepth.HasValue) assessment.AvgPocketDepth = req.AvgPocketDepth;
        if (req.BleedingPoints.HasValue) assessment.BleedingPoints = req.BleedingPoints;
        if (req.RecessionLevel is not null) assessment.RecessionLevel = req.RecessionLevel;
        if (req.PerioStage is not null) assessment.PerioStage = req.PerioStage;
        if (req.Recommendation is not null) assessment.Recommendation = req.Recommendation;
        if (req.DoctorId.HasValue) assessment.DoctorId = req.DoctorId;

        if (req.PocketDepths is not null)
            assessment.PocketDepths = JsonDocument.Parse(JsonSerializer.Serialize(req.PocketDepths));
        if (req.BleedingOnProbing is not null)
            assessment.BleedingOnProbing = JsonDocument.Parse(JsonSerializer.Serialize(req.BleedingOnProbing));
        if (req.Mobility is not null)
            assessment.Mobility = JsonDocument.Parse(JsonSerializer.Serialize(req.Mobility));
        if (req.FurcationInvolvement is not null)
            assessment.FurcationInvolvement = JsonDocument.Parse(JsonSerializer.Serialize(req.FurcationInvolvement));

        if (req.PlaqueIndex.HasValue) assessment.PlaqueIndex = req.PlaqueIndex;
        if (req.GingivalIndex.HasValue) assessment.GingivalIndex = req.GingivalIndex;
        if (req.AttachmentLoss.HasValue) assessment.AttachmentLoss = req.AttachmentLoss;
        if (req.BoneLossPattern is not null) assessment.BoneLossPattern = req.BoneLossPattern;
        if (req.TreatmentPlan is not null) assessment.TreatmentPlan = req.TreatmentPlan;

        await db.SaveChangesAsync();

        return MapPerioAssessment(assessment);
    }

    public async Task DeletePerioAssessmentAsync(Guid id)
    {
        var assessment = await db.PerioAssessments.FindAsync(id);
        if (assessment is null)
            throw new KeyNotFoundException("تقييم اللثة غير موجود");

        assessment.IsActive = false;
        await db.SaveChangesAsync();
    }

    private static PerioAssessmentDto MapPerioAssessment(PerioAssessment a)
    {
        return new PerioAssessmentDto
        {
            Id = a.Id,
            PatientId = a.PatientId,
            AssessmentDate = a.AssessmentDate?.ToString("yyyy-MM-dd"),
            AvgPocketDepth = a.AvgPocketDepth,
            BleedingPoints = a.BleedingPoints,
            RecessionLevel = a.RecessionLevel,
            PerioStage = a.PerioStage,
            Recommendation = a.Recommendation,
            DoctorName = a.Doctor?.Name,
            DoctorId = a.DoctorId,
            PocketDepths = a.PocketDepths?.Deserialize<object>(),
            BleedingOnProbing = a.BleedingOnProbing?.Deserialize<object>(),
            Mobility = a.Mobility?.Deserialize<object>(),
            FurcationInvolvement = a.FurcationInvolvement?.Deserialize<object>(),
            PlaqueIndex = a.PlaqueIndex,
            GingivalIndex = a.GingivalIndex,
            AttachmentLoss = a.AttachmentLoss,
            BoneLossPattern = a.BoneLossPattern,
            TreatmentPlan = a.TreatmentPlan,
            CreatedAt = a.CreatedAt.ToString("yyyy-MM-dd")
        };
    }
}
