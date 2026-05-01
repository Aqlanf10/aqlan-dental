using AqlanDentalPro.Application.DTOs.Common;
using AqlanDentalPro.Application.Interfaces.Repositories;
using AqlanDentalPro.Domain.Entities;
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

        if (branchId.HasValue)
            query = query.Where(p => p.BranchId == branchId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(p =>
                p.FirstName.ToLower().Contains(term) ||
                p.LastName.ToLower().Contains(term) ||
                (p.MiddleName != null && p.MiddleName.ToLower().Contains(term)) ||
                p.PatientNumber.ToLower().Contains(term) ||
                (p.Phone != null && p.Phone.Contains(term)));
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
}
