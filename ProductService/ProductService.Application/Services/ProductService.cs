using System.Linq.Expressions;
using System.Reflection;
using AutoMapper;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using ProductService.Core.Entities;
using ProductService.Core.Interfaces;
using ProductService.Infrastructure.Interfaces.Base;
using ProductService.Infrastructure.Interfaces.Entities;
using ProductService.Shared.DTO;
using Grpc.Net.Client;
using ProductService.Shared.DTO.DetailDtos;
using ProductService.Shared.Protos.GrpcCategoryService;
using ProductService.Shared.Protos.GrpcShopService;

namespace ProductService.Application.Services;

public class ProductService(
    IUnitOfWork unitOfWork,
    IProductRepository productRepository,
    IProductImageService productImageService,
    IProductCategoryRepository productCategoryRepository,
    ShopService.ShopServiceClient shopServiceClient,
    CategoryService.CategoryServiceClient categoryServiceClient,
    IMapper mapper) : IProductService
{
    public async Task<IEnumerable<GetProductDto>> SearchProduct(string parameter, int page)
    {
        var allProducts = await productRepository.GetAllProductsAsync(page, pageSize: 30);

        var filteredProducts = allProducts.Where(product =>
            product.Name.Contains(parameter, StringComparison.OrdinalIgnoreCase) ||
            product.Description.Contains(parameter, StringComparison.OrdinalIgnoreCase)
        );

        return mapper.Map<IEnumerable<GetProductDto>>(filteredProducts);
    }
    
    public async Task<IEnumerable<GetProductDto>> GetAllProductsAsync(int page)
    {
        var products = await productRepository.GetAllProductsAsync(page, pageSize: 30);
        return mapper.Map<IEnumerable<GetProductDto>>(products);
    }
    
    public async Task<GetProductDto> GetProductByIdAsync(Guid productId)
    {
        var product = await productRepository.GetProductByIdAsync(productId);
        
        if (product == null) return null!;
    
        return mapper.Map<GetProductDto>(product);
    }

    public async Task<IEnumerable<GetProductDto>> GetAllShopProductsAsync(Guid shopId, int page)
    {
        var shop = shopServiceClient.GetShopById(new GetShopByIdRequest()
        {
            ShopId = shopId.ToString()
        });

        if (shop == null)
            throw new NullReferenceException("Shop by this ID is null");
        
        var products = await productRepository.GetAllShopProductsAsync(shopId, page, pageSize: 30);
        return mapper.Map<IEnumerable<GetProductDto>>(products);
    }

    public async Task<GetProductDetailDto> GetDetailProductByIdAsync(Guid productId)
    {
        var product = await productRepository.GetProductByIdAsync(productId);
        if (product == null)
            return null!;

        var categoryId = await productCategoryRepository.GetCategoryIdByProductIdAsync(productId);
        
        var shop = await shopServiceClient.GetShopByIdAsync(new GetShopByIdRequest()
        {
            ShopId = product.ShopId.ToString()
        });

        var category = await categoryServiceClient.GetCategoryByIdAsync(new GetCategoryByIdRequest()
        {
            CategoryId = categoryId.ToString()
        });
        
        // Собираем DTO
        return new GetProductDetailDto(
                Id: product.Id.ToString(),
                Name: product.Name,
                Description: product.Description,
                Price: product.Price,
                StockQuantity: product.StockQuantity,
                ImageUrls: product.ImageUrls,
                Shop: new ShopInfoDto(
                    Id: shop.Id,
                    Name: shop.Name,
                    OwnerId: shop.OwnerId
                ),
                Category: new CategoryInfoDto(
                    Id: category.Category.Id,
                    Name: category.Category.Name,
                    Slug: category.Category.Slug,
                    Level: category.Category.Level,
                    IsActive: category.Category.IsActive,
                    ParentCategory: category.Category.ParentCategory != null
                        ? new CategoryInfoDto(
                            Id: category.Category.ParentCategory.Id,
                            Name: category.Category.ParentCategory.Name,
                            Slug: null,
                            Level: category.Category.ParentCategory.Level,
                            IsActive: true
                        )
                        : null
                )
            );
    }
        
    

     public async Task<GetProductDto> CreateProductAsync(Guid userId, CreateProductDto productDto)
     { 
         ValidateShopOwnershipResponse shop;
        try
        { 
            shop = await shopServiceClient.ValidateShopOwnershipAsync(new ValidateShopOwnershipRequest()
            {
                ShopId = productDto.ShopId,
                UserId = userId.ToString()
            });
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            throw new InvalidOperationException("Shop service is temporarily unavailable. Please try again later.");
        }
        catch (RpcException ex)
        {
            throw new InvalidOperationException("Unable to verify shop ownership");
        }
        
        if (!shop.ShopExists)
            throw new KeyNotFoundException($"Shop {productDto.ShopId} not found");

        if (!shop.IsOwner)
            throw new UnauthorizedAccessException("You are not the owner of this shop");

        if (!shop.ShopIsActive)
            throw new InvalidOperationException("Shop is not active. Cannot add products.");
        
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
                productEntity.ImageUrls = Array.Empty<string>();
            
            await unitOfWork.BeginTransactionAsync();
             
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


    public async Task<bool> UpdateProductAsync(Guid userId, Guid id, UpdateProductDto productDto)
    {
        if (productDto == null)
            throw new ArgumentNullException(nameof(productDto));

        if (!Guid.TryParse(productDto.ShopId, out var shopId))
            throw new ArgumentException("Invalid ShopId format");

        ValidateShopOwnershipResponse shopValidation;
        try
        {
            shopValidation = await shopServiceClient.ValidateShopOwnershipAsync(
                new ValidateShopOwnershipRequest
                {
                    ShopId = productDto.ShopId,
                    UserId = userId.ToString()
                });
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            throw new InvalidOperationException("Shop service is temporarily unavailable. Please try again later.");
        }
        catch (RpcException ex)
        {
            throw new InvalidOperationException("Unable to verify shop ownership");
        }

        if (!shopValidation.ShopExists)
            throw new KeyNotFoundException($"Shop {shopId} not found");

        if (!shopValidation.IsOwner)
            throw new UnauthorizedAccessException("You are not the owner of this shop");

        if (!shopValidation.ShopIsActive)
            throw new InvalidOperationException("Shop is not active. Cannot update products.");

        var product = await productRepository.GetQueryableEntities()
            .FirstOrDefaultAsync(p => p.Id == id && p.ShopId == shopId);  // ✅ правильное сравнение!

        if (product == null)
            throw new KeyNotFoundException($"Product {id} not found in your shop");
    
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
    
    
    
    public async Task<bool> DeleteProductAsync(Guid userId, Guid shopId, Guid productId)
    {
        ValidateShopOwnershipResponse shopValidation;
        try
        {
            shopValidation = await shopServiceClient.ValidateShopOwnershipAsync(
                new ValidateShopOwnershipRequest
                {
                    ShopId = shopId.ToString(),
                    UserId = userId.ToString()
                });
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            throw new InvalidOperationException("Shop service is temporarily unavailable. Please try again later.");
        }
        catch (RpcException ex)
        {
            throw new InvalidOperationException("Unable to verify shop ownership");
        }

        if (!shopValidation.ShopExists)
            throw new KeyNotFoundException($"Shop {shopId} not found");

        if (!shopValidation.IsOwner)
            throw new UnauthorizedAccessException("You are not the owner of this shop");

        if (!shopValidation.ShopIsActive)
            throw new InvalidOperationException("Shop is not active. Cannot update products.");

        var product = await productRepository.GetQueryableEntities()
            .FirstOrDefaultAsync(p => p.Id == productId && p.ShopId == shopId);  // ✅ правильное сравнение!
    
        if (product == null)
            throw new KeyNotFoundException($"Product {productId} not found in your shop");
    
        productRepository.Delete(product);
        var result = await productRepository.SaveChangesAsync();
        return result > 0;
    }
}