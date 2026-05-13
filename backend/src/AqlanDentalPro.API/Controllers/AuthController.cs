using AqlanDentalPro.Application.DTOs.Auth;
using AqlanDentalPro.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService, ICurrentUserService currentUser, ITokenService tokenService, ILoginAttemptService loginAttempts) : ControllerBase
{
    private const string RefreshTokenCookie = "refresh_token";

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
            return Unauthorized(new { message = "انتهت صلاحية الجلسة، يرجى تسجيل الدخول مجدداً" });
        }

        SetRefreshTokenCookie(result.Value.refreshToken);
        return Ok(new { accessToken = result.Value.accessToken });
    }

    [HttpGet("me")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<UserDto>> GetMe()
    {
        if (!currentUser.UserId.HasValue) return Unauthorized();
        var user = await authService.GetMeAsync(currentUser.UserId.Value);
        return user == null ? Unauthorized() : Ok(user);
    }

    [HttpPost("change-password")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = currentUser.UserId;
        if (!userId.HasValue) return Unauthorized();

        var success = await authService.ChangePasswordAsync(userId.Value, request.CurrentPassword, request.NewPassword);
        if (!success)
            return BadRequest(new { message = "كلمة المرور الحالية غير صحيحة" });

        return Ok(new { message = "تم تغيير كلمة المرور بنجاح" });
    }

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
}
