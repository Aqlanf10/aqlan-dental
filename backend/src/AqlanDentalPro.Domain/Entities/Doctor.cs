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

    public User User { get; set; } = null!;
    public Branch? Branch { get; set; }
    public ICollection<Patient> PrimaryPatients { get; set; } = [];
    public ICollection<Appointment> Appointments { get; set; } = [];
    public ICollection<DoctorSchedule> Schedules { get; set; } = [];
}
