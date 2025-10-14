namespace ProductService.Infrastructure.Interfaces.Base;

public interface IUnitOfWork
{
    public Task<bool> SaveChangesAsync();
    public Task BeginTransactionAsync();
    public Task CommitTransactionAsync();
    public Task RollbackTransactionAsync();
    public Task ExecuteTransactionAsync(Func<Task> action);
}