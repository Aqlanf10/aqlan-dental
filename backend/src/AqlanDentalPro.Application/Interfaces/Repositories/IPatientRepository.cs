using AqlanDentalPro.Application.DTOs.Common;
using AqlanDentalPro.Domain.Entities;
using System.Linq.Expressions;

namespace AqlanDentalPro.Application.Interfaces.Repositories;

public interface IPatientRepository : IGenericRepository<Patient>
{
    Task<PaginatedResponse<Patient>> SearchAsync(string? search, int page, int pageSize, Guid? branchId, string? gender = null, Guid? doctorId = null);
    Task<Patient?> GetWithHistoriesAsync(Guid id);
    Task<string> GeneratePatientNumberAsync(string prefix);
    Task<Patient?> FirstOrDefaultAsync(Expression<Func<Patient, bool>> predicate);
}
