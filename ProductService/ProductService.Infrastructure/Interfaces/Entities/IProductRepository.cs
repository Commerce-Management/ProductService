using ProductService.Core.Entities;
using ProductService.Infrastructure.Interfaces.Base;

namespace ProductService.Infrastructure.Interfaces.Entities;

public interface IProductRepository : IRepository<Product>
{
    public Task<ICollection<Product>> GetAllProductsAsync();
    public Task<Product?> GetProductByIdAsync(Guid id);
    public Task<IEnumerable<Product>> GetProductsByIdAsync(Guid[] productIds);
    public Task<(ICollection<Product> Products, int TotalCount)> GetPaginatedProductsAsync(int pageNumber, int pageSize);
    
    IQueryable<Product> GetQueryableEntities();
}