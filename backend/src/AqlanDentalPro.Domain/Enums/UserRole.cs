namespace AqlanDentalPro.Domain.Enums;

public enum UserRole
{
    /// <summary>
    /// مالك النظام الوحيد. يملك التحكم بالمستخدمين والصلاحيات والأمان العام.
    /// </summary>
    SuperAdmin,
    Admin,
    Orthodontist,
    GeneralDentist,
    OralSurgeon,
    Reception,
    Accountant,
    Assistant,
    BranchManager,
    Patient
}
