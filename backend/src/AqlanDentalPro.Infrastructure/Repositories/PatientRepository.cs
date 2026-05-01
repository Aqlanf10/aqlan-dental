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
        string? gender = null, Guid? doctorId = null, string? status = null)
    {
        var query = DbSet
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

    public async Task<string> GeneratePatientNumberAsync(string prefix)
    {
        var year = DateTime.Today.Year;
        var baseNum = $"{prefix}-{year}-";
        
        // Use retry loop to handle concurrent inserts
        for (int attempt = 0; attempt < 10; attempt++)
        {
            var count = await DbSet
                .IgnoreQueryFilters()
                .CountAsync(p => p.PatientNumber.StartsWith(baseNum));

            var number = $"{baseNum}{(count + 1 + attempt):D3}";
            
            // Check if this number already exists
            var exists = await DbSet
                .IgnoreQueryFilters()
                .AnyAsync(p => p.PatientNumber == number);
            
            if (!exists) return number;
        }
        
        // Fallback: use max + 1
        var maxNumber = await DbSet
            .IgnoreQueryFilters()
            .Where(p => p.PatientNumber.StartsWith(baseNum))
            .OrderByDescending(p => p.PatientNumber)
            .Select(p => p.PatientNumber)
            .FirstOrDefaultAsync();
        
        if (int.TryParse(maxNumber?.Split('-').Last(), out var maxSeq))
            return $"{baseNum}{(maxSeq + 1):D3}";
        
        return $"{baseNum}001";
    }
}
