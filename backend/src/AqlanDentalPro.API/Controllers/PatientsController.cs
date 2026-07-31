using AqlanDentalPro.API.Authorization;
using AqlanDentalPro.API.Configuration;
using AqlanDentalPro.Application.DTOs.Common;
using AqlanDentalPro.Application.DTOs.Patients;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/patients")]
[Authorize(Policy = "StaffOnly")]
public class PatientsController(
    PatientService service,
    AppDbContext db,
    IFinanceReadService financeReadService,
    ICurrentUserService currentUser,
    IPatientAccessService patientAccess,
    IAuditService audit,
    ILogger<PatientsController> logger) : ControllerBase
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private Task<bool> CanViewFinanceAsync(string resource) =>
        PermissionGuard.HasAsync(db, currentUser, resource, "view");

    private IActionResult DenyFinance() =>
        StatusCode(403, new { message = "غير مصرح لك بعرض البيانات المالية للمريض" });

    /// <summary>
    /// Returns 403 if the current doctor cannot access the patient, writing an AuditLog entry
    /// for both granted and denied outcomes.  Returns null when no check is needed.
    /// </summary>
    private async Task<IActionResult?> DenyIfDoctorCannotAccess(Guid patientId)
    {
        if (!patientAccess.IsDoctor)
            return null;

        if (!await patientAccess.CanAccessPatientAsync(patientId))
        {
            logger.LogWarning(
                "Patient access denied: user {UserId} (role {Role}) attempted to access patient {PatientId}",
                currentUser.UserId, currentUser.Role, patientId);

            await audit.LogAsync(AuditAction.View, "Patient", patientId,
                newData: new { status = "denied", role = currentUser.Role?.ToString(), userId = currentUser.UserId });

            return StatusCode(403, new { message = "غير مصرح لك بعرض بيانات هذا المريض" });
        }

        await audit.LogAsync(AuditAction.View, "Patient", patientId,
            newData: new { status = "allowed", accessType = "clinical-limited", role = currentUser.Role?.ToString() });

        return null;
    }

    // ── Patient list ──────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? gender = null,
        [FromQuery] Guid? doctorId = null,
        [FromQuery] string? status = "active")
    {
        try
        {
            // Doctors see only the patients they are linked to (all 5 link types).
            if (patientAccess.IsDoctor)
            {
                HashSet<Guid>? accessible;
                try
                {
                    accessible = await patientAccess.GetAccessiblePatientIdsAsync();
                }
                catch (Exception ex)
                {
                    // Blocker-1 fix: A database/query/migration failure must NOT be hidden
                    // as an empty list — that masks a real outage from the doctor.
                    // Log server-side and return a generic 500 so the frontend loadError UI shows.
                    logger.LogError(ex, "GetAccessiblePatientIdsAsync failed for user {UserId}",
                        currentUser.UserId);
                    return StatusCode(500, new { message = "تعذر تحميل بيانات المرضى حالياً" });
                }

                if (accessible == null || accessible.Count == 0)
                {
                    // Blocker-2 fix: Legitimate empty list must still return valid pagination
                    // metadata. Normalize page/pageSize the same way the repository does.
                    var normPage = Math.Max(1, page);
                    var normPageSize = Math.Max(1, Math.Min(pageSize, 100));
                    return Ok(new PaginatedResponse<PatientListDto>
                    {
                        Data = [],
                        TotalCount = 0,
                        Page = normPage,
                        PageSize = normPageSize
                    });
                }

                // Pass the full accessible set — covers primary, appointment, visit, step, referral links.
                var result = await service.GetListAsync(search, page, pageSize, gender,
                    doctorId: null, status, allowedPatientIds: accessible);
                return Ok(result);
            }

            var fullResult = await service.GetListAsync(search, page, pageSize, gender, doctorId, status);
            return Ok(fullResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PatientsController.GetList failed");
            return StatusCode(500, new { message = "تعذر تحميل بيانات المرضى حالياً" });
        }
    }

    // ── Single patient ────────────────────────────────────────────────────────

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var denied = await DenyIfDoctorCannotAccess(id);
            if (denied != null) return denied;

            var patient = await service.GetByIdAsync(id);
            if (patient == null) return NotFound(new { message = "المريض غير موجود" });

            // Doctors receive a clinical-only view without contact/finance fields.
            if (patientAccess.IsDoctor)
                return Ok(ToClinicalDto(patient));

            // Only admins receive the full profile. Reception and other
            // non-clinical staff receive operational/contact fields without
            // medical or dental history.
            await audit.LogAsync(AuditAction.View, "PatientContactInfo", id,
                newData: new { role = currentUser.Role?.ToString() });

            return currentUser.IsAdmin
                ? Ok(patient)
                : Ok(ToOperationalDto(patient));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PatientsController.GetById failed for {PatientId}", id);
            return StatusCode(500, new { message = "تعذر تحميل بيانات المريض حالياً" });
        }
    }

    // ── Resolve patient by number ──────────────────────────────────────────────
    /// <summary>
    /// Sprint 1 Fix: Resolve a patient GUID from their patient number (e.g. GM-2026-025).
    /// Returns { id, patientNumber, fullName } so the frontend can navigate to the GUID route.
    /// Returns 404 with Arabic message if the number doesn't exist.
    /// </summary>
    [HttpGet("by-number/{patientNumber}")]
    public async Task<IActionResult> GetByPatientNumber(string patientNumber)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(patientNumber))
                return BadRequest(new { message = "رقم الملف مطلوب" });

            var patient = await db.Patients
                .IgnoreQueryFilters()
                .Where(p => p.PatientNumber == patientNumber && p.IsActive)
                .Select(p => new
                {
                    p.Id,
                    p.PatientNumber,
                    FullName = (p.FirstName + " " + (string.IsNullOrWhiteSpace(p.MiddleName) ? "" : p.MiddleName + " ") + p.LastName).Trim()
                })
                .FirstOrDefaultAsync();

            if (patient == null)
                return NotFound(new { message = $"لا يوجد مريض برقم الملف {patientNumber}" });

            // Doctors must not access patients they aren't linked to
            var denied = await DenyIfDoctorCannotAccess(patient.Id);
            if (denied != null) return denied;

            return Ok(patient);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PatientsController.GetByPatientNumber failed for {PatientNumber}", patientNumber);
            return StatusCode(500, new { message = "تعذر البحث عن المريض حالياً" });
        }
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [HttpPost]
    [Authorize(Policy = "AdminOrReception")]
    public async Task<ActionResult<PatientProfileDto>> Create([FromBody] CreatePatientRequest req)
    {
        try
        {
            var patient = await service.CreateAsync(req);
            return CreatedAtAction(nameof(GetById), new { id = patient.Id }, patient);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Patient creation conflict");
            return Conflict(new { message = ex.Message });
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            logger.LogWarning(ex, "Duplicate patient data on create");
            return Conflict(new { message = "البيانات مكررة — رقم الهاتف أو الواتساب أو رقم الملف موجود مسبقاً" });
        }
    }

    // ── Duplicate check ───────────────────────────────────────────────────────

    [HttpGet("check-duplicate")]
    [Authorize(Policy = "AdminOrReception")]
    public async Task<IActionResult> CheckDuplicate(
        [FromQuery] string? phone,
        [FromQuery] string? whatsApp,
        [FromQuery] string? patientNumber,
        [FromQuery] string? firstName,
        [FromQuery] string? lastName,
        [FromQuery] string? dateOfBirth,
        [FromQuery] Guid? excludeId)
    {
        var duplicates = new List<object>();

        var normalizedPhone = PhoneNormalizer.Normalize(phone);
        var normalizedWhatsApp = PhoneNormalizer.Normalize(whatsApp);

        if (normalizedPhone != null)
        {
            var query = db.Patients
                .IgnoreQueryFilters()
                .Where(p => p.IsActive && (p.NormalizedPhone == normalizedPhone || p.NormalizedWhatsApp == normalizedPhone));
            if (excludeId.HasValue) query = query.Where(p => p.Id != excludeId.Value);
            var match = await query
                .Select(p => new { p.Id, p.PatientNumber, FullName = (p.FirstName + " " + (string.IsNullOrWhiteSpace(p.MiddleName) ? "" : p.MiddleName + " ") + p.LastName).Trim(), p.Phone, MatchType = "phone" })
                .FirstOrDefaultAsync();
            if (match != null) duplicates.Add(match);
        }

        if (normalizedWhatsApp != null && !duplicates.Any())
        {
            var query = db.Patients
                .IgnoreQueryFilters()
                .Where(p => p.IsActive && (p.NormalizedWhatsApp == normalizedWhatsApp || p.NormalizedPhone == normalizedWhatsApp));
            if (excludeId.HasValue) query = query.Where(p => p.Id != excludeId.Value);
            var match = await query
                .Select(p => new { p.Id, p.PatientNumber, FullName = (p.FirstName + " " + (string.IsNullOrWhiteSpace(p.MiddleName) ? "" : p.MiddleName + " ") + p.LastName).Trim(), p.Phone, MatchType = "whatsapp" })
                .FirstOrDefaultAsync();
            if (match != null) duplicates.Add(match);
        }

        if (!string.IsNullOrWhiteSpace(patientNumber))
        {
            var query = db.Patients
                .IgnoreQueryFilters()
                .Where(p => p.PatientNumber == patientNumber && p.IsActive);
            if (excludeId.HasValue) query = query.Where(p => p.Id != excludeId.Value);
            var match = await query
                .Select(p => new { p.Id, p.PatientNumber, FullName = (p.FirstName + " " + (string.IsNullOrWhiteSpace(p.MiddleName) ? "" : p.MiddleName + " ") + p.LastName).Trim(), p.Phone, MatchType = "patientNumber" })
                .FirstOrDefaultAsync();
            if (match != null) duplicates.Add(match);
        }

        if (!string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(lastName))
        {
            var nameQuery = db.Patients.IgnoreQueryFilters().Where(p => p.IsActive && p.FirstName == firstName && p.LastName == lastName);
            if (excludeId.HasValue) nameQuery = nameQuery.Where(p => p.Id != excludeId.Value);
            if (!string.IsNullOrWhiteSpace(dateOfBirth) && DateOnly.TryParse(dateOfBirth, out var dob))
                nameQuery = nameQuery.Where(p => p.DateOfBirth == dob);

            var nameMatches = await nameQuery
                .Select(p => new { p.Id, p.PatientNumber, FullName = (p.FirstName + " " + (string.IsNullOrWhiteSpace(p.MiddleName) ? "" : p.MiddleName + " ") + p.LastName).Trim(), p.Phone, MatchType = "name" })
                .Take(5)
                .ToListAsync();
            duplicates.AddRange(nameMatches.Where(nm => !duplicates.Any(d => ((dynamic)d).Id == nm.Id)));
        }

        return Ok(new { isDuplicate = duplicates.Count > 0, matches = duplicates });
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOrReception")]
    public async Task<ActionResult<PatientProfileDto>> Update(Guid id, [FromBody] UpdatePatientRequest req)
    {
        try
        {
            var result = await service.UpdateAsync(id, req);
            return result == null ? NotFound(new { message = "المريض غير موجود" }) : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Patient update conflict");
            return Conflict(new { message = ex.Message });
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            logger.LogWarning(ex, "Duplicate patient data on update");
            return Conflict(new { message = "رقم الهاتف أو الواتساب مستخدم مسبقاً لمريض آخر." });
        }
    }

    // ── Archive / Delete / Restore ────────────────────────────────────────────

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var success = await service.SoftDeleteAsync(id);
        return success ? Ok(new { message = "تم أرشفة المريض بنجاح" }) : NotFound(new { message = "المريض غير موجود" });
    }

    [HttpPut("{id:guid}/archive")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Archive(Guid id)
    {
        var success = await service.ArchiveAsync(id);
        return success ? Ok(new { message = "تم أرشفة المريض" }) : NotFound(new { message = "المريض غير موجود" });
    }

    [HttpPost("{id:guid}/restore")]
    [HttpPut("{id:guid}/restore")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Restore(Guid id)
    {
        var success = await service.RestoreAsync(id);
        return success ? Ok(new { message = "تم استعادة المريض بنجاح" }) : NotFound(new { message = "المريض غير موجود" });
    }

    // ── Clinical history ──────────────────────────────────────────────────────

    [HttpGet("{id:guid}/medical-history")]
    [Authorize(Policy = AuthorizationPolicyNames.ClinicalRead)]
    public async Task<IActionResult> GetMedicalHistory(Guid id)
    {
        var denied = await DenyIfDoctorCannotAccess(id);
        if (denied != null) return denied;

        var patient = await service.GetByIdAsync(id);
        if (patient == null) return NotFound(new { message = "المريض غير موجود" });
        return Ok(patient.MedicalHistory ?? new MedicalHistoryDto());
    }

    [HttpPut("{id:guid}/medical-history")]
    [Authorize(Policy = AuthorizationPolicyNames.ClinicalWrite)]
    public async Task<IActionResult> UpdateMedicalHistory(Guid id, [FromBody] MedicalHistoryDto dto)
    {
        var denied = await DenyIfDoctorCannotAccess(id);
        if (denied != null) return denied;

        try
        {
            var result = await service.UpsertMedicalHistoryAsync(id, dto);
            if (result == null) return NotFound(new { message = "المريض غير موجود" });
            return Ok(result);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "Medical history concurrency conflict for patient {PatientId}", id);
            return Conflict(new { message = "تعارض في تحديث السجل الطبي — حاول مرة أخرى" });
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to save medical history for patient {PatientId}", id);
            return StatusCode(500, new { message = "حدث خطأ أثناء حفظ التاريخ الطبي — حاول مرة أخرى لاحقاً" });
        }
    }

    [HttpGet("{id:guid}/dental-history")]
    [Authorize(Policy = AuthorizationPolicyNames.ClinicalRead)]
    public async Task<IActionResult> GetDentalHistory(Guid id)
    {
        var denied = await DenyIfDoctorCannotAccess(id);
        if (denied != null) return denied;

        var patient = await service.GetByIdAsync(id);
        if (patient == null) return NotFound(new { message = "المريض غير موجود" });
        return Ok(patient.DentalHistory ?? new DentalHistoryDto());
    }

    [HttpPut("{id:guid}/dental-history")]
    [Authorize(Policy = AuthorizationPolicyNames.ClinicalWrite)]
    public async Task<IActionResult> UpdateDentalHistory(Guid id, [FromBody] DentalHistoryDto dto)
    {
        var denied = await DenyIfDoctorCannotAccess(id);
        if (denied != null) return denied;

        try
        {
            var result = await service.UpsertDentalHistoryAsync(id, dto);
            if (result == null) return NotFound(new { message = "المريض غير موجود" });
            return Ok(result);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "Dental history concurrency conflict for patient {PatientId}", id);
            return Conflict(new { message = "تعارض في تحديث السجل السني — حاول مرة أخرى" });
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to save dental history for patient {PatientId}", id);
            return StatusCode(500, new { message = "حدث خطأ أثناء حفظ التاريخ السني — حاول مرة أخرى لاحقاً" });
        }
    }

    // ── Summary (financial data hidden without granular permission) ───────────

    [HttpGet("{id:guid}/summary")]
    public async Task<IActionResult> GetSummary(Guid id)
    {
        var denied = await DenyIfDoctorCannotAccess(id);
        if (denied != null) return denied;

        // CORE-PAT-015: the profile deliberately serves archived patients, so the
        // summary must use the same existence rule instead of returning a partial 404.
        var exists = await db.Patients.IgnoreQueryFilters().AnyAsync(p => p.Id == id);
        if (!exists) return NotFound(new { message = "المريض غير موجود" });

        var clinicNow = ClinicTimeProvider.ClinicNow();
        var today = DateOnly.FromDateTime(clinicNow);
        var currentTime = TimeOnly.FromDateTime(clinicNow);

        var latestVisit = await db.Visits
            .AsNoTracking()
            .Include(v => v.Doctor)
            .Where(v => v.PatientId == id)
            .OrderByDescending(v => v.VisitDate)
            .ThenByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync();

        var nextAppointment = await db.Appointments
            .AsNoTracking()
            .Include(a => a.Doctor)
            .Where(a => a.PatientId == id
                && (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed)
                && (a.AppointmentDate > today
                    || (a.AppointmentDate == today && a.StartTime >= currentTime)))
            .OrderBy(a => a.AppointmentDate)
            .ThenBy(a => a.StartTime)
            .FirstOrDefaultAsync();

        var dentalHistory = await db.DentalHistories
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.PatientId == id);
        var medicalHistory = await db.MedicalHistories
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.PatientId == id);

        var nextTreatmentStep = await db.PatientTreatmentPlanSteps
            .AsNoTracking()
            .Where(step => step.PatientId == id
                && step.Status != TreatmentStepStatus.Completed
                && step.Status != TreatmentStepStatus.Cancelled)
            .OrderBy(step => step.Status == TreatmentStepStatus.InProgress ? 0
                : step.Status == TreatmentStepStatus.Planned ? 1 : 2)
            .ThenBy(step => step.SequenceNumber)
            .FirstOrDefaultAsync();

        var activeOrthoSummary = await db.OrthoCases
            .AsNoTracking()
            .ActiveCases()
            .Where(o => o.PatientId == id)
            .OrderBy(o => o.CaseNumber)
            .Select(o => new PatientOrthoSummaryDto
            {
                CaseNumber = o.CaseNumber,
                ApplianceType = o.ApplianceType,
                StagePercentage = o.StagePercentage,
            })
            .ToListAsync();

        var activeSurgerySummary = await db.SurgeryCases
            .AsNoTracking()
            .Where(s => s.PatientId == id
                && (s.Status == SurgeryCaseStatus.Scheduled
                    || s.Status == SurgeryCaseStatus.InProgress
                    || s.Status == SurgeryCaseStatus.Postponed))
            .OrderBy(s => s.CaseNumber)
            .Select(s => new PatientSurgerySummaryDto
            {
                CaseNumber = s.CaseNumber,
                SurgeryType = s.SurgeryType,
                Status = s.Status.ToString(),
            })
            .ToListAsync();

        var summary = new PatientSummaryDto
        {
            TotalAppointments = await db.Appointments.CountAsync(a => a.PatientId == id),
            CompletedAppointments = await db.Appointments.CountAsync(a => a.PatientId == id
                && a.Status == AppointmentStatus.Completed),
            ActiveOrthoCases = activeOrthoSummary.Count,
            PrescriptionsCount = await db.Prescriptions.CountAsync(p => p.PatientId == id),
            LastVisitDate = latestVisit?.VisitDate.ToString("yyyy-MM-dd"),
            LastVisitDoctor = latestVisit?.Doctor?.Name,
            LastVisitDiagnosis = latestVisit?.Diagnosis,
            NextAppointmentDate = nextAppointment?.AppointmentDate.ToString("yyyy-MM-dd"),
            NextAppointmentTime = nextAppointment?.StartTime.ToString("HH:mm"),
            NextAppointmentType = nextAppointment?.AppointmentType,
            NextAppointmentDoctor = nextAppointment?.Doctor?.Name,
            ChiefComplaint = FirstNonBlank(latestVisit?.ChiefComplaint, dentalHistory?.ChiefComplaint),
            CurrentDiagnosis = latestVisit?.Diagnosis,
            NextPlannedStep = FirstNonBlank(
                nextTreatmentStep?.Title,
                nextTreatmentStep?.ServiceNameSnapshot,
                nextTreatmentStep?.Description,
                latestVisit?.NextVisitPlan),
            ActiveOrthoSummary = activeOrthoSummary,
            ActiveSurgerySummary = activeSurgerySummary,
            MedicalAlerts = BuildMedicalAlerts(medicalHistory),
        };

        // CORE-PAT-019: financial fields remain null unless the granular,
        // owner-configurable permission is granted. Clinical fields are stripped
        // from the final response for reception and other non-clinical roles.
        var canViewFinanceTotals = !patientAccess.IsDoctor
            && await CanViewFinanceAsync("finance.patient_balance");
        if (canViewFinanceTotals)
        {
            var financeSummary = await financeReadService.GetPatientFinanceSummaryAsync(id);
            summary.TotalPaid = financeSummary.TotalPaid;
            summary.TotalOutstanding = financeSummary.OutstandingBalance;
            summary.UnbilledVisitsAmount = financeSummary.UnbilledVisitsAmount;

            await audit.LogAsync(AuditAction.View, "PatientFinanceSummary", id,
                newData: new { role = currentUser.Role?.ToString() });
        }

        return Ok(currentUser.IsAdmin || patientAccess.IsDoctor
            ? summary
            : ToOperationalSummary(summary));
    }

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    internal static List<string> BuildMedicalAlerts(MedicalHistory? history)
    {
        if (history == null) return [];

        var alerts = new List<string>();
        void Add(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                alerts.Add($"{label}: {value.Trim()}");
        }

        Add("حساسية أدوية", history.DrugAllergies);
        Add("أمراض مزمنة", history.ChronicDiseases);
        Add("أدوية حالية", history.CurrentMedications);
        if (history.BleedingDisorders) alerts.Add("اضطرابات نزف");
        if (history.TmjProblems) alerts.Add("مشكلات مفصل الفك");

        // CORE-PAT-031: IsPregnant is a tri-state value (Yes / No / N/A).
        // Treat only explicit affirmative values as a clinical alert. The old
        // negative-list logic classified N/A (لا ينطبق) and unknown legacy values
        // as pregnancy, creating a false safety warning in the patient summary.
        var pregnancy = history.IsPregnant?.Trim();
        if (pregnancy != null && (
            pregnancy.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || pregnancy.Equals("true", StringComparison.OrdinalIgnoreCase)
            || pregnancy.Equals("نعم", StringComparison.OrdinalIgnoreCase)
            || pregnancy.Equals("حامل", StringComparison.OrdinalIgnoreCase)
            || pregnancy.Equals("يوجد حمل", StringComparison.OrdinalIgnoreCase)))
        {
            alerts.Add("حمل");
        }

        return alerts.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    // ── Timeline ──────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/timeline")]
    public async Task<IActionResult> GetTimeline(Guid id)
    {
        var denied = await DenyIfDoctorCannotAccess(id);
        if (denied != null) return denied;

        var patient = await service.GetByIdAsync(id);
        if (patient == null) return NotFound(new { message = "المريض غير موجود" });

        var canReadClinical = currentUser.IsAdmin || patientAccess.IsDoctor;

        var appointmentEvents = await db.Appointments
            .Where(a => a.PatientId == id)
            .Include(a => a.Doctor)
            .OrderByDescending(a => a.AppointmentDate).ThenByDescending(a => a.StartTime)
            .Select(a => new
            {
                type = "appointment",
                id = a.Id,
                date = a.AppointmentDate.ToString("yyyy-MM-dd"),
                title = a.AppointmentType,
                description = $"{a.Doctor.Name} · {a.StartTime:HH\\:mm}",
                status = a.Status.ToString()
            })
            .Take(50)
            .ToListAsync();

        var visitEvents = await db.Visits
            .Where(v => canReadClinical && v.PatientId == id)
            .Include(v => v.Doctor)
            .OrderByDescending(v => v.VisitDate)
            .Select(v => new
            {
                type = "visit",
                id = v.Id,
                date = v.VisitDate.ToString("yyyy-MM-dd"),
                title = "زيارة سريرية",
                description = v.Doctor != null
                    ? (v.Diagnosis != null ? $"{v.Doctor.Name} · {v.Diagnosis}" : $"{v.Doctor.Name} · {(v.VisitType ?? "استشارة")}")
                    : (v.Diagnosis ?? v.VisitType ?? "زيارة"),
                status = (string?)null
            })
            .Take(50)
            .ToListAsync();

        // Referral events — show referrals sent/received for this patient
        var referralEvents = await db.InternalReferrals
            .Where(r => canReadClinical && r.PatientId == id && r.IsActive)
            .Include(r => r.FromDoctor)
            .Include(r => r.ToDoctor)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                type = "referral",
                id = r.Id,
                date = r.CreatedAt.ToString("yyyy-MM-dd"),
                title = r.Status == "pending" ? "إحالة جديدة" : r.Status == "accepted" ? "إحالة مقبولة" : "إحالة مكتملة",
                description = r.FromDoctor != null && r.ToDoctor != null
                    ? $"من {r.FromDoctor.Name} إلى {r.ToDoctor.Name}"
                    : "إحالة داخلية",
                status = r.Status
            })
            .Take(50)
            .ToListAsync();
        var allEvents = appointmentEvents
            .Cast<object>()
            .Concat(visitEvents)
            .Concat(referralEvents)
            .OrderByDescending(e => ((dynamic)e).date)
            .Take(50)
            .ToList();

        return Ok(allEvents);
    }

    // CORE-PAT-047: removed dead GET {id}/portal-credentials — zero frontend
    // callers (verified: grep across frontend/src). The live path is
    // PatientPortalController's GET /api/portal/credentials/{patientId},
    // which shares the same IPatientPortalService.GetPatientCredentialsAsync.

    // ── Account statement (granular finance permission) ───────────────────────

    [HttpGet("{id:guid}/account-statement")]
    [Authorize(Policy = "FinanceAccess")]
    public async Task<IActionResult> GetAccountStatement(Guid id)
    {
        // CORE-PAT-019: FinanceAccess is only the coarse role gate. The exact,
        // owner-configurable permission is required before returning contracts,
        // payments and balances from the detailed statement.
        if (!await CanViewFinanceAsync("finance.account_statement"))
            return DenyFinance();

        var result = await financeReadService.GetAccountStatementAsync(id);
        return result == null ? NotFound(new { message = "المريض غير موجود" }) : Ok(result);
    }

    // ── Mapping helpers ───────────────────────────────────────────────────────

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

    private static PatientClinicalDto ToClinicalDto(PatientProfileDto p) => new()
    {
        Id = p.Id,
        PatientNumber = p.PatientNumber,
        FirstName = p.FirstName,
        MiddleName = p.MiddleName,
        LastName = p.LastName,
        DateOfBirth = p.DateOfBirth,
        Gender = p.Gender,
        Age = p.Age,
        Occupation = p.Occupation,
        PrimaryDoctorId = p.PrimaryDoctorId,
        PrimaryDoctorName = p.PrimaryDoctorName,
        BranchId = p.BranchId,
        BranchName = p.BranchName,
        CreatedAt = p.CreatedAt,
        IsActive = p.IsActive,
        MedicalHistory = p.MedicalHistory,
        DentalHistory = p.DentalHistory,
        IsLimitedView = true,
    };

    private static PatientOperationalDto ToOperationalDto(PatientProfileDto p) => new()
    {
        Id = p.Id,
        PatientNumber = p.PatientNumber,
        FirstName = p.FirstName,
        MiddleName = p.MiddleName,
        LastName = p.LastName,
        DateOfBirth = p.DateOfBirth,
        Gender = p.Gender,
        Age = p.Age,
        Phone = p.Phone,
        Email = p.Email,
        WhatsApp = p.WhatsApp,
        Address = p.Address,
        PrimaryDoctorId = p.PrimaryDoctorId,
        PrimaryDoctorName = p.PrimaryDoctorName,
        BranchId = p.BranchId,
        BranchName = p.BranchName,
        CreatedAt = p.CreatedAt,
        IsActive = p.IsActive,
    };

    private static PatientSummaryDto ToOperationalSummary(PatientSummaryDto summary) => new()
    {
        TotalAppointments = summary.TotalAppointments,
        CompletedAppointments = summary.CompletedAppointments,
        NextAppointmentDate = summary.NextAppointmentDate,
        NextAppointmentTime = summary.NextAppointmentTime,
        NextAppointmentType = summary.NextAppointmentType,
        NextAppointmentDoctor = summary.NextAppointmentDoctor,
        TotalPaid = summary.TotalPaid,
        TotalOutstanding = summary.TotalOutstanding,
        UnbilledVisitsAmount = summary.UnbilledVisitsAmount,
    };
}
