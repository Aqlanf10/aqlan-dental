using AqlanDentalPro.Application.DTOs.PatientPortal;
using AqlanDentalPro.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/portal")]
public class PatientPortalController(IPatientPortalService portalService) : ControllerBase
{
    // ── Auth Endpoints (No auth required) ───────────────────────────────────

    [HttpPost("auth/login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] PatientLoginRequest req)
    {
        try
        {
            var (response, error) = await portalService.LoginAsync(req.Username, req.Password);
            if (response == null) return BadRequest(new { message = error });
            return Ok(response);
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "حدث خطأ أثناء تسجيل الدخول. يرجى المحاولة لاحقاً" });
        }
    }

    [HttpPost("auth/forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] PatientForgotPasswordRequest req)
    {
        try
        {
            var (success, error) = await portalService.ForgotPasswordAsync(req.PhoneNumber);
            if (!success) return BadRequest(new { message = error });
            return Ok(new { message = "تم إرسال رمز التحقق عبر واتساب" });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "حدث خطأ أثناء إرسال الرمز. يرجى المحاولة لاحقاً" });
        }
    }

    [HttpPost("auth/reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] PatientResetPasswordRequest req)
    {
        try
        {
            var (response, error) = await portalService.ResetPasswordAsync(req.PhoneNumber, req.Code, req.NewPassword);
            if (response == null) return BadRequest(new { message = error });
            return Ok(response);
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "حدث خطأ أثناء إعادة تعيين كلمة المرور. يرجى المحاولة لاحقاً" });
        }
    }

    // ── Staff-Only Endpoints ────────────────────────────────────────────────

    [HttpGet("credentials/{patientId:guid}")]
    [Authorize(Policy = "DoctorAccess")]
    public async Task<IActionResult> GetPatientCredentials(Guid patientId)
    {
        var creds = await portalService.GetPatientCredentialsAsync(patientId);
        if (creds == null) return NotFound(new { message = "لا يوجد حساب بوابة لهذا المريض" });
        return Ok(creds);
    }

    /// <summary>إعادة تعيين كلمة مرور بوابة المريض (يعرض الكلمة المؤقتة مرة واحدة فقط)</summary>
    [HttpPost("credentials/{patientId:guid}/reset-password")]
    [Authorize(Policy = "DoctorAccess")]
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
    public IActionResult GetClinicInfo()
    {
        var info = portalService.GetClinicInfoAsync();
        return Ok(info);
    }

    // ── Helper ─────────────────────────────────────────────────────────────
    private Guid? GetPatientId()
    {
        var claim = User.FindFirst("patientId")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
