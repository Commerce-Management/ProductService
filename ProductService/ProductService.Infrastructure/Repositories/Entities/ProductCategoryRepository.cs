using Microsoft.EntityFrameworkCore;
using ProductService.Core.Entities;
using ProductService.Infrastructure.Context;
using ProductService.Infrastructure.Interfaces.Entities;
using ProductService.Infrastructure.Repositories.Base;

namespace ProductService.Infrastructure.Repositories.Entities;

public class ProductCategoryRepository(ProductDbContext context)
    : Repository<ProductCategory>(context), IProductCategoryRepository
{
    private IQueryable<ProductCategory> GetProductCategoryQuery() =>
        Entities
            .Include(pc => pc.Product)
            .AsNoTracking();

    public async Task UpdateProductCategoriesAsync(Guid productId, IEnumerable<Guid> categoryIds)
    {
        var allCategories = await context.ProductCategories
            .AsNoTracking()
            .ToListAsync();

        var remainingCategories = allCategories
            .Where(c => !categoryIds.Contains(c.Id))
            .ToList();

        var currentProductCategories = await GetProductCategoryQuery()
            .Where(pc => pc.ProductId == productId)
            .ToListAsync();

        foreach (var category in remainingCategories)
        {
            var existingAssociation = currentProductCategories
                .FirstOrDefault(pc => pc.CategoryId == category.Id);
            if (existingAssociation != null)
            {
                context.Attach(existingAssociation);
                context.ProductCategories.Remove(existingAssociation);
            }
        }

        foreach (var catId in categoryIds)
        {
            var exists = await GetProductCategoryQuery()
                .AnyAsync(pc => pc.ProductId == productId && pc.CategoryId == catId);

            if (!exists)
            {
                context.ProductCategories.Add(new ProductCategory
                {
                    ProductId = productId,
                    CategoryId = catId
                });
            }
        }

        await context.SaveChangesAsync();
    }

    public async Task<Guid> GetCategoryIdByProductIdAsync(Guid productId)
    {
        var categoryId = await context.ProductCategories
            .AsNoTracking()
            .Where(pc => pc.ProductId == productId)
            .Select(pc => pc.CategoryId)
            .SingleOrDefaultAsync();

        if (categoryId == default)
            throw new KeyNotFoundException($"No category found for product {productId}");

        return categoryId;
    }

    public async Task<IEnumerable<Guid>> GetCategoryIdsByProductIdsAsync(IEnumerable<Guid> productIds)
    {
        return await Entities
            .AsNoTracking()
            .Where(pc => productIds.Contains(pc.ProductId))
            .Select(pc => pc.CategoryId)
            .Distinct()
            .ToListAsync();
    }


    public async Task<IEnumerable<Guid>> GetProductIdsByCategoryIdsAsync(Guid[] categoryIds)
    {
        return await Entities
            .AsNoTracking()
            .Where(pc => categoryIds.Contains(pc.CategoryId)) 
            .Select(pc => pc.ProductId)
            .Distinct()
            .ToListAsync();
    }
}