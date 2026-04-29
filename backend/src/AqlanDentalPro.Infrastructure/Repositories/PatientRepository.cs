using AqlanDentalPro.Application.DTOs.Common;
using AqlanDentalPro.Application.Interfaces.Repositories;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Infrastructure.Repositories;

public class PatientRepository(AppDbContext context)
    : GenericRepository<Patient>(context), IPatientRepository
{
    public async Task<PaginatedResponse<Patient>> SearchAsync(
        string? search, int page, int pageSize, Guid? branchId,
        string? gender = null, Guid? doctorId = null, bool? isActive = null)
    {
        var query = DbSet
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

        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

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

    public async Task<string> GeneratePatientNumberAsync(string prefix)
    {
        var year = DateTime.Today.Year;
        var count = await DbSet
            .IgnoreQueryFilters()
            .CountAsync(p => p.PatientNumber.StartsWith($"{prefix}-{year}-"));

        return $"{prefix}-{year}-{(count + 1):D3}";
    }
}
