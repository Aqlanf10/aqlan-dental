using AqlanDentalPro.Application.DTOs.Ortho;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Application.Services;

public class OrthoService(AppDbContext db, ICurrentUserService currentUser)
{
    public async Task<List<OrthoCaseListDto>> GetListAsync(int page, int pageSize, Guid? doctorId, string? status, string? search = null, Guid? patientId = null)
    {
        pageSize = Math.Max(1, Math.Min(pageSize, 100));
        var branchId = currentUser.BranchId;

        var query = db.OrthoCases
            .Include(c => c.Patient)
            .Include(c => c.Doctor)
            .Where(c => branchId == null || c.BranchId == branchId);

        if (doctorId.HasValue) query = query.Where(c => c.DoctorId == doctorId);
        if (patientId.HasValue) query = query.Where(c => c.PatientId == patientId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(c => c.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c =>
                c.CaseNumber.ToLower().Contains(term) ||
                c.Patient.FirstName.ToLower().Contains(term) ||
                c.Patient.LastName.ToLower().Contains(term) ||
                (c.Patient.MiddleName != null && c.Patient.MiddleName.ToLower().Contains(term)) ||
                c.Patient.PatientNumber.ToLower().Contains(term));
        }

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new OrthoCaseListDto
            {
                Id = c.Id,
                CaseNumber = c.CaseNumber,
                PatientId = c.PatientId,
                PatientName = c.Patient.FirstName + " " + c.Patient.LastName,
                PatientNumber = c.Patient.PatientNumber,
                DoctorName = c.Doctor != null ? c.Doctor.Name : null,
                DoctorColor = c.Doctor != null ? c.Doctor.Color : null,
                ApplianceType = c.ApplianceType,
                StartDate = c.StartDate.HasValue ? c.StartDate.Value.ToString("yyyy-MM-dd") : null,
                ExpectedDurationMonths = c.ExpectedDurationMonths,
                CurrentStage = c.CurrentStage,
                StagePercentage = c.StagePercentage,
                Status = c.Status,
                TotalFee = c.TotalFee
            })
            .ToListAsync();
    }

    public async Task<OrthoCaseDetailDto?> GetByIdAsync(Guid id)
    {
        var c = await db.OrthoCases
            .Include(c => c.Patient)
            .Include(c => c.Doctor)
            .Include(c => c.Stages)
            .Include(c => c.Visits.OrderByDescending(v => v.VisitDate).Take(5))
                .ThenInclude(v => v.Doctor)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (c == null) return null;

        return new OrthoCaseDetailDto
        {
            Id = c.Id,
            CaseNumber = c.CaseNumber,
            PatientId = c.PatientId,
            PatientName = c.Patient.FirstName + " " + c.Patient.LastName,
            PatientNumber = c.Patient.PatientNumber,
            DoctorId = c.DoctorId,
            DoctorName = c.Doctor?.Name,
            DoctorColor = c.Doctor?.Color,
            ApplianceType = c.ApplianceType,
            StartDate = c.StartDate?.ToString("yyyy-MM-dd"),
            ExpectedDurationMonths = c.ExpectedDurationMonths,
            CurrentStage = c.CurrentStage,
            StagePercentage = c.StagePercentage,
            Status = c.Status,
            TotalFee = c.TotalFee,
            ExtractionDecisionValue = c.ExtractionDecisionValue,
            RetentionPlan = c.RetentionPlan,
            Stages = c.Stages.OrderBy(s => s.StageOrder).Select(s => new TreatmentStageDto
            {
                Id = s.Id,
                StageName = s.StageName,
                StageOrder = s.StageOrder,
                StartedAt = s.StartedAt?.ToString("yyyy-MM-dd"),
                CompletedAt = s.CompletedAt?.ToString("yyyy-MM-dd"),
                TargetDurationMonths = s.TargetDurationMonths,
                Status = s.Status,
                Notes = s.Notes
            }).ToList(),
            RecentVisits = c.Visits.Select(v => MapVisit(v)).ToList()
        };
    }

    public async Task<OrthoCaseDetailDto> CreateAsync(CreateOrthoCaseRequest req)
    {
        var year = DateTime.UtcNow.Year;
        var count = await db.OrthoCases.IgnoreQueryFilters()
            .CountAsync(c => c.CaseNumber.StartsWith($"OR-{year}-"));
        var caseNumber = $"OR-{year}-{(count + 1):D3}";

        var orthoCase = new OrthoCase
        {
            CaseNumber = caseNumber,
            PatientId = req.PatientId,
            DoctorId = req.DoctorId,
            BranchId = currentUser.BranchId,
            ApplianceType = req.ApplianceType,
            StartDate = req.StartDate != null ? DateOnly.Parse(req.StartDate) : null,
            ExpectedDurationMonths = req.ExpectedDurationMonths,
            TotalFee = req.TotalFee,
            Status = "active"
        };

        db.OrthoCases.Add(orthoCase);

        // Create default treatment stages
        var defaultStages = new[] { "المحاذاة والتسوية", "إغلاق الفراغات", "التشطيب والتفصيل", "الاحتفاظ" };
        for (int i = 0; i < defaultStages.Length; i++)
        {
            db.TreatmentStages.Add(new TreatmentStage
            {
                OrthoCaseId = orthoCase.Id,
                StageName = defaultStages[i],
                StageOrder = i + 1,
                Status = i == 0 ? "active" : "pending"
            });
        }

        await db.SaveChangesAsync();
        return (await GetByIdAsync(orthoCase.Id))!;
    }

    public async Task<List<OrthoVisitListDto>> GetVisitsAsync(Guid caseId)
    {
        return await db.OrthoVisits
            .Include(v => v.Doctor)
            .Where(v => v.OrthoCaseId == caseId)
            .OrderByDescending(v => v.VisitDate)
            .Select(v => MapVisit(v))
            .ToListAsync();
    }

    public async Task<OrthoVisitListDto> AddVisitAsync(Guid caseId, CreateOrthoVisitRequest req)
    {
        var lastVisit = await db.OrthoVisits
            .Where(v => v.OrthoCaseId == caseId)
            .MaxAsync(v => (int?)v.VisitNumber) ?? 0;

        var visit = new OrthoVisit
        {
            OrthoCaseId = caseId,
            VisitNumber = lastVisit + 1,
            VisitDate = DateOnly.Parse(req.VisitDate),
            VisitType = req.VisitType,
            CurrentStage = req.CurrentStage,
            WireUpper = req.WireUpper,
            WireLower = req.WireLower,
            ElasticsType = req.ElasticsType,
            CurrentOverjet = req.CurrentOverjet,
            CurrentOverbite = req.CurrentOverbite,
            ClinicalNotes = req.ClinicalNotes,
            PatientInstructions = req.PatientInstructions,
            NextAppointmentDate = req.NextAppointmentDate != null ? DateOnly.Parse(req.NextAppointmentDate) : null,
            NextAppointmentType = req.NextAppointmentType,
            DoctorId = req.DoctorId
        };

        db.OrthoVisits.Add(visit);

        // Update case current stage
        if (!string.IsNullOrWhiteSpace(req.CurrentStage))
        {
            var orthoCase = await db.OrthoCases.FindAsync(caseId);
            if (orthoCase != null) orthoCase.CurrentStage = req.CurrentStage;
        }

        await db.SaveChangesAsync();

        await db.Entry(visit).Reference(v => v.Doctor).LoadAsync();
        return MapVisit(visit);
    }

    public async Task<List<TreatmentStageDto>> GetStagesAsync(Guid caseId)
    {
        return await db.TreatmentStages
            .Where(s => s.OrthoCaseId == caseId)
            .OrderBy(s => s.StageOrder)
            .Select(s => new TreatmentStageDto
            {
                Id = s.Id,
                StageName = s.StageName,
                StageOrder = s.StageOrder,
                StartedAt = s.StartedAt.HasValue ? s.StartedAt.Value.ToString("yyyy-MM-dd") : null,
                CompletedAt = s.CompletedAt.HasValue ? s.CompletedAt.Value.ToString("yyyy-MM-dd") : null,
                TargetDurationMonths = s.TargetDurationMonths,
                Status = s.Status,
                Notes = s.Notes
            })
            .ToListAsync();
    }

    public async Task<TreatmentStageDto?> UpdateStageAsync(Guid stageId, string status)
    {
        var stage = await db.TreatmentStages.FindAsync(stageId);
        if (stage == null) return null;

        stage.Status = status;
        if (status == "active" && !stage.StartedAt.HasValue)
            stage.StartedAt = DateOnly.FromDateTime(DateTime.Today);
        if (status == "completed")
            stage.CompletedAt = DateOnly.FromDateTime(DateTime.Today);

        await db.SaveChangesAsync();

        return new TreatmentStageDto
        {
            Id = stage.Id,
            StageName = stage.StageName,
            StageOrder = stage.StageOrder,
            StartedAt = stage.StartedAt?.ToString("yyyy-MM-dd"),
            CompletedAt = stage.CompletedAt?.ToString("yyyy-MM-dd"),
            TargetDurationMonths = stage.TargetDurationMonths,
            Status = stage.Status,
            Notes = stage.Notes
        };
    }

    private static OrthoVisitListDto MapVisit(OrthoVisit v) => new()
    {
        Id = v.Id,
        VisitNumber = v.VisitNumber,
        VisitDate = v.VisitDate.ToString("yyyy-MM-dd"),
        VisitType = v.VisitType,
        CurrentStage = v.CurrentStage,
        WireUpper = v.WireUpper,
        WireLower = v.WireLower,
        ElasticsType = v.ElasticsType,
        CurrentOverjet = v.CurrentOverjet,
        CurrentOverbite = v.CurrentOverbite,
        ClinicalNotes = v.ClinicalNotes,
        NextAppointmentDate = v.NextAppointmentDate?.ToString("yyyy-MM-dd"),
        NextAppointmentType = v.NextAppointmentType,
        DoctorName = v.Doctor?.Name
    };
}
