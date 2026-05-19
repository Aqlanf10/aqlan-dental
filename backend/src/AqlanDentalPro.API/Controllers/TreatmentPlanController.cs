using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

/// <summary>
/// Unified Treatment Plan — manages planned clinical procedures inside the patient file.
/// Organizes steps across all departments with service catalog integration.
/// Estimated cost is reference only — does NOT affect finance or invoices.
/// </summary>
[ApiController]
[Route("api/patients/{patientId:guid}/treatment-plan")]
[Authorize(Policy = "StaffOnly")]
public class TreatmentPlanController(AppDbContext db) : ControllerBase
{
    // ─── 1. GET — List all active steps ────────────────────────────────────
    /// <summary>Returns all active treatment plan steps for a patient, ordered by SequenceNumber.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(Guid patientId)
    {
        var patientExists = await db.Patients.AnyAsync(p => p.Id == patientId && p.IsActive);
        if (!patientExists)
            return NotFound(new { message = "المريض غير موجود" });

        var steps = await db.PatientTreatmentPlanSteps
            .Include(s => s.Service)
            .Include(s => s.ResponsibleDoctor)
            .Where(s => s.PatientId == patientId)
            .OrderBy(s => s.SequenceNumber)
            .Select(s => new
            {
                s.Id,
                s.PatientId,
                s.SequenceNumber,
                s.ServiceId,
                ServiceName = s.Service != null ? s.Service.ArabicName : s.ServiceNameSnapshot,
                s.ServiceNameSnapshot,
                Department = s.Department != null ? s.Department.ToString() : null,
                DepartmentArabic = s.Department != null ? GetSpecialtyArabic(s.Department.Value) : null,
                s.ToothNumber,
                s.ToothArea,
                s.Title,
                s.Description,
                Priority = s.Priority.ToString(),
                PriorityArabic = GetPriorityArabic(s.Priority),
                Status = s.Status.ToString(),
                StatusArabic = GetStatusArabic(s.Status),
                s.ResponsibleDoctorId,
                DoctorName = s.ResponsibleDoctor != null ? s.ResponsibleDoctor.Name : null,
                s.PlannedDate,
                s.CompletedDate,
                s.EstimatedCost,
                s.RelatedAppointmentId,
                s.RelatedVisitId,
                s.Notes,
                s.CreatedAt,
                s.UpdatedAt
            })
            .ToListAsync();

        return Ok(steps);
    }

    // ─── 2. POST — Create new step ─────────────────────────────────────────
    /// <summary>Creates a new treatment plan step for the patient.</summary>
    [HttpPost]
    [Authorize(Policy = "DoctorAccess")]
    public async Task<IActionResult> Create(Guid patientId, [FromBody] CreateTreatmentStepRequest req)
    {
        var patientExists = await db.Patients.AnyAsync(p => p.Id == patientId && p.IsActive);
        if (!patientExists)
            return NotFound(new { message = "المريض غير موجود" });

        // Determine next sequence number
        var maxSeq = await db.PatientTreatmentPlanSteps
            .Where(s => s.PatientId == patientId)
            .MaxAsync(s => (int?)s.SequenceNumber) ?? 0;

        var userId = GetCurrentUserId();

        // Snapshot service name if service is provided
        string? serviceNameSnapshot = req.ServiceNameSnapshot;
        if (req.ServiceId.HasValue && string.IsNullOrWhiteSpace(serviceNameSnapshot))
        {
            var service = await db.ClinicServices.FindAsync(req.ServiceId.Value);
            if (service != null)
            {
                serviceNameSnapshot = service.ArabicName;
                // Use service DefaultPrice as EstimatedCost suggestion if not provided
                if (!req.EstimatedCost.HasValue && service.DefaultPrice > 0)
                {
                    // Just a suggestion — set only if caller didn't specify
                }
            }
        }

        var step = new PatientTreatmentPlanStep
        {
            PatientId = patientId,
            SequenceNumber = maxSeq + 1,
            ServiceId = req.ServiceId,
            ServiceNameSnapshot = serviceNameSnapshot,
            Department = req.Department,
            ToothNumber = req.ToothNumber,
            ToothArea = req.ToothArea,
            Title = req.Title,
            Description = req.Description,
            Priority = req.Priority ?? TreatmentStepPriority.Normal,
            Status = req.Status ?? TreatmentStepStatus.Planned,
            ResponsibleDoctorId = req.ResponsibleDoctorId,
            PlannedDate = req.PlannedDate,
            CompletedDate = req.CompletedDate,
            EstimatedCost = req.EstimatedCost,
            RelatedAppointmentId = req.RelatedAppointmentId,
            RelatedVisitId = req.RelatedVisitId,
            Notes = req.Notes,
            CreatedBy = userId,
            UpdatedBy = userId
        };

        db.PatientTreatmentPlanSteps.Add(step);
        await db.SaveChangesAsync();

        return Ok(new
        {
            step.Id,
            step.SequenceNumber,
            step.Title,
            step.Status,
            message = "تمت إضافة خطوة العلاج بنجاح"
        });
    }

    // ─── 3. PUT — Update step ──────────────────────────────────────────────
    /// <summary>Updates a treatment plan step.</summary>
    [HttpPut("{stepId:guid}")]
    [Authorize(Policy = "DoctorAccess")]
    public async Task<IActionResult> Update(Guid patientId, Guid stepId, [FromBody] UpdateTreatmentStepRequest req)
    {
        var step = await db.PatientTreatmentPlanSteps
            .FirstOrDefaultAsync(s => s.Id == stepId && s.PatientId == patientId);

        if (step == null)
            return NotFound(new { message = "خطوة العلاج غير موجودة" });
        if (!step.IsActive)
            return BadRequest(new { message = "خطوة العلاج محذوفة" });

        var userId = GetCurrentUserId();

        // Update fields if provided
        if (req.ServiceId.HasValue) step.ServiceId = req.ServiceId;
        if (req.ServiceNameSnapshot != null) step.ServiceNameSnapshot = req.ServiceNameSnapshot;
        if (req.Department.HasValue) step.Department = req.Department;
        if (req.ToothNumber != null) step.ToothNumber = req.ToothNumber;
        if (req.ToothArea != null) step.ToothArea = req.ToothArea;
        if (req.Title != null) step.Title = req.Title;
        if (req.Description != null) step.Description = req.Description;
        if (req.Priority.HasValue) step.Priority = req.Priority.Value;
        if (req.ResponsibleDoctorId.HasValue) step.ResponsibleDoctorId = req.ResponsibleDoctorId;
        if (req.PlannedDate.HasValue) step.PlannedDate = req.PlannedDate;
        if (req.EstimatedCost.HasValue) step.EstimatedCost = req.EstimatedCost;
        if (req.RelatedAppointmentId.HasValue) step.RelatedAppointmentId = req.RelatedAppointmentId;
        if (req.RelatedVisitId.HasValue) step.RelatedVisitId = req.RelatedVisitId;
        if (req.Notes != null) step.Notes = req.Notes;

        step.UpdatedBy = userId;

        // Snapshot service name if service changed
        if (req.ServiceId.HasValue)
        {
            var service = await db.ClinicServices.FindAsync(req.ServiceId.Value);
            if (service != null && string.IsNullOrWhiteSpace(req.ServiceNameSnapshot))
                step.ServiceNameSnapshot = service.ArabicName;
        }

        await db.SaveChangesAsync();

        return Ok(new
        {
            step.Id,
            step.Title,
            step.UpdatedAt,
            message = "تم تحديث خطوة العلاج بنجاح"
        });
    }

    // ─── 4. PATCH — Change status ──────────────────────────────────────────
    /// <summary>Changes the status of a treatment plan step.</summary>
    [HttpPatch("{stepId:guid}/status")]
    [Authorize(Policy = "DoctorAccess")]
    public async Task<IActionResult> ChangeStatus(Guid patientId, Guid stepId, [FromBody] ChangeStepStatusRequest req)
    {
        var step = await db.PatientTreatmentPlanSteps
            .FirstOrDefaultAsync(s => s.Id == stepId && s.PatientId == patientId);

        if (step == null)
            return NotFound(new { message = "خطوة العلاج غير موجودة" });
        if (!step.IsActive)
            return BadRequest(new { message = "خطوة العلاج محذوفة" });

        // Validate status transition
        if (!IsValidStatusTransition(step.Status, req.Status))
            return BadRequest(new { message = $"لا يمكن تغيير الحالة من {GetStatusArabic(step.Status)} إلى {GetStatusArabic(req.Status)}" });

        var userId = GetCurrentUserId();
        step.Status = req.Status;
        step.UpdatedBy = userId;

        // Auto-set completed date
        if (req.Status == TreatmentStepStatus.Completed && !step.CompletedDate.HasValue)
            step.CompletedDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // Clear completed date if reverting from completed
        if (req.Status != TreatmentStepStatus.Completed && step.CompletedDate.HasValue)
            step.CompletedDate = null;

        await db.SaveChangesAsync();

        return Ok(new
        {
            step.Id,
            Status = step.Status.ToString(),
            StatusArabic = GetStatusArabic(step.Status),
            step.CompletedDate,
            message = "تم تغيير حالة خطوة العلاج بنجاح"
        });
    }

    // ─── 5. PATCH — Reorder steps ──────────────────────────────────────────
    /// <summary>Updates the sequence numbers of treatment plan steps.</summary>
    [HttpPatch("reorder")]
    [Authorize(Policy = "DoctorAccess")]
    public async Task<IActionResult> Reorder(Guid patientId, [FromBody] ReorderStepsRequest req)
    {
        var patientExists = await db.Patients.AnyAsync(p => p.Id == patientId && p.IsActive);
        if (!patientExists)
            return NotFound(new { message = "المريض غير موجود" });

        var userId = GetCurrentUserId();

        foreach (var item in req.Items)
        {
            var step = await db.PatientTreatmentPlanSteps
                .FirstOrDefaultAsync(s => s.Id == item.StepId && s.PatientId == patientId);

            if (step != null)
            {
                step.SequenceNumber = item.SequenceNumber;
                step.UpdatedBy = userId;
            }
        }

        await db.SaveChangesAsync();

        return Ok(new { message = "تم إعادة ترتيب خطوات العلاج بنجاح" });
    }

    // ─── 6. DELETE — Soft delete ───────────────────────────────────────────
    /// <summary>Soft-deletes a treatment plan step.</summary>
    [HttpDelete("{stepId:guid}")]
    [Authorize(Policy = "DoctorAccess")]
    public async Task<IActionResult> Delete(Guid patientId, Guid stepId)
    {
        var step = await db.PatientTreatmentPlanSteps
            .FirstOrDefaultAsync(s => s.Id == stepId && s.PatientId == patientId);

        if (step == null)
            return NotFound(new { message = "خطوة العلاج غير موجودة" });

        var userId = GetCurrentUserId();
        step.IsActive = false;
        step.DeletedAt = DateTime.UtcNow;
        step.DeletedBy = userId;

        await db.SaveChangesAsync();

        return Ok(new { message = "تم حذف خطوة العلاج بنجاح" });
    }

    // ─── Private helpers ───────────────────────────────────────────────────

    private static bool IsValidStatusTransition(TreatmentStepStatus current, TreatmentStepStatus target)
    {
        // Planned → any; InProgress → any; Completed → Deferred only; Cancelled → Planned only; Deferred → any
        return current switch
        {
            TreatmentStepStatus.Planned => true,
            TreatmentStepStatus.InProgress => true,
            TreatmentStepStatus.Completed => target == TreatmentStepStatus.Deferred,
            TreatmentStepStatus.Cancelled => target == TreatmentStepStatus.Planned,
            TreatmentStepStatus.Deferred => true,
            _ => false
        };
    }

    private static string GetStatusArabic(TreatmentStepStatus status) => status switch
    {
        TreatmentStepStatus.Planned => "مخطط",
        TreatmentStepStatus.InProgress => "قيد التنفيذ",
        TreatmentStepStatus.Completed => "مكتمل",
        TreatmentStepStatus.Cancelled => "ملغي",
        TreatmentStepStatus.Deferred => "مؤجل",
        _ => status.ToString()
    };

    private static string GetPriorityArabic(TreatmentStepPriority priority) => priority switch
    {
        TreatmentStepPriority.Low => "منخفض",
        TreatmentStepPriority.Normal => "عادي",
        TreatmentStepPriority.High => "مرتفع",
        TreatmentStepPriority.Urgent => "عاجل",
        _ => priority.ToString()
    };

    private static string GetSpecialtyArabic(Specialty specialty) => specialty switch
    {
        Specialty.General => "طب الأسنان العام",
        Specialty.Orthodontics => "التقويم",
        Specialty.OralSurgery => "جراحة الفم والوجه والفكين",
        Specialty.Periodontics => "أمراض اللثة",
        Specialty.Endodontics => "علاج العصب",
        Specialty.Prosthodontics => "التركيبات",
        _ => specialty.ToString()
    };

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

// ─── Request DTOs ────────────────────────────────────────────────────────────

public class CreateTreatmentStepRequest
{
    public Guid? ServiceId { get; set; }
    public string? ServiceNameSnapshot { get; set; }
    public Specialty? Department { get; set; }
    public string? ToothNumber { get; set; }
    public string? ToothArea { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TreatmentStepPriority? Priority { get; set; }
    public TreatmentStepStatus? Status { get; set; }
    public Guid? ResponsibleDoctorId { get; set; }
    public DateOnly? PlannedDate { get; set; }
    public DateOnly? CompletedDate { get; set; }
    public decimal? EstimatedCost { get; set; }
    public Guid? RelatedAppointmentId { get; set; }
    public Guid? RelatedVisitId { get; set; }
    public string? Notes { get; set; }
}

public class UpdateTreatmentStepRequest
{
    public Guid? ServiceId { get; set; }
    public string? ServiceNameSnapshot { get; set; }
    public Specialty? Department { get; set; }
    public string? ToothNumber { get; set; }
    public string? ToothArea { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public TreatmentStepPriority? Priority { get; set; }
    public Guid? ResponsibleDoctorId { get; set; }
    public DateOnly? PlannedDate { get; set; }
    public decimal? EstimatedCost { get; set; }
    public Guid? RelatedAppointmentId { get; set; }
    public Guid? RelatedVisitId { get; set; }
    public string? Notes { get; set; }
}

public class ChangeStepStatusRequest
{
    public TreatmentStepStatus Status { get; set; }
}

public class ReorderStepsRequest
{
    public List<ReorderItem> Items { get; set; } = [];
}

public class ReorderItem
{
    public Guid StepId { get; set; }
    public int SequenceNumber { get; set; }
}
