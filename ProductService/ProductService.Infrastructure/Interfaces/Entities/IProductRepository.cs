using ProductService.Core.Entities;
using ProductService.Infrastructure.Interfaces.Base;

namespace ProductService.Infrastructure.Interfaces.Entities;

public interface IProductRepository : IRepository<Product>
{
    public Task<ICollection<Product>> GetAllProductsAsync(int page, int pageSize);
    public Task<Product?> GetProductByIdAsync(Guid id);
    public Task<IEnumerable<Product>> GetAllShopProductsAsync(Guid shopId, int page, int pageSize);
    public Task<IEnumerable<Product>> GetProductsByIdAsync(Guid[] productIds);
    public Task<int> GetTotalCountAsync();
    Task<(IEnumerable<Product> Items, int TotalCount)> GetProductsPaginatedAsync(int page, int pageSize);

    public Task<ICollection<Product>> GetProductsByCategoriesAsync(
        Guid[] categoryIds,
        int page,
        int pageSize);


    IQueryable<Product> GetQueryableEntities();
}