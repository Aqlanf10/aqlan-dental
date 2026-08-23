using AqlanDentalPro.Application.DTOs.Journey;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

/// <summary>
/// Patient Journey Command Center — unified operational screen for reception
/// and clinical workflow: Appointment → Arrived → Queue → Visit → Checkout.
/// </summary>
/// <remarks>
/// CLIN-22: All business logic now lives in <see cref="PatientJourneyService"/>
/// (read aggregates + journey routing) and <see cref="CheckoutService"/>
/// (transactional mutations + advisory locks). This controller is a thin
/// HTTP adapter — it parses query/route input, delegates to the services,
/// and returns the <c>IActionResult</c> they produce. No business decisions
/// are made here.
/// </remarks>
[ApiController]
[Route("api/patient-journey")]
[Authorize(Policy = "StaffOnly")]
public class PatientJourneyController(
    PatientJourneyService journeyService,
    CheckoutService checkoutService) : ControllerBase
{
    // ─── 1. GET /api/patient-journey/today ────────────────────────────────────
    /// <summary>Returns today's patient journey list combining appointments,
    /// queue status, visit data, and payment info.</summary>
    [HttpGet("today")]
    public async Task<IActionResult> GetToday([FromQuery] string? date, [FromQuery] string? status,
        [FromQuery] Guid? doctorId, [FromQuery] Guid? serviceId, [FromQuery] Guid? roomId)
    {
        // Parse date - default to clinic today (SEQ-05: was DateTime.UtcNow which is
        // wrong by up to 3 hours on Railway UTC for a Yemen clinic).
        DateOnly queryDate = ClinicTimeProvider.ClinicToday();
        try
        {
            if (!string.IsNullOrWhiteSpace(date))
            {
                if (!DateOnly.TryParse(date, out var parsed))
                    return BadRequest(new { message = "صيغة التاريخ غير صالحة. استخدم YYYY-MM-DD" });
                queryDate = parsed;
            }

            // Validate status filter if provided
            AppointmentStatus? statusFilter = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<AppointmentStatus>(status, true, out var parsedStatus))
                    return BadRequest(new { message = "حالة غير صالحة" });
                statusFilter = parsedStatus;
            }

            return await journeyService.GetTodayAsync(User, queryDate, statusFilter, doctorId, serviceId, roomId);
        }
        catch (Exception ex)
        {
            // Defensive: service has its own try/catch, but parsing/early-returns
            // could still throw unexpectedly. Preserve original 500 contract.
            return StatusCode(500, new { message = "حدث خطأ أثناء تحميل رحلة المرضى", detail = ex.Message });
        }
    }

    // ─── 1B. GET /api/patient-journey/{patientId}/daily-summary ───────────
    /// <summary>Returns a comprehensive daily journey summary for a specific patient,
    /// aggregating patient info, today's appointment, queue status, finance snapshot,
    /// ortho case, medical alerts, recent visits, and timeline events.</summary>
    [HttpGet("{patientId:guid}/daily-summary")]
    public async Task<IActionResult> GetDailySummary(Guid patientId)
        => await journeyService.GetDailySummaryAsync(User, patientId);

    // ─── 2. POST /api/patient-journey/{appointmentId}/intake ────────────────
    /// <summary>Reception confirms patient arrival and records intake info.</summary>
    /// <remarks>
    /// Sprint 2 FIX: Added transaction + advisory lock to prevent race condition
    /// that could allow double-intake when concurrent requests hit this endpoint.
    /// Pattern matches Sprint 1 SendToQueue/StartVisit fixes.
    /// </remarks>
    [HttpPost("{appointmentId:guid}/intake")]
    [Authorize(Policy = "AdminOrReception")]
    public async Task<IActionResult> Intake(Guid appointmentId, [FromBody] IntakeRequest req)
        => await checkoutService.IntakeAsync(appointmentId, req);

    // ─── 3. POST /api/patient-journey/{appointmentId}/send-to-queue ─────────
    /// <summary>Create or reuse queue item for the appointment.</summary>
    /// <remarks>
    /// Sprint 1 FIX: Added transaction + advisory lock to prevent race condition
    /// that could create duplicate queue items when concurrent requests hit this endpoint.
    /// Pattern matches ClinicQueueController.AddToQueue.
    /// </remarks>
    [HttpPost("{appointmentId:guid}/send-to-queue")]
    [Authorize(Policy = "AdminOrReception")]
    public async Task<IActionResult> SendToQueue(Guid appointmentId, [FromBody] SendToQueueRequest? req = null)
        => await checkoutService.SendToQueueAsync(appointmentId, req);

    // ─── 4. POST /api/patient-journey/{appointmentId}/start-visit ───────────
    /// <summary>Doctor starts the visit. Reuses existing visit/queue logic.</summary>
    /// <remarks>
    /// Sprint 1 FIX: Added transaction + advisory lock to prevent race condition
    /// that could create duplicate visits when concurrent requests hit this endpoint.
    /// Pattern matches ClinicQueueController.StartVisit.
    /// </remarks>
    [HttpPost("{appointmentId:guid}/start-visit")]
    public async Task<IActionResult> StartVisit(Guid appointmentId, [FromBody] StartVisitRequest? req = null)
        => await checkoutService.StartVisitAsync(appointmentId, req);

    // ─── 5. POST /api/patient-journey/{visitId}/handoff-to-reception ────────
    /// <summary>Doctor finishes and sends patient to reception for checkout.</summary>
    /// <remarks>
    /// Sprint 2 FIX: Added transaction + advisory lock to prevent race condition
    /// that could allow double-handoff when concurrent requests hit this endpoint.
    /// Also restricted to Doctor+Admin only (was StaffOnly) and added InProgress validation.
    /// </remarks>
    [HttpPost("{visitId:guid}/handoff-to-reception")]
    [Authorize(Policy = "DoctorAccess")]
    public async Task<IActionResult> HandoffToReception(Guid visitId, [FromBody] HandoffRequest req)
        => await checkoutService.HandoffToReceptionAsync(visitId, req);

    // ─── 6. POST /api/patient-journey/{id}/checkout ──────────────
    /// <summary>Complete checkout by appointmentId or visitId.
    /// First attempts to find by appointmentId; if not found, tries by visitId.
    /// This supports both appointment-based and walk-in patients.</summary>
    /// <remarks>
    /// Sprint 2 FIX: Added transaction + advisory lock to prevent race condition
    /// that could allow double-checkout when concurrent requests hit this endpoint.
    /// Pattern matches Sprint 1 SendToQueue/StartVisit and Sprint 2 Intake/Handoff fixes.
    /// </remarks>
    [HttpPost("{id:guid}/checkout")]
    [Authorize(Policy = "AdminOrReception")]
    public async Task<IActionResult> Checkout(Guid id, [FromBody] CheckoutRequest? req)
        => await checkoutService.CheckoutAsync(id, req);

    // ─── 7. POST /api/patient-journey/{visitId}/left-without-completion ─────
    /// <summary>Marks a visit as an explicit terminal operational state when
    /// the patient leaves after arrival without completing care. This does not
    /// create payments, alter invoices, or change financial balances.</summary>
    [HttpPost("{visitId:guid}/left-without-completion")]
    [Authorize(Policy = "AdminOrReception")]
    public async Task<IActionResult> MarkLeftWithoutCompletion(Guid visitId, [FromBody] LeftWithoutCompletionRequest req)
        => await checkoutService.MarkLeftWithoutCompletionAsync(visitId, req);

    /// <summary>Creates a Draft Invoice from a visit that is ready for checkout.
    /// Uses Visit.AmountDueReference and linked ServiceId for line item pricing.
    /// Does NOT create a Payment. Does NOT alter Contract or Patient balance.
    /// If a Draft invoice already exists for this Visit, returns the existing one.
    /// Uses transaction + advisory lock + local GenerateInvoiceNumberAsync
    /// + commissionService.AutoFillFromServiceAsync to match main branch safe behavior.
    /// Re-checks for duplicate draft inside the transaction to prevent race conditions.</summary>
    [HttpPost("{visitId:guid}/create-draft-invoice")]
    [Authorize(Policy = "FinanceAccess")]
    public async Task<IActionResult> CreateDraftInvoice(Guid visitId)
        => await checkoutService.CreateDraftInvoiceAsync(visitId);

    // ─── 8. POST /api/patient-journey/{patientId}/validate-financial-closure ─
    /// <summary>
    /// Validates whether a visit can be financially closed.
    /// Checks outstanding balance and treatment plan status.
    /// </summary>
    [HttpPost("{patientId:guid}/validate-financial-closure")]
    [Authorize(Policy = "FinanceAccess")]
    public async Task<IActionResult> ValidateFinancialClosure(
        Guid patientId,
        [FromBody] ValidateFinancialClosureRequest req)
        => await checkoutService.ValidateFinancialClosureAsync(patientId, req);
}
