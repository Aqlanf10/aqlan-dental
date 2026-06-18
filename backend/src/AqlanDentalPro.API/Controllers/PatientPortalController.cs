using AqlanDentalPro.Application.DTOs.PatientPortal;
using AqlanDentalPro.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/portal")]
public class PatientPortalController(IPatientPortalService portalService, IConfiguration configuration, ILogger<PatientPortalController> logger, IRecaptchaService recaptcha) : ControllerBase
{
    private readonly IConfiguration Configuration = configuration;
    private readonly ILogger<PatientPortalController> Logger = logger;
    // ── Auth Endpoints (No auth required) ───────────────────────────────────

    [HttpPost("auth/login")]
    [AllowAnonymous]
    [EnableRateLimiting("PortalAuthPolicy")]
    public async Task<IActionResult> Login([FromBody] PatientLoginRequest req)
    {
        try
        {
            var (response, error) = await portalService.LoginAsync(req.Username, req.Password);
            if (response == null) return BadRequest(new { message = error });
            return Ok(response);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Portal login failed for username {Username}", req.Username);
            return StatusCode(500, new { message = "حدث خطأ أثناء تسجيل الدخول. يرجى المحاولة لاحقاً" });
        }
    }

    [HttpPost("auth/forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("PortalPasswordResetPolicy")] // SEC-04 FIX: Stricter limit on forgot-password
    public async Task<IActionResult> ForgotPassword([FromBody] PatientForgotPasswordRequest req)
    {
        // SEC-06: Always call ValidateTokenAsync. The service handles dev mode
        // (no Recaptcha:SecretKey configured → allows) and rejects empty/invalid
        // tokens when reCAPTCHA IS configured. Previously the outer guard skipped
        // validation entirely when the token field was omitted, letting bots trigger
        // unlimited paid WhatsApp OTP messages via forgot-password.
        var (recaptchaValid, _, recaptchaError) = await recaptcha.ValidateTokenAsync(req.RecaptchaToken);
        if (!recaptchaValid)
        {
            return BadRequest(new { message = recaptchaError ?? "فشل التحقق الأمني" });
        }

        try
        {
            var (success, error) = await portalService.ForgotPasswordAsync(req.PhoneNumber);
            if (!success) return BadRequest(new { message = error });
            return Ok(new { message = "تم إرسال رمز التحقق عبر واتساب" });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Portal forgot-password failed for phone {Phone}", req.PhoneNumber);
            return StatusCode(500, new { message = "حدث خطأ أثناء إرسال الرمز. يرجى المحاولة لاحقاً" });
        }
    }

    [HttpPost("auth/reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("PortalPasswordResetPolicy")] // SEC-04 FIX: Stricter limit on reset-password (3/15min)
    public async Task<IActionResult> ResetPassword([FromBody] PatientResetPasswordRequest req)
    {
        try
        {
            var (response, error) = await portalService.ResetPasswordAsync(req.PhoneNumber, req.Code, req.NewPassword);
            if (response == null) return BadRequest(new { message = error });
            return Ok(response);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Portal reset-password failed");
            return StatusCode(500, new { message = "حدث خطأ أثناء إعادة تعيين كلمة المرور. يرجى المحاولة لاحقاً" });
        }
    }

    [HttpPost("auth/change-password")]
    [Authorize(Policy = "PatientAccess")]
    public async Task<IActionResult> ChangePassword([FromBody] PatientChangePasswordRequest req)
    {
        var patientId = GetPatientId();
        if (patientId == null) return Unauthorized(new { message = "غير مصرح" });

        try
        {
            var (response, error) = await portalService.ChangePasswordAsync(patientId.Value, req.CurrentPassword, req.NewPassword);
            if (response == null) return BadRequest(new { message = error });
            return Ok(response);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Portal change-password failed for patient {PatientId}", patientId);
            return StatusCode(500, new { message = "حدث خطأ أثناء تغيير كلمة المرور. يرجى المحاولة لاحقاً" });
        }
    }

    [HttpPost("auth/refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken([FromBody] PatientRefreshTokenRequest req)
    {
        // Extract patientId from the (possibly expired) JWT in the Authorization header
        var patientId = ExtractPatientIdFromToken();
        if (patientId == null) return Unauthorized(new { message = "غير مصرح — رمز التحديث غير صالح" });

        try
        {
            var (response, error) = await portalService.RefreshTokenAsync(patientId.Value, req.RefreshToken);
            if (response == null) return BadRequest(new { message = error });
            return Ok(response);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Portal refresh-token failed for patient {PatientId}", patientId);
            return StatusCode(500, new { message = "حدث خطأ أثناء تحديث الجلسة. يرجى تسجيل الدخول مرة أخرى" });
        }
    }

    /// <summary>
    /// Extracts patientId from the Authorization header JWT, allowing expired tokens.
    /// This is needed because the refresh-token endpoint must work when the access token has expired.
    /// </summary>
    private Guid? ExtractPatientIdFromToken()
    {
        var authHeader = HttpContext.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var token = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(token))
            return null;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = false, // Allow expired tokens — this is the refresh flow
                ValidateIssuerSigningKey = true,
                ValidIssuer = Configuration["Jwt:Issuer"],
                ValidAudience = Configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["Jwt:SecretKey"]!))
            };

            var principal = handler.ValidateToken(token, parameters, out _);
            var patientIdClaim = principal.FindFirst("patientId")?.Value;
            return Guid.TryParse(patientIdClaim, out var id) ? id : null;
        }
        catch
        {
            return null;
        }
    }

    // ── Staff-Only Endpoints ────────────────────────────────────────────────

    [HttpGet("credentials/{patientId:guid}")]
    [Authorize(Policy = "AdminOrReception")]
    public async Task<IActionResult> GetPatientCredentials(Guid patientId)
    {
        var creds = await portalService.GetPatientCredentialsAsync(patientId);
        if (creds == null) return NotFound(new { message = "لا يوجد حساب بوابة لهذا المريض" });
        return Ok(creds);
    }

    /// <summary>إنشاء حساب بوابة المريض (يعرض كلمة المرور المؤقتة مرة واحدة فقط)</summary>
    [HttpPost("credentials/{patientId:guid}/create")]
    [Authorize(Policy = "AdminOrReception")]
    public async Task<IActionResult> CreatePortalAccount(Guid patientId)
    {
        try
        {
            // Fetch patient info to get patientNumber and phone
            var patient = await portalService.GetPatientInfoForAccountCreationAsync(patientId);
            if (patient == null) return NotFound(new { message = "المريض غير موجود" });

            var (username, plainPassword) = await portalService.EnsurePatientAccountAsync(patientId, patient.PatientNumber, patient.Phone);

            if (string.IsNullOrEmpty(plainPassword))
            {
                // Account already existed — return credentials info without password
                var existingCreds = await portalService.GetPatientCredentialsAsync(patientId);
                return Ok(new { message = "الحساب موجود بالفعل", username = existingCreds?.Username ?? username, alreadyExists = true });
            }

            return Ok(new { username, temporaryPassword = plainPassword, message = "تم إنشاء حساب البوابة بنجاح", alreadyExists = false });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to create portal account for patient {PatientId}", patientId);
            return StatusCode(500, new { message = "حدث خطأ أثناء إنشاء حساب البوابة. يرجى المحاولة لاحقاً" });
        }
    }

    /// <summary>إعادة تعيين كلمة مرور بوابة المريض (يعرض الكلمة المؤقتة مرة واحدة فقط)</summary>
    [HttpPost("credentials/{patientId:guid}/reset-password")]
    [Authorize(Policy = "AdminOrReception")]
    public async Task<ActionResult<PatientPasswordResetResponseDto>> ResetPortalPassword(Guid patientId)
    {
        var (result, error) = await portalService.StaffResetPasswordAsync(patientId);
        if (result == null) return BadRequest(new { message = error });
        return Ok(result);
    }

    // ── Protected Endpoints (Patient auth required) ────────────────────────

    [HttpGet("dashboard")]
    [Authorize(Policy = "PatientAccess")]
    public async Task<IActionResult> GetDashboard()
    {
        var patientId = GetPatientId();
        if (patientId == null) return Unauthorized(new { message = "غير مصرح" });

        var dashboard = await portalService.GetDashboardAsync(patientId.Value);
        return Ok(dashboard);
    }

    [HttpGet("profile")]
    [Authorize(Policy = "PatientAccess")]
    public async Task<IActionResult> GetProfile()
    {
        var patientId = GetPatientId();
        if (patientId == null) return Unauthorized(new { message = "غير مصرح" });

        var profile = await portalService.GetProfileAsync(patientId.Value);
        return Ok(profile);
    }

    [HttpPut("profile")]
    [Authorize(Policy = "PatientAccess")]
    public async Task<IActionResult> UpdateProfile([FromBody] PatientProfileUpdateDto req)
    {
        var patientId = GetPatientId();
        if (patientId == null) return Unauthorized(new { message = "غير مصرح" });

        var (result, error) = await portalService.UpdateProfileAsync(patientId.Value, req);
        if (result == null) return BadRequest(new { message = error });
        return Ok(result);
    }

    [HttpGet("appointments")]
    [Authorize(Policy = "PatientAccess")]
    public async Task<IActionResult> GetAppointments([FromQuery] int limit = 50)
    {
        var patientId = GetPatientId();
        if (patientId == null) return Unauthorized(new { message = "غير مصرح" });

        var appointments = await portalService.GetAppointmentsAsync(patientId.Value, limit);
        return Ok(appointments);
    }

    [HttpPost("appointments")]
    [Authorize(Policy = "PatientAccess")]
    public async Task<IActionResult> RequestAppointment([FromBody] PatientAppointmentRequestDto req)
    {
        var patientId = GetPatientId();
        if (patientId == null) return Unauthorized(new { message = "غير مصرح" });

        var (result, error) = await portalService.RequestAppointmentAsync(patientId.Value, req);
        if (result == null) return BadRequest(new { message = error });
        return CreatedAtAction(nameof(GetAppointments), result);
    }

    [HttpDelete("appointments/{id:guid}")]
    [Authorize(Policy = "PatientAccess")]
    public async Task<IActionResult> CancelAppointment(Guid id)
    {
        var patientId = GetPatientId();
        if (patientId == null) return Unauthorized(new { message = "غير مصرح" });

        var (success, error) = await portalService.CancelAppointmentAsync(patientId.Value, id);
        if (!success) return BadRequest(new { message = error });
        return Ok(new { message = "تم إلغاء الموعد بنجاح" });
    }

    [HttpGet("treatments")]
    [Authorize(Policy = "PatientAccess")]
    public async Task<IActionResult> GetTreatments([FromQuery] int limit = 50)
    {
        var patientId = GetPatientId();
        if (patientId == null) return Unauthorized(new { message = "غير مصرح" });

        var treatments = await portalService.GetTreatmentsAsync(patientId.Value, limit);
        return Ok(treatments);
    }

    [HttpGet("visits")]
    [Authorize(Policy = "PatientAccess")]
    public async Task<IActionResult> GetVisits([FromQuery] int limit = 50)
    {
        var patientId = GetPatientId();
        if (patientId == null) return Unauthorized(new { message = "غير مصرح" });

        var visits = await portalService.GetVisitsAsync(patientId.Value, limit);
        return Ok(visits);
    }

    [HttpGet("prescriptions")]
    [Authorize(Policy = "PatientAccess")]
    public async Task<IActionResult> GetPrescriptions([FromQuery] int limit = 50)
    {
        var patientId = GetPatientId();
        if (patientId == null) return Unauthorized(new { message = "غير مصرح" });

        var prescriptions = await portalService.GetPrescriptionsAsync(patientId.Value, limit);
        return Ok(prescriptions);
    }

    [HttpGet("finance")]
    [Authorize(Policy = "PatientAccess")]
    public async Task<IActionResult> GetFinancialSummary()
    {
        var patientId = GetPatientId();
        if (patientId == null) return Unauthorized(new { message = "غير مصرح" });

        var finance = await portalService.GetFinancialSummaryAsync(patientId.Value);
        return Ok(finance);
    }

    [HttpGet("doctors")]
    [Authorize(Policy = "PatientAccess")]
    public async Task<IActionResult> GetDoctors()
    {
        var doctors = await portalService.GetDoctorsAsync();
        return Ok(doctors);
    }

    [HttpGet("clinic-info")]
    [AllowAnonymous]
    public async Task<IActionResult> GetClinicInfo()
    {
        var info = await portalService.GetClinicInfoAsync();
        return Ok(info);
    }

    // ── Helper ─────────────────────────────────────────────────────────────
    private Guid? GetPatientId()
    {
        var claim = User.FindFirst("patientId")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
