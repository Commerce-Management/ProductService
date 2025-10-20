using Microsoft.EntityFrameworkCore;
using ProductService.Core.Entities;
using ProductService.Infrastructure.Context;
using ProductService.Infrastructure.Interfaces.Entities;
using ProductService.Infrastructure.Repositories.Base;
using ProductService.Shared.DTO;

namespace ProductService.Infrastructure.Repositories.Entities;

public class ProductRepository(ProductDbContext context) : Repository<Product>(context), IProductRepository
{
    private IQueryable<Product> GetProductQuery() =>
        Entities
            .Include(product => product.ProductCategories)
            .ThenInclude(pc => pc.CategoryId)
            .AsNoTracking();

    public async Task<ICollection<Product>> GetAllProductsAsync()
    {
        return await GetProductQuery()
            .OrderByDescending(product => product.Name)
            .ToListAsync();
    }

    public async Task<Product?> GetProductByIdAsync(Guid id)
    {
        return await GetProductQuery()
            .SingleOrDefaultAsync(product => product.Id == id);
    }

    public async Task<IEnumerable<Product>> GetProductsByIdAsync(Guid[] productIds) =>
        await GetProductQuery()
            .Where(product => productIds.Contains(product.Id))
            .ToListAsync();
    
    public async Task<(ICollection<Product> Products, int TotalCount)> GetPaginatedProductsAsync(int pageNumber, int pageSize)
    {
        var query = GetProductQuery();
        var totalCount = await query.CountAsync();
        
        var products = await query
            .OrderByDescending(product => product.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (products, totalCount);
    }

    public async Task<(IEnumerable<GetProductDto> Products, int TotalCount)> GetPaginatedProductsOptimizedAsync(int pageNumber, int pageSize)
    {
        var query = GetProductQuery();
        var totalCount = await query.CountAsync();

        var products = await query
            .OrderByDescending(p => p.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new GetProductDto(
                p.Id.ToString(),
                p.Name,
                p.Description,
                p.Price,
                p.StockQuantity,
                p.ImageUrls
            ))
            .ToListAsync();

        return (products, totalCount);
    }

    public IQueryable<Product> GetQueryableEntities() => GetProductQuery();
}