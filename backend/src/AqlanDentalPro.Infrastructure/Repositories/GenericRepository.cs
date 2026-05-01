using AqlanDentalPro.Application.Interfaces.Repositories;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace AqlanDentalPro.Infrastructure.Repositories;

public class GenericRepository<T>(AppDbContext context) : IGenericRepository<T> where T : class
{
    protected readonly AppDbContext Context = context;
    protected readonly DbSet<T> DbSet = context.Set<T>();

    public async Task<T?> GetByIdAsync(Guid id) =>
        await DbSet.FindAsync(id);

    public async Task<IEnumerable<T>> GetAllAsync() =>
        await DbSet.ToListAsync();

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate) =>
        await DbSet.Where(predicate).ToListAsync();

    public async Task AddAsync(T entity) =>
        await DbSet.AddAsync(entity);

    public async Task AddRangeAsync(IEnumerable<T> entities) =>
        await DbSet.AddRangeAsync(entities);

    public void Update(T entity) =>
        DbSet.Update(entity);

    public void Remove(T entity) =>
        DbSet.Remove(entity);

    public void Detach(T entity) =>
        Context.Entry(entity).State = EntityState.Detached;

    public async Task AddChildAsync<TChild>(TChild entity) where TChild : class
    {
        // Add the child entity to the DbContext so EF Core tracks it as Added
        // This is used when creating new child entities (e.g., MedicalHistory, DentalHistory)
        // for an existing parent, where DbSet.Update(parent) would incorrectly mark them as Modified.
        await Context.Set<TChild>().AddAsync(entity);
    }

    public async Task<int> SaveChangesAsync() =>
        await Context.SaveChangesAsync();
}
