namespace AqlanDentalPro.Domain.Enums;

public enum AuditAction
{
    Create,
    Update,
    Delete,
    View,
    Export,
    Login,
    Logout,
    Approve,
    PasswordChange,
    PasswordResetByAdmin,
    ForgotPasswordRequested,
    ResetTokenGenerated,
    ResetTokenUsed,
    ImpersonationStarted,
    ImpersonationStopped,
    ImpersonationDenied,
    UserActivated,
    UserDeactivated,
    UserRestored,
    PermissionsChanged,
    UnauthorizedAttempt,
    Refund               // استرداد دفعة
}
