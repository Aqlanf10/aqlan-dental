namespace AqlanDentalPro.Domain.Entities;

public class Branch : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public bool IsMain { get; set; } = false;

    public ICollection<User> Users { get; set; } = [];
    public ICollection<Doctor> Doctors { get; set; } = [];
    public ICollection<Patient> Patients { get; set; } = [];
    public ICollection<Appointment> Appointments { get; set; } = [];
}
