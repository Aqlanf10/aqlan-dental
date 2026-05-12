namespace AqlanDentalPro.Application.DTOs.Appointments;

public class DailyAppointmentStatsDto
{
    /// <summary>Total appointments for the day</summary>
    public int Total { get; set; }
    /// <summary>Completed appointments</summary>
    public int Completed { get; set; }
    /// <summary>Cancelled appointments</summary>
    public int Cancelled { get; set; }
    /// <summary>No-show appointments</summary>
    public int NoShow { get; set; }
    /// <summary>Currently in progress</summary>
    public int InProgress { get; set; }
    /// <summary>Waiting in queue</summary>
    public int Waiting { get; set; }
    /// <summary>Completion rate percentage</summary>
    public double CompletionRate { get; set; }
    /// <summary>No-show rate percentage</summary>
    public double NoShowRate { get; set; }
}
