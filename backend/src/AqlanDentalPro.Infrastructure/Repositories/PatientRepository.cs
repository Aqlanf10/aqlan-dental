using AqlanDentalPro.Application.DTOs.Common;
using AqlanDentalPro.Application.Interfaces.Repositories;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace AqlanDentalPro.Infrastructure.Repositories;

public class PatientRepository(AppDbContext context)
    : GenericRepository<Patient>(context), IPatientRepository
{
    public async Task<Patient?> FirstOrDefaultAsync(Expression<Func<Patient, bool>> predicate) =>
        await DbSet.FirstOrDefaultAsync(predicate);
    public async Task<PaginatedResponse<Patient>> SearchAsync(
        string? search, int page, int pageSize, Guid? branchId,
        string? gender = null, Guid? doctorId = null, string? status = "active")
    {
        pageSize = Math.Max(1, Math.Min(pageSize, 100));
        var baseQuery = status?.ToLower() switch
        {
            "archived" => DbSet.IgnoreQueryFilters().Where(p => !p.IsActive),
            "all"      => DbSet.IgnoreQueryFilters(),
            _          => DbSet.AsQueryable(), // "active" — global filter applies
        };

        var query = baseQuery
            .Include(p => p.PrimaryDoctor)
            .Include(p => p.Branch)
            .AsQueryable();

        // Status filtering
        if (status == "archived")
        {
            query = query.IgnoreQueryFilters().Where(p => !p.IsActive);
        }
        else if (status == "all")
        {
            query = query.IgnoreQueryFilters();
        }
        // else status == null or "active": global filter already applies (IsActive = true)

        if (branchId.HasValue)
            query = query.Where(p => p.BranchId == branchId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            // Normalize search term for phone matching
            var normalizedTerm = AqlanDentalPro.Application.Services.PhoneNormalizer.Normalize(term);
            query = query.Where(p =>
                p.FirstName.ToLower().Contains(term) ||
                p.LastName.ToLower().Contains(term) ||
                (p.MiddleName != null && p.MiddleName.ToLower().Contains(term)) ||
                p.PatientNumber.ToLower().Contains(term) ||
                (p.Phone != null && p.Phone.Contains(term)) ||
                (normalizedTerm != null && (
                    (p.NormalizedPhone != null && p.NormalizedPhone.Contains(normalizedTerm)) ||
                    (p.NormalizedWhatsApp != null && p.NormalizedWhatsApp.Contains(normalizedTerm))
                )));
        }

        if (!string.IsNullOrWhiteSpace(gender) &&
            Enum.TryParse<Domain.Enums.Gender>(gender, true, out var parsedGender))
            query = query.Where(p => p.Gender == parsedGender);

        if (doctorId.HasValue)
            query = query.Where(p => p.PrimaryDoctorId == doctorId);

        var total = await query.CountAsync();

        var data = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedResponse<Patient>
        {
            Data = data,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<Patient?> GetWithHistoriesAsync(Guid id) =>
        await DbSet
            .Include(p => p.MedicalHistory)
            .Include(p => p.DentalHistory)
            .Include(p => p.PrimaryDoctor)
            .Include(p => p.Branch)
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Patient?> GetWithHistoriesIgnoreFiltersAsync(Guid id) =>
        await DbSet
            .IgnoreQueryFilters()
            .Include(p => p.MedicalHistory)
            .Include(p => p.DentalHistory)
            .Include(p => p.PrimaryDoctor)
            .Include(p => p.Branch)
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Patient?> GetByIdIgnoreFiltersAsync(Guid id) =>
        await DbSet.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Patient?> GetArchivedByIdAsync(Guid id) =>
        await DbSet
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Patient?> FindByNormalizedPhoneAsync(string normalizedPhone, Guid? excludeId = null) =>
        await DbSet
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p =>
                p.NormalizedPhone == normalizedPhone &&
                (excludeId == null || p.Id != excludeId));

    public async Task<string> GeneratePatientNumberAsync(string prefix)
    {
        var year = DateTime.Today.Year;
        var yearPrefix = $"{prefix}-{year}-";

        // Use MAX of the numeric suffix so gaps (soft-deleted rows) don't cause reuse
        var maxSuffix = await DbSet
            .IgnoreQueryFilters()
            .Where(p => p.PatientNumber.StartsWith(yearPrefix))
            .Select(p => p.PatientNumber.Substring(yearPrefix.Length))
            .ToListAsync()
            .ContinueWith(t => t.Result
                .Select(s => int.TryParse(s, out var n) ? n : 0)
                .DefaultIfEmpty(0)
                .Max());

        return $"{yearPrefix}{(maxSuffix + 1):D3}";
    }

    public async Task<Dictionary<Guid, DateTime?>> GetLastVisitDatesAsync(IEnumerable<Guid> patientIds)
    {
        var ids = patientIds.ToList();
        if (ids.Count == 0) return new Dictionary<Guid, DateTime?>();

        var result = new Dictionary<Guid, DateTime?>();

        // Batch-load last appointment date (excluding Cancelled and NoShow)
        var lastAppointmentDates = await Context.Set<Appointment>()
            .Where(a => ids.Contains(a.PatientId)
                     && a.Status != AppointmentStatus.Cancelled
                     && a.Status != AppointmentStatus.NoShow)
            .GroupBy(a => a.PatientId)
            .Select(g => new { PatientId = g.Key, LastDate = g.Max(a => (DateTime?)a.AppointmentDate.ToDateTime(TimeOnly.MinValue)) })
            .ToDictionaryAsync(x => x.PatientId, x => x.LastDate);

        // Batch-load last visit date
        var lastVisitDates = await Context.Set<Visit>()
            .Where(v => ids.Contains(v.PatientId))
            .GroupBy(v => v.PatientId)
            .Select(g => new { PatientId = g.Key, LastDate = g.Max(v => (DateTime?)v.VisitDate.ToDateTime(TimeOnly.MinValue)) })
            .ToDictionaryAsync(x => x.PatientId, x => x.LastDate);

        foreach (var id in ids)
        {
            var apptDate = lastAppointmentDates.GetValueOrDefault(id);
            var visitDate = lastVisitDates.GetValueOrDefault(id);

            if (apptDate.HasValue && visitDate.HasValue)
                result[id] = apptDate.Value > visitDate.Value ? apptDate : visitDate;
            else
                result[id] = apptDate ?? visitDate;
        }

        return result;
    }
}
