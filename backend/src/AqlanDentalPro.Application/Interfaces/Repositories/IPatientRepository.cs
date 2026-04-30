using AqlanDentalPro.Application.DTOs.Common;
using AqlanDentalPro.Domain.Entities;

namespace AqlanDentalPro.Application.Interfaces.Repositories;

public interface IPatientRepository : IGenericRepository<Patient>
{
    Task<PaginatedResponse<Patient>> SearchAsync(string? search, int page, int pageSize, Guid? branchId, string? gender = null, Guid? doctorId = null, string? status = "active");
    Task<Patient?> GetWithHistoriesAsync(Guid id);
    Task<Patient?> GetArchivedByIdAsync(Guid id);
    Task<Patient?> FindByNormalizedPhoneAsync(string normalizedPhone, Guid? excludeId = null);
    Task<string> GeneratePatientNumberAsync(string prefix);
}
