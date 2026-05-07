namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Working hours for a doctor on a specific day of the week.
/// Sprint 5: Doctor schedule management.
/// </summary>
public class DoctorSchedule : BaseEntity
{
    public Guid DoctorId { get; set; }
    /// <summary>Day of week: 0=Sunday, 1=Monday, ..., 6=Saturday</summary>
    public int DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    /// <summary>Is this a working day? If false, doctor is off this day.</summary>
    public bool IsWorking { get; set; } = true;
    /// <summary>Break start time (optional)</summary>
    public TimeOnly? BreakStart { get; set; }
    /// <summary>Break end time (optional)</summary>
    public TimeOnly? BreakEnd { get; set; }
    /// <summary>Slot duration in minutes for this schedule</summary>
    public int SlotDurationMinutes { get; set; } = 30;

    // Navigation
    public Doctor Doctor { get; set; } = null!;
}
