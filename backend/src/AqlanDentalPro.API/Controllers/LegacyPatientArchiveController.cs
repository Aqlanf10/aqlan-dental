using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/patients/{patientId:guid}/legacy-archive")]
[Authorize(Policy = "AdminOnly")]
public class LegacyPatientArchiveController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid patientId)
    {
        if (!await db.Patients.IgnoreQueryFilters().AnyAsync(p => p.Id == patientId))
            return NotFound(new { message = "Patient not found." });

        var treatments = await db.LegacyTreatmentArchives
            .Where(x => x.PatientId == patientId)
            .OrderByDescending(x => x.TreatmentDate)
            .ThenBy(x => x.ServiceName)
            .Select(x => new
            {
                x.Id,
                x.TreatmentDate,
                x.DocumentType,
                x.ServiceName,
                x.Description,
                x.LineTotal,
                x.DiscountAmount,
                x.DoctorName,
                x.IsOrthodonticService
            })
            .ToListAsync();

        var financialEntries = await db.LegacyFinancialArchiveEntries
            .Where(x => x.PatientId == patientId)
            .OrderByDescending(x => x.EntryDate)
            .Select(x => new
            {
                x.Id,
                x.EntryDate,
                x.AccountName,
                x.Description,
                x.DebitAmount,
                x.CreditAmount,
                x.ReconciliationStatus
            })
            .ToListAsync();

        var appointments = await db.LegacyAppointmentArchives
            .Where(x => x.PatientId == patientId)
            .OrderByDescending(x => x.AppointmentAt)
            .Select(x => new
            {
                x.Id,
                x.AppointmentAt,
                x.ArchiveType,
                x.Description,
                x.Notes
            })
            .ToListAsync();

        var linkedRecords = await db.LegacyLinkedArchiveRecords
            .Where(x => x.PatientId == patientId)
            .OrderByDescending(x => x.DateValue01)
            .Select(x => new
            {
                x.Id,
                x.SourceTable,
                x.Classification,
                x.LegacyTypeId,
                x.DateValue01,
                x.DateValue02,
                x.NumberValue01,
                x.AccountName,
                x.Notes
            })
            .ToListAsync();

        return Ok(new
        {
            appointments,
            treatments,
            financialEntries,
            linkedRecords,
            summary = new
            {
                appointmentCards = appointments.Count,
                treatmentLines = treatments.Count,
                treatmentValue = treatments.Sum(x => x.LineTotal),
                financialEntryLines = financialEntries.Count,
                debitTotal = financialEntries.Sum(x => x.DebitAmount),
                creditTotal = financialEntries.Sum(x => x.CreditAmount),
                unclassifiedLinkedRecords = linkedRecords.Count,
                balanceAffecting = false
            }
        });
    }
}
