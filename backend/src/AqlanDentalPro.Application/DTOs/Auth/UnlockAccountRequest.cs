namespace AqlanDentalPro.Application.DTOs.Auth;

/// <summary>
/// Request DTO for unlocking a locked-out account.
/// Requires the server-side ADMIN_UNLOCK_SECRET environment variable.
/// </summary>
public class UnlockAccountRequest
{
    public string Username { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
}
