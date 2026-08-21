using AqlanDentalPro.Application.Common;
using AqlanDentalPro.Application.DTOs.Auth;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace AqlanDentalPro.API.Controllers;

public sealed class ForgotPasswordRequest
{
    public string UsernameOrEmail { get; init; } = string.Empty;
}

public sealed class ResetPasswordRequest
{
    public string Token { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
    public string ConfirmPassword { get; init; } = string.Empty;
}

public sealed class ImpersonateRequest
{
    public string Reason { get; init; } = string.Empty;
}

[ApiController]
[Route("api/auth")]
// CORE-P1-S4 — deny by default.
//
// This controller used to carry no class-level policy: every action opted in, either with
// [AllowAnonymous] or with its own [Authorize]. Every action did so correctly, but the
// structure meant a future action that simply forgot both attributes would be publicly
// reachable — on the controller that issues tokens, unlocks accounts and impersonates users.
//
// The five genuinely public actions (Login, RefreshToken, UnlockAccount, ForgotPassword,
// ResetPassword) keep their [AllowAnonymous], which overrides this. Impersonate keeps its
// stricter [Authorize(Policy = "AdminOnly")]; the two policies combine, so it still requires
// Admin. What changes is only the default for anything added later.
[Authorize(Policy = "StaffOnly")]
public class AuthController(
    IAuthService authService,
    ICurrentUserService currentUser,
    ITokenService tokenService,
    ILoginAttemptService loginAttempts,
    IAuditService auditService,
    IEmailService emailService,
    AppDbContext db,
    ILogger<AuthController> logger) : ControllerBase
{
    private const string RefreshTokenCookie = "refresh_token";
    private const string AccessTokenCookie = "aqlan_access_token";

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        // Check if account is locked out
        var (isLocked, remainingMinutes) = await loginAttempts.IsLockedOutAsync(request.Username);
        if (isLocked)
        {
            return StatusCode(429, new { 
                message = $"تم قفل الحساب بسبب {5} محاولات فاشلة. حاول مرة أخرى بعد {remainingMinutes} دقيقة.",
                lockedUntil = remainingMinutes
            });
        }

        var result = await authService.LoginAsync(request);
        if (result == null)
        {
            var failCount = await loginAttempts.RecordFailedAttemptAsync(request.Username);
            
            if (failCount >= 5)
            {
                return StatusCode(429, new { 
                    message = "تم قفل الحساب بسبب 5 محاولات فاشلة متتالية. حاول مرة أخرى بعد 15 دقيقة.",
                    lockedUntil = 15
                });
            }
            
            return Unauthorized(new { message = "اسم المستخدم أو كلمة المرور غير صحيحة" });
        }

        // Reset failed attempts on successful login
        await loginAttempts.ResetFailedAttemptsAsync(request.Username);

        SetRefreshTokenCookie(result.RefreshToken);
        SetAccessTokenCookie(result.AccessToken);
        return Ok(new { result.AccessToken, result.User });
    }

    [HttpPost("logout")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> Logout()
    {
        var refreshToken = Request.Cookies[RefreshTokenCookie];
        if (refreshToken != null && currentUser.UserId.HasValue)
            await authService.LogoutAsync(currentUser.UserId.Value, refreshToken);

        Response.Cookies.Delete(RefreshTokenCookie);
        DeleteAccessTokenCookie();
        return NoContent();
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<ActionResult<object>> RefreshToken()
    {
        var refreshToken = Request.Cookies[RefreshTokenCookie];
        if (string.IsNullOrEmpty(refreshToken))
            return Unauthorized(new { message = "لا يوجد refresh token" });

        try
        {
            // The access token may be expired here, so we cannot rely on JWT claims.
            // Look up the owner directly from the refresh token stored in Redis.
            var userId = currentUser.UserId
                ?? await tokenService.GetOwnerOfRefreshTokenAsync(refreshToken);

            if (userId is null)
                return Unauthorized(new { message = "انتهت صلاحية الجلسة، يرجى تسجيل الدخول مجدداً" });

            var result = await authService.RefreshAsync(userId.Value, refreshToken);
            if (result == null)
            {
                Response.Cookies.Delete(RefreshTokenCookie);
                DeleteAccessTokenCookie();
                return Unauthorized(new { message = "انتهت صلاحية الجلسة، يرجى تسجيل الدخول مجدداً" });
            }

            SetRefreshTokenCookie(result.Value.refreshToken);
            SetAccessTokenCookie(result.Value.accessToken);
            return Ok(new { accessToken = result.Value.accessToken });
        }
        catch (Exception ex)
        {
            // Sprint 1 Fix: Log refresh failure safely WITHOUT printing tokens
            // The user is logged out only if refresh genuinely fails (expired/invalid token),
            // not due to transient server errors (Redis timeout, etc.)
            logger.LogError(ex, "RefreshToken failed: {ExceptionType}", ex.GetType().Name);
            
            // For transient errors (Redis timeout, network issues), return 500 instead of 401
            // so the frontend can retry instead of force-logging-out the user
            if (ex is InvalidOperationException or TimeoutException or System.Net.Sockets.SocketException)
            {
                return StatusCode(500, new { message = "حدث خطأ مؤقت أثناء تجديد الجلسة، يرجى المحاولة مرة أخرى" });
            }
            
            // For auth errors (invalid/expired token), clear cookie and return 401
            Response.Cookies.Delete(RefreshTokenCookie);
            DeleteAccessTokenCookie();
            return Unauthorized(new { message = "انتهت صلاحية الجلسة، يرجى تسجيل الدخول مجدداً" });
        }
    }

    [HttpGet("me")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<UserDto>> GetMe()
    {
        if (!currentUser.UserId.HasValue) return Unauthorized();
        var user = await authService.GetMeAsync(currentUser.UserId.Value);
        return user == null ? Unauthorized() : Ok(user);
    }

    [HttpGet("me/permissions")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<UserPermissionsDto>> GetMyPermissions()
    {
        if (!currentUser.UserId.HasValue) return Unauthorized();

        var user = await authService.GetMeAsync(currentUser.UserId.Value);
        if (user == null) return Unauthorized();

        // Get role permissions from database
        var roleKey = user.Role; // e.g. "Admin", "Reception"

        // Admin has all permissions implicitly
        if (string.Equals(roleKey, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            var allResources = await db.RolePermissions
                .Select(rp => rp.Resource)
                .Distinct()
                .ToListAsync();

            var allPermissionKeys = new List<string>();
            foreach (var resource in allResources)
            {
                allPermissionKeys.Add($"{resource}.view");
                allPermissionKeys.Add($"{resource}.create");
                allPermissionKeys.Add($"{resource}.edit");
                allPermissionKeys.Add($"{resource}.delete");
                allPermissionKeys.Add($"{resource}.export");
                allPermissionKeys.Add($"{resource}.approve");
            }

            return Ok(new UserPermissionsDto { Role = roleKey, Permissions = allPermissionKeys });
        }

        // Non-admin: derive permission keys from RolePermission records
        var permissions = await db.RolePermissions
            .Where(rp => rp.Role == roleKey)
            .ToListAsync();

        var permissionKeys = new List<string>();
        foreach (var perm in permissions)
        {
            if (perm.CanView) permissionKeys.Add($"{perm.Resource}.view");
            if (perm.CanCreate) permissionKeys.Add($"{perm.Resource}.create");
            if (perm.CanEdit) permissionKeys.Add($"{perm.Resource}.edit");
            if (perm.CanDelete) permissionKeys.Add($"{perm.Resource}.delete");
            if (perm.CanExport) permissionKeys.Add($"{perm.Resource}.export");
            if (perm.CanApprove) permissionKeys.Add($"{perm.Resource}.approve");
        }

        return Ok(new UserPermissionsDto { Role = roleKey, Permissions = permissionKeys });
    }

    [HttpPost("change-password")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = currentUser.UserId;
        if (!userId.HasValue) return Unauthorized();

        // SEC-11: enforce centralized password complexity policy before hashing.
        var (valid, policyError) = PasswordPolicy.Validate(request.NewPassword);
        if (!valid)
            return BadRequest(new { message = policyError });

        var newAccessToken = await authService.ChangePasswordAsync(userId.Value, request.CurrentPassword, request.NewPassword);
        if (newAccessToken == null)
            return BadRequest(new { message = "كلمة المرور الحالية غير صحيحة" });

        await auditService.LogAsync(AuditAction.PasswordChange, "users", userId.Value);

        return Ok(new { message = "تم تغيير كلمة المرور بنجاح", accessToken = newAccessToken });
    }

    /// <summary>
    /// Unlocks a locked-out account. Accessible without authentication but requires
    /// the server-side ADMIN_UNLOCK_SECRET environment variable. This allows a
    /// sysadmin to unlock any account (including the admin account) when locked out
    /// due to too many failed login attempts.
    /// </summary>
    [HttpPost("unlock-account")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<IActionResult> UnlockAccount([FromBody] UnlockAccountRequest request)
    {
        // Validate the unlock secret from environment variables
        var unlockSecret = Environment.GetEnvironmentVariable("ADMIN_UNLOCK_SECRET");
        if (string.IsNullOrWhiteSpace(unlockSecret))
            return StatusCode(403, new { message = "إلغاء القفل غير مفعّل — لم يتم تعيين المفتاح السري" });

        if (request.Secret != unlockSecret)
            return BadRequest(new { message = "المفتاح السري غير صحيح" });

        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(new { message = "اسم المستخدم مطلوب" });

        await loginAttempts.ResetFailedAttemptsAsync(request.Username);
        return Ok(new { message = $"تم إلغاء قفل الحساب '{request.Username}' بنجاح" });
    }

    // ── Forgot Password ─────────────────────────────────────────────────────

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("ForgotPasswordPolicy")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        // Always return the same message — never reveal if account exists
        var genericMessage = "إذا كان الحساب موجوداً، سيتم إرسال تعليمات استعادة كلمة المرور";

        // Find user by username or email
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Username == request.UsernameOrEmail || u.Email == request.UsernameOrEmail);

        if (user is null || !user.IsActive)
        {
            // User not found — just return generic message, no audit
            return Ok(new { message = genericMessage });
        }

        var smtpConfigured = await emailService.IsConfiguredAsync();

        if (!string.IsNullOrWhiteSpace(user.Email) && smtpConfigured)
        {
            // Generate secure random token (64 bytes, Base64)
            var tokenBytes = RandomNumberGenerator.GetBytes(64);
            var rawToken = Convert.ToBase64String(tokenBytes);

            // Hash token for storage (SHA256)
            var tokenHash = HashToken(rawToken);

            // Store hashed token with 30min expiry
            db.PasswordResetTokens.Add(new PasswordResetToken
            {
                UserId = user.Id,
                TokenHash = tokenHash,
                ExpiresAt = DateTime.UtcNow.AddMinutes(30)
            });

            await db.SaveChangesAsync();

            // Send email with reset link — may fail even when configured
            var emailSent = await emailService.SendPasswordResetEmailAsync(user.Email, rawToken, "reset-password");

            if (emailSent)
            {
                await auditService.LogAsync(AuditAction.ForgotPasswordRequested, "users", user.Id);
                await auditService.LogAsync(AuditAction.ResetTokenGenerated, "password_reset_tokens", user.Id);
            }
            else
            {
                // Email configured but sending failed — create fallback PasswordResetRequest
                // so the admin can still recover the account from the dashboard.
                db.PasswordResetRequests.Add(new PasswordResetRequest
                {
                    UserId = user.Id,
                    UsernameOrEmail = request.UsernameOrEmail,
                    Status = "Pending",
                    RequestedAt = DateTime.UtcNow,
                    Notes = "Email delivery failed; reset token was generated but not delivered."
                });

                await db.SaveChangesAsync();

                await auditService.LogAsync(AuditAction.ForgotPasswordRequested, "users", user.Id);
            }
        }
        else
        {
            // No email or SMTP not configured — create a PasswordResetRequest for admin
            db.PasswordResetRequests.Add(new PasswordResetRequest
            {
                UserId = user.Id,
                UsernameOrEmail = request.UsernameOrEmail,
                Status = "Pending",
                RequestedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();

            await auditService.LogAsync(AuditAction.ForgotPasswordRequested, "users", user.Id);
        }

        return Ok(new { message = genericMessage });
    }

    // ── Reset Password with Token ───────────────────────────────────────────

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("PortalPasswordResetPolicy")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        // Validate passwords match
        if (request.NewPassword != request.ConfirmPassword)
            return BadRequest(new { message = "كلمة المرور الجديدة وتأكيدها غير متطابقين" });

        // SEC-11: enforce centralized password complexity policy (replaces inline regex checks).
        var (valid, policyError) = PasswordPolicy.Validate(request.NewPassword);
        if (!valid)
            return BadRequest(new { message = policyError });

        // Hash provided token, look up in PasswordResetTokens
        var tokenHash = HashToken(request.Token);
        var resetToken = await db.PasswordResetTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        // Validate: exists, not expired, not used
        if (resetToken is null)
            return BadRequest(new { message = "رابط إعادة التعيين غير صالح" });

        if (resetToken.IsUsed)
            return BadRequest(new { message = "رابط إعادة التعيين مستخدم بالفعل" });

        if (resetToken.ExpiresAt < DateTime.UtcNow)
            return BadRequest(new { message = "انتهت صلاحية رابط إعادة التعيين" });

        // Get user
        var user = await db.Users.FindAsync(resetToken.UserId);
        if (user is null)
            return BadRequest(new { message = "المستخدم غير موجود" });

        // Update user password hash/salt
        var newSalt = AuthService.GenerateSalt();
        var newHash = AuthService.HashPassword(request.NewPassword, newSalt);
        user.PasswordHash = newHash;
        user.PasswordSalt = newSalt;
        user.MustChangePassword = false;

        // Mark token as used
        resetToken.IsUsed = true;
        resetToken.UsedAt = DateTime.UtcNow;

        // Reset login lockout
        await loginAttempts.ResetFailedAttemptsAsync(user.Username);

        // Revoke all refresh tokens
        await tokenService.RevokeAllRefreshTokensAsync(user.Id);

        await db.SaveChangesAsync();

        await auditService.LogAsync(AuditAction.ResetTokenUsed, "users", user.Id);

        return Ok(new { message = "تم إعادة تعيين كلمة المرور بنجاح" });
    }

    // ── Impersonation ───────────────────────────────────────────────────────

    [HttpPost("impersonate/{userId:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Impersonate(Guid userId, [FromBody] ImpersonateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { message = "يجب تحديد سبب الانتحال" });

        // Target user must exist and be active (not deleted)
        var targetUser = await db.Users
            .Include(u => u.Doctor)
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

        if (targetUser is null)
            return NotFound(new { message = "المستخدم المستهدف غير موجود أو غير نشط" });

        // Normal admin cannot impersonate another admin
        if (targetUser.Role == UserRole.Admin)
        {
            await auditService.LogAsync(AuditAction.ImpersonationDenied, "users", userId,
                details: $"Admin {currentUser.Username} attempted to impersonate another admin {targetUser.Username}");
            return StatusCode(403, new { message = "لا يمكن الانتحال بحساب مدير آخر" });
        }

        var originalUserId = currentUser.OriginalUserId ?? currentUser.UserId;
        var originalRole = currentUser.Role?.ToString();

        // Generate access token with impersonation claims
        var accessToken = tokenService.GenerateAccessToken(targetUser, originalUserId, originalRole);
        var refreshToken = tokenService.GenerateRefreshToken();
        await tokenService.StoreRefreshTokenAsync(targetUser.Id, refreshToken);

        SetRefreshTokenCookie(refreshToken);
        SetAccessTokenCookie(accessToken);

        await auditService.LogAsync(AuditAction.ImpersonationStarted, "users", userId,
            details: $"Admin {currentUser.Username} started impersonating {targetUser.Username}. Reason: {request.Reason}");

        var userDto = new UserDto
        {
            Id = targetUser.Id,
            Username = targetUser.Username,
            Role = targetUser.Role.ToString(),
            BranchId = targetUser.BranchId,
            DoctorName = targetUser.Doctor?.Name,
            DoctorId = targetUser.Doctor?.Id,
            DoctorColor = targetUser.Doctor?.Color,
            DoctorInitials = targetUser.Doctor?.AvatarInitials,
            MustChangePassword = targetUser.MustChangePassword,
            Email = targetUser.Email,
            IsActive = targetUser.IsActive
        };

        return Ok(new { accessToken, user = userDto, isImpersonating = true });
    }

    [HttpPost("stop-impersonation")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> StopImpersonation()
    {
        // Must be currently impersonating
        if (!currentUser.IsImpersonating || !currentUser.OriginalUserId.HasValue)
            return BadRequest(new { message = "أنت لا تنتحل حالياً" });

        var originalUserId = currentUser.OriginalUserId.Value;

        // Get original user
        var originalUser = await db.Users
            .Include(u => u.Doctor)
            .FirstOrDefaultAsync(u => u.Id == originalUserId);

        if (originalUser is null)
            return NotFound(new { message = "المستخدم الأصلي غير موجود" });

        // Generate new access token for original user (without impersonation claims)
        var accessToken = tokenService.GenerateAccessToken(originalUser);
        var refreshToken = tokenService.GenerateRefreshToken();
        await tokenService.StoreRefreshTokenAsync(originalUser.Id, refreshToken);

        SetRefreshTokenCookie(refreshToken);
        SetAccessTokenCookie(accessToken);

        await auditService.LogAsync(AuditAction.ImpersonationStopped, "users", originalUserId,
            details: $"Stopped impersonation, returned to user {originalUser.Username}");

        var userDto = new UserDto
        {
            Id = originalUser.Id,
            Username = originalUser.Username,
            Role = originalUser.Role.ToString(),
            BranchId = originalUser.BranchId,
            DoctorName = originalUser.Doctor?.Name,
            DoctorId = originalUser.Doctor?.Id,
            DoctorColor = originalUser.Doctor?.Color,
            DoctorInitials = originalUser.Doctor?.AvatarInitials,
            MustChangePassword = originalUser.MustChangePassword,
            Email = originalUser.Email,
            IsActive = originalUser.IsActive
        };

        return Ok(new { accessToken, user = userDto, isImpersonating = false });
    }

    // ── Helper Methods ─────────────────────────────────────────────────────

    private void SetRefreshTokenCookie(string token)
    {
        var opts = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        };
        Response.Cookies.Append(RefreshTokenCookie, token, opts);
    }

    private void SetAccessTokenCookie(string token)
    {
        Response.Cookies.Append(AccessTokenCookie, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/uploads",
            Expires = DateTimeOffset.UtcNow.AddMinutes(30)
        });
    }

    private void DeleteAccessTokenCookie() =>
        Response.Cookies.Delete(AccessTokenCookie, new CookieOptions
        {
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/uploads"
        });

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
