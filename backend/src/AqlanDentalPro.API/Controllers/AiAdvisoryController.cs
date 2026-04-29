using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize(Policy = "AIAccess")]
public class AiAdvisoryController(AppDbContext db) : ControllerBase
{
    // GET /api/ai/ortho-case-data/{id}
    // Aggregates ALL ortho case data (clinical exam, ceph, problem list, treatment plan, visits,
    // extraction decision, model analysis, retention) into a single JSON object for AI consumption
    [HttpGet("ortho-case-data/{id:guid}")]
    public async Task<IActionResult> GetOrthoCaseDataForAi(Guid id)
    {
        var orthoCase = await db.OrthoCases
            .Include(c => c.Patient)
            .Include(c => c.Doctor)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (orthoCase is null) return NotFound(new { message = "الحالة التقويمية غير موجودة" });

        // Clinical exam
        var clinicalExam = await db.OrthoClinicalExams
            .Where(e => e.OrthoCaseId == id)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync();

        // Problem list
        var problemList = await db.ProblemListItems
            .Where(p => p.OrthoCaseId == id)
            .OrderBy(p => p.SortOrder)
            .ToListAsync();

        // Latest ceph analysis
        var cephAnalysis = await db.CephAnalyses
            .Where(a => a.OrthoCaseId == id)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync();

        List<object>? cephMeasurements = null;
        if (cephAnalysis is not null)
        {
            cephMeasurements = await db.CephMeasurements
                .Where(m => m.AnalysisId == cephAnalysis.Id)
                .Select(m => new { m.MeasurementName, m.MeasurementValue, m.NormalValue, m.Unit, m.Classification })
                .Cast<object>()
                .ToListAsync();
        }

        // Treatment plan
        var treatmentPlan = await db.TreatmentPlans
            .Where(p => p.OrthoCaseId == id && p.IsSelected)
            .FirstOrDefaultAsync();

        // Visits count
        var visitsCount = await db.OrthoVisits.CountAsync(v => v.OrthoCaseId == id);

        // Extraction decision
        var extractionDecision = await db.ExtractionDecisions
            .Where(e => e.OrthoCaseId == id)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync();

        // Model analysis
        var modelAnalysis = await db.ModelAnalyses
            .Where(m => m.OrthoCaseId == id)
            .FirstOrDefaultAsync();

        // Stages
        var stages = await db.TreatmentStages
            .Where(s => s.OrthoCaseId == id)
            .OrderBy(s => s.StageOrder)
            .Select(s => new { s.StageName, s.Status, s.StartedAt, s.CompletedAt })
            .ToListAsync();

        return Ok(new
        {
            OrthoCase = new
            {
                orthoCase.CaseNumber,
                PatientName = orthoCase.Patient != null ? $"{orthoCase.Patient.FirstName} {orthoCase.Patient.MiddleName} {orthoCase.Patient.LastName}" : null,
                PatientAge = orthoCase.Patient?.DateOfBirth.HasValue == true ? (int?)DateTime.Today.Year - orthoCase.Patient.DateOfBirth!.Value.Year : null,
                PatientGender = orthoCase.Patient?.Gender,
                DoctorName = orthoCase.Doctor?.Name,
                orthoCase.ApplianceType,
                StartDate = orthoCase.StartDate?.ToString("yyyy-MM-dd"),
                orthoCase.ExpectedDurationMonths,
                orthoCase.CurrentStage,
                orthoCase.StagePercentage,
                orthoCase.Status,
                orthoCase.ExtractionDecisionValue,
                orthoCase.TotalFee,
            },
            ClinicalExam = clinicalExam != null ? new
            {
                clinicalExam.FacialSymmetry,
                clinicalExam.Profile,
                clinicalExam.LipsCompetence,
                clinicalExam.SmileLine,
                clinicalExam.VerticalProportion,
                clinicalExam.MolarRelation,
                clinicalExam.CanineRelation,
                clinicalExam.Overjet,
                clinicalExam.Overbite,
                clinicalExam.Crossbite,
                clinicalExam.OpenBite,
                clinicalExam.UpperCrowding,
                clinicalExam.LowerCrowding,
                clinicalExam.UpperSpacing,
                clinicalExam.MidlineUpper,
                clinicalExam.MidlineLower,
                clinicalExam.CoCrDiscrepancy,
                clinicalExam.TmjFindings,
                clinicalExam.Habits,
            } : null,
            ProblemList = problemList.Select(p => new { p.Category, p.Description, p.Severity }),
            CephAnalysis = cephAnalysis != null ? new
            {
                cephAnalysis.AnalysisType,
                cephAnalysis.IsAutoTraced,
                cephAnalysis.AiAssisted,
                Measurements = cephMeasurements,
            } : null,
            TreatmentPlan = treatmentPlan != null ? new
            {
                treatmentPlan.PlanLabel,
                treatmentPlan.ApplianceType,
                treatmentPlan.BracketSystem,
                treatmentPlan.ExtractionPlan,
                treatmentPlan.AnchoragePlan,
                treatmentPlan.UseTads,
                treatmentPlan.UseElastics,
                treatmentPlan.ExpectedDurationMonths,
                treatmentPlan.TreatmentGoals,
                treatmentPlan.RisksLimitations,
            } : null,
            ExtractionDecision = extractionDecision != null ? new
            {
                extractionDecision.Decision,
                extractionDecision.AiRecommendation,
                extractionDecision.DoctorNotes,
            } : null,
            ModelAnalysis = modelAnalysis != null ? new
            {
                modelAnalysis.BoltonOverall,
                modelAnalysis.BoltonAnterior,
                modelAnalysis.UpperArchLength,
                modelAnalysis.LowerArchLength,
                modelAnalysis.UpperAld,
                modelAnalysis.LowerAld,
                modelAnalysis.PontIndex,
            } : null,
            Stages = stages,
            TotalVisits = visitsCount,
        });
    }

    // POST /api/ai/save-recommendation
    // Saves an AI recommendation to the database
    [HttpPost("save-recommendation")]
    public async Task<IActionResult> SaveRecommendation([FromBody] SaveAiRecommendationRequest req)
    {
        var recommendation = new AqlanDentalPro.Domain.Entities.AiRecommendation
        {
            PatientId = req.PatientId,
            Context = req.Context,
            InputData = req.InputData != null ? JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(req.InputData)) : null,
            Recommendation = req.Recommendation,
            Confidence = req.Confidence,
            DoctorAction = "pending",
        };

        db.AiRecommendations.Add(recommendation);
        await db.SaveChangesAsync();

        return Ok(new { recommendation.Id, message = "تم حفظ التوصية" });
    }

    // PUT /api/ai/recommendation/{id}/action
    // Doctor approves/rejects/modifies an AI recommendation
    [HttpPut("recommendation/{id:guid}/action")]
    public async Task<IActionResult> UpdateRecommendationAction(Guid id, [FromBody] UpdateAiRecommendationActionRequest req)
    {
        var rec = await db.AiRecommendations.FindAsync(id);
        if (rec is null) return NotFound(new { message = "التوصية غير موجودة" });

        rec.DoctorAction = req.Action; // approved, rejected, modified
        rec.DoctorId = req.DoctorId;
        rec.ActionAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok(new { rec.Id, rec.DoctorAction, message = "تم تحديث إجراء الطبيب" });
    }

    // GET /api/ai/recommendations/{patientId}
    // Get all AI recommendations for a patient
    [HttpGet("recommendations/{patientId:guid}")]
    public async Task<IActionResult> GetRecommendations(Guid patientId, [FromQuery] string? context = null)
    {
        var query = db.AiRecommendations
            .Where(r => r.PatientId == patientId);

        if (!string.IsNullOrEmpty(context))
            query = query.Where(r => r.Context == context);

        var recommendations = await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.Context,
                r.Recommendation,
                r.Confidence,
                r.DoctorAction,
                DoctorName = r.Doctor != null ? r.Doctor.Name : null,
                ActionAt = r.ActionAt.HasValue ? r.ActionAt.Value.ToString("yyyy-MM-dd HH:mm") : null,
                CreatedAt = r.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
            })
            .Take(20)
            .ToListAsync();

        return Ok(recommendations);
    }

    // GET /api/ai/smart-alerts
    // Returns smart alerts for the current user (overdue contracts, delayed ortho cases, due payments, etc.)
    [HttpGet("smart-alerts")]
    public async Task<IActionResult> GetSmartAlerts()
    {
        var alerts = new List<object>();

        // 1. Overdue contracts (payments past due date)
        var overdueContracts = await db.Contracts
            .Include(c => c.Patient)
            .Where(c => c.Status == "active" && c.StartDate != null)
            .ToListAsync();

        foreach (var contract in overdueContracts)
        {
            var totalPaid = await db.Payments
                .Where(p => p.ContractId == contract.Id)
                .SumAsync(p => (decimal?)p.Amount) ?? 0;

            var remaining = contract.TotalAmount - contract.DownPayment - totalPaid;
            if (remaining > 0)
            {
                var monthsElapsed = contract.StartDate.HasValue
                    ? Math.Max(0, (DateTime.Today - contract.StartDate.Value.ToDateTime(TimeOnly.MinValue)).Days / 30)
                    : 0;
                var expectedPayments = contract.DownPayment + (contract.InstallmentsCount > 0
                    ? (contract.InstallmentAmount ?? 0) * Math.Min(monthsElapsed, contract.InstallmentsCount)
                    : 0);

                if (totalPaid < expectedPayments - (contract.InstallmentAmount ?? 0))
                {
                    alerts.Add(new
                    {
                        Type = "overdue_payment",
                        Severity = "warning",
                        Title = "قسط متأخر",
                        Message = $"المريض {contract.Patient?.FirstName} {contract.Patient?.LastName} لديه أقساط متأخرة — المتبقي {remaining:N0} ريال",
                        RelatedEntity = "contract",
                        RelatedId = contract.Id,
                        PatientId = contract.PatientId,
                    });
                }
            }
        }

        // 2. Ortho cases that are past expected duration
        var delayedCases = await db.OrthoCases
            .Include(c => c.Patient)
            .Where(c => c.Status == "active" && c.StartDate != null && c.ExpectedDurationMonths != null)
            .ToListAsync();

        foreach (var orthoCase in delayedCases)
        {
            var expectedEnd = orthoCase.StartDate!.Value.AddMonths(orthoCase.ExpectedDurationMonths!.Value);
            if (expectedEnd < DateOnly.FromDateTime(DateTime.Today))
            {
                var monthsOverdue = (DateTime.Today - expectedEnd.ToDateTime(TimeOnly.MinValue)).Days / 30;
                alerts.Add(new
                {
                    Type = "delayed_ortho_case",
                    Severity = "info",
                    Title = "حالة تقويمية متأخرة",
                    Message = $"الحالة {orthoCase.CaseNumber} للمريض {orthoCase.Patient?.FirstName} {orthoCase.Patient?.LastName} متأخرة {monthsOverdue} شهر عن الموعد المتوقع",
                    RelatedEntity = "ortho_case",
                    RelatedId = orthoCase.Id,
                    PatientId = orthoCase.PatientId,
                });
            }
        }

        // 3. Today's appointments
        var todayAppointments = await db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Where(a => a.AppointmentDate == DateOnly.FromDateTime(DateTime.Today) && a.Status == AppointmentStatus.Scheduled)
            .OrderBy(a => a.StartTime)
            .Take(5)
            .ToListAsync();

        foreach (var appt in todayAppointments)
        {
            alerts.Add(new
            {
                Type = "today_appointment",
                Severity = "info",
                Title = "موعد اليوم",
                Message = $"{appt.Patient?.FirstName} {appt.Patient?.LastName} — {appt.Doctor?.Name} — {appt.StartTime:hh\\:mm}",
                RelatedEntity = "appointment",
                RelatedId = appt.Id,
                PatientId = appt.PatientId,
            });
        }

        // 4. Upcoming appointments (next 24 hours) without confirmation
        var tomorrow = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var unconfirmedAppointments = await db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Where(a => a.AppointmentDate == tomorrow && a.Status == AppointmentStatus.Scheduled && !a.ConfirmationSent)
            .Take(5)
            .ToListAsync();

        foreach (var appt in unconfirmedAppointments)
        {
            alerts.Add(new
            {
                Type = "unconfirmed_appointment",
                Severity = "warning",
                Title = "موعد غير مؤكد غداً",
                Message = $"{appt.Patient?.FirstName} {appt.Patient?.LastName} — لم يتم تأكيد الموعد بعد",
                RelatedEntity = "appointment",
                RelatedId = appt.Id,
                PatientId = appt.PatientId,
            });
        }

        return Ok(alerts);
    }
}

public sealed class SaveAiRecommendationRequest
{
    public Guid PatientId { get; init; }
    public string? Context { get; init; }
    public object? InputData { get; init; }
    public string? Recommendation { get; init; }
    public decimal? Confidence { get; init; }
}

public sealed class UpdateAiRecommendationActionRequest
{
    public string Action { get; init; } = string.Empty; // approved, rejected, modified
    public Guid? DoctorId { get; init; }
}
