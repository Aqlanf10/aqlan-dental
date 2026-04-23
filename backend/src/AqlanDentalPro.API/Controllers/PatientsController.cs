using AqlanDentalPro.Application.DTOs.Patients;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/patients")]
[Authorize]
public class PatientsController(PatientService service, AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await service.GetListAsync(search, page, pageSize);
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
        var patient = await service.CreateAsync(req);
        return CreatedAtAction(nameof(GetById), new { id = patient.Id }, patient);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PatientProfileDto>> Update(Guid id, [FromBody] UpdatePatientRequest req)
    {
        var result = await service.UpdateAsync(id, req);
        return result == null ? NotFound(new { message = "المريض غير موجود" }) : Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var success = await service.SoftDeleteAsync(id);
        return success ? NoContent() : NotFound(new { message = "المريض غير موجود" });
    }

    [HttpGet("{id:guid}/medical-history")]
    public async Task<IActionResult> GetMedicalHistory(Guid id)
    {
        var patient = await service.GetByIdAsync(id);
        if (patient == null) return NotFound(new { message = "المريض غير موجود" });
        return Ok(patient.MedicalHistory);
    }

    [HttpPut("{id:guid}/medical-history")]
    public async Task<IActionResult> UpdateMedicalHistory(Guid id, [FromBody] MedicalHistoryDto dto)
    {
        var req = new UpdatePatientRequest();
        var patient = await service.GetByIdAsync(id);
        if (patient == null) return NotFound(new { message = "المريض غير موجود" });

        var updateReq = new UpdatePatientRequest
        {
            FirstName = patient.FirstName,
            MiddleName = patient.MiddleName,
            LastName = patient.LastName,
            DateOfBirth = patient.DateOfBirth,
            Gender = patient.Gender,
            Phone = patient.Phone,
            WhatsApp = patient.WhatsApp,
            Address = patient.Address,
            Occupation = patient.Occupation,
            ReferralSource = patient.ReferralSource,
            PrimaryDoctorId = patient.PrimaryDoctorId,
            MedicalHistory = dto
        };
        var result = await service.UpdateAsync(id, updateReq);
        return result == null ? NotFound() : Ok(result.MedicalHistory);
    }

    [HttpGet("{id:guid}/dental-history")]
    public async Task<IActionResult> GetDentalHistory(Guid id)
    {
        var patient = await service.GetByIdAsync(id);
        if (patient == null) return NotFound(new { message = "المريض غير موجود" });
        return Ok(patient.DentalHistory);
    }

    [HttpPut("{id:guid}/dental-history")]
    public async Task<IActionResult> UpdateDentalHistory(Guid id, [FromBody] DentalHistoryDto dto)
    {
        var patient = await service.GetByIdAsync(id);
        if (patient == null) return NotFound(new { message = "المريض غير موجود" });

        var updateReq = new UpdatePatientRequest
        {
            FirstName = patient.FirstName,
            MiddleName = patient.MiddleName,
            LastName = patient.LastName,
            DateOfBirth = patient.DateOfBirth,
            Gender = patient.Gender,
            Phone = patient.Phone,
            WhatsApp = patient.WhatsApp,
            Address = patient.Address,
            Occupation = patient.Occupation,
            ReferralSource = patient.ReferralSource,
            PrimaryDoctorId = patient.PrimaryDoctorId,
            DentalHistory = dto
        };
        var result = await service.UpdateAsync(id, updateReq);
        return result == null ? NotFound() : Ok(result.DentalHistory);
    }

    [HttpGet("{id:guid}/timeline")]
    public async Task<IActionResult> GetTimeline(Guid id)
    {
        var patient = await service.GetByIdAsync(id);
        if (patient == null) return NotFound(new { message = "المريض غير موجود" });

        var appointments = await db.Appointments
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

        return Ok(appointments);
    }
}
