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

    // ─── Treatment Plan A/B ──────────────────────────────────────────────────────

    public async Task<TreatmentPlanListDto> GetAllTreatmentPlansAsync(Guid orthoCaseId)
    {
        var plans = await db.TreatmentPlans
            .Include(p => p.ApprovedByDoctor)
            .Where(p => p.OrthoCaseId == orthoCaseId)
            .OrderBy(p => p.PlanLabel)
            .ThenByDescending(p => p.PlanVersion)
            .ToListAsync();

        var planDtos = plans.Select(MapTreatmentPlan).ToList();
        var selectedPlan = planDtos.FirstOrDefault(p => p.IsSelected) ?? planDtos.FirstOrDefault();

        return new TreatmentPlanListDto
        {
            Plans = planDtos,
            SelectedPlan = selectedPlan
        };
    }

    public async Task<TreatmentPlanDto> SaveTreatmentPlanAsync(Guid orthoCaseId, UpsertTreatmentPlanServiceRequest req)
    {
        var orthoCase = await db.OrthoCases.FindAsync(orthoCaseId)
            ?? throw new InvalidOperationException("الحالة التقويمية غير موجودة");

        var planLabel = req.PlanLabel ?? "A";

        // If PlanLabel = "B" and no existing B plan, create a new one
        // If PlanLabel = "A" and existing A plan, update it
        TreatmentPlan plan;

        if (planLabel == "B")
        {
            // Check if a B plan already exists for this case
            var existingB = await db.TreatmentPlans
                .Where(p => p.OrthoCaseId == orthoCaseId && p.PlanLabel == "B")
                .OrderByDescending(p => p.PlanVersion)
                .FirstOrDefaultAsync();

            if (existingB != null)
            {
                // Update existing B plan
                plan = existingB;
            }
            else
            {
                // Create new B plan
                var maxVersion = await db.TreatmentPlans
                    .Where(p => p.OrthoCaseId == orthoCaseId)
                    .MaxAsync(p => (int?)p.PlanVersion) ?? 0;

                plan = new TreatmentPlan
                {
                    OrthoCaseId = orthoCaseId,
                    PlanLabel = "B",
                    PlanVersion = maxVersion + 1,
                    IsSelected = false
                };
                db.TreatmentPlans.Add(plan);
            }
        }
        else
        {
            // Plan A — upsert
            var existingA = await db.TreatmentPlans
                .Where(p => p.OrthoCaseId == orthoCaseId && p.PlanLabel == "A")
                .OrderByDescending(p => p.PlanVersion)
                .FirstOrDefaultAsync();

            if (existingA != null)
            {
                plan = existingA;
            }
            else
            {
                plan = new TreatmentPlan
                {
                    OrthoCaseId = orthoCaseId,
                    PlanLabel = "A",
                    PlanVersion = 1,
                    IsSelected = true
                };
                db.TreatmentPlans.Add(plan);
            }
        }

        plan.ApplianceType = req.ApplianceType;
        plan.BracketSystem = req.BracketSystem;
        plan.InitialWire = req.InitialWire;
        plan.ExtractionPlan = req.ExtractionPlan;
        plan.AnchoragePlan = req.AnchoragePlan;
        plan.UseTads = req.UseTads;
        plan.UseElastics = req.UseElastics;
        plan.ExpectedDurationMonths = req.ExpectedDurationMonths;
        plan.RetentionPlan = req.RetentionPlan;
        plan.TreatmentGoals = req.TreatmentGoals;
        plan.RisksLimitations = req.RisksLimitations;

        await db.SaveChangesAsync();

        // Reload with navigation property
        await db.Entry(plan).Reference(p => p.ApprovedByDoctor).LoadAsync();

        return MapTreatmentPlan(plan);
    }

    public async Task<TreatmentPlanDto?> SelectTreatmentPlanAsync(Guid orthoCaseId, Guid planId)
    {
        var orthoCase = await db.OrthoCases.FindAsync(orthoCaseId);
        if (orthoCase is null) return null;

        var allPlans = await db.TreatmentPlans
            .Where(p => p.OrthoCaseId == orthoCaseId)
            .ToListAsync();

        var targetPlan = allPlans.FirstOrDefault(p => p.Id == planId);
        if (targetPlan is null) return null;

        // Deselect all plans
        foreach (var p in allPlans)
        {
            p.IsSelected = false;
        }

        // Select the target plan
        targetPlan.IsSelected = true;

        await db.SaveChangesAsync();

        await db.Entry(targetPlan).Reference(p => p.ApprovedByDoctor).LoadAsync();
        return MapTreatmentPlan(targetPlan);
    }

    private static TreatmentPlanDto MapTreatmentPlan(TreatmentPlan p) => new()
    {
        Id = p.Id,
        OrthoCaseId = p.OrthoCaseId,
        PlanVersion = p.PlanVersion,
        PlanLabel = p.PlanLabel,
        IsSelected = p.IsSelected,
        IsApproved = p.IsApproved,
        ApplianceType = p.ApplianceType,
        BracketSystem = p.BracketSystem,
        InitialWire = p.InitialWire,
        ExtractionPlan = p.ExtractionPlan,
        AnchoragePlan = p.AnchoragePlan,
        UseTads = p.UseTads,
        UseElastics = p.UseElastics,
        ExpectedDurationMonths = p.ExpectedDurationMonths,
        RetentionPlan = p.RetentionPlan,
        TreatmentGoals = p.TreatmentGoals,
        RisksLimitations = p.RisksLimitations,
        ApprovedByName = p.ApprovedByDoctor?.Name,
        ApprovedAt = p.ApprovedAt?.ToString("yyyy-MM-dd")
    };

    // ─── Model Analysis ─────────────────────────────────────────────────────────

    public async Task<ModelAnalysisDto?> GetModelAnalysisAsync(Guid orthoCaseId)
    {
        var analysis = await db.ModelAnalyses
            .Where(a => a.OrthoCaseId == orthoCaseId)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync();

        if (analysis is null) return null;

        return new ModelAnalysisDto
        {
            Id = analysis.Id,
            OrthoCaseId = analysis.OrthoCaseId,
            AnalysisDate = analysis.AnalysisDate?.ToString("yyyy-MM-dd"),
            BoltonOverall = analysis.BoltonOverall,
            BoltonAnterior = analysis.BoltonAnterior,
            UpperSum12 = analysis.UpperSum12,
            LowerSum12 = analysis.LowerSum12,
            UpperArchLength = analysis.UpperArchLength,
            LowerArchLength = analysis.LowerArchLength,
            UpperAld = analysis.UpperAld,
            LowerAld = analysis.LowerAld,
            PontIndex = analysis.PontIndex,
            Notes = analysis.Notes
        };
    }

    public async Task<ModelAnalysisDto> SaveModelAnalysisAsync(Guid orthoCaseId, SaveModelAnalysisRequest req)
    {
        var orthoCase = await db.OrthoCases.FindAsync(orthoCaseId)
            ?? throw new InvalidOperationException("الحالة التقويمية غير موجودة");

        var existing = await db.ModelAnalyses
            .Where(a => a.OrthoCaseId == orthoCaseId)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync();

        if (existing is null)
        {
            existing = new ModelAnalysis { OrthoCaseId = orthoCaseId };
            db.ModelAnalyses.Add(existing);
        }

        existing.AnalysisDate = req.AnalysisDate != null
            ? DateOnly.Parse(req.AnalysisDate)
            : DateOnly.FromDateTime(DateTime.Today);
        existing.BoltonOverall = req.BoltonOverall;
        existing.BoltonAnterior = req.BoltonAnterior;
        existing.UpperSum12 = req.UpperSum12;
        existing.LowerSum12 = req.LowerSum12;
        existing.UpperArchLength = req.UpperArchLength;
        existing.LowerArchLength = req.LowerArchLength;
        existing.UpperAld = req.UpperAld;
        existing.LowerAld = req.LowerAld;
        existing.PontIndex = req.PontIndex;
        existing.Notes = req.Notes;

        await db.SaveChangesAsync();

        return new ModelAnalysisDto
        {
            Id = existing.Id,
            OrthoCaseId = existing.OrthoCaseId,
            AnalysisDate = existing.AnalysisDate?.ToString("yyyy-MM-dd"),
            BoltonOverall = existing.BoltonOverall,
            BoltonAnterior = existing.BoltonAnterior,
            UpperSum12 = existing.UpperSum12,
            LowerSum12 = existing.LowerSum12,
            UpperArchLength = existing.UpperArchLength,
            LowerArchLength = existing.LowerArchLength,
            UpperAld = existing.UpperAld,
            LowerAld = existing.LowerAld,
            PontIndex = existing.PontIndex,
            Notes = existing.Notes
        };
    }

    public async Task<BoltonResult> CalculateBoltonAsync(Guid orthoCaseId, BoltonCalculationRequest req)
    {
        // Verify the ortho case exists
        var orthoCase = await db.OrthoCases.FindAsync(orthoCaseId)
            ?? throw new InvalidOperationException("الحالة التقويمية غير موجودة");

        var upperSum = req.UpperWidths.Sum();
        var lowerSum = req.LowerWidths.Sum();

        // Overall Bolton ratio
        var overallRatio = upperSum > 0 ? Math.Round(lowerSum / upperSum * 100, 2) : 0;
        var normalOverall = 91.3m;
        var overallDiscrepancy = 0m;

        if (upperSum > 0)
        {
            // Ideal lower sum for normal ratio
            var idealLower = upperSum * normalOverall / 100;
            overallDiscrepancy = Math.Round(lowerSum - idealLower, 2);
        }

        // Anterior Bolton ratio
        decimal? anteriorRatio = null;
        decimal? anteriorDiscrepancy = null;
        var normalAnterior = 77.2m;

        var upperAnteriorSum = req.UpperAnteriorWidths.Sum();
        var lowerAnteriorSum = req.LowerAnteriorWidths.Sum();

        if (upperAnteriorSum > 0)
        {
            anteriorRatio = Math.Round(lowerAnteriorSum / upperAnteriorSum * 100, 2);
            var idealLowerAnterior = upperAnteriorSum * normalAnterior / 100;
            anteriorDiscrepancy = Math.Round(lowerAnteriorSum - idealLowerAnterior, 2);
        }

        // Build Arabic interpretation
        var interpretation = BuildBoltonInterpretation(overallRatio, normalOverall, overallDiscrepancy,
            anteriorRatio, normalAnterior, anteriorDiscrepancy);

        return new BoltonResult
        {
            OverallRatio = overallRatio,
            NormalOverall = normalOverall,
            OverallDiscrepancy = overallDiscrepancy,
            AnteriorRatio = anteriorRatio,
            NormalAnterior = normalAnterior,
            AnteriorDiscrepancy = anteriorDiscrepancy,
            InterpretationAr = interpretation
        };
    }

    private static string BuildBoltonInterpretation(
        decimal overallRatio, decimal normalOverall, decimal overallDiscrepancy,
        decimal? anteriorRatio, decimal normalAnterior, decimal? anteriorDiscrepancy)
    {
        var parts = new List<string>();

        // Overall interpretation
        if (overallRatio > normalOverall + 1.91m)
        {
            parts.Add($"نسبة بولتون الكلية ({overallRatio}%) أعلى من المعدل الطبيعي ({normalOverall}%): " +
                      $"زيادة في المادة السنية للفك السفلي بمقدار {Math.Abs(overallDiscrepancy):F2} مم");
        }
        else if (overallRatio < normalOverall - 1.91m)
        {
            parts.Add($"نسبة بولتون الكلية ({overallRatio}%) أقل من المعدل الطبيعي ({normalOverall}%): " +
                      $"زيادة في المادة السنية للفك العلوي بمقدار {Math.Abs(overallDiscrepancy):F2} مم");
        }
        else
        {
            parts.Add($"نسبة بولتون الكلية ({overallRatio}%) ضمن المعدل الطبيعي ({normalOverall}% ± 1.91)");
        }

        // Anterior interpretation
        if (anteriorRatio.HasValue && anteriorDiscrepancy.HasValue)
        {
            if (anteriorRatio > normalAnterior + 1.65m)
            {
                parts.Add($"نسبة بولتون الأمامية ({anteriorRatio}%) أعلى من المعدل الطبيعي ({normalAnterior}%): " +
                          $"زيادة في المادة السنية الأمامية للفك السفلي بمقدار {Math.Abs(anteriorDiscrepancy.Value):F2} مم");
            }
            else if (anteriorRatio < normalAnterior - 1.65m)
            {
                parts.Add($"نسبة بولتون الأمامية ({anteriorRatio}%) أقل من المعدل الطبيعي ({normalAnterior}%): " +
                          $"زيادة في المادة السنية الأمامية للفك العلوي بمقدار {Math.Abs(anteriorDiscrepancy.Value):F2} مم");
            }
            else
            {
                parts.Add($"نسبة بولتون الأمامية ({anteriorRatio}%) ضمن المعدل الطبيعي ({normalAnterior}% ± 1.65)");
            }
        }

        return string.Join(". ", parts);
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
