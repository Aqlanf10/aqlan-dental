using AqlanDentalPro.Application.DTOs.Ortho;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
        // SURG-FIX (cross-cutting): normalize snake_case → PascalCase before Enum.TryParse.
        // Same bug as SurgeryController: frontend sends "on_hold" (snake_case) but the enum is
        // OnHold (PascalCase). Enum.TryParse doesn't handle underscores, so without normalization
        // the filter silently drops and ALL ortho cases are returned instead of just on_hold ones.
        // "on_hold" → "onhold" → matches OnHold case-insensitively.
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrthoCaseStatus>(NormalizeStatusEnum(status), true, out var orthoStatus))
            query = query.Where(c => c.Status == orthoStatus);
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
                Status = c.Status.ToString(),
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
            Status = c.Status.ToString(),
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
        const int maxRetries = 3;
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                // CON FIX: Advisory lock to prevent race condition on case number generation
                var lockKey = Math.Abs("OrthoCaseNumber".GetHashCode()) % 100000;
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

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
                    Status = OrthoCaseStatus.Active
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

                try
                {
                    await db.SaveChangesAsync();
                    await tx.CommitAsync();
                }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex))
                {
                    await tx.RollbackAsync();
                    continue;
                }

                return (await GetByIdAsync(orthoCase.Id))!;
            }
            catch (Exception ex) when (ex is not DbUpdateException)
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        throw new InvalidOperationException("فشل إنشاء حالة التقويم بعد عدة محاولات");
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

        var visitDate = DateOnly.Parse(req.VisitDate);

        var visit = new OrthoVisit
        {
            OrthoCaseId = caseId,
            VisitNumber = lastVisit + 1,
            VisitDate = visitDate,
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

        // CLIN-05: Unify the parallel visit concepts.
        // In addition to the OrthoVisit row (read by the ortho case detail page),
        // create or refresh a linked Visit row (read by daily-operations) so the
        // orthodontist's daily clinical activity shows up in the daily-operations
        // screen. Both rows are persisted by the same SaveChangesAsync call below
        // — if either fails, neither is committed (atomic).
        //
        // Idempotent by VisitDate: if a Visit already exists for this OrthoCase
        // on the same calendar day (e.g. a back-to-back POST or a same-day edit),
        // we link to it and refresh the mirrored ortho fields instead of inserting
        // a duplicate.
        var orthoCase = await db.OrthoCases
            .Where(c => c.Id == caseId)
            .Select(c => new { c.PatientId, c.DoctorId, c.BranchId })
            .FirstOrDefaultAsync();

        if (orthoCase is not null)
        {
            var existingLinkedVisit = await db.Visits
                .Where(v => v.OrthoCaseId == caseId && v.VisitDate == visitDate && v.IsActive)
                .OrderByDescending(v => v.UpdatedAt)
                .FirstOrDefaultAsync();

            if (existingLinkedVisit is null)
            {
                db.Visits.Add(new Visit
                {
                    PatientId = orthoCase.PatientId,
                    DoctorId = req.DoctorId ?? orthoCase.DoctorId,
                    VisitDate = visitDate,
                    VisitType = req.VisitType,
                    Specialty = Specialty.Orthodontics,
                    OrthoCaseId = caseId,
                    ChiefComplaint = req.VisitType,
                    ClinicalNotes = req.ClinicalNotes,
                    WireUpper = req.WireUpper,
                    WireLower = req.WireLower,
                    CurrentStage = req.CurrentStage,
                    NextVisitDate = req.NextAppointmentDate != null
                        ? DateOnly.Parse(req.NextAppointmentDate)
                        : null,
                    NextVisitPlan = req.NextAppointmentType,
                });
            }
            else
            {
                // Refresh mirrored ortho fields on the existing same-day Visit.
                existingLinkedVisit.WireUpper = req.WireUpper;
                existingLinkedVisit.WireLower = req.WireLower;
                existingLinkedVisit.CurrentStage = req.CurrentStage;
                if (!string.IsNullOrWhiteSpace(req.ClinicalNotes))
                    existingLinkedVisit.ClinicalNotes = req.ClinicalNotes;
                if (!string.IsNullOrWhiteSpace(req.VisitType))
                    existingLinkedVisit.VisitType = req.VisitType;
            }
        }

        // Update case current stage
        if (!string.IsNullOrWhiteSpace(req.CurrentStage))
        {
            var orthoCaseForStage = await db.OrthoCases.FindAsync(caseId);
            if (orthoCaseForStage != null) orthoCaseForStage.CurrentStage = req.CurrentStage;
        }

        await db.SaveChangesAsync();

        await db.Entry(visit).Reference(v => v.Doctor).LoadAsync();
        return MapVisit(visit);
    }

    /// <summary>
    /// Sprint 3 — Edit an existing OrthoVisit and atomically sync the linked daily-operations
    /// <see cref="Visit"/> row (CLIN-05 mirror). The linked Visit is matched by (OrthoCaseId, VisitDate)
    /// using the visit's ORIGINAL date (captured before the update), so changing the date moves
    /// the linked Visit along with it instead of orphaning it. Wrapped in an explicit transaction
    /// so a partial sync (OrthoVisit updated but linked Visit failed) never commits.
    /// Returns null when the visit does not exist, is soft-deleted, or does not belong to this
    /// OrthoCase — the controller maps that to a 404 with an Arabic message.
    /// </summary>
    public async Task<OrthoVisitListDto?> UpdateVisitAsync(Guid caseId, Guid visitId, CreateOrthoVisitRequest req)
    {
        var visit = await db.OrthoVisits
            .FirstOrDefaultAsync(v => v.Id == visitId && v.OrthoCaseId == caseId && v.IsActive);
        if (visit is null) return null;

        var originalDate = visit.VisitDate;
        var newDate = DateOnly.Parse(req.VisitDate);

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            visit.VisitDate = newDate;
            visit.VisitType = req.VisitType;
            visit.CurrentStage = req.CurrentStage;
            visit.WireUpper = req.WireUpper;
            visit.WireLower = req.WireLower;
            visit.ElasticsType = req.ElasticsType;
            visit.CurrentOverjet = req.CurrentOverjet;
            visit.CurrentOverbite = req.CurrentOverbite;
            visit.ClinicalNotes = req.ClinicalNotes;
            visit.PatientInstructions = req.PatientInstructions;
            visit.NextAppointmentDate = req.NextAppointmentDate != null
                ? DateOnly.Parse(req.NextAppointmentDate)
                : null;
            visit.NextAppointmentType = req.NextAppointmentType;
            if (req.DoctorId.HasValue)
                visit.DoctorId = req.DoctorId;
            visit.UpdatedAt = DateTime.UtcNow;

            // CLIN-05: sync the linked Visit row (matched on the ORIGINAL date — the same row
            // AddVisitAsync would have created/refreshed). Update mirrored fields + date so the
            // daily-operations view of this clinical session stays consistent.
            var linkedVisit = await db.Visits
                .Where(v => v.OrthoCaseId == caseId && v.VisitDate == originalDate && v.IsActive)
                .OrderByDescending(v => v.UpdatedAt)
                .FirstOrDefaultAsync();

            if (linkedVisit is not null)
            {
                linkedVisit.VisitDate = newDate;
                linkedVisit.WireUpper = req.WireUpper;
                linkedVisit.WireLower = req.WireLower;
                linkedVisit.CurrentStage = req.CurrentStage;
                if (!string.IsNullOrWhiteSpace(req.ClinicalNotes))
                    linkedVisit.ClinicalNotes = req.ClinicalNotes;
                if (!string.IsNullOrWhiteSpace(req.VisitType))
                    linkedVisit.VisitType = req.VisitType;
                if (req.NextAppointmentDate != null)
                    linkedVisit.NextVisitDate = DateOnly.Parse(req.NextAppointmentDate);
                if (!string.IsNullOrWhiteSpace(req.NextAppointmentType))
                    linkedVisit.NextVisitPlan = req.NextAppointmentType;
                if (req.DoctorId.HasValue)
                    linkedVisit.DoctorId = req.DoctorId;
                linkedVisit.UpdatedAt = DateTime.UtcNow;
            }

            // Mirror the current stage onto the OrthoCase (matches AddVisitAsync behaviour).
            if (!string.IsNullOrWhiteSpace(req.CurrentStage))
            {
                var orthoCaseForStage = await db.OrthoCases.FindAsync(caseId);
                if (orthoCaseForStage != null) orthoCaseForStage.CurrentStage = req.CurrentStage;
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        await db.Entry(visit).Reference(v => v.Doctor).LoadAsync();
        return MapVisit(visit);
    }

    /// <summary>
    /// Sprint 3 — Soft-delete an OrthoVisit (IsActive = false) and unlink the linked
    /// daily-operations Visit row by nulling its OrthoCaseId (the Visit row itself is preserved —
    /// it may carry payments/checkouts per CLIN-05). Returns false when the visit does not exist,
    /// is already soft-deleted, or does not belong to this OrthoCase.
    /// </summary>
    public async Task<bool> DeleteVisitAsync(Guid caseId, Guid visitId, Guid? deletedByUserId)
    {
        var visit = await db.OrthoVisits
            .FirstOrDefaultAsync(v => v.Id == visitId && v.OrthoCaseId == caseId && v.IsActive);
        if (visit is null) return false;

        visit.IsActive = false;
        visit.DeletedAt = DateTime.UtcNow;
        visit.DeletedBy = deletedByUserId;
        visit.UpdatedAt = DateTime.UtcNow;

        // CLIN-05: unlink any Visit row pointing at this OrthoCase for the same date —
        // do NOT hard-delete (the Visit may carry payments, checkouts, or prescriptions).
        var linkedVisits = await db.Visits
            .Where(v => v.OrthoCaseId == caseId && v.VisitDate == visit.VisitDate && v.IsActive)
            .ToListAsync();
        foreach (var lv in linkedVisits)
        {
            lv.OrthoCaseId = null;
            lv.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return true;
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

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is NpgsqlException pgEx && pgEx.SqlState == "23505";

    // SURG-FIX (cross-cutting): Converts snake_case status strings (the frontend contract, e.g.
    // "on_hold") to a form Enum.TryParse can match against PascalCase enum names (OnHold).
    // Mirrors SurgeryController.NormalizeStatusEnum — see that method for the full rationale.
    private static string NormalizeStatusEnum(string? s) =>
        string.IsNullOrWhiteSpace(s) ? "" : s.Trim().Replace("_", "");
}
