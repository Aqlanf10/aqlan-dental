namespace AqlanDentalPro.Domain.Common;

/// <summary>
/// CORE-PAT-003 — the single, correct patient-age calculation.
///
/// Three different implementations existed and disagreed with each other:
///   * PatientService.ToListDto / ToProfileDto: <c>today.Year - dob.Year</c>,
///     which is one year TOO HIGH for every patient whose birthday has not
///     happened yet this year (roughly half of all patients on any given day).
///   * PatientPortalService.CalculateAge: correct, so the patient saw a
///     different age in their own portal than the clinician saw in the file.
///   * RadiologyReferralPrint (frontend): a third copy for printed referrals.
///
/// Age drives clinical judgement (paediatric dosing, growth assessment for
/// orthodontic timing), so it must be computed one way everywhere.
/// </summary>
public static class PatientAge
{
    /// <summary>
    /// Completed years between <paramref name="dateOfBirth"/> and
    /// <paramref name="today"/>. Returns null for an unknown date of birth and
    /// for a date of birth in the future (bad data must not print as a
    /// negative age).
    /// </summary>
    public static int? Calculate(DateOnly? dateOfBirth, DateOnly today)
    {
        if (!dateOfBirth.HasValue) return null;

        var dob = dateOfBirth.Value;
        if (dob > today) return null;

        var age = today.Year - dob.Year;
        // The birthday has not occurred yet this year.
        if (dob > today.AddYears(-age)) age--;
        return age < 0 ? null : age;
    }
}
