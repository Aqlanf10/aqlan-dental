using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

public class Visit : BaseEntity
{
    public Guid PatientId { get; set; }
    public Guid? AppointmentId { get; set; }
    public DateOnly VisitDate { get; set; }
    public string? VisitType { get; set; }
    public Specialty? Specialty { get; set; }
    public Guid? DoctorId { get; set; }
    public string? ChiefComplaint { get; set; }
    public string? ClinicalNotes { get; set; }
    public string? TreatmentDone { get; set; }
    public string? Diagnosis { get; set; }
    public string? Instructions { get; set; }
    public string? NextVisitPlan { get; set; }
    public decimal? Cost { get; set; }
    public DateOnly? NextVisitDate { get; set; }

    // ── Patient Journey fields (Sprint: Command Center) ────────────────────
    /// <summary>Service associated with this visit.</summary>
    public Guid? ServiceId { get; set; }
    /// <summary>Checkout status: null=pending, ReadyForCheckout, CheckedOut.</summary>
    public string? CheckoutStatus { get; set; }
    /// <summary>When the doctor marked the visit as ready for checkout.</summary>
    public DateTime? ReadyForCheckoutAt { get; set; }
    /// <summary>Reference amount due at checkout (not a finance calculation).</summary>
    public decimal? AmountDueReference { get; set; }
    /// <summary>Suggested billing procedure from clinical diagnosis (without price).</summary>
    public string? ProposedProcedure { get; set; }

    public Patient Patient { get; set; } = null!;
    public Appointment? Appointment { get; set; }
    public Doctor? Doctor { get; set; }
    public ICollection<GeneralTreatment> GeneralTreatments { get; set; } = [];
    public ICollection<Prescription> Prescriptions { get; set; } = [];
}
