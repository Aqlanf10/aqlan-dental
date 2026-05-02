using AqlanDentalPro.Application.DTOs.PatientPortal;
using AqlanDentalPro.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/portal")]
public class PatientPortalController(IPatientPortalService portalService) : ControllerBase
{
    // ── Auth Endpoints (No auth required) ───────────────────────────────────

    /// <summary>تسجيل دخول المريض باسم المستخدم وكلمة المرور</summary>
    [HttpPost("auth/login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] PatientPasswordLoginRequest req)
    {
        var (response, error) = await portalService.LoginAsync(req.Username, req.Password);
        if (response == null) return BadRequest(new { message = error });
        return Ok(response);
    }

    /// <summary>طلب بيانات الدخول عبر الواتساب</summary>
    [HttpPost("auth/request-credentials")]
    [AllowAnonymous]
    public async Task<IActionResult> RequestCredentials([FromBody] PatientCredentialsRequest req)
    {
        var (success, error) = await portalService.RequestCredentialsViaWhatsAppAsync(req.PhoneNumber);
        if (!success) return BadRequest(new { message = error });
        return Ok(new { message = "تم إرسال بيانات الدخول عبر الواتساب إذا كان الرقم مسجلاً لدينا" });
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

    [HttpGet("appointments")]
    [Authorize(Policy = "PatientAccess")]
    public async Task<IActionResult> GetAppointments([FromQuery] int limit = 20)
    {
        var patientId = GetPatientId();
        if (patientId == null) return Unauthorized();

        var appointments = await portalService.GetAppointmentsAsync(patientId.Value, limit);
        return Ok(appointments);
    }

    [HttpPost("appointments")]
    [Authorize(Policy = "PatientAccess")]
    public async Task<IActionResult> RequestAppointment([FromBody] PatientAppointmentRequestDto req)
    {
        var patientId = GetPatientId();
        if (patientId == null) return Unauthorized();

        var (result, error) = await portalService.RequestAppointmentAsync(patientId.Value, req);
        if (result == null) return BadRequest(new { message = error });
        return CreatedAtAction(nameof(GetAppointments), result);
    }

    [HttpDelete("appointments/{id:guid}")]
    [Authorize(Policy = "PatientAccess")]
    public async Task<IActionResult> CancelAppointment(Guid id)
    {
        var patientId = GetPatientId();
        if (patientId == null) return Unauthorized();

        var (success, error) = await portalService.CancelAppointmentAsync(patientId.Value, id);
        if (!success) return BadRequest(new { message = error });
        return Ok(new { message = "تم إلغاء الموعد بنجاح" });
    }

    [HttpGet("treatments")]
    [Authorize(Policy = "PatientAccess")]
    public async Task<IActionResult> GetTreatments([FromQuery] int limit = 20)
    {
        var patientId = GetPatientId();
        if (patientId == null) return Unauthorized();

        var treatments = await portalService.GetTreatmentsAsync(patientId.Value, limit);
        return Ok(treatments);
    }

    [HttpGet("prescriptions")]
    [Authorize(Policy = "PatientAccess")]
    public async Task<IActionResult> GetPrescriptions([FromQuery] int limit = 20)
    {
        var patientId = GetPatientId();
        if (patientId == null) return Unauthorized();

        var prescriptions = await portalService.GetPrescriptionsAsync(patientId.Value, limit);
        return Ok(prescriptions);
    }

    [HttpGet("finance")]
    [Authorize(Policy = "PatientAccess")]
    public async Task<IActionResult> GetFinancialSummary()
    {
        var patientId = GetPatientId();
        if (patientId == null) return Unauthorized();

        var finance = await portalService.GetFinancialSummaryAsync(patientId.Value);
        return Ok(finance);
    }

    // ── Helper ─────────────────────────────────────────────────────────────
    private Guid? GetPatientId()
    {
        var claim = User.FindFirst("patientId")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
