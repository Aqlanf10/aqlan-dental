using AqlanDentalPro.Application.Interfaces.Repositories;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Infrastructure.Repositories;

public class UserRepository(AppDbContext context)
    : GenericRepository<User>(context), IUserRepository
{
    public async Task<User?> GetByUsernameAsync(string username) =>
        await DbSet
            .Include(u => u.Doctor)
            .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

    public async Task<User?> GetByIdWithDoctorAsync(Guid id) =>
        await DbSet
            .Include(u => u.Doctor)
            .Include(u => u.Branch)
            .FirstOrDefaultAsync(u => u.Id == id && u.IsActive);
}
