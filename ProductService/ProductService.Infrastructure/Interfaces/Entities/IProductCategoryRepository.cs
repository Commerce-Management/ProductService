using ProductService.Core.Entities;
using ProductService.Infrastructure.Interfaces.Base;

namespace ProductService.Infrastructure.Interfaces.Entities;

public interface IProductCategoryRepository : IRepository<ProductCategory>
{
    public Task UpdateProductCategoriesAsync(Guid productId, IEnumerable<Guid> categoryIds);
    public Task<Guid> GetCategoryIdByProductIdAsync(Guid productId);
}