using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json;

namespace AqlanDentalPro.API.Controllers;

/// <summary>
/// Patient Journey Command Center — unified operational screen for reception
/// and clinical workflow: Appointment → Arrived → Queue → Visit → Checkout.
/// </summary>
[ApiController]
[Route("api/patient-journey")]
[Authorize(Policy = "StaffOnly")]
public class PatientJourneyController(
    AppDbContext db,
    ILogger<PatientJourneyController> logger,
    ICommissionService commissionService,
    IFinanceService financeService,
    IPatientAccessService patientAccessService) : ControllerBase
{
    // ─── 1. GET /api/patient-journey/today ────────────────────────────────────
    /// <summary>Returns today's patient journey list combining appointments,
    /// queue status, visit data, and payment info.</summary>
    [HttpGet("today")]
    public async Task<IActionResult> GetToday([FromQuery] string? date, [FromQuery] string? status,
        [FromQuery] Guid? doctorId, [FromQuery] Guid? serviceId, [FromQuery] Guid? roomId)
    {
        // Parse date - default to today (declared before try so catch can reference it)
        DateOnly queryDate = DateOnly.FromDateTime(DateTime.UtcNow);
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

        // Build base query for today's appointments
        // Also include past-dated appointments that have an active ClinicQueueItem for today,
        // so patients who were transferred to today's queue (even from a past appointment) appear.
        var todayQueueAppointmentIds = await db.ClinicQueueItems
            .IgnoreQueryFilters()
            .Where(q => q.QueueDate == queryDate && q.IsActive && q.AppointmentId != null)
            .Select(q => q.AppointmentId!.Value)
            .Distinct()
            .ToListAsync();

        var query = db.Appointments
            .IgnoreQueryFilters()
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Where(a => a.IsActive && (
                a.AppointmentDate == queryDate ||
                todayQueueAppointmentIds.Contains(a.Id)
            ))
            .AsQueryable();

        // Doctor access control: only show appointments for patients they can access
        if (patientAccessService.IsDoctor)
        {
            var accessibleIds = await patientAccessService.GetAccessiblePatientIdsAsync();
            if (accessibleIds != null)
                query = query.Where(a => accessibleIds.Contains(a.PatientId));
        }

        // Apply filters
        if (statusFilter.HasValue)
            query = query.Where(a => a.Status == statusFilter.Value);
        if (doctorId.HasValue)
            query = query.Where(a => a.DoctorId == doctorId.Value);
        if (serviceId.HasValue)
            query = query.Where(a => a.ServiceId == serviceId.Value);
        if (roomId.HasValue)
            query = query.Where(a => a.ClinicRoomId == roomId.Value);

        var appointments = await query
            .OrderBy(a => a.StartTime)
            .ToListAsync();

        // No appointments for today — return empty list immediately
        if (appointments.Count == 0)
            return Ok(new List<object>());

        // Load related queue items for today
        // For past-dated appointments, include queue items from any date (they were transferred today)
        var appointmentIds = appointments.Select(a => a.Id).ToList();
        var pastAppointmentIds = appointments
            .Where(a => a.AppointmentDate != queryDate)
            .Select(a => a.Id)
            .ToHashSet();
        var queueItemsList = await db.ClinicQueueItems
            .IgnoreQueryFilters()
            .Where(q => q.AppointmentId != null && appointmentIds.Contains(q.AppointmentId.Value) && q.IsActive &&
                (q.QueueDate == queryDate || pastAppointmentIds.Contains(q.AppointmentId.Value)))
            .OrderByDescending(q => q.UpdatedAt)
            .ThenByDescending(q => q.CreatedAt)
            .ToListAsync();

        var queueItems = queueItemsList
            .GroupBy(q => q.AppointmentId!.Value)
            .ToDictionary(g => g.Key, g => g.First()); // Handle duplicates safely

        // Load visits for these appointments
        var visitsList = await db.Visits
            .IgnoreQueryFilters()
            .Where(v => v.AppointmentId != null && appointmentIds.Contains(v.AppointmentId.Value) && v.IsActive)
            .OrderByDescending(v => v.UpdatedAt)
            .ThenByDescending(v => v.CreatedAt)
            .ToListAsync();

        var visits = visitsList
            .GroupBy(v => v.AppointmentId!.Value)
            .ToDictionary(g => g.Key, g => g.First()); // Handle duplicates safely

        var visitIds = visitsList.Select(v => v.Id).ToList();

        var labOrdersList = visitIds.Count > 0
            ? await db.LabOrders
                .IgnoreQueryFilters()
                .Where(l => l.VisitId.HasValue && visitIds.Contains(l.VisitId.Value) && l.IsActive)
                .OrderByDescending(l => l.UpdatedAt)
                .ThenByDescending(l => l.CreatedAt)
                .ToListAsync()
            : [];

        var labOrdersByVisit = labOrdersList
            .GroupBy(l => l.VisitId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        // FIX: Invoice query with fallback — some environments may have stale schema
        // where Invoice.IsActive column is missing or AppointmentId/VisitId columns
        // are not yet migrated. Use try/catch per-query to avoid crashing the entire
        // patient-journey/today endpoint.
        List<dynamic> invoiceRefsRaw = [];
        HashSet<Guid> draftInvoiceAppointmentIds = [];
        HashSet<Guid> draftInvoiceVisitIds = [];
        try
        {
            var invoiceRefs = await db.Invoices
                .IgnoreQueryFilters()
                .Where(i => i.IsActive
                    && ((i.AppointmentId.HasValue && appointmentIds.Contains(i.AppointmentId.Value))
                        || (i.VisitId.HasValue && visitIds.Contains(i.VisitId.Value))))
                .Select(i => new { i.AppointmentId, i.VisitId, i.Status })
                .ToListAsync();

            draftInvoiceAppointmentIds = invoiceRefs
                .Where(i => i.Status == InvoiceStatus.Draft && i.AppointmentId.HasValue)
                .Select(i => i.AppointmentId!.Value)
                .ToHashSet();

            draftInvoiceVisitIds = invoiceRefs
                .Where(i => i.Status == InvoiceStatus.Draft && i.VisitId.HasValue)
                .Select(i => i.VisitId!.Value)
                .ToHashSet();
        }
        catch (Exception invEx)
        {
            logger.LogWarning(invEx, "PatientJourney.GetToday: Invoice query failed (likely missing columns), skipping invoice refs");
        }

        // Load service info for appointments that have ServiceId
        var serviceIds = appointments.Where(a => a.ServiceId.HasValue).Select(a => a.ServiceId!.Value).Distinct().ToList();
        var services = serviceIds.Count > 0
            ? await db.ClinicServices.IgnoreQueryFilters().Where(s => serviceIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id)
            : new Dictionary<Guid, ClinicService>();

        // Check consultation fee payment status for today
        var patientIds = appointments.Select(a => a.PatientId).Distinct().ToList();
        var todayPaymentsList = patientIds.Count > 0
            ? await db.Payments
                .Where(p => patientIds.Contains(p.PatientId) && p.PaymentDate == queryDate && p.IsActive)
                .ToListAsync()
            : [];

        var todayPayments = todayPaymentsList
            .GroupBy(p => p.PatientId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

        // Privacy: Doctors must not see patient phone numbers
        var isDoctor = patientAccessService.IsDoctor;

        // Build journey items
        var result = appointments.Select(a =>
        {
            queueItems.TryGetValue(a.Id, out var queueItem);
            visits.TryGetValue(a.Id, out var visit);
            var service = a.ServiceId.HasValue && services.TryGetValue(a.ServiceId.Value, out var s) ? s : null;
            var labOrder = visit != null && labOrdersByVisit.TryGetValue(visit.Id, out var lo) ? lo : null;
            var hasDraftInvoice = draftInvoiceAppointmentIds.Contains(a.Id)
                || (visit != null && draftInvoiceVisitIds.Contains(visit.Id));

            var consultationFeeRequired = service?.RequiresConsultationFee ?? false;
            var consultationFeePaid = false;
            if (consultationFeeRequired && todayPayments.TryGetValue(a.PatientId, out var paidAmount))
            {
                consultationFeePaid = paidAmount >= (service?.DefaultPrice ?? 0);
            }
            var isEmergencyVisit = IsEmergencyAppointmentType(a.AppointmentType);
            var paymentBeforeEntryRequired = consultationFeeRequired && !consultationFeePaid && !isEmergencyVisit;
            var financialEntryStatus = paymentBeforeEntryRequired ? "WaitingForPayment" : "Clear";
            var financialEntryReason = paymentBeforeEntryRequired
                ? "يتطلب هذا النوع من الزيارة دفع رسوم الكشف قبل الدخول"
                : isEmergencyVisit
                    ? "حالة إسعافية — يسمح بالدخول حسب أولوية الحالة"
                    : null;

            string? checkoutStatus = visit?.CheckoutStatus;
            string nextAction = DetermineNextAction(a.Status, queueItem?.Status, checkoutStatus);

            // Compute in-room timestamp from queue item
            DateTime? inRoomSince = queueItem?.InRoomAt ?? queueItem?.StartedAt;

            return new
            {
                AppointmentId = (Guid?)a.Id,
                PatientId = a.PatientId,
                PatientName = BuildPatientDisplayName(a.Patient),
                PatientNumber = a.Patient?.PatientNumber,
                PatientPhone = isDoctor ? null : a.Patient?.Phone,
                AppointmentTime = a.StartTime.ToString("HH:mm"),
                AppointmentType = a.AppointmentType,
                AppointmentStatus = a.Status.ToString(),
                DoctorId = (Guid?)a.DoctorId,
                DoctorName = a.Doctor?.Name ?? "",
                ServiceId = a.ServiceId,
                ServiceName = service?.ArabicName,
                RoomId = a.ClinicRoomId ?? queueItem?.ClinicRoomId,
                RoomName = a.RoomName ?? queueItem?.RoomName,
                QueueItemId = (Guid?)queueItem?.Id,
                QueueStatus = queueItem?.Status.ToString(),
                VisitId = visit?.Id,
                VisitStatus = visit != null ? (checkoutStatus ?? "InProgress") : null,
                ConsultationFeeRequired = consultationFeeRequired,
                ConsultationFeePaid = consultationFeePaid,
                PaymentBeforeEntryRequired = paymentBeforeEntryRequired,
                FinancialEntryStatus = financialEntryStatus,
                FinancialEntryReason = financialEntryReason,
                CanEnterWithoutPayment = !paymentBeforeEntryRequired,
                ManagerOverrideAllowed = paymentBeforeEntryRequired,
                CheckoutStatus = checkoutStatus,
                AmountDueReference = visit?.AmountDueReference,
                TreatmentDone = visit?.TreatmentDone,
                ProposedProcedure = visit?.ProposedProcedure,
                ChiefComplaint = visit?.ChiefComplaint,
                HasDraftInvoice = hasDraftInvoice,
                HasLabOrder = labOrder != null,
                LabOrderStatus = labOrder?.Status,
                InRoomSince = inRoomSince,
                NextAction = nextAction
            };
        }).ToList();

        // Also include walk-in patients: ClinicQueueItems for today with no appointment
        var walkInQueueItems = await db.ClinicQueueItems
            .IgnoreQueryFilters()
            .Include(q => q.Patient)
            .Include(q => q.Doctor)
            .Where(q => q.QueueDate == queryDate && q.IsActive && q.AppointmentId == null)
            .ToListAsync();

        if (walkInQueueItems.Count > 0)
        {
            // Doctor access control for walk-ins
            if (patientAccessService.IsDoctor)
            {
                var accessibleIds = await patientAccessService.GetAccessiblePatientIdsAsync();
                if (accessibleIds != null)
                    walkInQueueItems = walkInQueueItems.Where(q => accessibleIds.Contains(q.PatientId)).ToList();
            }

            // Load visits for walk-in queue items (linked via VisitId)
            var walkInVisitIds = walkInQueueItems
                .Where(q => q.VisitId.HasValue)
                .Select(q => q.VisitId!.Value)
                .ToList();
            var walkInVisits = walkInVisitIds.Count > 0
                ? await db.Visits.IgnoreQueryFilters()
                    .Where(v => walkInVisitIds.Contains(v.Id) && v.IsActive)
                    .ToDictionaryAsync(v => v.Id)
                : new Dictionary<Guid, Visit>();

            // Load payments for walk-in patients
            var walkInPatientIds = walkInQueueItems.Select(q => q.PatientId).Distinct().ToList();
            var walkInPayments = walkInPatientIds.Count > 0
                ? (await db.Payments
                    .Where(p => walkInPatientIds.Contains(p.PatientId) && p.PaymentDate == queryDate && p.IsActive)
                    .ToListAsync())
                    .GroupBy(p => p.PatientId)
                    .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount))
                : new Dictionary<Guid, decimal>();

            foreach (var q in walkInQueueItems)
            {
                var patientName = BuildPatientDisplayName(q.Patient);
                var queueStatus = q.Status;
                Visit? visit = q.VisitId.HasValue && walkInVisits.TryGetValue(q.VisitId.Value, out var v) ? v : null;
                string? checkoutStatus = visit?.CheckoutStatus;
                string nextAction = DetermineNextAction(
                    queueStatus == ClinicQueueStatus.Waiting ? AppointmentStatus.Waiting :
                    queueStatus == ClinicQueueStatus.Called ? AppointmentStatus.Called :
                    queueStatus == ClinicQueueStatus.InRoom ? AppointmentStatus.InRoom :
                    queueStatus == ClinicQueueStatus.InProgress ? AppointmentStatus.InProgress :
                    AppointmentStatus.Completed,
                    queueStatus, checkoutStatus);

                result.Add(new
                {
                    AppointmentId = (Guid?)null,
                    PatientId = q.PatientId,
                    PatientName = patientName,
                    PatientNumber = q.Patient?.PatientNumber,
                    PatientPhone = isDoctor ? null : q.Patient?.Phone,
                    AppointmentTime = (string?)null,
                    AppointmentType = (string?)null,
                    AppointmentStatus = nextAction == "None" ? "Completed" : queueStatus.ToString(),
                    DoctorId = q.DoctorId,
                    DoctorName = q.Doctor?.Name ?? "",
                    ServiceId = (Guid?)q.ServiceId,
                    ServiceName = (string?)null,
                    RoomId = (Guid?)q.ClinicRoomId,
                    RoomName = q.RoomName,
                    QueueItemId = (Guid?)q.Id,
                    QueueStatus = queueStatus.ToString(),
                    VisitId = visit?.Id,
                    VisitStatus = visit != null ? (checkoutStatus ?? "InProgress") : null,
                    ConsultationFeeRequired = false,
                    ConsultationFeePaid = false,
                    PaymentBeforeEntryRequired = false,
                    FinancialEntryStatus = "Clear",
                    FinancialEntryReason = (string?)null,
                    CanEnterWithoutPayment = true,
                    ManagerOverrideAllowed = false,
                    CheckoutStatus = checkoutStatus,
                    AmountDueReference = visit?.AmountDueReference,
                    TreatmentDone = visit?.TreatmentDone,
                    ProposedProcedure = visit?.ProposedProcedure,
                    ChiefComplaint = visit?.ChiefComplaint,
                    HasDraftInvoice = false,
                    HasLabOrder = false,
                    LabOrderStatus = (string?)null,
                    InRoomSince = q.InRoomAt ?? q.StartedAt,
                    NextAction = nextAction
                });
            }
        }

        return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PatientJourney.GetToday failed for date {Date}", queryDate);
            return StatusCode(500, new { message = "حدث خطأ أثناء تحميل رحلة المرضى" });
        }
    }

    // ─── 1B. GET /api/patient-journey/{patientId}/daily-summary ───────────
    /// <summary>Returns a comprehensive daily journey summary for a specific patient,
    /// aggregating patient info, today's appointment, queue status, finance snapshot,
    /// ortho case, medical alerts, recent visits, and timeline events.</summary>
    [HttpGet("{patientId:guid}/daily-summary")]
    public async Task<IActionResult> GetDailySummary(Guid patientId)
    {
        try
        {
            // ── Doctor access enforcement ──
            // Doctors can only access patients they are assigned to.
            // Admin, Reception, Accountant have full access per existing policies.
            if (patientAccessService.IsDoctor)
            {
                var canAccess = await patientAccessService.CanAccessPatientAsync(patientId);
                if (!canAccess)
                    return StatusCode(403, new { message = "ليس لديك صلاحية الوصول لبيانات هذا المريض" });
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // 1. Patient basic info
            var patient = await db.Patients
                .IgnoreQueryFilters()
                .Where(p => p.Id == patientId && p.IsActive)
                .Select(p => new
                {
                    p.Id,
                    p.PatientNumber,
                    p.FirstName,
                    p.MiddleName,
                    p.LastName,
                    p.Phone,
                    p.Email,
                    p.Gender,
                    p.DateOfBirth,
                    p.BranchId,
                    p.PrimaryDoctorId
                })
                .FirstOrDefaultAsync();

            if (patient == null)
                return NotFound(new { message = "المريض غير موجود" });

            var nameParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(patient.FirstName)) nameParts.Add(patient.FirstName.Trim());
            if (!string.IsNullOrWhiteSpace(patient.MiddleName)) nameParts.Add(patient.MiddleName.Trim());
            if (!string.IsNullOrWhiteSpace(patient.LastName)) nameParts.Add(patient.LastName.Trim());
            var patientName = string.Join(" ", nameParts);

            int? age = null;
            if (patient.DateOfBirth.HasValue)
                age = today.Year - patient.DateOfBirth.Value.Year -
                    (today.DayOfYear < patient.DateOfBirth.Value.DayOfYear ? 1 : 0);

            // 2. Today's appointment
            var todayAppt = await db.Appointments
                .IgnoreQueryFilters()
                .Include(a => a.Doctor)
                .Where(a => a.PatientId == patientId && a.AppointmentDate == today && a.IsActive)
                .OrderBy(a => a.StartTime)
                .Select(a => new
                {
                    a.Id,
                    a.AppointmentDate,
                    a.StartTime,
                    a.EndTime,
                    a.AppointmentType,
                    a.Status,
                    a.DoctorId,
                    DoctorName = a.Doctor != null ? a.Doctor.Name : "",
                    a.ServiceId,
                    a.RoomName,
                    a.Specialty,
                    a.ArrivedAt,
                    a.CalledAt,
                    a.InRoomAt,
                    a.Notes
                })
                .FirstOrDefaultAsync();

            // 3. Queue status for today
            var queueItem = await db.ClinicQueueItems
                .IgnoreQueryFilters()
                .Where(q => q.PatientId == patientId && q.QueueDate == today
                    && q.Status != ClinicQueueStatus.Completed
                    && q.Status != ClinicQueueStatus.Cancelled
                    && q.IsActive)
                .OrderByDescending(q => q.CreatedAt)
                .Select(q => new
                {
                    q.Id,
                    q.Status,
                    q.RoomName,
                    q.CalledAt,
                    q.InRoomAt,
                    q.StartedAt,
                    q.DoctorId,
                    q.ServiceId
                })
                .FirstOrDefaultAsync();

            // 4. Today's visit
            var todayVisit = await db.Visits
                .IgnoreQueryFilters()
                .Where(v => v.PatientId == patientId && v.VisitDate == today && v.IsActive)
                .OrderByDescending(v => v.CreatedAt)
                .Select(v => new
                {
                    v.Id,
                    v.VisitType,
                    v.Specialty,
                    v.DoctorId,
                    v.ChiefComplaint,
                    v.ClinicalNotes,
                    v.TreatmentDone,
                    v.Diagnosis,
                    v.Instructions,
                    v.NextVisitPlan,
                    v.Cost,
                    v.NextVisitDate,
                    v.CheckoutStatus,
                    v.ReadyForCheckoutAt,
                    v.AmountDueReference,
                    v.AppointmentId
                })
                .FirstOrDefaultAsync();

            // FIX-2: Finance access tiers
            // Full:    Admin + Accountant (via FinanceAccess policy / finance.view permission)
            // Limited: Reception (daily checkout only — outstandingBalance, overdueAmount, latestPayment, financialStatus)
            // None:    Doctors
            var isAdminOrAccountant = User.IsInRole("Admin") || User.IsInRole("Accountant") ||
                User.HasClaim("permission", "finance.view");
            var isReception = User.IsInRole("Reception");
            var hasFullFinanceAccess = isAdminOrAccountant;
            var hasLimitedFinanceAccess = isReception;
            var hasAnyFinanceAccess = hasFullFinanceAccess || hasLimitedFinanceAccess;

            object? financeSummary = null;
            int unpaidInvoicesCount = 0;
            object? activeContract = null;

            if (hasAnyFinanceAccess)
            {
                // FIX-3: Use central FinanceService.GetPatientFinanceSummaryAsync()
                // instead of duplicating financial calculations here.
                var centralSummary = await financeService.GetPatientFinanceSummaryAsync(patientId);

                if (hasFullFinanceAccess)
                {
                    // Full finance summary for Admin/Accountant
                    financeSummary = new
                    {
                        TotalTreatmentCost = centralSummary.TotalTreatmentCost,
                        TotalPaid = centralSummary.TotalPaid,
                        OutstandingBalance = centralSummary.OutstandingBalance,
                        OverdueAmount = centralSummary.OverdueAmount,
                        LatestPayment = centralSummary.LatestPayment != null ? new
                        {
                            centralSummary.LatestPayment.Id,
                            centralSummary.LatestPayment.Amount,
                            centralSummary.LatestPayment.PaymentDate,
                            centralSummary.LatestPayment.PaymentMethod,
                            centralSummary.LatestPayment.ReceiptNumber
                        } : null,
                        FinancialStatus = centralSummary.FinancialStatus,
                        ActiveContractsCount = centralSummary.ActiveContractsCount,
                        TotalPaymentsCount = centralSummary.TotalPaymentsCount
                    };
                }
                else
                {
                    // FIX-2: Limited finance summary for Reception (daily checkout only)
                    financeSummary = new
                    {
                        OutstandingBalance = centralSummary.OutstandingBalance,
                        OverdueAmount = centralSummary.OverdueAmount,
                        LatestPayment = centralSummary.LatestPayment != null ? new
                        {
                            centralSummary.LatestPayment.Id,
                            centralSummary.LatestPayment.Amount,
                            centralSummary.LatestPayment.PaymentDate,
                            centralSummary.LatestPayment.PaymentMethod,
                            centralSummary.LatestPayment.ReceiptNumber
                        } : null,
                        FinancialStatus = centralSummary.FinancialStatus
                    };
                }

                // Unpaid invoices count (both full and limited access need this for checkout)
                unpaidInvoicesCount = await db.Invoices
                    .CountAsync(i => i.PatientId == patientId &&
                        (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.Draft) && i.IsActive);

                // Active contract short info — compute PaidAmount/RemainingAmount from Payments
                // since Contract entity does not store these directly.
                var firstContractEntity = await db.Contracts
                    .IgnoreQueryFilters()
                    .Include(c => c.Payments)
                    .Where(c => c.PatientId == patientId && c.Status == ContractStatus.Active && c.IsActive)
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync();

                if (firstContractEntity != null)
                {
                    var contractPaid = firstContractEntity.Payments
                        .Where(p => p.IsActive)
                        .Sum(p => p.Amount);
                    var contractNetTotal = firstContractEntity.TotalAmount - firstContractEntity.DiscountAmount;
                    activeContract = new
                    {
                        firstContractEntity.Id,
                        firstContractEntity.TotalAmount,
                        PaidAmount = contractPaid,
                        RemainingAmount = contractNetTotal - contractPaid,
                        firstContractEntity.InstallmentAmount,
                        firstContractEntity.InstallmentsCount,
                        firstContractEntity.Specialty,
                        firstContractEntity.StartDate,
                        firstContractEntity.Status
                    };
                }
            }

            // 6. Active ortho case — use actual OrthoCase entity fields only
            var activeOrthoCase = await db.OrthoCases
                .IgnoreQueryFilters()
                .Where(o => o.PatientId == patientId && o.IsActive)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new
                {
                    o.Id,
                    o.CaseNumber,
                    o.Status,
                    ApplianceType = o.ApplianceType,       // replaces CaseType (does not exist)
                    o.StartDate,
                    ExpectedDurationMonths = o.ExpectedDurationMonths, // replaces EstimatedEndDate
                    CurrentStage = o.CurrentStage,          // clinical info instead of Notes
                    o.DoctorId,
                    o.TotalFee,
                    StagePercentage = o.StagePercentage
                })
                .FirstOrDefaultAsync();

            // 7. Medical alerts
            var medicalHistory = await db.MedicalHistories
                .IgnoreQueryFilters()
                .Where(m => m.PatientId == patientId && m.IsActive)
                .Select(m => new
                {
                    m.ChronicDiseases,
                    m.CurrentMedications,
                    m.DrugAllergies,
                    m.BleedingDisorders,
                    m.IsPregnant,
                    m.TmjProblems,
                    m.PreviousSurgeries,
                    m.Notes
                })
                .FirstOrDefaultAsync();

            var medicalAlerts = new List<object>();
            if (medicalHistory != null)
            {
                if (!string.IsNullOrWhiteSpace(medicalHistory.DrugAllergies))
                    medicalAlerts.Add(new { Type = "allergy", Label = "حساسية دوائية", Value = medicalHistory.DrugAllergies, Severity = "danger" });
                if (medicalHistory.BleedingDisorders)
                    medicalAlerts.Add(new { Type = "bleeding", Label = "اضطرابات نزيف", Value = "نعم", Severity = "danger" });
                if (!string.IsNullOrWhiteSpace(medicalHistory.ChronicDiseases) && medicalHistory.ChronicDiseases != "لا يوجد" && medicalHistory.ChronicDiseases != "لا")
                    medicalAlerts.Add(new { Type = "chronic", Label = "أمراض مزمنة", Value = medicalHistory.ChronicDiseases, Severity = "warning" });
                if (!string.IsNullOrWhiteSpace(medicalHistory.CurrentMedications) && medicalHistory.CurrentMedications != "لا يوجد" && medicalHistory.CurrentMedications != "لا")
                    medicalAlerts.Add(new { Type = "medication", Label = "أدوية حالية", Value = medicalHistory.CurrentMedications, Severity = "info" });
                if (medicalHistory.IsPregnant == "نعم" || medicalHistory.IsPregnant == "yes")
                    medicalAlerts.Add(new { Type = "pregnancy", Label = "حمل", Value = "نعم", Severity = "warning" });
            }

            // 8. Recent visits (last 5)
            var recentVisits = await db.Visits
                .IgnoreQueryFilters()
                .Where(v => v.PatientId == patientId && v.IsActive)
                .OrderByDescending(v => v.VisitDate)
                .ThenByDescending(v => v.CreatedAt)
                .Take(5)
                .Select(v => new
                {
                    v.Id,
                    v.VisitDate,
                    v.VisitType,
                    v.ChiefComplaint,
                    v.TreatmentDone,
                    v.Diagnosis,
                    v.DoctorId,
                    v.Cost
                })
                .ToListAsync();

            // 9. Timeline events (appointments + payments if finance access, last 20)
            var appointmentEvents = await db.Appointments
                .IgnoreQueryFilters()
                .Where(a => a.PatientId == patientId && a.IsActive)
                .OrderByDescending(a => a.AppointmentDate)
                .Take(10)
                .Select(a => new { Date = a.AppointmentDate.ToString(), Type = "appointment", Title = a.AppointmentType ?? "موعد", Sub = a.Doctor != null ? a.Doctor.Name : "", Status = a.Status.ToString() })
                .Cast<object>()
                .ToListAsync();

            if (hasAnyFinanceAccess)
            {
                var pEvents = await db.Payments
                    .Where(p => p.PatientId == patientId && p.IsActive)
                    .OrderByDescending(p => p.PaymentDate)
                    .Take(10)
                    .Select(p => new { Date = p.PaymentDate.ToString(), Type = "payment", Title = "دفعة مالية", Sub = p.Amount.ToString("N0") + " ر.ي", Status = "" })
                    .Cast<object>()
                    .ToListAsync();
                appointmentEvents.AddRange(pEvents);
            }

            var timeline = appointmentEvents
                .OrderByDescending(e => ((dynamic)e).Date)
                .Take(20)
                .ToList();

            // 10. Determine journey step
            string journeyStep = "none";
            string nextAction = "None";

            if (todayAppt != null)
            {
                journeyStep = todayAppt.Status.ToString();
                nextAction = DetermineNextAction(todayAppt.Status, queueItem?.Status, todayVisit?.CheckoutStatus);
            }

            // Blocker-5: Remove messaging/SMS — conversationIds query removed from this PR.
            // Messaging improvements must be a separate focused sprint later.

            // Fix-3: Clinical privacy — shape response by role on the backend.
            // Do not rely only on frontend hiding. Backend must shape the response safely.
            var isDoctor = patientAccessService.IsDoctor;
            var isAccountant = User.IsInRole("Accountant") && !User.IsInRole("Admin");
            var isAdmin = User.IsInRole("Admin");
            // isReception already declared above (line ~305) for finance access tiers

            // ── Build role-specific response ──

            // Patient info (privacy-aware)
            var patientInfo = new
            {
                patient.Id,
                patient.PatientNumber,
                FullName = patientName,
                Phone = isDoctor ? (string?)null : patient.Phone,
                Email = isDoctor ? (string?)null : patient.Email,
                patient.Gender,
                Age = age,
                patient.BranchId,
                patient.PrimaryDoctorId
            };

            // TodayAppointment: all roles see this (operational)
            // QueueStatus: all roles see this (operational)

            // TodayVisit: role-filtered
            object? todayVisitResponse;
            if (isAccountant)
            {
                // Accountant: checkout/finance only — no clinical notes, diagnosis, instructions, nextVisitPlan
                todayVisitResponse = todayVisit != null ? new
                {
                    todayVisit.Id,
                    todayVisit.Cost,
                    todayVisit.CheckoutStatus,
                    todayVisit.ReadyForCheckoutAt,
                    todayVisit.AmountDueReference,
                    todayVisit.AppointmentId
                } : null;
            }
            else if (isDoctor)
            {
                // Doctor: clinical summary only (no finance-specific fields hidden, but financeSummary is null)
                todayVisitResponse = todayVisit != null ? new
                {
                    todayVisit.Id,
                    todayVisit.VisitType,
                    todayVisit.Specialty,
                    todayVisit.DoctorId,
                    todayVisit.ChiefComplaint,
                    todayVisit.ClinicalNotes,
                    todayVisit.TreatmentDone,
                    todayVisit.Diagnosis,
                    todayVisit.Instructions,
                    todayVisit.NextVisitPlan,
                    todayVisit.Cost,
                    todayVisit.NextVisitDate,
                    todayVisit.CheckoutStatus,
                    todayVisit.AppointmentId
                } : null;
            }
            else if (isReception)
            {
                // Fix-3: Reception — operational checkout information only.
                // Do not return ClinicalNotes, Diagnosis, Instructions, NextVisitPlan.
                todayVisitResponse = todayVisit != null ? new
                {
                    todayVisit.Id,
                    todayVisit.VisitType,
                    todayVisit.Specialty,
                    todayVisit.DoctorId,
                    todayVisit.ChiefComplaint,
                    todayVisit.Cost,
                    todayVisit.CheckoutStatus,
                    todayVisit.ReadyForCheckoutAt,
                    todayVisit.AmountDueReference,
                    todayVisit.NextVisitDate,
                    todayVisit.AppointmentId
                } : null;
            }
            else
            {
                // Admin: full visit info
                todayVisitResponse = todayVisit;
            }

            // FinanceSummary: already filtered (null for doctors, limited for reception, full for admin/accountant)

            // ActiveContract: only for roles with full finance access
            object? activeContractResponse = hasFullFinanceAccess ? activeContract : null;

            // ActiveOrthoCase: clinical data — accountants and reception must NOT see ortho clinical data
            object? orthoCaseResponse;
            if (isAccountant || isReception)
            {
                // Accountant: no ortho clinical data
                // Reception: no ortho clinical data unless explicit permission
                // Fix-4: Use canonical permission key patient_journey.view (not patientJourney.view)
                orthoCaseResponse = User.HasClaim("permission", "patient_journey.view") ? activeOrthoCase : null;
            }
            else if (isDoctor || isAdmin)
            {
                orthoCaseResponse = activeOrthoCase;
            }
            else
            {
                orthoCaseResponse = null;
            }

            // MedicalAlerts: role-filtered
            object? medicalAlertsResponse;
            if (isAccountant)
            {
                // Accountant: no medical alerts / clinical data
                medicalAlertsResponse = null;
            }
            else if (isReception)
            {
                // Fix-3: Reception — safety alerts only (allergy, bleeding, pregnancy).
                // Do not return chronic diseases or current medications by default.
                var safetyOnly = medicalAlerts
                    .Where(a => ((dynamic)a).Type == "allergy" || ((dynamic)a).Type == "bleeding" || ((dynamic)a).Type == "pregnancy")
                    .ToList();
                medicalAlertsResponse = safetyOnly;
            }
            else
            {
                // Doctor / Admin: full medical alerts
                medicalAlertsResponse = medicalAlerts;
            }

            // RecentVisits: role-filtered
            object? recentVisitsResponse;
            if (isAccountant)
            {
                // Accountant: only cost info, no clinical details
                recentVisitsResponse = recentVisits.Select(v => new
                {
                    v.Id,
                    v.VisitDate,
                    v.Cost,
                    v.DoctorId
                }).ToList();
            }
            else if (isReception)
            {
                // Fix-3: Reception — operational visit info, no Diagnosis/ClinicalNotes
                recentVisitsResponse = recentVisits.Select(v => new
                {
                    v.Id,
                    v.VisitDate,
                    v.VisitType,
                    v.ChiefComplaint,
                    v.TreatmentDone,
                    v.Cost,
                    v.DoctorId
                }).ToList();
            }
            else
            {
                recentVisitsResponse = recentVisits;
            }

            // Timeline: already filtered (payment events only for finance roles)

            // Build final response
            return Ok(new
            {
                Patient = patientInfo,
                TodayAppointment = todayAppt,
                QueueStatus = queueItem,
                TodayVisit = todayVisitResponse,
                FinanceSummary = financeSummary,
                UnpaidInvoicesCount = unpaidInvoicesCount,
                ActiveContract = activeContractResponse,
                ActiveOrthoCase = orthoCaseResponse,
                MedicalAlerts = medicalAlertsResponse,
                RecentVisits = recentVisitsResponse,
                Timeline = timeline,
                JourneyStep = journeyStep,
                NextAction = nextAction
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PatientJourney.GetDailySummary failed for patient {PatientId}", patientId);
            return StatusCode(500, new { message = "حدث خطأ أثناء تحميل ملخص الرحلة اليومية" });
        }
    }

    // ─── 2. POST /api/patient-journey/{appointmentId}/intake ────────────────
    /// <summary>Reception confirms patient arrival and records intake info.</summary>
    [HttpPost("{appointmentId:guid}/intake")]
    [Authorize(Policy = "AdminOrReception")]
    public async Task<IActionResult> Intake(Guid appointmentId, [FromBody] IntakeRequest req)
    {
        var appointment = await db.Appointments.FindAsync(appointmentId);
        if (appointment == null)
            return NotFound(new { message = "الموعد غير موجود" });
        if (!appointment.IsActive)
            return BadRequest(new { message = "الموعد محذوف" });

        // Validate transition: Scheduled/Confirmed → Arrived
        var targetStatus = AppointmentStatus.Arrived;
        if (!AppointmentStatusTransitions.IsValidTransition(appointment.Status, targetStatus))
            return BadRequest(new { message = $"لا يمكن تسجيل الوصول لموعد بحالة {appointment.Status}" });

        // Update appointment
        appointment.Status = targetStatus;
        appointment.ArrivedAt = DateTime.UtcNow;
        appointment.UpdatedAt = DateTime.UtcNow;

        // Attach service if provided
        if (req.ServiceId.HasValue)
        {
            var serviceExists = await db.ClinicServices.AnyAsync(s => s.Id == req.ServiceId.Value && s.IsActive);
            if (serviceExists)
                appointment.ServiceId = req.ServiceId.Value;
        }

        // Attach room if provided
        if (req.RoomId.HasValue)
        {
            var roomExists = await db.ClinicRooms.AnyAsync(r => r.Id == req.RoomId.Value && r.IsActive);
            if (roomExists)
            {
                appointment.ClinicRoomId = req.RoomId.Value;
                var room = await db.ClinicRooms.FindAsync(req.RoomId.Value);
                if (room != null)
                    appointment.RoomName = room.ArabicName;
            }
        }

        // Store notes
        if (!string.IsNullOrWhiteSpace(req.Notes))
            appointment.Notes = string.IsNullOrWhiteSpace(appointment.Notes)
                ? req.Notes
                : $"{appointment.Notes}\n{req.Notes}";

        await db.SaveChangesAsync();

        return Ok(new
        {
            appointment.Id,
            Status = appointment.Status.ToString(),
            appointment.ArrivedAt,
            ServiceId = appointment.ServiceId,
            message = "تم تسجيل وصول المريض بنجاح"
        });
    }

    // ─── 3. POST /api/patient-journey/{appointmentId}/send-to-queue ─────────
    /// <summary>Create or reuse queue item for the appointment.</summary>
    [HttpPost("{appointmentId:guid}/send-to-queue")]
    [Authorize(Policy = "AdminOrReception")]
    public async Task<IActionResult> SendToQueue(Guid appointmentId, [FromBody] SendToQueueRequest? req = null)
    {
        var appointment = await db.Appointments.FindAsync(appointmentId);
        if (appointment == null)
            return NotFound(new { message = "الموعد غير موجود" });
        if (!appointment.IsActive)
            return BadRequest(new { message = "الموعد محذوف" });

        // Must be Arrived or Waiting status
        if (appointment.Status != AppointmentStatus.Arrived && appointment.Status != AppointmentStatus.Waiting)
            return BadRequest(new { message = "يجب أن يكون المريض وصل أو في الانتظار قبل إضافته لقائمة الانتظار" });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Check for existing active queue item for this appointment
        var existingQueueItem = await db.ClinicQueueItems
            .AnyAsync(q => q.AppointmentId == appointmentId && q.QueueDate == today
                && q.Status != ClinicQueueStatus.Completed
                && q.Status != ClinicQueueStatus.Cancelled
                && q.IsActive);

        if (existingQueueItem)
            return Conflict(new { message = "المريض موجود بالفعل في قائمة الانتظار" });

        // Determine room name
        string? roomName = appointment.RoomName;
        if (req?.RoomId.HasValue == true)
        {
            var room = await db.ClinicRooms.FindAsync(req.RoomId.Value);
            if (room != null)
            {
                roomName = room.ArabicName;
                appointment.ClinicRoomId = room.Id;
                appointment.RoomName = roomName;
            }
        }

        // Create queue item
        var queueItem = new ClinicQueueItem
        {
            PatientId = appointment.PatientId,
            AppointmentId = appointment.Id,
            DoctorId = appointment.DoctorId,
            ServiceId = appointment.ServiceId,           // FIX: Copy ServiceId from appointment
            ClinicRoomId = appointment.ClinicRoomId,     // FIX: Copy ClinicRoomId from appointment
            RoomName = roomName,
            Status = ClinicQueueStatus.Waiting,
            QueueDate = today,
            AddedByUserId = GetCurrentUserId(),
            Notes = req?.Notes
        };

        db.ClinicQueueItems.Add(queueItem);

        // Update appointment status to Waiting
        if (AppointmentStatusTransitions.IsValidTransition(appointment.Status, AppointmentStatus.Waiting))
        {
            appointment.Status = AppointmentStatus.Waiting;
            appointment.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        return Ok(new
        {
            queueItem.Id,
            QueueStatus = queueItem.Status.ToString(),
            StatusArabic = ClinicQueueStatusTransitions.GetArabicLabel(queueItem.Status),
            message = "تمت إضافة المريض إلى قائمة الانتظار بنجاح"
        });
    }

    // ─── 4. POST /api/patient-journey/{appointmentId}/start-visit ───────────
    /// <summary>Doctor starts the visit. Reuses existing visit/queue logic.</summary>
    [HttpPost("{appointmentId:guid}/start-visit")]
    public async Task<IActionResult> StartVisit(Guid appointmentId)
    {
        var appointment = await db.Appointments.FindAsync(appointmentId);
        if (appointment == null)
            return NotFound(new { message = "الموعد غير موجود" });
        if (!appointment.IsActive)
            return BadRequest(new { message = "الموعد محذوف" });

        // Must be in InRoom or Called status
        if (appointment.Status != AppointmentStatus.InRoom && appointment.Status != AppointmentStatus.Called)
            return BadRequest(new { message = "يجب أن يكون المريض داخل الغرفة قبل بدء الزيارة" });

        // FIX: If appointment is Called, transition to InRoom first since Called→InProgress is not a valid transition.
        // This ensures the appointment status stays in sync with the queue/visit progression.
        if (appointment.Status == AppointmentStatus.Called)
        {
            if (AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Called, AppointmentStatus.InRoom))
            {
                appointment.Status = AppointmentStatus.InRoom;
                appointment.UpdatedAt = DateTime.UtcNow;
            }
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Find the queue item for this appointment
        var queueItem = await db.ClinicQueueItems
            .FirstOrDefaultAsync(q => q.AppointmentId == appointmentId && q.QueueDate == today
                && q.IsActive
                && q.Status != ClinicQueueStatus.Completed
                && q.Status != ClinicQueueStatus.Cancelled);

        if (queueItem == null)
            return BadRequest(new { message = "لا يوجد عنصر انتظار نشط لهذا الموعد" });

        // Validate queue transition to InProgress
        var validationError = ClinicQueueStatusTransitions.GetValidationError(queueItem.Status, ClinicQueueStatus.InProgress);
        if (validationError != null)
            return BadRequest(new { message = validationError });

        // Check for existing visit
        var existingVisit = await db.Visits
            .FirstOrDefaultAsync(v => v.AppointmentId == appointmentId && v.IsActive);

        if (existingVisit != null)
        {
            // Visit already exists, just update statuses
            queueItem.Status = ClinicQueueStatus.InProgress;
            queueItem.StartedAt = DateTime.UtcNow;
            queueItem.UpdatedAt = DateTime.UtcNow;

            if (AppointmentStatusTransitions.IsValidTransition(appointment.Status, AppointmentStatus.InProgress))
            {
                appointment.Status = AppointmentStatus.InProgress;
                appointment.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();

            return Ok(new
            {
                existingVisit.Id,
                QueueItemId = queueItem.Id,
                QueueStatus = queueItem.Status.ToString(),
                AppointmentStatus = appointment.Status.ToString(),
                message = "تم بدء الزيارة بنجاح (زيارة موجودة)"
            });
        }

        // Create new visit
        var visit = new Visit
        {
            PatientId = appointment.PatientId,
            AppointmentId = appointment.Id,
            VisitDate = today,
            DoctorId = appointment.DoctorId,
            Specialty = appointment.Specialty,
            ServiceId = appointment.ServiceId
        };

        db.Visits.Add(visit);

        try
        {
            await db.SaveChangesAsync(); // Save to get the visit ID
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to create visit for appointment {AppointmentId}", appointmentId);
            return StatusCode(500, new { message = "فشل إنشاء الزيارة — يرجى المحاولة مرة أخرى" });
        }

        // Update queue item
        queueItem.VisitId = visit.Id;
        queueItem.Status = ClinicQueueStatus.InProgress;
        queueItem.StartedAt = DateTime.UtcNow;
        queueItem.UpdatedAt = DateTime.UtcNow;

        // Update appointment
        if (AppointmentStatusTransitions.IsValidTransition(appointment.Status, AppointmentStatus.InProgress))
        {
            appointment.Status = AppointmentStatus.InProgress;
            appointment.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        return Ok(new
        {
            visit.Id,
            QueueItemId = queueItem.Id,
            QueueStatus = queueItem.Status.ToString(),
            AppointmentStatus = appointment.Status.ToString(),
            message = "تم بدء الزيارة بنجاح"
        });
    }

    // ─── 5. POST /api/patient-journey/{visitId}/handoff-to-reception ────────
    /// <summary>Doctor finishes and sends patient to reception for checkout.</summary>
    [HttpPost("{visitId:guid}/handoff-to-reception")]
    public async Task<IActionResult> HandoffToReception(Guid visitId, [FromBody] HandoffRequest req)
    {
        var visit = await db.Visits
            .Include(v => v.Appointment)
            .FirstOrDefaultAsync(v => v.Id == visitId);
        if (visit == null)
            return NotFound(new { message = "الزيارة غير موجودة" });
        if (!visit.IsActive)
            return BadRequest(new { message = "الزيارة محذوفة" });

        // FIX: Guard against overwriting terminal checkout states
        if (visit.CheckoutStatus == "CheckedOut")
            return BadRequest(new { message = "لا يمكن تسليم زيارة مكتملة الفاتورة للاستقبال" });
        if (IsLeftWithoutCompletionCheckoutStatus(visit.CheckoutStatus))
            return BadRequest(new { message = "لا يمكن تسليم زيارة مغادرة بدون إكمال" });

        // Update visit clinical data
        if (!string.IsNullOrWhiteSpace(req.TreatmentDone))
            visit.TreatmentDone = req.TreatmentDone;
        if (!string.IsNullOrWhiteSpace(req.Diagnosis))
            visit.Diagnosis = req.Diagnosis;
        if (!string.IsNullOrWhiteSpace(req.NextVisitPlan))
            visit.NextVisitPlan = req.NextVisitPlan;
        if (!string.IsNullOrWhiteSpace(req.Instructions))
            visit.Instructions = req.Instructions;
        if (req.SuggestedServiceId.HasValue)
            visit.ServiceId = req.SuggestedServiceId;
        if (req.FollowUpDate.HasValue)
            visit.NextVisitDate = req.FollowUpDate;
        if (req.AmountDue.HasValue)
            visit.AmountDueReference = req.AmountDue;
        if (!string.IsNullOrWhiteSpace(req.ProposedProcedure))
            visit.ProposedProcedure = req.ProposedProcedure;

        // Mark as ready for checkout
        visit.CheckoutStatus = "ReadyForCheckout";
        visit.ReadyForCheckoutAt = DateTime.UtcNow;
        visit.UpdatedAt = DateTime.UtcNow;

        // Update queue item if exists
        if (visit.AppointmentId.HasValue)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var queueItem = await db.ClinicQueueItems
                .FirstOrDefaultAsync(q => q.AppointmentId == visit.AppointmentId && q.QueueDate == today && q.IsActive);

            if (queueItem != null && ClinicQueueStatusTransitions.IsValidTransition(queueItem.Status, ClinicQueueStatus.Completed))
            {
                queueItem.Status = ClinicQueueStatus.Completed;
                queueItem.CompletedAt = DateTime.UtcNow;
                queueItem.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync();

        return Ok(new
        {
            visit.Id,
            CheckoutStatus = visit.CheckoutStatus,
            ReadyForCheckoutAt = visit.ReadyForCheckoutAt,
            AmountDueReference = visit.AmountDueReference,
            ProposedProcedure = visit.ProposedProcedure,
            message = "تم تسليم المريض للاستقبال بنجاح"
        });
    }

    // ─── 6. POST /api/patient-journey/{appointmentId}/checkout ──────────────
    /// <summary>Complete checkout — workflow-status only. No payment is created;
    /// the frontend should redirect to the Payments module for actual payment processing.</summary>
    [HttpPost("{appointmentId:guid}/checkout")]
    [Authorize(Policy = "AdminOrReception")]
    public async Task<IActionResult> Checkout(Guid appointmentId, [FromBody] CheckoutRequest req)
    {
        var appointment = await db.Appointments.FindAsync(appointmentId);
        if (appointment == null)
            return NotFound(new { message = "الموعد غير موجود" });
        if (!appointment.IsActive)
            return BadRequest(new { message = "الموعد محذوف" });

        // Find the visit for this appointment
        var visit = await db.Visits
            .FirstOrDefaultAsync(v => v.AppointmentId == appointmentId && v.IsActive);

        if (visit == null)
            return BadRequest(new { message = "لا توجد زيارة مرتبطة بهذا الموعد" });

        if (visit.CheckoutStatus != "ReadyForCheckout")
            return BadRequest(new { message = "الزيارة ليست جاهزة للحساب بعد" });

        // Checkout is workflow-status only — no direct Payment creation.
        // Actual payment processing should be done via the existing Payments module
        // (FinanceService.CreatePaymentAsync) to ensure receipt generation, notifications,
        // contract linking, and audit trail are handled correctly.

        // Mark visit as checked out
        visit.CheckoutStatus = "CheckedOut";
        visit.UpdatedAt = DateTime.UtcNow;

        // Complete appointment if valid
        if (AppointmentStatusTransitions.IsValidTransition(appointment.Status, AppointmentStatus.Completed))
        {
            appointment.Status = AppointmentStatus.Completed;
            appointment.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        // Build recommended next actions
        var nextActions = new List<string>();
        if (req.NextAppointmentDate.HasValue)
            nextActions.Add("حجز موعد متابعة");
        if (req.PaymentAmount.HasValue && req.PaymentAmount.Value > 0)
            nextActions.Add("تسجيل الدفع عبر صفحة المالية");
        if (visit.AmountDueReference.HasValue && visit.AmountDueReference.Value > 0)
            nextActions.Add("المبلغ المستحق: " + visit.AmountDueReference.Value.ToString("N0") + " ر.ي");

        return Ok(new
        {
            AppointmentId = appointment.Id,
            VisitId = visit.Id,
            CheckoutStatus = visit.CheckoutStatus,
            AppointmentStatus = appointment.Status.ToString(),
            NextActions = nextActions,
            message = "تم إنهاء الحساب بنجاح"
        });
    }

    // ─── 6B. POST /api/patient-journey/{visitId}/checkout ─────────────────
    /// <summary>Complete checkout by visitId — supports walk-in patients
    /// who have no appointmentId. Same logic as appointmentId-based checkout.</summary>
    [HttpPost("{visitId:guid}/checkout")]
    [Authorize(Policy = "AdminOrReception")]
    public async Task<IActionResult> CheckoutByVisit(Guid visitId, [FromBody] CheckoutRequest req)
    {
        var visit = await db.Visits
            .Include(v => v.Appointment)
            .FirstOrDefaultAsync(v => v.Id == visitId);

        if (visit == null)
            return NotFound(new { message = "الزيارة غير موجودة" });
        if (!visit.IsActive)
            return BadRequest(new { message = "الزيارة محذوفة" });
        if (visit.CheckoutStatus != "ReadyForCheckout")
            return BadRequest(new { message = "الزيارة ليست جاهزة للحساب بعد" });

        // Mark visit as checked out
        visit.CheckoutStatus = "CheckedOut";
        visit.UpdatedAt = DateTime.UtcNow;

        // Complete appointment if linked and valid
        if (visit.Appointment != null && visit.Appointment.IsActive)
        {
            if (AppointmentStatusTransitions.IsValidTransition(visit.Appointment.Status, AppointmentStatus.Completed))
            {
                visit.Appointment.Status = AppointmentStatus.Completed;
                visit.Appointment.UpdatedAt = DateTime.UtcNow;
            }
        }

        // Complete queue item if still active
        if (visit.AppointmentId.HasValue)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var queueItem = await db.ClinicQueueItems
                .FirstOrDefaultAsync(q => q.AppointmentId == visit.AppointmentId && q.QueueDate == today && q.IsActive
                    && q.Status != ClinicQueueStatus.Completed && q.Status != ClinicQueueStatus.Cancelled);
            if (queueItem != null && ClinicQueueStatusTransitions.IsValidTransition(queueItem.Status, ClinicQueueStatus.Completed))
            {
                queueItem.Status = ClinicQueueStatus.Completed;
                queueItem.CompletedAt = DateTime.UtcNow;
                queueItem.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync();

        // Build recommended next actions
        var nextActions = new List<string>();
        if (req.NextAppointmentDate.HasValue)
            nextActions.Add("حجز موعد متابعة");
        if (req.PaymentAmount.HasValue && req.PaymentAmount.Value > 0)
            nextActions.Add("تسجيل الدفع عبر صفحة المالية");
        if (visit.AmountDueReference.HasValue && visit.AmountDueReference.Value > 0)
            nextActions.Add("المبلغ المستحق: " + visit.AmountDueReference.Value.ToString("N0") + " ر.ي");

        return Ok(new
        {
            VisitId = visit.Id,
            AppointmentId = visit.AppointmentId,
            CheckoutStatus = visit.CheckoutStatus,
            AppointmentStatus = visit.Appointment?.Status.ToString(),
            NextActions = nextActions,
            message = "تم إنهاء الحساب بنجاح"
        });
    }

    // ─── 7. POST /api/patient-journey/{visitId}/left-without-completion ─────
    /// <summary>Marks a visit as an explicit terminal operational state when
    /// the patient leaves after arrival without completing care. This does not
    /// create payments, alter invoices, or change financial balances.</summary>
    [HttpPost("{visitId:guid}/left-without-completion")]
    [Authorize(Policy = "AdminOrReception")]
    public async Task<IActionResult> MarkLeftWithoutCompletion(Guid visitId, [FromBody] LeftWithoutCompletionRequest req)
    {
        var reason = req.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            return BadRequest(new { message = "سبب الخروج بدون إكمال مطلوب" });

        var status = string.IsNullOrWhiteSpace(req.Status)
            ? "LeftWithoutCompletion"
            : req.Status.Trim();

        if (!IsLeftWithoutCompletionCheckoutStatus(status))
            return BadRequest(new { message = "حالة الخروج بدون إكمال غير صالحة" });

        var visit = await db.Visits
            .Include(v => v.Appointment)
            .FirstOrDefaultAsync(v => v.Id == visitId);

        if (visit == null)
            return NotFound(new { message = "الزيارة غير موجودة" });
        if (!visit.IsActive)
            return BadRequest(new { message = "الزيارة محذوفة" });
        if (visit.CheckoutStatus == "CheckedOut")
            return BadRequest(new { message = "لا يمكن تعليم زيارة مكتملة كمغادرة بدون إكمال" });

        var previousCheckoutStatus = visit.CheckoutStatus;
        var previousAppointmentStatus = visit.Appointment?.Status.ToString();
        var now = DateTime.UtcNow;

        visit.CheckoutStatus = status;
        visit.UpdatedAt = now;

        ClinicQueueItem? queueItem = null;
        if (visit.AppointmentId.HasValue)
        {
            queueItem = await db.ClinicQueueItems
                .Where(q => q.AppointmentId == visit.AppointmentId.Value
                    && q.IsActive
                    && q.Status != ClinicQueueStatus.Completed
                    && q.Status != ClinicQueueStatus.Cancelled
                    && q.Status != ClinicQueueStatus.NoShow)
                .OrderByDescending(q => q.UpdatedAt)
                .ThenByDescending(q => q.CreatedAt)
                .FirstOrDefaultAsync();

            if (queueItem != null)
            {
                queueItem.Status = ClinicQueueStatus.Cancelled;
                queueItem.CancelledAt = now;
                queueItem.UpdatedAt = now;
            }
        }

        db.AuditLogs.Add(new AuditLog
        {
            Resource = "Visit.LeftWithoutCompletion",
            ResourceId = visit.Id,
            Action = AuditAction.Update,
            UserId = GetCurrentUserId(),
            OldData = JsonSerializer.SerializeToDocument(new
            {
                checkoutStatus = previousCheckoutStatus,
                appointmentStatus = previousAppointmentStatus
            }),
            NewData = JsonSerializer.SerializeToDocument(new
            {
                checkoutStatus = status,
                reason,
                visit.PatientId,
                visit.AppointmentId,
                queueItemId = queueItem?.Id
            })
        });

        await db.SaveChangesAsync();

        return Ok(new
        {
            VisitId = visit.Id,
            visit.AppointmentId,
            CheckoutStatus = visit.CheckoutStatus,
            Reason = reason,
            QueueStatus = queueItem?.Status.ToString(),
            message = "تم تسجيل خروج المريض بدون إكمال"
        });
    }

    /// <summary>Creates a Draft Invoice from a visit that is ready for checkout.
    /// Uses Visit.AmountDueReference and linked ServiceId for line item pricing.
    /// Does NOT create a Payment. Does NOT alter Contract or Patient balance.
    /// If a Draft invoice already exists for this Visit, returns the existing one.
    /// Uses transaction + advisory lock + InvoicesController.GenerateInvoiceNumberAsync
    /// + commissionService.AutoFillFromServiceAsync to match main branch safe behavior.
    /// Re-checks for duplicate draft inside the transaction to prevent race conditions.</summary>
    [HttpPost("{visitId:guid}/create-draft-invoice")]
    [Authorize(Policy = "FinanceAccess")]
    public async Task<IActionResult> CreateDraftInvoice(Guid visitId)
    {
        var visit = await db.Visits
            .Include(v => v.Appointment)
            .FirstOrDefaultAsync(v => v.Id == visitId);
        if (visit == null)
            return NotFound(new { message = "الزيارة غير موجودة" });
        if (!visit.IsActive)
            return BadRequest(new { message = "الزيارة محذوفة" });

        // Fast-path duplicate prevention: check before acquiring lock
        var existingDraft = await db.Invoices
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.VisitId == visitId && i.Status == InvoiceStatus.Draft && i.IsActive);

        if (existingDraft != null)
        {
            return Ok(new
            {
                IsExisting = true,
                existingDraft.Id,
                existingDraft.InvoiceNumber,
                Status = existingDraft.Status.ToString(),
                StatusArabic = GetInvoiceStatusArabic(existingDraft.Status),
                existingDraft.Subtotal,
                existingDraft.TotalAmount,
                LineItemCount = existingDraft.LineItems.Count,
                message = "فاتورة مسودة موجودة مسبقاً"
            });
        }

        // Determine amounts
        var lineAmount = visit.AmountDueReference ?? visit.Cost ?? 0;
        if (lineAmount <= 0)
            return BadRequest(new { message = "لا يمكن إنشاء فاتورة بمبلغ صفر — حدد المبلغ المستحق أولاً" });

        // Get service name for line item description + ServiceId fallback from appointment
        string lineDescription = "زيارة طبية";
        Guid? lineServiceId = visit.ServiceId;

        // Fallback: if visit has no ServiceId, try appointment's ServiceId
        if (!lineServiceId.HasValue && visit.Appointment?.ServiceId.HasValue == true)
            lineServiceId = visit.Appointment.ServiceId;

        if (lineServiceId.HasValue)
        {
            var svc = await db.ClinicServices.FindAsync(lineServiceId.Value);
            if (svc != null)
                lineDescription = svc.ArabicName ?? svc.Code ?? lineDescription;
        }

        // Use transaction + advisory lock (matching main branch InvoicesController.Create)
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var lockKey = (int)(DateTime.UtcNow.ToString("yyyyMMdd").GetHashCode() % 100000);
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

            // Fix-1: Re-check for existing draft INSIDE the transaction after advisory lock
            // to prevent two concurrent requests from creating duplicate drafts
            var inTxExistingDraft = await db.Invoices
                .Include(i => i.LineItems)
                .FirstOrDefaultAsync(i => i.VisitId == visitId && i.Status == InvoiceStatus.Draft && i.IsActive);

            if (inTxExistingDraft != null)
            {
                await tx.RollbackAsync();
                return Ok(new
                {
                    IsExisting = true,
                    inTxExistingDraft.Id,
                    inTxExistingDraft.InvoiceNumber,
                    Status = inTxExistingDraft.Status.ToString(),
                    StatusArabic = GetInvoiceStatusArabic(inTxExistingDraft.Status),
                    inTxExistingDraft.Subtotal,
                    inTxExistingDraft.TotalAmount,
                    LineItemCount = inTxExistingDraft.LineItems.Count,
                    message = "فاتورة مسودة موجودة مسبقاً"
                });
            }

            // Use InvoicesController.GenerateInvoiceNumberAsync (same as main branch)
            var invoiceNumber = await InvoicesController.GenerateInvoiceNumberAsync(db);
            var userId = GetCurrentUserId();

            var invoice = new Invoice
            {
                InvoiceNumber = invoiceNumber,
                PatientId = visit.PatientId,
                VisitId = visitId,
                AppointmentId = visit.AppointmentId,
                Status = InvoiceStatus.Draft,
                Notes = $"فاتورة مسودة من زيارة يوم {visit.VisitDate:yyyy-MM-dd}",
                CreatedBy = userId,
                UpdatedBy = userId
            };

            db.Invoices.Add(invoice);

            // Add line item
            var lineItem = new InvoiceLineItem
            {
                InvoiceId = invoice.Id,
                ServiceId = lineServiceId,
                Description = lineDescription,
                ServiceNameSnapshot = lineDescription,
                Quantity = 1,
                UnitPrice = lineAmount,
                TotalPrice = lineAmount,
                RelatedVisitId = visitId,
                SortOrder = 0
            };
            db.InvoiceLineItems.Add(lineItem);

            await db.SaveChangesAsync();

            // Auto-fill commission defaults from service catalog entry (matching main branch)
            try { await commissionService.AutoFillFromServiceAsync(lineItem.Id); }
            catch (Exception ex) { logger.LogWarning(ex, "Commission auto-fill failed for line item {LineItemId}", lineItem.Id); }

            // Recalculate Subtotal from line items (matching main branch)
            var allLineItems = await db.InvoiceLineItems
                .Where(l => l.InvoiceId == invoice.Id && l.IsActive)
                .ToListAsync();
            invoice.Subtotal = allLineItems.Sum(l => l.TotalPrice);
            invoice.TotalAmount = invoice.Subtotal; // No discount/tax in draft from visit

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            // Preserve response fields: IsExisting, StatusArabic, Subtotal, TotalAmount, LineItemCount
            return Ok(new
            {
                IsExisting = false,
                invoice.Id,
                invoice.InvoiceNumber,
                Status = invoice.Status.ToString(),
                StatusArabic = GetInvoiceStatusArabic(invoice.Status),
                invoice.Subtotal,
                invoice.TotalAmount,
                LineItemCount = allLineItems.Count,
                message = "تم إنشاء الفاتورة المسودة بنجاح"
            });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ─── 8. POST /api/patient-journey/{patientId}/validate-financial-closure ─
    /// <summary>
    /// Validates whether a visit can be financially closed.
    /// Checks outstanding balance and treatment plan status.
    /// </summary>
    [HttpPost("{patientId:guid}/validate-financial-closure")]
    public async Task<IActionResult> ValidateFinancialClosure(
        Guid patientId,
        [FromBody] ValidateFinancialClosureRequest req)
    {
        var patient = await db.Patients.FindAsync(patientId);
        if (patient == null) return NotFound(new { message = "المريض غير موجود" });

        // Get the latest active visit
        var visit = await db.Visits
            .Include(v => v.Appointment)
            .FirstOrDefaultAsync(v => v.PatientId == patientId && v.IsActive && v.CheckoutStatus != "CheckedOut");

        if (visit == null) return Ok(new { canClose = true, reason = "" });

        // Get outstanding balance for this patient
        var invoices = await db.Invoices
            .Include(i => i.Payments.Where(p => p.IsActive))
            .Include(i => i.LineItems.Where(l => l.IsActive))
            .Where(i => i.PatientId == patientId && i.IsActive && i.Status != InvoiceStatus.Cancelled)
            .ToListAsync();

        var totalInvoiced = invoices.Sum(i => i.TotalAmount);
        var totalPaid = invoices.Sum(i => i.Payments.Sum(p => p.Amount));
        var outstanding = totalInvoiced - totalPaid;

        if (outstanding <= 0)
        {
            return Ok(new { canClose = true, reason = "الرصيد متساوي", outstandingAmount = 0 });
        }

        // Check if there's a multi-session treatment plan (active ortho case or in-progress general treatment items)
        var hasActiveOrthoCase = await db.OrthoCases
            .AnyAsync(o => o.PatientId == patientId && o.IsActive && o.Status == OrthoCaseStatus.Active);

        var hasActiveGeneralPlan = await db.GeneralTreatmentPlanItems
            .AnyAsync(g => g.PatientId == patientId && g.IsActive && g.Status == "in_progress");

        if (hasActiveOrthoCase || hasActiveGeneralPlan)
        {
            return Ok(new
            {
                canClose = true,
                reason = "خطة علاج متعددة الجلسات",
                reasonCode = "MULTI_SESSION_PLAN",
                outstandingAmount = outstanding,
                requiresAuditLog = true
            });
        }

        // If completed visit with outstanding and no plan
        if (req.ManagerOverride)
        {
            // Log audit for manager override
            var userId = GetCurrentUserId();
            db.AuditLogs.Add(new AuditLog
            {
                Resource = "Visit.FinancialClosure",
                ResourceId = visit.Id,
                Action = AuditAction.Approve,
                UserId = userId,
                NewData = JsonSerializer.SerializeToDocument(new
                {
                    action = "ManagerOverrideFinancialClosure",
                    outstandingAmount = outstanding,
                    reason = req.ClosureReason ?? "موافقة مدير على الدين"
                })
            });
            await db.SaveChangesAsync();

            return Ok(new
            {
                canClose = true,
                reason = "موافقة مدير على الدين",
                reasonCode = "MANAGER_OVERRIDE",
                outstandingAmount = outstanding,
                requiresAuditLog = true
            });
        }

        // Cannot close without manager approval
        return Ok(new
        {
            canClose = false,
            reason = "يوجد مبلغ متبقي. يلزم دفع كامل أو موافقة مدير أو تحويل لخطة أقساط.",
            reasonCode = "OUTSTANDING_BALANCE",
            outstandingAmount = outstanding
        });
    }

    private static string GetInvoiceStatusArabic(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Draft => "مسودة",
        InvoiceStatus.Issued => "صادرة",
        InvoiceStatus.Paid => "مدفوعة",
        InvoiceStatus.Cancelled => "ملغاة",
        _ => status.ToString()
    };

    // ─── Helper Methods ─────────────────────────────────────────────────────

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        while (inner != null)
        {
            if (inner is PostgresException pgEx && pgEx.SqlState == "23505")
                return true;
            inner = inner.InnerException;
        }
        return false;
    }

    private static string DetermineNextAction(AppointmentStatus apptStatus, ClinicQueueStatus? queueStatus, string? checkoutStatus)
    {
        // Blocker-3: checkoutStatus takes precedence for workflow routing
        if (IsLeftWithoutCompletionCheckoutStatus(checkoutStatus))
            return "None";
        if (checkoutStatus == "ReadyForCheckout")
            return "Checkout";
        if (checkoutStatus == "CheckedOut")
            return "None";

        return apptStatus switch
        {
            AppointmentStatus.Scheduled or AppointmentStatus.Confirmed => "Intake",
            AppointmentStatus.Arrived => "SendToQueue",
            AppointmentStatus.Waiting => queueStatus == ClinicQueueStatus.Waiting ? "CallPatient" : "EnterRoom",
            AppointmentStatus.Called => "EnterRoom",
            AppointmentStatus.InRoom => "StartVisit",
            AppointmentStatus.InProgress => "InProgress",
            AppointmentStatus.Completed => "None",
            _ => "None"
        };
    }

    private static string BuildPatientDisplayName(Patient? patient)
    {
        if (patient == null) return "—";
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(patient.FirstName)) parts.Add(patient.FirstName.Trim());
        if (!string.IsNullOrWhiteSpace(patient.MiddleName)) parts.Add(patient.MiddleName.Trim());
        if (!string.IsNullOrWhiteSpace(patient.LastName)) parts.Add(patient.LastName.Trim());
        return parts.Count > 0 ? string.Join(" ", parts) : "—";
    }

    private static bool IsEmergencyAppointmentType(string? appointmentType)
    {
        if (string.IsNullOrWhiteSpace(appointmentType))
            return false;

        return appointmentType.Equals("Emergency", StringComparison.OrdinalIgnoreCase)
            || appointmentType.Contains("إسعاف", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLeftWithoutCompletionCheckoutStatus(string? checkoutStatus)
    {
        return checkoutStatus is "LeftWithoutCompletion" or "CancelledAfterArrival" or "Incomplete" or "Abandoned";
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User?.Claims.FirstOrDefault(c => c.Type == "sub" || c.Type == "userId");
        if (claim != null && Guid.TryParse(claim.Value, out var uid))
            return uid;
        return null;
    }

    private bool IsDoctorRole()
    {
        return patientAccessService.IsDoctor;
    }
}

// ─── Request DTOs ────────────────────────────────────────────────────────────

public class IntakeRequest
{
    public Guid? ServiceId { get; set; }
    public string? ChiefComplaint { get; set; }
    public string? Notes { get; set; }
    public Guid? RoomId { get; set; }
    public bool RequiresConsultationFee { get; set; }
    public decimal? ConsultationFeeAmount { get; set; }
}

public class SendToQueueRequest
{
    public Guid? RoomId { get; set; }
    public string? Notes { get; set; }
}

public class HandoffRequest
{
    public string? TreatmentDone { get; set; }
    public string? Diagnosis { get; set; }
    public string? NextVisitPlan { get; set; }
    public string? Instructions { get; set; }
    public Guid? SuggestedServiceId { get; set; }
    public DateOnly? FollowUpDate { get; set; }
    public decimal? AmountDue { get; set; }
    public string? Notes { get; set; }
    public string? ProposedProcedure { get; set; }
}

public class CheckoutRequest
{
    /// <summary>
    /// PaymentAmount is for reference/guidance ONLY.
    /// Checkout is workflow-status only — it does NOT create a Payment.
    /// To record actual payment, use the Finance module (POST /api/payments)
    /// via FinanceService.CreatePaymentAsync().
    /// </summary>
    public decimal? PaymentAmount { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Notes { get; set; }
    public DateOnly? NextAppointmentDate { get; set; }
    public Guid? NextServiceId { get; set; }
}

public class LeftWithoutCompletionRequest
{
    public string? Reason { get; set; }
    public string? Status { get; set; }
}

/// <summary>
/// Request DTO for financial closure validation.
/// </summary>
public sealed class ValidateFinancialClosureRequest
{
    /// <summary>If true, a manager is overriding the outstanding balance check.</summary>
    public bool ManagerOverride { get; init; }

    /// <summary>Reason for closure override (e.g. manager approval reason).</summary>
    public string? ClosureReason { get; init; }

    /// <summary>Specific visit to validate (optional — defaults to latest active visit).</summary>
    public Guid? VisitId { get; init; }
}
