using AqlanDentalPro.Application.DTOs.General;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Application.Services;

public class GeneralService(AppDbContext db)
{
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

    // ── Periodontal Records ──────────────────────────────────────────────────

    public async Task<PerioRecordDto> CreatePerioRecordAsync(CreatePerioRecordRequest req)
    {
        var record = new PerioRecord
        {
            PatientId = req.PatientId,
            ToothNumber = req.ToothNumber,
            ProbingDepth = req.ProbingDepth,
            ClinicalAttachment = req.ClinicalAttachment,
            BleedingOnProbing = req.BleedingOnProbing,
            PlaqueIndex = req.PlaqueIndex,
            GingivalIndex = req.GingivalIndex,
            Furcation = req.Furcation,
            Mobility = req.Mobility,
            Notes = req.Notes,
            DoctorId = req.DoctorId
        };

        db.PerioRecords.Add(record);
        await db.SaveChangesAsync();

        await db.Entry(record).Reference(r => r.Doctor).LoadAsync();

        return MapPerioRecord(record);
    }

    public async Task<List<PerioRecordDto>> GetPerioRecordsAsync(Guid patientId)
    {
        return await db.PerioRecords
            .Include(r => r.Doctor)
            .Where(r => r.PatientId == patientId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => MapPerioRecord(r))
            .ToListAsync();
    }

    private static PerioRecordDto MapPerioRecord(PerioRecord r) => new()
    {
        Id = r.Id,
        PatientId = r.PatientId,
        ToothNumber = r.ToothNumber,
        ProbingDepth = r.ProbingDepth,
        ClinicalAttachment = r.ClinicalAttachment,
        BleedingOnProbing = r.BleedingOnProbing,
        PlaqueIndex = r.PlaqueIndex,
        GingivalIndex = r.GingivalIndex,
        Furcation = r.Furcation,
        Mobility = r.Mobility,
        Notes = r.Notes,
        DoctorName = r.Doctor?.Name,
        CreatedAt = r.CreatedAt.ToString("yyyy-MM-dd")
    };

    // ── Treatment Plan Items ─────────────────────────────────────────────────

    public async Task<TreatmentPlanItemDto> CreateTreatmentPlanItemAsync(CreateTreatmentPlanItemRequest req)
    {
        var item = new GeneralTreatmentPlanItem
        {
            PatientId = req.PatientId,
            ToothNumber = req.ToothNumber,
            Treatment = req.Treatment,
            Priority = req.Priority,
            Status = "planned",
            EstimatedCost = req.EstimatedCost,
            Notes = req.Notes,
            DoctorId = req.DoctorId
        };

        db.GeneralTreatmentPlanItems.Add(item);
        await db.SaveChangesAsync();

        await db.Entry(item).Reference(i => i.Doctor).LoadAsync();

        return MapTreatmentPlanItem(item);
    }

    public async Task<List<TreatmentPlanItemDto>> GetTreatmentPlanItemsAsync(Guid patientId)
    {
        return await db.GeneralTreatmentPlanItems
            .Include(i => i.Doctor)
            .Where(i => i.PatientId == patientId)
            .OrderBy(i => i.Status == "completed" ? 1 : i.Priority == "high" ? 0 : 2)
            .ThenBy(i => i.CreatedAt)
            .Select(i => MapTreatmentPlanItem(i))
            .ToListAsync();
    }

    public async Task<TreatmentPlanItemDto?> UpdateTreatmentPlanItemStatusAsync(Guid itemId, string status)
    {
        var item = await db.GeneralTreatmentPlanItems.Include(i => i.Doctor).FirstOrDefaultAsync(i => i.Id == itemId);
        if (item == null) return null;

        item.Status = status;
        if (status == "completed")
            item.CompletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return MapTreatmentPlanItem(item);
    }

    private static TreatmentPlanItemDto MapTreatmentPlanItem(GeneralTreatmentPlanItem i) => new()
    {
        Id = i.Id,
        PatientId = i.PatientId,
        ToothNumber = i.ToothNumber,
        Treatment = i.Treatment,
        Priority = i.Priority,
        Status = i.Status,
        EstimatedCost = i.EstimatedCost,
        Notes = i.Notes,
        DoctorName = i.Doctor?.Name,
        CreatedAt = i.CreatedAt.ToString("yyyy-MM-dd"),
        CompletedAt = i.CompletedAt?.ToString("yyyy-MM-dd")
    };
}
