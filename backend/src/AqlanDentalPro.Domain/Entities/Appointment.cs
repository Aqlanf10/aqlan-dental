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
    public bool ConfirmationSent { get; set; } = false;
    public string? Notes { get; set; }
    public Guid? CreatedBy { get; set; }

    public Patient Patient { get; set; } = null!;
    public Doctor Doctor { get; set; } = null!;
    public Branch? Branch { get; set; }
    public Visit? Visit { get; set; }
}
