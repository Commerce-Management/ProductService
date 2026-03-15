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
            .AsNoTracking();

    public async Task<ICollection<Product>> GetAllProductsAsync(int page, int pageSize)
    {
        var skip = (page - 1) * pageSize;
        
        return await GetProductQuery()
            .OrderByDescending(product => product.Name)
            .Skip(skip)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<(IEnumerable<Product> Items, int TotalCount)> GetProductsPaginatedAsync(int page, int pageSize)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 20;

        var query = GetProductQuery(); 
        var totalCount = await query.CountAsync();

        var skip = (page - 1) * pageSize;

        var items = await query
            .OrderByDescending(product => product.Name)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }


    public async Task<IEnumerable<Product>> GetAllShopProductsAsync(Guid shopId, int page, int pageSize)
    {
        var skip = (page - 1) * pageSize;
        
        return await Entities.Where(product => product.ShopId == shopId)
            .OrderByDescending(product => product.Name)
            .Skip(skip)
            .Take(pageSize)
            .AsNoTracking().ToListAsync();
        
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

    public async Task<int> GetTotalCountAsync()
    {
        return await Entities.CountAsync();
    }
    
    public async Task<ICollection<Product>> GetProductsByCategoriesAsync(
        Guid[] categoryIds,
        int page,
        int pageSize)
    {
        if (page <= 0)
            page = 1;

        if (pageSize <= 0)
            pageSize = 20;

        var skip = (page - 1) * pageSize;

        return await Entities
            .AsNoTracking()
            .Where(product =>
                product.ProductCategories.Any(pc =>
                    categoryIds.Contains(pc.CategoryId)))
            .OrderBy(product => product.Name)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();
    }



    
    public IQueryable<Product> GetQueryableEntities() => GetProductQuery();
}