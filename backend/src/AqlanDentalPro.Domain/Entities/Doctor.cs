using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

public class Doctor : BaseEntity
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Specialty { get; set; }
    public string? LicenseNumber { get; set; }
    public Guid? BranchId { get; set; }
    public string? Color { get; set; }
    public string? AvatarInitials { get; set; }

    // Future compensation compatibility (Sprint 6)
    public CompensationType CompensationType { get; set; } = CompensationType.None;
    public decimal? DefaultCommissionPercentage { get; set; }
    public string? CompensationNotes { get; set; }

    /// <summary>
    /// The doctor's standing room assignment ("تعيينات غرف الأطباء" — CLAUDE.md priority).
    /// Used to pre-fill the room when calling this doctor's patient from the clinic queue,
    /// so reception doesn't re-pick the same room on every call. Nullable — a doctor with
    /// no assignment keeps the old manual-selection behavior.
    /// </summary>
    public Guid? DefaultClinicRoomId { get; set; }

    public User User { get; set; } = null!;
    public Branch? Branch { get; set; }
    public ClinicRoom? DefaultClinicRoom { get; set; }
    public ICollection<Patient> PrimaryPatients { get; set; } = [];
    public ICollection<Appointment> Appointments { get; set; } = [];
    public ICollection<DoctorSchedule> Schedules { get; set; } = [];
}
