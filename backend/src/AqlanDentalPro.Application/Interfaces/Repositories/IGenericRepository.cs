using System.Linq.Expressions;

namespace AqlanDentalPro.Application.Interfaces.Repositories;

public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task AddAsync(T entity);
    Task AddRangeAsync(IEnumerable<T> entities);
    void Update(T entity);
    void Remove(T entity);
    void Detach(T entity);
    Task<int> SaveChangesAsync();
    Task AddChildAsync<TChild>(TChild entity) where TChild : class;
}
