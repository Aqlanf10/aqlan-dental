using AqlanDentalPro.Domain.Entities;

namespace AqlanDentalPro.Application.Interfaces.Repositories;

public interface IUserRepository : IGenericRepository<User>
{
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByIdWithDoctorAsync(Guid id);
}
