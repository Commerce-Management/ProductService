using System.Linq.Expressions;
using System.Reflection;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ProductService.Core.Entities;
using ProductService.Core.Interfaces;
using ProductService.Infrastructure.Interfaces.Base;
using ProductService.Infrastructure.Interfaces.Entities;
using ProductService.Shared.DTO;

namespace ProductService.Application.Services;

public class ProductService(
    IUnitOfWork unitOfWork,
    IProductRepository productRepository,
    IProductImageService productImageService,
    IProductCategoryRepository productCategoryRepository,
    IShopRepository shopRepository,
    ICategoryService categoryService,
    IMapper mapper) : IProductService
{
    public async Task<(IEnumerable<GetProductDto> Products, int TotalCount)> GetPaginatedProductsAsync(
        Guid currentUserId,
        int pageNumber,
        int pageSize,
        bool loadFullImages = false,
        Guid? categoryId = null,
        string? sortField = null,
        string? sortDirection = "asc")
    {
        var shop = await shopRepository.GetShopByOwnerIdAsync(currentUserId);
        
        if (shop == null)
            throw new InvalidOperationException("Shop is not found by this id");
        if(!shop.IsActive)
            throw new InvalidOperationException("Access for this shop is locked");

        var query = productRepository.GetQueryableEntities()
            .Where(p => p.ShopId == shop.Id)
            .Include(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category)
            .AsNoTracking();

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.ProductCategories.Any(pc => pc.CategoryId == categoryId.Value));
        }

        if (!string.IsNullOrEmpty(sortField))                         
        {                                                                         
            var propertyInfo = typeof(Product).GetProperty(sortField, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (propertyInfo != null)
            {
                var parameter = Expression.Parameter(typeof(Product), "p");
                var property = Expression.Property(parameter, propertyInfo);
                var lambda = Expression.Lambda(property, parameter);

                var method = typeof(Queryable)
                    .GetMethods()
                    .Where(m => m.Name == (sortDirection?.ToLower() == "desc" ? "OrderByDescending" : "OrderBy")
                                && m.IsGenericMethodDefinition
                                && m.GetGenericArguments().Length == 2
                                && m.GetParameters().Length == 2)
                    .First()
                    .MakeGenericMethod(typeof(Product), propertyInfo.PropertyType);

                query = (IQueryable<Product>)method.Invoke(null, new object[] { query, lambda });
            }
            else
            {
                query = query.OrderByDescending(p => p.Name);
            }
        }
        else
        {
            query = query.OrderByDescending(p => p.Name);
        }

        var totalCount = await query.CountAsync();

        var products = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new GetProductDto(
                p.Id.ToString(),
                p.Name,
                p.Description,
                p.Price,
                p.StockQuantity,
                loadFullImages
                    ? p.ImageUrls
                    : (p.ImageUrls != null && p.ImageUrls.Length > 0 ? new[] { p.ImageUrls[0] } : Array.Empty<string>()),
                p.ProductCategories
                    .Select(pc => new CategoryNameDto(pc.Category.Id.ToString(), pc.Category.Name))
                    .ToList(),
                p.DesignData,
                p.PreviewImage,
                p.Status,
                p.UserId
            ))
            .ToListAsync();

        return (products, totalCount);
    }

    public async Task<IEnumerable<GetProductDto>> SearchProduct(
        Guid currentUserId,
        string searchTerm,
        bool loadFullImages = false)
    {
        var shop = await shopRepository.GetShopByOwnerIdAsync(currentUserId);
        if (shop == null)
            throw new InvalidOperationException("Магазин не найден или доступ закрыт.");

        var query = productRepository.GetQueryableEntities()
            .Where(p => p.ShopId == shop.Id)
            .Include(p => p.ProductCategories)
            .ThenInclude(pc => pc.Category)
            .Where(product =>
                EF.Functions.Like(product.Name, $"%{searchTerm}%") ||
                EF.Functions.Like(product.Description, $"%{searchTerm}%"));

        var products = await query
            .Select(p => new GetProductDto(
                p.Id.ToString(),
                p.Name,
                p.Description,
                p.Price,
                p.StockQuantity,
                loadFullImages
                    ? p.ImageUrls
                    : (p.ImageUrls != null && p.ImageUrls.Length > 0 ? new[] { p.ImageUrls[0] } : Array.Empty<string>()),
                p.ProductCategories
                    .Select(pc => new CategoryNameDto(pc.Category.Id.ToString(), pc.Category.Name))
                    .ToList(),
                p.DesignData,
                p.PreviewImage,
                p.Status,
                p.UserId
            ))
            .ToListAsync();
        return products;
    }

    public async Task<IEnumerable<GetProductDto>> GetAllProductsAsync(
        Guid currentUserId,
        bool loadFullImages = false)
    {
        var shop = await shopRepository.GetShopByOwnerIdAsync(currentUserId);
        if (shop == null)
            throw new InvalidOperationException("Магазин не найден или доступ закрыт.");

        var products = await productRepository.GetQueryableEntities()
            .Where(p => p.ShopId == shop.Id)
            .Include(p => p.ProductCategories)
            .ThenInclude(pc => pc.Category)
            .AsNoTracking()
            .ToListAsync();

        var productDtos = mapper.Map<IEnumerable<GetProductDto>>(products);

        if (!loadFullImages)
        {
            productDtos = productDtos.Select(p =>
            {
                if (p.ImageUrls != null && p.ImageUrls.Length > 0)
                {
                    return p with { ImageUrls = new[] { p.ImageUrls[0] } };
                }
                return p;
            }).ToList();
        }
        return productDtos;
    }


    public async Task<GetProductDto> GetProductByIdAsync(
        Guid currentUserId,
        Guid id,
        bool loadFullImages = true)
    {
        var shop = await shopRepository.GetShopByOwnerIdAsync(currentUserId);
        if (shop == null)
            throw new InvalidOperationException("Магазин не найден или доступ закрыт.");

        var product = await productRepository.GetQueryableEntities()
            .FirstOrDefaultAsync(p => p.Id == id && p.ShopId == shop.Id);

        if (product == null)
            return null!;

        var productDto = mapper.Map<GetProductDto>(product);

        if (!loadFullImages && productDto.ImageUrls != null && productDto.ImageUrls.Length > 0)
        {
            productDto = productDto with { ImageUrls = new[] { productDto.ImageUrls[0] } };
        }

        return productDto;
    }


     public async Task<GetProductDto> CreateProductAsync(
        Guid currentUserId,
        CreateProductDto productDto)
    {
        var shop = await shopRepository.GetShopByOwnerIdAsync(currentUserId);
        if (shop == null)
            throw new InvalidOperationException("Магазин не найден или доступ закрыт.");

        if (productDto.ShopId != shop.Id.ToString())
            throw new InvalidOperationException("Вы пытаетесь создать товар в чужом магазине.");

        await unitOfWork.BeginTransactionAsync();
        try
        {
            var productEntity = mapper.Map<Product>(productDto);

            var customBagCategory = await categoryService.GetCategoryByName("Custom Bag");
            if (customBagCategory == null)
                throw new ArgumentException("Custom Bag category not found");

            var isCustomBag = productDto.CategoryIds != null
                              && productDto.CategoryIds.Contains(customBagCategory.Id.ToString());

            if (isCustomBag)
            {
                if (string.IsNullOrEmpty(productDto.DesignData))
                    throw new ArgumentException("DesignData is required for custom bag");
                if (string.IsNullOrEmpty(productDto.PreviewImage))
                    throw new ArgumentException("PreviewImage is required for custom bag");
                if (productDto.UserId == null)
                    throw new ArgumentException("UserId is required for custom bag");

                productEntity.DesignData = productDto.DesignData;
                productEntity.PreviewImage = productDto.PreviewImage;
                productEntity.Status = "Draft";
                productEntity.UserId = productDto.UserId;
                productEntity.ImageUrls = new[] { productDto.PreviewImage };
            }
            else
            {
                if (productDto.Images is { Length: > 0 })
                {
                    var imageUrls = new List<string>();
                    foreach (var image in productDto.Images)
                    {
                        var url = await productImageService.UploadProductImageAsync(image);
                        imageUrls.Add(url);
                    }
                    productEntity.ImageUrls = imageUrls.ToArray();
                }
                else
                {
                    productEntity.ImageUrls = Array.Empty<string>();
                }

                productEntity.DesignData = null;
                productEntity.PreviewImage = null;
                productEntity.Status = null;
                productEntity.UserId = null;
            }

            var insertedProduct = await productRepository.InsertAsync(productEntity);

            if (productDto.CategoryIds != null && productDto.CategoryIds.Any())
            {
                var catGuids = productDto.CategoryIds
                    .Where(x => Guid.TryParse(x, out _))
                    .Select(Guid.Parse)
                    .ToList();

                await productCategoryRepository.UpdateProductCategoriesAsync(insertedProduct.Id, catGuids);
            }

            await unitOfWork.CommitTransactionAsync();
            await unitOfWork.SaveChangesAsync();

            var productWithCats = await productRepository.GetProductByIdAsync(insertedProduct.Id);
            return mapper.Map<GetProductDto>(productWithCats);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }


    public async Task<bool> UpdateProductAsync(
        Guid currentUserId,
        Guid id,
        UpdateProductDto productDto)
    {
        var shop = await shopRepository.GetShopByOwnerIdAsync(currentUserId);
        if (shop == null)
            throw new InvalidOperationException("Магазин не найден или доступ закрыт.");

        var product = await productRepository.GetQueryableEntities()
            .FirstOrDefaultAsync(p => p.Id == id && p.ShopId == shop.Id);

        if (product == null)
            return false;

        product.Name = productDto.Name;
        product.Description = productDto.Description;
        product.Price = productDto.Price;
        product.StockQuantity = productDto.StockQuantity;

        var existingImageUrls = product.ImageUrls != null
            ? product.ImageUrls.ToList()
            : new List<string>();

        if (productDto.RemoveImageUrls != null && productDto.RemoveImageUrls.Any())
        {
            existingImageUrls = existingImageUrls
                .Where(url => !productDto.RemoveImageUrls.Contains(url))
                .ToList();
        }

        if (productDto.NewImages != null && productDto.NewImages.Length > 0)
        {
            foreach (var image in productDto.NewImages)
            {
                var url = await productImageService.UploadProductImageAsync(image);
                existingImageUrls.Add(url);
            }
        }

        product.ImageUrls = existingImageUrls.ToArray();

        if (productDto.CategoryIds != null && productDto.CategoryIds.Any())
        {
            var catGuids = productDto.CategoryIds
                .Where(x => Guid.TryParse(x, out _))
                .Select(Guid.Parse)
                .ToList();

            await productCategoryRepository.UpdateProductCategoriesAsync(product.Id, catGuids);
        }

        productRepository.Update(product);
        var result = await productRepository.SaveChangesAsync();
        return result > 0;
    }


    public async Task<bool> DeleteProductAsync(
        Guid currentUserId,
        Guid productId)
    {
        var shop = await shopRepository.GetShopByOwnerIdAsync(currentUserId);
        if (shop == null)
            throw new InvalidOperationException("Магазин не найден или доступ закрыт.");

        var product = await productRepository.GetQueryableEntities()
            .FirstOrDefaultAsync(p => p.Id == productId && p.ShopId == shop.Id);

        if (product == null)
            return false;

        productRepository.Delete(product);
        var result = await productRepository.SaveChangesAsync();
        return result > 0;
    }
}