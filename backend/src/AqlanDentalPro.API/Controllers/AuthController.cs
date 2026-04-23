using AqlanDentalPro.Application.DTOs.Auth;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService authService, ICurrentUserService currentUser) : ControllerBase
{
    private const string RefreshTokenCookie = "refresh_token";

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var result = await authService.LoginAsync(request);
        if (result == null)
            return Unauthorized(new { message = "اسم المستخدم أو كلمة المرور غير صحيحة" });

        SetRefreshTokenCookie(result.RefreshToken);
        return Ok(new { result.AccessToken, result.User });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var refreshToken = Request.Cookies[RefreshTokenCookie];
        if (refreshToken != null && currentUser.UserId.HasValue)
            await authService.LogoutAsync(currentUser.UserId.Value, refreshToken);

        Response.Cookies.Delete(RefreshTokenCookie);
        return NoContent();
    }

    [HttpPost("refresh-token")]
    public async Task<ActionResult<object>> RefreshToken()
    {
        var refreshToken = Request.Cookies[RefreshTokenCookie];
        if (string.IsNullOrEmpty(refreshToken))
            return Unauthorized(new { message = "لا يوجد refresh token" });

        if (!currentUser.UserId.HasValue)
            return Unauthorized();

        var result = await authService.RefreshAsync(currentUser.UserId.Value, refreshToken);
        if (result == null)
        {
            Response.Cookies.Delete(RefreshTokenCookie);
            return Unauthorized(new { message = "انتهت صلاحية الجلسة، يرجى تسجيل الدخول مجدداً" });
        }

        SetRefreshTokenCookie(result.Value.refreshToken);
        return Ok(new { accessToken = result.Value.accessToken });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> GetMe()
    {
        if (!currentUser.UserId.HasValue) return Unauthorized();
        var user = await authService.GetMeAsync(currentUser.UserId.Value);
        return user == null ? Unauthorized() : Ok(user);
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
