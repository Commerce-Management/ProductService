using Microsoft.EntityFrameworkCore;
using ProductService.Core.Entities;
using ProductService.Infrastructure.Context;
using ProductService.Infrastructure.Interfaces.Entities;
using ProductService.Infrastructure.Repositories.Base;

namespace ProductService.Infrastructure.Repositories.Entities;

public class ProductCategoryRepository(ProductDbContext context) : Repository<ProductCategory>(context), IProductCategoryRepository
{
    private IQueryable<ProductCategory> GetProductCategoryQuery() =>
        Entities
            .Include(pc => pc.Product)
            .Include(pc => pc.Category)
            .AsNoTracking();

    public async Task UpdateProductCategoriesAsync(Guid productId, IEnumerable<Guid> categoryIds)
    {
        var allCategories = await context.Categories
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
}