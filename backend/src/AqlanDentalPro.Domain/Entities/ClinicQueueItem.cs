using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Represents a patient in the clinic queue for a given day.
/// Sprint 7: Clinic Queue, Rooms, and Patient Calling Integration.
/// </summary>
public class ClinicQueueItem : BaseEntity
{
    public Guid PatientId { get; set; }
    public Guid? AppointmentId { get; set; }
    public Guid? VisitId { get; set; }
    public Guid? DoctorId { get; set; }
    public Guid? ServiceId { get; set; }
    public Guid? ClinicRoomId { get; set; }
    public string? RoomName { get; set; }
    public ClinicQueueStatus Status { get; set; } = ClinicQueueStatus.Waiting;

    // Timestamps for queue flow
    public DateTime? CalledAt { get; set; }
    public Guid? CalledByUserId { get; set; }
    public DateTime? InRoomAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    // Who added the patient to the queue
    public Guid? AddedByUserId { get; set; }

    // Optional notes
    public string? Notes { get; set; }

    // The date this queue item belongs to (for querying today's queue)
    public DateOnly QueueDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    // Navigation properties
    public Patient Patient { get; set; } = null!;
    public Appointment? Appointment { get; set; }
    public Visit? Visit { get; set; }
    public Doctor? Doctor { get; set; }
    public User? AddedByUser { get; set; }
    public User? CalledByUser { get; set; }
}
