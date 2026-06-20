namespace AqlanDentalPro.Application.DTOs.Journey;

/// <summary>Reception-side intake request — marks the patient as arrived and
/// optionally attaches a service / room to the appointment.</summary>
public class IntakeRequest
{
    public Guid? ServiceId { get; set; }
    public string? ChiefComplaint { get; set; }
    public string? Notes { get; set; }
    public Guid? RoomId { get; set; }
    public bool RequiresConsultationFee { get; set; }
    public decimal? ConsultationFeeAmount { get; set; }
}

/// <summary>Optional room assignment + notes when adding an appointment to the
/// clinic queue.</summary>
public class SendToQueueRequest
{
    public Guid? RoomId { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Doctor handoff payload — clinical findings, proposed procedure, and
/// amount-due reference handed to reception for checkout.</summary>
public class HandoffRequest
{
    public string? ChiefComplaint { get; set; }
    public string? TreatmentDone { get; set; }
    public string? Diagnosis { get; set; }
    public string? NextVisitPlan { get; set; }
    public string? Instructions { get; set; }
    /// <summary>Extraoral examination findings — appended to ClinicalNotes with Arabic label.</summary>
    public string? ExtraoralExamination { get; set; }
    /// <summary>Intraoral examination findings — appended to ClinicalNotes with Arabic label.</summary>
    public string? IntraoralExamination { get; set; }
    public Guid? SuggestedServiceId { get; set; }
    /// <summary>Additional services beyond the first — appended as text to ClinicalNotes (temporary compatibility, F6).</summary>
    public string? AdditionalServicesText { get; set; }
    public DateOnly? FollowUpDate { get; set; }
    public decimal? AmountDue { get; set; }
    public string? Notes { get; set; }
    public string? ProposedProcedure { get; set; }
}

public class CheckoutRequest
{
    /// <summary>
    /// PaymentAmount is for reference/guidance ONLY.
    /// Checkout is workflow-status only — it does NOT create a Payment.
    /// To record actual payment, use the Finance module (POST /api/payments)
    /// via FinanceService.CreatePaymentAsync().
    /// </summary>
    public decimal? PaymentAmount { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Notes { get; set; }
    public DateOnly? NextAppointmentDate { get; set; }
    public Guid? NextServiceId { get; set; }
}

public class LeftWithoutCompletionRequest
{
    public string? Reason { get; set; }
    public string? Status { get; set; }
}

/// <summary>
/// Request DTO for financial closure validation.
/// </summary>
public sealed class ValidateFinancialClosureRequest
{
    /// <summary>If true, a manager is overriding the outstanding balance check.</summary>
    public bool ManagerOverride { get; init; }

    /// <summary>Reason for closure override (e.g. manager approval reason).</summary>
    public string? ClosureReason { get; init; }

    /// <summary>Specific visit to validate (optional — defaults to latest active visit).</summary>
    public Guid? VisitId { get; init; }
}
