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

    [HttpPost("auth/send-code")]
    [AllowAnonymous]
    public async Task<IActionResult> SendVerificationCode([FromBody] PatientLoginRequest req)
    {
        try
        {
            var (success, error) = await portalService.SendVerificationCodeAsync(req.PhoneNumber);
            if (!success) return BadRequest(new { message = error });
            return Ok(new { message = "تم إرسال رمز التحقق بنجاح" });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "حدث خطأ أثناء إرسال رمز التحقق. يرجى المحاولة لاحقاً" });
        }
    }

    [HttpPost("auth/verify")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyCode([FromBody] PatientVerifyRequest req)
    {
        try
        {
            var (response, error) = await portalService.VerifyCodeAsync(req.PhoneNumber, req.Code);
            if (response == null) return BadRequest(new { message = error });
            return Ok(response);
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "حدث خطأ أثناء التحقق من الرمز. يرجى المحاولة لاحقاً" });
        }
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
