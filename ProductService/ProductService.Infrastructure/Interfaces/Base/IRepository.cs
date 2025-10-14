using ProductService.Core.Entities;

namespace ProductService.Infrastructure.Interfaces.Base;

public interface IRepository<T> where T : IEntity
{
    IQueryable<T> GetAll();
    public Task<IEnumerable<T>> GetAllAsync();
    public Task<T?> GetByIdAsync(Guid id);
    public Task<T> InsertAsync(T entity);
    public Task InsertManyAsync(IEnumerable<T> entities);
    
    void Update(T entity);
    void Delete(T entity);
    public Task<int> SaveChangesAsync();    
}