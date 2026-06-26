using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

public class Appointment : BaseEntity
{
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public Guid? BranchId { get; set; }
    public DateOnly AppointmentDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int DurationMinutes { get; set; } = 30;
    public string AppointmentType { get; set; } = string.Empty;
    public Specialty? Specialty { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;
    public bool ConfirmationSent { get; set; } = false; // Legacy — kept for backward compat

    /// <summary>When the last email reminder was sent for this appointment.</summary>
    public DateTime? EmailReminderSentAt { get; set; }

    /// <summary>When the last WhatsApp reminder was sent for this appointment.</summary>
    public DateTime? WhatsAppReminderSentAt { get; set; }

    /// <summary>JSON array of reminder window hours that have already been sent (e.g. [24,2] means both 24h and 2h email reminders sent).</summary>
    public string? EmailReminderWindowsSent { get; set; }

    /// <summary>JSON array of SMS reminder window hours that have already been sent (e.g. [24,2]).</summary>
    public string? SmsReminderWindowsSent { get; set; }

    public string? Notes { get; set; }
    public Guid? CreatedBy { get; set; }

    // ── Queue / clinic-flow fields (Sprint 4.5) ─────────────────────────────
    /// <summary>Room name assigned when calling the patient (e.g. "غرفة 1").</summary>
    public string? RoomName { get; set; }
    /// <summary>When the patient arrived at the clinic.</summary>
    public DateTime? ArrivedAt { get; set; }
    /// <summary>When the patient was called to a room.</summary>
    public DateTime? CalledAt { get; set; }
    /// <summary>When the patient entered the room.</summary>
    public DateTime? InRoomAt { get; set; }

    // ── Patient Journey fields (Sprint: Command Center) ────────────────────
    /// <summary>Selected service for this appointment (from services catalog).</summary>
    public Guid? ServiceId { get; set; }
    /// <summary>Selected room from ClinicRooms table.</summary>
    public Guid? ClinicRoomId { get; set; }
    /// <summary>Optional orthodontic case linked to this appointment.</summary>
    public Guid? OrthoCaseId { get; set; }

    // ── YOLO-S1: Companion/Guardian + Color + Treatment Package ────────────
    // Important for children / ortho patients who are accompanied by a parent
    // or guardian. WhatsApp reminders are sent to BOTH patient phone and the
    // companion phone when CompanionPhone is present (WhatsAppService).
    /// <summary>Name of the parent/guardian accompanying the patient (children/ortho common case).</summary>
    public string? CompanionName { get; set; }
    /// <summary>WhatsApp number of the companion. Reminders are sent here IN ADDITION to patient phone.</summary>
    public string? CompanionPhone { get; set; }
    /// <summary>Relationship to patient (e.g. "الأم", "الأب", "الجد"). Free-text Arabic.</summary>
    public string? CompanionRelationship { get; set; }

    /// <summary>Hex color (e.g. "#3b82f6") used as left-border / tint on calendar display. Optional — defaults to doctor color or status color when null.</summary>
    public string? AppointmentColor { get; set; }

    /// <summary>Optional treatment package linked to this appointment. Pre-fills type from package name when selected.</summary>
    public Guid? PackageId { get; set; }

    public Patient Patient { get; set; } = null!;
    public Doctor Doctor { get; set; } = null!;
    public Branch? Branch { get; set; }
    public Visit? Visit { get; set; }
    public ClinicService? Service { get; set; }
    public ClinicRoom? ClinicRoom { get; set; }
    public OrthoCase? OrthoCase { get; set; }
    public TreatmentPackage? Package { get; set; }
}
