using AqlanDentalPro.Application.DTOs.Patients;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/patients")]
[Authorize]
public class PatientsController(PatientService service, AppDbContext db, IPatientPortalService portalService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? gender = null,
        [FromQuery] Guid? doctorId = null,
        [FromQuery] string? status = "active")
    {
        var result = await service.GetListAsync(search, page, pageSize, gender, doctorId, status);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PatientProfileDto>> GetById(Guid id)
    {
        var patient = await service.GetByIdAsync(id);
        return patient == null ? NotFound(new { message = "المريض غير موجود" }) : Ok(patient);
    }

    [HttpPost]
    public async Task<ActionResult<PatientProfileDto>> Create([FromBody] CreatePatientRequest req)
    {
        try
        {
            var patient = await service.CreateAsync(req);
            return CreatedAtAction(nameof(GetById), new { id = patient.Id }, patient);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("IX_Patients_NormalizedPhone") == true
                                         || ex.InnerException?.Message?.Contains("IX_Patients_NormalizedWhatsApp") == true
                                         || ex.InnerException?.Message?.Contains("IX_Patients_Phone") == true
                                         || ex.InnerException?.Message?.Contains("IX_Patients_WhatsApp") == true
                                         || ex.InnerException?.Message?.Contains("IX_Patients_PatientNumber") == true)
        {
            return Conflict(new { message = "البيانات مكررة — رقم الهاتف أو الواتساب أو رقم الملف موجود مسبقاً" });
        }
    }

    [HttpGet("check-duplicate")]
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

        // Normalize phone for checking
        var normalizedPhone = PhoneNormalizer.Normalize(phone);
        var normalizedWhatsApp = PhoneNormalizer.Normalize(whatsApp);

        // Check by normalized phone
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

        // Check by normalized WhatsApp
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

        // Check by patient number
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

        // Check by similar name + date of birth
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

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PatientProfileDto>> Update(Guid id, [FromBody] UpdatePatientRequest req)
    {
        try
        {
            var result = await service.UpdateAsync(id, req);
            return result == null ? NotFound(new { message = "المريض غير موجود" }) : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("IX_Patients_NormalizedPhone") == true
                                         || ex.InnerException?.Message?.Contains("IX_Patients_NormalizedWhatsApp") == true
                                         || ex.InnerException?.Message?.Contains("IX_Patients_Phone") == true
                                         || ex.InnerException?.Message?.Contains("IX_Patients_WhatsApp") == true
                                         || ex.InnerException?.Message?.Contains("IX_Patients_PatientNumber") == true)
        {
            return Conflict(new { message = "رقم الهاتف أو الواتساب مستخدم مسبقاً لمريض آخر." });
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var success = await service.SoftDeleteAsync(id);
        return success ? Ok(new { message = "تم أرشفة المريض بنجاح" }) : NotFound(new { message = "المريض غير موجود" });
    }

    [HttpPut("{id:guid}/archive")]
    [Authorize(Roles = "Admin,admin")]
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

    [HttpGet("{id:guid}/medical-history")]
    public async Task<IActionResult> GetMedicalHistory(Guid id)
    {
        var patient = await service.GetByIdAsync(id);
        if (patient == null) return NotFound(new { message = "المريض غير موجود" });
        // Return empty DTO instead of null when no history exists yet
        return Ok(patient.MedicalHistory ?? new MedicalHistoryDto());
    }

    [HttpPut("{id:guid}/medical-history")]
    public async Task<IActionResult> UpdateMedicalHistory(Guid id, [FromBody] MedicalHistoryDto dto)
    {
        try
        {
            var result = await service.UpsertMedicalHistoryAsync(id, dto);
            if (result == null) return NotFound(new { message = "المريض غير موجود" });
            return Ok(result);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "تعارض في تحديث السجل الطبي — حاول مرة أخرى" });
        }
        catch (DbUpdateException)
        {
            return StatusCode(500, new { message = "حدث خطأ أثناء حفظ التاريخ الطبي — حاول مرة أخرى لاحقاً" });
        }
    }

    [HttpGet("{id:guid}/dental-history")]
    public async Task<IActionResult> GetDentalHistory(Guid id)
    {
        var patient = await service.GetByIdAsync(id);
        if (patient == null) return NotFound(new { message = "المريض غير موجود" });
        // Return empty DTO instead of null when no history exists yet
        return Ok(patient.DentalHistory ?? new DentalHistoryDto());
    }

    [HttpPut("{id:guid}/dental-history")]
    public async Task<IActionResult> UpdateDentalHistory(Guid id, [FromBody] DentalHistoryDto dto)
    {
        try
        {
            var result = await service.UpsertDentalHistoryAsync(id, dto);
            if (result == null) return NotFound(new { message = "المريض غير موجود" });
            return Ok(result);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "تعارض في تحديث السجل السني — حاول مرة أخرى" });
        }
        catch (DbUpdateException)
        {
            return StatusCode(500, new { message = "حدث خطأ أثناء حفظ التاريخ السني — حاول مرة أخرى لاحقاً" });
        }
    }

    [HttpGet("{id:guid}/summary")]
    public async Task<IActionResult> GetSummary(Guid id)
    {
        var exists = await db.Patients.AnyAsync(p => p.Id == id);
        if (!exists) return NotFound(new { message = "المريض غير موجود" });

        var totalAppointments = await db.Appointments.CountAsync(a => a.PatientId == id);
        var completedAppointments = await db.Appointments.CountAsync(a => a.PatientId == id && a.Status == Domain.Enums.AppointmentStatus.Completed);
        var activeOrthoCases = await db.OrthoCases.CountAsync(o => o.PatientId == id && o.Status == "active");
        var totalPaid = await db.Payments.Where(p => p.PatientId == id).SumAsync(p => (decimal?)p.Amount) ?? 0;
        var totalOutstanding = await db.Contracts
            .Where(c => c.PatientId == id && c.Status == "active")
            .Include(c => c.Payments)
            .Select(c => c.TotalAmount - c.DiscountAmount - c.Payments.Sum(p => p.Amount))
            .SumAsync(r => (decimal?)r) ?? 0;
        var prescriptionsCount = await db.Prescriptions.CountAsync(p => p.PatientId == id);

        // ── Extended summary fields ──
        var lastVisit = await db.Visits
            .Where(v => v.PatientId == id)
            .OrderByDescending(v => v.VisitDate)
            .Select(v => new { v.VisitDate, v.Diagnosis, v.TreatmentDone, v.Doctor.Name })
            .FirstOrDefaultAsync();

        var nextAppointment = await db.Appointments
            .Where(a => a.PatientId == id && a.AppointmentDate >= DateOnly.FromDateTime(DateTime.Today))
            .OrderBy(a => a.AppointmentDate).ThenBy(a => a.StartTime)
            .Select(a => new { a.AppointmentDate, a.StartTime, a.AppointmentType, DoctorName = a.Doctor.Name })
            .FirstOrDefaultAsync();

        // Active treatment summary: latest visit with diagnosis + active ortho/surgery cases
        var latestDiagnosisVisit = await db.Visits
            .Where(v => v.PatientId == id && v.Diagnosis != null)
            .OrderByDescending(v => v.VisitDate)
            .Select(v => new { v.Diagnosis, v.NextVisitPlan, v.VisitDate })
            .FirstOrDefaultAsync();

        var activeOrthoSummary = await db.OrthoCases
            .Where(o => o.PatientId == id && o.Status == "active")
            .Select(o => new { o.CaseNumber, o.ApplianceType, o.StagePercentage })
            .ToListAsync();

        var activeSurgerySummary = await db.SurgeryCases
            .Where(s => s.PatientId == id && (s.Status == "scheduled" || s.Status == "in_progress"))
            .Select(s => new { s.CaseNumber, s.SurgeryType, s.Status })
            .ToListAsync();

        // Medical alerts from medical history
        var medicalAlerts = new List<string>();
        var medHistory = await db.MedicalHistories.FirstOrDefaultAsync(m => m.PatientId == id);
        if (medHistory != null)
        {
            if (medHistory.BleedingDisorders) medicalAlerts.Add("اضطرابات نزيف");
            if (medHistory.TmjProblems) medicalAlerts.Add("مشاكل المفصل الفكي");
            if (medHistory.IsPregnant == "Yes" || medHistory.IsPregnant == "yes") medicalAlerts.Add("حامل");
            if (!string.IsNullOrWhiteSpace(medHistory.DrugAllergies)) medicalAlerts.Add($"حساسية أدوية: {medHistory.DrugAllergies}");
            if (!string.IsNullOrWhiteSpace(medHistory.ChronicDiseases)) medicalAlerts.Add($"أمراض مزمنة: {medHistory.ChronicDiseases}");
        }

        // Chief complaint from dental history
        var dentalHistory = await db.DentalHistories.FirstOrDefaultAsync(d => d.PatientId == id);

        return Ok(new
        {
            totalAppointments,
            completedAppointments,
            activeOrthoCases,
            totalPaid,
            totalOutstanding,
            prescriptionsCount,
            // Extended fields
            lastVisitDate = lastVisit?.VisitDate.ToString("yyyy-MM-dd"),
            lastVisitDoctor = lastVisit?.Name,
            lastVisitDiagnosis = lastVisit?.Diagnosis,
            nextAppointmentDate = nextAppointment?.AppointmentDate.ToString("yyyy-MM-dd"),
            nextAppointmentTime = nextAppointment?.StartTime.ToString("HH:mm"),
            nextAppointmentType = nextAppointment?.AppointmentType,
            nextAppointmentDoctor = nextAppointment?.DoctorName,
            chiefComplaint = dentalHistory?.ChiefComplaint,
            currentDiagnosis = latestDiagnosisVisit?.Diagnosis,
            nextPlannedStep = latestDiagnosisVisit?.NextVisitPlan,
            activeOrthoSummary,
            activeSurgerySummary,
            medicalAlerts
        });
    }

    [HttpGet("{id:guid}/timeline")]
    public async Task<IActionResult> GetTimeline(Guid id)
    {
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
            .Take(30)
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
            .Take(30)
            .ToListAsync();

        var paymentEvents = await db.Payments
            .Where(p => p.PatientId == id)
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => new
            {
                type = "payment",
                id = p.Id,
                date = p.PaymentDate.ToString("yyyy-MM-dd"),
                title = "دفعة مالية",
                description = $"{p.Amount} ر.ي · {p.PaymentMethod}" + (p.ServiceDescription != null ? $" · {p.ServiceDescription}" : ""),
                status = (string?)null
            })
            .Take(20)
            .ToListAsync();

        var documentEvents = await db.Documents
            .Where(d => d.PatientId == id && d.IsActive)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new
            {
                type = "document",
                id = d.Id,
                date = d.CreatedAt.ToString("yyyy-MM-dd"),
                title = d.Title ?? "مستند",
                description = d.DocumentType ?? "مستند",
                status = d.Signed ? "signed" : (string?)null
            })
            .Take(15)
            .ToListAsync();

        var photoEvents = await db.ClinicalPhotos
            .Where(p => p.PatientId == id && p.IsActive)
            .OrderByDescending(p => p.PhotoDate)
            .Select(p => new
            {
                type = "photo",
                id = p.Id,
                date = p.PhotoDate.ToString("yyyy-MM-dd"),
                title = "صورة سريرية",
                description = (p.Category ?? "") + (p.PhotoType != null ? $" · {p.PhotoType}" : "") + (p.Stage != null ? $" · {p.Stage}" : ""),
                status = (string?)null
            })
            .Take(10)
            .ToListAsync();

        var radiographEvents = await db.Radiographs
            .Where(r => r.PatientId == id && r.IsActive)
            .OrderByDescending(r => r.XrayDate)
            .Select(r => new
            {
                type = "radiograph",
                id = r.Id,
                date = r.XrayDate.ToString("yyyy-MM-dd"),
                title = "أشعة سنية",
                description = (r.XrayType ?? "") + (r.ToothRelated != null ? $" · سن: {r.ToothRelated}" : ""),
                status = (string?)null
            })
            .Take(10)
            .ToListAsync();

        // Merge all events and sort by date descending
        var allEvents = appointmentEvents.Cast<object>()
            .Concat(visitEvents)
            .Concat(paymentEvents)
            .Concat(documentEvents)
            .Concat(photoEvents)
            .Concat(radiographEvents)
            .OrderByDescending(e => ((dynamic)e).date)
            .Take(50)
            .ToList();

        return Ok(allEvents);
    }

    [HttpGet("{id:guid}/portal-credentials")]
    [Authorize(Policy = "DoctorAccess")]
    public async Task<IActionResult> GetPortalCredentials(Guid id)
    {
        var exists = await db.Patients.AnyAsync(p => p.Id == id);
        if (!exists) return NotFound(new { message = "المريض غير موجود" });

        var creds = await portalService.GetPatientCredentialsAsync(id);
        if (creds == null) return NotFound(new { message = "لا يوجد حساب بوابة لهذا المريض" });
        return Ok(creds);
    }
}
