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

    public Patient Patient { get; set; } = null!;
    public Doctor Doctor { get; set; } = null!;
    public Branch? Branch { get; set; }
    public Visit? Visit { get; set; }
    public ClinicService? Service { get; set; }
    public ClinicRoom? ClinicRoom { get; set; }
}
