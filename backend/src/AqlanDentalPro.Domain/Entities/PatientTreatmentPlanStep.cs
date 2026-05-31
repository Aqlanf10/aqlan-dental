using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// A single step in a patient's unified treatment plan.
/// Organizes planned clinical procedures across all departments.
/// </summary>
public class PatientTreatmentPlanStep : BaseEntity
{
    public Guid PatientId { get; set; }
    public int SequenceNumber { get; set; }

    /// <summary>Linked service from the catalog (optional).</summary>
    public Guid? ServiceId { get; set; }

    /// <summary>Snapshot of the service Arabic name at time of creation for history safety.</summary>
    public string? ServiceNameSnapshot { get; set; }

    /// <summary>Department / specialty responsible for this step.</summary>
    public Specialty? Department { get; set; }

    /// <summary>Universal tooth number (FDI notation, e.g. 11, 36, 48).</summary>
    public string? ToothNumber { get; set; }

    /// <summary>Free-text area description (e.g. "upper right quadrant", "anterior segment").</summary>
    public string? ToothArea { get; set; }

    /// <summary>Short title of the planned procedure.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Detailed description of the planned procedure.</summary>
    public string? Description { get; set; }

    /// <summary>Priority level.</summary>
    public TreatmentStepPriority Priority { get; set; } = TreatmentStepPriority.Normal;

    /// <summary>Current status of this step.</summary>
    public TreatmentStepStatus Status { get; set; } = TreatmentStepStatus.Planned;

    /// <summary>Doctor responsible for performing this step.</summary>
    public Guid? ResponsibleDoctorId { get; set; }

    /// <summary>Planned date for performing this step.</summary>
    public DateOnly? PlannedDate { get; set; }

    /// <summary>Date the step was actually completed.</summary>
    public DateOnly? CompletedDate { get; set; }

    /// <summary>Estimated cost reference — does NOT affect finance or invoices.</summary>
    public decimal? EstimatedCost { get; set; }

    /// <summary>Related appointment reference (optional).</summary>
    public Guid? RelatedAppointmentId { get; set; }

    /// <summary>Related visit reference (optional).</summary>
    public Guid? RelatedVisitId { get; set; }

    /// <summary>Clinical notes.</summary>
    public string? Notes { get; set; }

    /// <summary>User who created this step.</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>User who last updated this step.</summary>
    public Guid? UpdatedBy { get; set; }

    // Navigation properties
    public Patient Patient { get; set; } = null!;
    public ClinicService? Service { get; set; }
    public Doctor? ResponsibleDoctor { get; set; }
    public Appointment? RelatedAppointment { get; set; }
    public Visit? RelatedVisit { get; set; }
}
