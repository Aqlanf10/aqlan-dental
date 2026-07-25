namespace AqlanDentalPro.Application.Interfaces.Services;

/// <summary>
/// CORE-PAT-048: the Application project only references Domain (see the
/// project's ProjectReference list), so it cannot call
/// Infrastructure.Services.ClinicTimeProvider directly — the same layering
/// constraint IPatientSettingsReader exists to cross. PatientService's age
/// calculation used DateOnly.FromDateTime(DateTime.UtcNow) as a stand-in,
/// which is wrong for ~3 hours a year (the window between the UTC day
/// rolling over and the clinic's Asia/Aden day rolling over): a patient
/// whose birthday is "today" in clinic time but not yet in UTC time shows
/// one year younger than they are.
/// </summary>
public interface IClinicClock
{
    DateOnly Today();
}
