using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using static AqlanDentalPro.Infrastructure.Services.MvcResults;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// READ-side service for the Patient Journey Command Center.
/// Hosts the heavy aggregate queries (GetToday, GetDailySummary) and the
/// ortho-journey prefetch helper. No mutations — those live in <see cref="CheckoutService"/>.
/// Extracted from PatientJourneyController (CLIN-22).
/// </summary>
public class PatientJourneyService(
    AppDbContext db,
    IPatientAccessService patientAccessService,
    IFinanceReadService financeReadService,
    ILogger<PatientJourneyService> logger)
{
    // ─── 1. GET /api/patient-journey/today ────────────────────────────────────
    /// <summary>Returns today's patient journey list combining appointments,
    /// queue status, visit data, and payment info.</summary>
    public async Task<IActionResult> GetTodayAsync(
        DateOnly queryDate,
        AppointmentStatus? statusFilter,
        Guid? doctorId,
        Guid? serviceId,
        Guid? roomId)
    {
        try
        {
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

            // Patients with an active orthodontic case — single prefetch query for all today's patients
            var orthoSummaries = await LoadOrthoJourneySummariesAsync(patientIds);

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
                orthoSummaries.TryGetValue(a.PatientId, out var orthoSummary);

                // Compute in-room timestamp from queue item
                DateTime? inRoomSince = queueItem?.InRoomAt ?? queueItem?.StartedAt;

                return new
                {
                    AppointmentId = (Guid?)a.Id,
                    PatientId = a.PatientId,
                    PatientName = BuildPatientDisplayName(a.Patient),
                    PatientNumber = a.Patient?.PatientNumber,
                    PatientPhone = isDoctor ? null : a.Patient?.Phone,
                    AppointmentTime = (string?)a.StartTime.ToString("HH:mm"),
                    AppointmentType = (string?)a.AppointmentType,
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
                    HasActiveOrthoCase = orthoSummary is not null,
                    OrthoCaseId = orthoSummary?.CaseId,
                    OrthoCaseNumber = orthoSummary?.CaseNumber,
                    OrthoCurrentStage = orthoSummary?.CurrentStage,
                    OrthoLastVisitDate = orthoSummary?.LastVisitDate?.ToString("yyyy-MM-dd"),
                    OrthoNextAppointmentDate = orthoSummary?.NextAppointmentDate?.ToString("yyyy-MM-dd"),
                    OrthoContractRemaining = orthoSummary?.ContractRemaining,
                    // CLIN-05: ortho bridge fields mirrored from the linked Visit
                    // (set by OrthoService.AddVisitAsync). Null for non-ortho visits.
                    OrthoVisitWireUpper = visit?.WireUpper,
                    OrthoVisitWireLower = visit?.WireLower,
                    OrthoVisitCurrentStage = visit?.CurrentStage,
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

                // Extend the active-ortho prefetch set with walk-in patients not already covered
                var uncoveredOrthoIds = walkInPatientIds.Where(id => !orthoSummaries.ContainsKey(id)).ToList();
                if (uncoveredOrthoIds.Count > 0)
                {
                    var walkInOrthoSummaries = await LoadOrthoJourneySummariesAsync(uncoveredOrthoIds);
                    foreach (var pair in walkInOrthoSummaries)
                        orthoSummaries[pair.Key] = pair.Value;
                }
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
                    orthoSummaries.TryGetValue(q.PatientId, out var orthoSummary);

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
                        QueueStatus = (string?)queueStatus.ToString(),
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
                        HasActiveOrthoCase = orthoSummary is not null,
                        OrthoCaseId = orthoSummary?.CaseId,
                        OrthoCaseNumber = orthoSummary?.CaseNumber,
                        OrthoCurrentStage = orthoSummary?.CurrentStage,
                        OrthoLastVisitDate = orthoSummary?.LastVisitDate?.ToString("yyyy-MM-dd"),
                        OrthoNextAppointmentDate = orthoSummary?.NextAppointmentDate?.ToString("yyyy-MM-dd"),
                        OrthoContractRemaining = orthoSummary?.ContractRemaining,
                        // CLIN-05: ortho bridge fields mirrored from the linked Visit.
                        OrthoVisitWireUpper = visit?.WireUpper,
                        OrthoVisitWireLower = visit?.WireLower,
                        OrthoVisitCurrentStage = visit?.CurrentStage,
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

    // FIN-PERM: database-driven finance permission check for the daily-summary
    // balance tiers. The owner configures finance.* permissions from Settings
    // (RolePermissions), so checkout balance access is NOT tied to hardcoded role
    // names. Admin always bypasses. Evaluated via IsInRole against each matching
    // RolePermission row so it works with the request ClaimsPrincipal without
    // needing ICurrentUserService (this service receives a ClaimsPrincipal).
    private async Task<bool> HasFinancePermissionAsync(ClaimsPrincipal user, string resource, string action)
    {
        if (user.IsInRole("Admin")) return true;

        var rows = await db.RolePermissions
            .AsNoTracking()
            .Where(p => p.Resource == resource)
            .ToListAsync();

        foreach (var p in rows)
        {
            if (!user.IsInRole(p.Role)) continue;
            var granted = action switch
            {
                "view" => p.CanView,
                "create" => p.CanCreate,
                "edit" => p.CanEdit,
                "delete" => p.CanDelete,
                "export" => p.CanExport,
                "approve" => p.CanApprove,
                _ => false
            };
            if (granted) return true;
        }
        return false;
    }

    // ─── 1B. GET /api/patient-journey/{patientId}/daily-summary ───────────
    /// <summary>Returns a comprehensive daily journey summary for a specific patient,
    /// aggregating patient info, today's appointment, queue status, finance snapshot,
    /// ortho case, medical alerts, recent visits, and timeline events.</summary>
    public async Task<IActionResult> GetDailySummaryAsync(ClaimsPrincipal user, Guid patientId)
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

            var today = ClinicTimeProvider.ClinicToday();

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
                    v.AppointmentId,
                    // CLIN-05: ortho bridge fields mirrored from OrthoVisit
                    v.WireUpper,
                    v.WireLower,
                    v.CurrentStage
                })
                .FirstOrDefaultAsync();

            // FIX-2 / FIN-PERM: Finance access tiers — now database/permission-driven
            // (owner-configurable from Settings) instead of hardcoded role names.
            // Full:    finance.patient_balance.view — Admin/Accountant seeded; the owner
            //          may grant it to other roles. Exposes the complete snapshot.
            // Limited: finance.payments.view — cashier-safe checkout only. Exposes ONLY
            //          the collection essentials (outstandingBalance, overdueAmount,
            //          latestPayment for receipt reprint, financialStatus) — no total
            //          treatment cost, no totals/history counts, no account statement.
            // None:    no finance permission (e.g. doctors).
            // Legacy Admin/Accountant role + finance.view claim are kept as a safety
            // fallback so a not-yet-seeded DB never strips Admin/Accountant access.
            var hasFullFinanceAccess = user.IsInRole("Admin") || user.IsInRole("Accountant") ||
                user.HasClaim("permission", "finance.view") ||
                await HasFinancePermissionAsync(user, "finance.patient_balance", "view");
            var hasLimitedFinanceAccess = !hasFullFinanceAccess &&
                await HasFinancePermissionAsync(user, "finance.payments", "view");
            var hasAnyFinanceAccess = hasFullFinanceAccess || hasLimitedFinanceAccess;
            // Reused below for non-finance display gating (patient contact fields, etc.).
            var isReception = user.IsInRole("Reception");

            object? financeSummary = null;
            int unpaidInvoicesCount = 0;
            object? activeContract = null;

            if (hasAnyFinanceAccess)
            {
                // FIX-3: Use central FinanceReadService.GetPatientFinanceSummaryAsync()
                // instead of duplicating financial calculations here.
                // (TD-021 PR A2: moved from FinanceService to FinanceReadService.)
                var centralSummary = await financeReadService.GetPatientFinanceSummaryAsync(patientId);

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
            var isAccountant = user.IsInRole("Accountant") && !user.IsInRole("Admin");
            var isAdmin = user.IsInRole("Admin");
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
                    todayVisit.AppointmentId,
                    // CLIN-05: ortho bridge fields
                    todayVisit.WireUpper,
                    todayVisit.WireLower,
                    todayVisit.CurrentStage
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
                orthoCaseResponse = user.HasClaim("permission", "patient_journey.view") ? activeOrthoCase : null;
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

    private async Task<Dictionary<Guid, OrthoJourneySummary>> LoadOrthoJourneySummariesAsync(
        IReadOnlyCollection<Guid> patientIds)
    {
        if (patientIds.Count == 0)
            return [];

        var cases = await db.OrthoCases
            .IgnoreQueryFilters()
            .Where(c => c.IsActive
                && c.Status == OrthoCaseStatus.Active
                && patientIds.Contains(c.PatientId))
            .Select(c => new
            {
                c.Id,
                c.PatientId,
                c.CaseNumber,
                c.CurrentStage,
                c.CreatedAt
            })
            .ToListAsync();

        if (cases.Count == 0)
            return [];

        var selectedCases = cases
            .GroupBy(c => c.PatientId)
            .Select(g => g.OrderByDescending(c => c.CreatedAt).First())
            .ToList();
        var caseIds = selectedCases.Select(c => c.Id).ToList();
        var today = ClinicTimeProvider.ClinicToday();

        var visits = await db.OrthoVisits
            .IgnoreQueryFilters()
            .Where(v => v.IsActive && caseIds.Contains(v.OrthoCaseId))
            .Select(v => new
            {
                v.OrthoCaseId,
                v.VisitDate,
                v.NextAppointmentDate
            })
            .ToListAsync();

        var scheduledAppointments = await db.Appointments
            .IgnoreQueryFilters()
            .Where(a => a.IsActive
                && a.OrthoCaseId.HasValue
                && caseIds.Contains(a.OrthoCaseId.Value)
                && a.AppointmentDate >= today
                && a.Status != AppointmentStatus.Cancelled
                && a.Status != AppointmentStatus.NoShow)
            .Select(a => new
            {
                OrthoCaseId = a.OrthoCaseId!.Value,
                a.AppointmentDate
            })
            .ToListAsync();

        var contracts = await db.Contracts
            .IgnoreQueryFilters()
            .Include(c => c.Payments.Where(p => p.IsActive))
            .Where(c => c.IsActive
                && c.RelatedCaseId.HasValue
                && caseIds.Contains(c.RelatedCaseId.Value))
            .ToListAsync();

        return selectedCases.ToDictionary(
            c => c.PatientId,
            c =>
            {
                var caseVisits = visits.Where(v => v.OrthoCaseId == c.Id).ToList();
                var lastVisitDate = caseVisits
                    .OrderByDescending(v => v.VisitDate)
                    .Select(v => (DateOnly?)v.VisitDate)
                    .FirstOrDefault();
                var nextAppointmentDate = scheduledAppointments
                    .Where(a => a.OrthoCaseId == c.Id)
                    .OrderBy(a => a.AppointmentDate)
                    .Select(a => (DateOnly?)a.AppointmentDate)
                    .FirstOrDefault()
                    ?? caseVisits
                        .Where(v => v.NextAppointmentDate >= today)
                        .OrderBy(v => v.NextAppointmentDate)
                        .Select(v => v.NextAppointmentDate)
                        .FirstOrDefault();

                var contract = contracts
                    .Where(x => x.RelatedCaseId == c.Id)
                    .OrderByDescending(x => x.CreatedAt)
                    .FirstOrDefault();
                decimal? remaining = contract is null
                    ? null
                    : Math.Max(
                        0,
                        contract.TotalAmount
                        - contract.DiscountAmount
                        - contract.Payments.Sum(p => p.Amount));

                return new OrthoJourneySummary(
                    c.Id,
                    c.CaseNumber,
                    c.CurrentStage,
                    lastVisitDate,
                    nextAppointmentDate,
                    remaining);
            });
    }

    private sealed record OrthoJourneySummary(
        Guid CaseId,
        string CaseNumber,
        string? CurrentStage,
        DateOnly? LastVisitDate,
        DateOnly? NextAppointmentDate,
        decimal? ContractRemaining);

    // ─── Static helpers (shared with CheckoutService) ─────────────────────────

    internal static string DetermineNextAction(AppointmentStatus apptStatus, ClinicQueueStatus? queueStatus, string? checkoutStatus)
    {
        // Blocker-3: checkoutStatus takes precedence for workflow routing
        if (IsLeftWithoutCompletionCheckoutStatus(checkoutStatus))
            return "None";
        if (checkoutStatus == "ReadyForCheckout")
            return "Checkout";
        if (checkoutStatus == "CheckedOut")
            return "None";

        // FIX: If the appointment is Completed but checkoutStatus is null,
        // the visit is still InProgress — the doctor needs to hand off to reception.
        // This happens when the queue item is completed but the visit hasn't been handed off yet.
        if (apptStatus == AppointmentStatus.Completed && string.IsNullOrEmpty(checkoutStatus))
            return "HandoffToReception";

        return apptStatus switch
        {
            AppointmentStatus.Scheduled or AppointmentStatus.Confirmed => "Intake",
            AppointmentStatus.Arrived => "SendToQueue",
            AppointmentStatus.Waiting => queueStatus == ClinicQueueStatus.Waiting ? "CallPatient" : "EnterRoom",
            AppointmentStatus.Called => "EnterRoom",
            AppointmentStatus.InRoom => "StartVisit",
            AppointmentStatus.InProgress => "HandoffToReception",
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

    /// <summary>
    /// True when the visit's checkout status represents a terminal "left without completing"
    /// state. Shared with <see cref="CheckoutService"/>. Kept here because <see cref="DetermineNextAction"/>
    /// (also in this service) depends on it.
    /// </summary>
    internal static bool IsLeftWithoutCompletionCheckoutStatus(string? checkoutStatus)
    {
        return checkoutStatus is "LeftWithoutCompletion" or "CancelledAfterArrival" or "Incomplete" or "Abandoned";
    }
}
