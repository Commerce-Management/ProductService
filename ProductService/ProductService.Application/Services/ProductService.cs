using System.Linq.Expressions;
using System.Reflection;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ProductService.Core.Entities;
using ProductService.Core.Interfaces;
using ProductService.Infrastructure.Interfaces.Base;
using ProductService.Infrastructure.Interfaces.Entities;
using ProductService.Shared.DTO;
using Grpc.Net.Client;
using ProductService.Shared.Protos.GrpcShopService;

namespace ProductService.Application.Services;

public class ProductService(
    IUnitOfWork unitOfWork,
    IProductRepository productRepository,
    IProductImageService productImageService,
    IProductCategoryRepository productCategoryRepository,
    ShopService.ShopServiceClient shopServiceClient,
    IMapper mapper) : IProductService
{
    // public async Task<(IEnumerable<GetProductDto> Products, int TotalCount)> GetPaginatedProductsAsync(
    //     Guid currentUserId,
    //     int pageNumber,
    //     int pageSize,
    //     bool loadFullImages = false,
    //     Guid? categoryId = null,
    //     string? sortField = null,
    //     string? sortDirection = "asc")
    // {
    //
    //     var shop = await shopServiceClient.GetShopByIdAsync(new GetShopByIdRequest()
    //     {
    //         ShopId = currentUserId.ToString()
    //     });
    //     
    //     if (shop == null)
    //         throw new InvalidOperationException("Shop is not found by this id");
    //     if(!shop.IsActive)
    //         throw new InvalidOperationException("Access for this shop is locked");
    //
    //     var query = productRepository.GetQueryableEntities()
    //         .Where(p => p.ShopId.ToString() == shop.ShopId)
    //         .Include(p => p.ProductCategories)
    //             .ThenInclude(pc => pc.CategoryId)
    //         .AsNoTracking();
    //
    //     if (categoryId.HasValue)
    //     {
    //         query = query.Where(p => p.ProductCategories.Any(pc => pc.CategoryId == categoryId.Value));
    //     }
    //
    //     if (!string.IsNullOrEmpty(sortField))                         
    //     {                                                                         
    //         var propertyInfo = typeof(Product).GetProperty(sortField, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
    //         if (propertyInfo != null)
    //         {
    //             var parameter = Expression.Parameter(typeof(Product), "p");
    //             var property = Expression.Property(parameter, propertyInfo);
    //             var lambda = Expression.Lambda(property, parameter);
    //
    //             var method = typeof(Queryable)
    //                 .GetMethods()
    //                 .Where(m => m.Name == (sortDirection?.ToLower() == "desc" ? "OrderByDescending" : "OrderBy")
    //                             && m.IsGenericMethodDefinition
    //                             && m.GetGenericArguments().Length == 2
    //                             && m.GetParameters().Length == 2)
    //                 .First()
    //                 .MakeGenericMethod(typeof(Product), propertyInfo.PropertyType);
    //
    //             query = (IQueryable<Product>)method.Invoke(null, new object[] { query, lambda });
    //         }
    //         else
    //         {
    //             query = query.OrderByDescending(p => p.Name);
    //         }
    //     }
    //     else
    //     {
    //         query = query.OrderByDescending(p => p.Name);
    //     }
    //
    //     var totalCount = await query.CountAsync();
    //
    //     var products = await query
    //         .Skip((pageNumber - 1) * pageSize)
    //         .Take(pageSize)
    //         .Select(p => new GetProductDto(
    //             p.Id.ToString(),
    //             p.Name,
    //             p.Description,
    //             p.Price,
    //             p.StockQuantity,
    //             p.ImageUrls
    //         ))
    //         .ToListAsync();
    //
    //     return (products, totalCount);
    // }
    //
    // public async Task<IEnumerable<GetProductDto>> SearchProduct(
    //     Guid currentUserId,
    //     string searchTerm,
    //     bool loadFullImages = false)
    // {
    //     var shop = await shopServiceClient.GetShopByOwnerAsync(new GetShopByOwnerRequest
    //     {
    //         OwnerUserId = currentUserId.ToString()
    //     });
    //     if (shop == null)
    //         throw new InvalidOperationException("Магазин не найден или доступ закрыт.");
    //
    //     var query = productRepository.GetQueryableEntities()
    //         .Where(p => p.ShopId.ToString() == shop.ShopId)
    //         .Include(p => p.ProductCategories)
    //         .ThenInclude(pc => pc.CategoryId)
    //         .Where(product =>
    //             EF.Functions.Like(product.Name, $"%{searchTerm}%") ||
    //             EF.Functions.Like(product.Description, $"%{searchTerm}%"));
    //
    //     var products = await query
    //         .Select(p => new GetProductDto(
    //             p.Id.ToString(),
    //             p.Name,
    //             p.Description,
    //             p.Price,
    //             p.StockQuantity,
    //             p.ImageUrls
    //         ))
    //         .ToListAsync();
    //     return products;
    // }
    //
    // public async Task<IEnumerable<GetProductDto>> GetAllProductsAsync(
    //     Guid currentUserId,
    //     bool loadFullImages = false)
    // {
    //     var shop = await shopServiceClient.GetShopByOwnerAsync(new GetShopByOwnerRequest
    //     {
    //         OwnerUserId = currentUserId.ToString()
    //     });
    //     if (shop == null)
    //         throw new InvalidOperationException("Магазин не найден или доступ закрыт.");
    //
    //     var products = await productRepository.GetQueryableEntities()
    //         .Where(p => p.ShopId.ToString() == shop.ShopId)
    //         .Include(p => p.ProductCategories)
    //         .ThenInclude(pc => pc.CategoryId)
    //         .AsNoTracking()
    //         .ToListAsync();
    //
    //     var productDtos = mapper.Map<IEnumerable<GetProductDto>>(products);
    //
    //     if (!loadFullImages)
    //     {
    //         productDtos = productDtos.Select(p =>
    //         {
    //             if (p.ImageUrls != null && p.ImageUrls.Length > 0)
    //             {
    //                 return p with { ImageUrls = new[] { p.ImageUrls[0] } };
    //             }
    //             return p;
    //         }).ToList();
    //     }
    //     return productDtos;
    // }
    //
    //
    // public async Task<GetProductDto> GetProductByIdAsync(
    //     Guid currentUserId,
    //     Guid id,
    //     bool loadFullImages = true)
    // {
    //     var shop = await shopServiceClient.GetShopByOwnerAsync(new GetShopByOwnerRequest
    //     {
    //         OwnerUserId = currentUserId.ToString()
    //     });
    //     if (shop == null)
    //         throw new InvalidOperationException("Магазин не найден или доступ закрыт.");
    //
    //     var product = await productRepository.GetQueryableEntities()
    //         .FirstOrDefaultAsync(p => p.Id == id && p.ShopId.ToString() == shop.ShopId);
    //
    //     if (product == null)
    //         return null!;
    //
    //     var productDto = mapper.Map<GetProductDto>(product);
    //
    //     if (!loadFullImages && productDto.ImageUrls != null && productDto.ImageUrls.Length > 0)
    //     {
    //         productDto = productDto with { ImageUrls = new[] { productDto.ImageUrls[0] } };
    //     }
    //
    //     return productDto;
    // }


     public async Task<GetProductDto> CreateProductAsync(CreateProductDto productDto)
    {
        var shopId = await shopServiceClient.GetShopByIdAsync(new GetShopByIdRequest()
        {
            ShopId = productDto.ShopId
        });

        if (shopId == null)
            throw new InvalidOperationException("Магазин не найден или доступ закрыт.");

        if (productDto.ShopId != shopId.Id)
            throw new InvalidOperationException("Вы пытаетесь создать товар в чужом магазине.");

        await unitOfWork.BeginTransactionAsync();
        try
        {
            var productEntity = mapper.Map<Product>(productDto);

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

             
            var insertedProduct = await productRepository.InsertAsync(productEntity);

            
            // added for ProductCategory table
            var catGuids = productDto.CategoryIds
                .Where(x => Guid.TryParse(x, out _))
                .Select(Guid.Parse)
                .ToList();

            await productCategoryRepository.UpdateProductCategoriesAsync(insertedProduct.Id, catGuids);

            
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


    // public async Task<bool> UpdateProductAsync(
    //     Guid currentUserId,
    //     Guid id,
    //     UpdateProductDto productDto)
    // {
    //     var shop = await shopServiceClient.GetShopByIdAsync(new GetShopByIdRequest()
    //     {
    //         ShopId = currentUserId.ToString()
    //     });
    //     if (shop == null)
    //         throw new InvalidOperationException("Магазин не найден или доступ закрыт.");
    //
    //     var product = await productRepository.GetQueryableEntities()
    //         .FirstOrDefaultAsync(p => p.Id == id && p.ShopId.ToString() == shop.Id);
    //
    //     if (product == null)
    //         return false;
    //
    //     product.Name = productDto.Name;
    //     product.Description = productDto.Description;
    //     product.Price = productDto.Price;
    //     product.StockQuantity = productDto.StockQuantity;
    //
    //     var existingImageUrls = product.ImageUrls != null
    //         ? product.ImageUrls.ToList()
    //         : new List<string>();
    //
    //     if (productDto.RemoveImageUrls != null && productDto.RemoveImageUrls.Any())
    //     {
    //         existingImageUrls = existingImageUrls
    //             .Where(url => !productDto.RemoveImageUrls.Contains(url))
    //             .ToList();
    //     }
    //
    //     if (productDto.NewImages != null && productDto.NewImages.Length > 0)
    //     {
    //         foreach (var image in productDto.NewImages)
    //         {
    //             var url = await productImageService.UploadProductImageAsync(image);
    //             existingImageUrls.Add(url);
    //         }
    //     }
    //
    //     product.ImageUrls = existingImageUrls.ToArray();
    //
    //     if (productDto.CategoryIds != null && productDto.CategoryIds.Any())
    //     {
    //         var catGuids = productDto.CategoryIds
    //             .Where(x => Guid.TryParse(x, out _))
    //             .Select(Guid.Parse)
    //             .ToList();
    //
    //         await productCategoryRepository.UpdateProductCategoriesAsync(product.Id, catGuids);
    //     }
    //
    //     productRepository.Update(product);
    //     var result = await productRepository.SaveChangesAsync();
    //     return result > 0;
    // }
    //
    //
    // public async Task<bool> DeleteProductAsync(
    //     Guid currentUserId,
    //     Guid productId)
    // {
    //     var shop = await shopServiceClient.GetShopByIdAsync(new GetShopByIdRequest()
    //     {
    //         ShopId = currentUserId.ToString()
    //     });
    //     if (shop == null)
    //         throw new InvalidOperationException("Магазин не найден или доступ закрыт.");
    //
    //     var product = await productRepository.GetQueryableEntities()
    //         .FirstOrDefaultAsync(p => p.Id == productId && p.ShopId.ToString() == shop.Id);
    //
    //     if (product == null)
    //         return false;
    //
    //     productRepository.Delete(product);
    //     var result = await productRepository.SaveChangesAsync();
    //     return result > 0;
    // }
}