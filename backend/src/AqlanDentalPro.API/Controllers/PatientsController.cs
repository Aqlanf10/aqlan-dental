using AqlanDentalPro.Application.DTOs.Patients;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Infrastructure.Services;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/patients")]
[Authorize(Policy = "StaffOnly")]
public class PatientsController(
    PatientService service,
    AppDbContext db,
    IPatientPortalService portalService,
    FinanceService financeService,
    ICurrentUserService currentUser,
    IPatientAccessService patientAccess,
    IAuditService audit,
    ILogger<PatientsController> logger) : ControllerBase
{
    // ── Helpers ───────────────────────────────────────────────────────────────

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
                logger.LogError(ex, "GetAccessiblePatientIdsAsync failed for user {UserId} role {Role}",
                    patientAccess.IsDoctor, User.Identity?.Name);
                return StatusCode(500, new
                {
                    title = "Patient access query failed",
                    detail = ex.InnerException?.Message ?? ex.Message,
                    stackTrace = ex.StackTrace?.Split('\n').Take(3)
                });
            }

            if (accessible == null || accessible.Count == 0)
                return Ok(new { items = Array.Empty<object>(), total = 0, page, pageSize });

            // Pass the full accessible set — covers primary, appointment, visit, step, referral links.
            var result = await service.GetListAsync(search, page, pageSize, gender,
                doctorId: null, status, allowedPatientIds: accessible);
            return Ok(result);
        }

        var fullResult = await service.GetListAsync(search, page, pageSize, gender, doctorId, status);
        return Ok(fullResult);
    }

    // ── Single patient ────────────────────────────────────────────────────────

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var denied = await DenyIfDoctorCannotAccess(id);
        if (denied != null) return denied;

        var patient = await service.GetByIdAsync(id);
        if (patient == null) return NotFound(new { message = "المريض غير موجود" });

        // Doctors receive a clinical-only view without contact/finance fields.
        if (patientAccess.IsDoctor)
            return Ok(ToClinicalDto(patient));

        // Non-doctor roles receive the full profile including contact info — log for compliance.
        await audit.LogAsync(AuditAction.View, "PatientContactInfo", id,
            newData: new { role = currentUser.Role?.ToString() });

        return Ok(patient);
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
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("IX_Patients_NormalizedPhone") == true
                                         || ex.InnerException?.Message?.Contains("IX_Patients_NormalizedWhatsApp") == true
                                         || ex.InnerException?.Message?.Contains("IX_Patients_Phone") == true
                                         || ex.InnerException?.Message?.Contains("IX_Patients_WhatsApp") == true
                                         || ex.InnerException?.Message?.Contains("IX_Patients_PatientNumber") == true)
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
                .Select(p => new { p.Id, p.PatientNumber, FullName = p.FirstName + " " + p.MiddleName + " " + p.LastName, p.Phone, MatchType = "phone" })
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
                .Select(p => new { p.Id, p.PatientNumber, FullName = p.FirstName + " " + p.MiddleName + " " + p.LastName, p.Phone, MatchType = "whatsapp" })
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
                .Select(p => new { p.Id, p.PatientNumber, FullName = p.FirstName + " " + p.MiddleName + " " + p.LastName, p.Phone, MatchType = "patientNumber" })
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
                .Select(p => new { p.Id, p.PatientNumber, FullName = p.FirstName + " " + p.MiddleName + " " + p.LastName, p.Phone, MatchType = "name" })
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
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("IX_Patients_NormalizedPhone") == true
                                         || ex.InnerException?.Message?.Contains("IX_Patients_NormalizedWhatsApp") == true
                                         || ex.InnerException?.Message?.Contains("IX_Patients_Phone") == true
                                         || ex.InnerException?.Message?.Contains("IX_Patients_WhatsApp") == true
                                         || ex.InnerException?.Message?.Contains("IX_Patients_PatientNumber") == true)
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
    public async Task<IActionResult> GetMedicalHistory(Guid id)
    {
        var denied = await DenyIfDoctorCannotAccess(id);
        if (denied != null) return denied;

        var patient = await service.GetByIdAsync(id);
        if (patient == null) return NotFound(new { message = "المريض غير موجود" });
        return Ok(patient.MedicalHistory ?? new MedicalHistoryDto());
    }

    [HttpPut("{id:guid}/medical-history")]
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
    public async Task<IActionResult> GetDentalHistory(Guid id)
    {
        var denied = await DenyIfDoctorCannotAccess(id);
        if (denied != null) return denied;

        var patient = await service.GetByIdAsync(id);
        if (patient == null) return NotFound(new { message = "المريض غير موجود" });
        return Ok(patient.DentalHistory ?? new DentalHistoryDto());
    }

    [HttpPut("{id:guid}/dental-history")]
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

    // ── Summary (financial data hidden from doctors) ──────────────────────────

    [HttpGet("{id:guid}/summary")]
    public async Task<IActionResult> GetSummary(Guid id)
    {
        var denied = await DenyIfDoctorCannotAccess(id);
        if (denied != null) return denied;

        var exists = await db.Patients.AnyAsync(p => p.Id == id);
        if (!exists) return NotFound(new { message = "المريض غير موجود" });

        var totalAppointments = await db.Appointments.CountAsync(a => a.PatientId == id);
        var completedAppointments = await db.Appointments.CountAsync(a => a.PatientId == id && a.Status == Domain.Enums.AppointmentStatus.Completed);
        var activeOrthoCases = await db.OrthoCases.CountAsync(o => o.PatientId == id && o.Status == OrthoCaseStatus.Active);
        var prescriptionsCount = await db.Prescriptions.CountAsync(p => p.PatientId == id);

        // Financial totals are restricted to non-doctor roles.
        if (patientAccess.IsDoctor)
        {
            return Ok(new
            {
                totalAppointments,
                completedAppointments,
                activeOrthoCases,
                prescriptionsCount,
                totalPaid = (decimal?)null,
                totalOutstanding = (decimal?)null,
            });
        }

        var totalPaid = await db.Payments.Where(p => p.PatientId == id).SumAsync(p => (decimal?)p.Amount) ?? 0;
        var totalOutstanding = await db.Contracts
            .Where(c => c.PatientId == id && c.Status == ContractStatus.Active)
            .Include(c => c.Payments)
            .Select(c => c.TotalAmount - c.DiscountAmount - c.Payments.Sum(p => p.Amount))
            .SumAsync(r => (decimal?)r) ?? 0;

        // Audit: non-doctor viewed financial summary.
        await audit.LogAsync(AuditAction.View, "PatientFinanceSummary", id,
            newData: new { role = currentUser.Role?.ToString() });

        return Ok(new
        {
            totalAppointments,
            completedAppointments,
            activeOrthoCases,
            totalPaid,
            totalOutstanding,
            prescriptionsCount
        });
    }

    // ── Timeline ──────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/timeline")]
    public async Task<IActionResult> GetTimeline(Guid id)
    {
        var denied = await DenyIfDoctorCannotAccess(id);
        if (denied != null) return denied;

        var patient = await service.GetByIdAsync(id);
        if (patient == null) return NotFound(new { message = "المريض غير موجود" });

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
            .Where(v => v.PatientId == id)
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
            .Where(r => r.PatientId == id && r.IsActive)
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

    // ── Portal credentials (reception / admin only) ───────────────────────────

    [HttpGet("{id:guid}/portal-credentials")]
    [Authorize(Policy = "AdminOrReception")]
    public async Task<IActionResult> GetPortalCredentials(Guid id)
    {
        var exists = await db.Patients.AnyAsync(p => p.Id == id);
        if (!exists) return NotFound(new { message = "المريض غير موجود" });

        var creds = await portalService.GetPatientCredentialsAsync(id);
        if (creds == null) return NotFound(new { message = "لا يوجد حساب بوابة لهذا المريض" });
        return Ok(creds);
    }

    // ── Account statement (finance only) ──────────────────────────────────────

    [HttpGet("{id:guid}/account-statement")]
    [Authorize(Policy = "FinanceAccess")]
    public async Task<IActionResult> GetAccountStatement(Guid id)
    {
        var result = await financeService.GetAccountStatementAsync(id);
        return result == null ? NotFound(new { message = "المريض غير موجود" }) : Ok(result);
    }

    // ── Mapping helpers ───────────────────────────────────────────────────────

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
}
