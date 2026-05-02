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

    public User User { get; set; } = null!;
    public Branch? Branch { get; set; }
    public ICollection<Patient> PrimaryPatients { get; set; } = [];
    public ICollection<Appointment> Appointments { get; set; } = [];
    public ICollection<DoctorSchedule> Schedules { get; set; } = [];
}
