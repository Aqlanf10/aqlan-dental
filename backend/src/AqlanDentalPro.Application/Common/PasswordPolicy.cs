namespace AqlanDentalPro.Application.Common;

/// <summary>
/// Centralized password complexity policy for Aqlan Dental Pro.
/// Applied uniformly to every endpoint that accepts a NEW password:
/// portal register / reset / change, staff register / create-user /
/// create-employee / reset / change. Login does NOT validate (it
/// verifies the hash) so existing users are never locked out by a
/// policy tightening.
///
/// SEC-11: previously the portal accepted any string as a password
/// and the staff side enforced the rules only sporadically via
/// FluentValidation rules and ad-hoc regex checks. This validator is
/// the single source of truth.
///
/// NOTE: thresholds are constants for now. A follow-up will make them
/// configurable via the Settings table (keys like
/// "security.password.minLength" etc.). Keep the surface area minimal
/// until then — every consumer just calls <see cref="Validate"/>.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 128;
    public const bool RequireDigit = true;
    public const bool RequireUppercase = true;
    public const bool RequireLowercase = true;
    // Keep user-friendly for Arabic patients — do NOT require symbols.
    public const bool RequireNonAlphanumeric = false;

    /// <summary>
    /// Validates the given password against the policy.
    /// Returns (valid=true, arabicError="") when it passes, otherwise
    /// (valid=false, arabicError="...") with the first failing rule's
    /// Arabic message. Arabic messages are returned to the user
    /// verbatim by the controllers.
    /// </summary>
    public static (bool valid, string arabicError) Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return (false, "كلمة المرور مطلوبة");
        if (password.Length < MinLength)
            return (false, $"كلمة المرور يجب ألا تقل عن {MinLength} أحرف");
        if (password.Length > MaxLength)
            return (false, $"كلمة المرور يجب ألا تتجاوز {MaxLength} حرف");
        if (RequireDigit && !password.Any(char.IsDigit))
            return (false, "كلمة المرور يجب أن تحتوي على رقم واحد على الأقل");
        if (RequireUppercase && !password.Any(char.IsUpper))
            return (false, "كلمة المرور يجب أن تحتوي على حرف كبير واحد على الأقل");
        if (RequireLowercase && !password.Any(char.IsLower))
            return (false, "كلمة المرور يجب أن تحتوي على حرف صغير واحد على الأقل");
        if (RequireNonAlphanumeric && password.All(char.IsLetterOrDigit))
            return (false, "كلمة المرور يجب أن تحتوي على رمز خاص واحد على الأقل");
        return (true, "");
    }
}
