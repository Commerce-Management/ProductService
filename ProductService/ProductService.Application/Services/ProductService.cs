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
    public async Task<IEnumerable<GetProductDto>> SearchProduct(string parameter, int page, int pageSize = 30)
    {
        if (string.IsNullOrWhiteSpace(parameter))
            return Array.Empty<GetProductDto>();

        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 30;

        var searchTerm = parameter.Trim().ToLower();

        var query = productRepository.GetQueryableEntities();

        var filteredQuery = query.Where(p =>
            (!string.IsNullOrEmpty(p.Name) && EF.Functions.Like(p.Name.ToLower(), $"%{searchTerm}%")) ||
            (!string.IsNullOrEmpty(p.Description) && EF.Functions.Like(p.Description.ToLower(), $"%{searchTerm}%"))
        );

        var skip = (page - 1) * pageSize;

        var matched = await filteredQuery
            .OrderByDescending(p => p.Name)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        return mapper.Map<IEnumerable<GetProductDto>>(matched);
    }


    public async Task<IEnumerable<GetProductDto>> GetAllProductsAsync(int page)
    {
        var products = await productRepository.GetAllProductsAsync(page, pageSize: 30);
        return mapper.Map<IEnumerable<GetProductDto>>(products);
    }

    public async Task<(IEnumerable<GetProductDto> Items, int TotalCount)> GetProductsPaginatedAsync(int page, int pageSize)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 30;  

        var (products, totalCount) = await productRepository.GetProductsPaginatedAsync(page, pageSize);

        var dtos = mapper.Map<IEnumerable<GetProductDto>>(products);

        return (dtos, totalCount);
    }


    public async Task<GetProductDto> GetProductByIdAsync(Guid productId)
    {
        var product = await productRepository.GetProductByIdAsync(productId);
        
        if (product == null) return null!;
    
        return mapper.Map<GetProductDto>(product);
    }

    public async Task<IEnumerable<GetProductDto>> GetAllShopProductsAsync(Guid shopId, int page, int pageSize = 30)
    {
        var shop = await shopServiceClient.GetShopByIdAsync(new GetShopByIdRequest()
        {
            ShopId = shopId.ToString()
        });

        if (shop == null)
            throw new NullReferenceException("Shop by this ID is null");

        var products = await productRepository.GetAllShopProductsAsync(shopId, page, pageSize);
        return mapper.Map<IEnumerable<GetProductDto>>(products);
    }

    public async Task<IEnumerable<GetProductDto>> GetProductsByCategoriesAsync(Guid[] categoryIds, int page, int pageSize = 30)
    {
        if (categoryIds == null || categoryIds.Length == 0)
            throw new ArgumentException("CategoryIds is empty");

        var products = await productRepository.GetProductsByCategoriesAsync(
            categoryIds,
            page,
            pageSize
        );

        return mapper.Map<IEnumerable<GetProductDto>>(products);
    }


    public async Task<GetProductDetailDto> GetDetailProductByIdAsync(Guid productId)
    {
        var product = await productRepository.GetProductByIdAsync(productId);
        if (product == null)
            return null!;

      
        var categoryIds = await productCategoryRepository.GetCategoryIdsByProductIdAsync(productId);

        List<CategoryInfoDto> categories = new();

        if (categoryIds != null && categoryIds.Length > 0)
        {
  
            var tasks = categoryIds
                .Where(id => id != Guid.Empty)
                .Select(async cid =>
                {
                    try
                    {
                        var resp = await categoryServiceClient.GetCategoryByIdAsync(new GetCategoryByIdRequest
                        {
                            CategoryId = cid.ToString()
                        });

                        var cat = resp?.Category;
                        if (cat == null) return null;

                        CategoryInfoDto? parent = null;
                        if (cat.ParentCategory != null)
                        {
                            parent = new CategoryInfoDto(
                                Id: cat.ParentCategory.Id,
                                Name: cat.ParentCategory.Name,
                                Slug: null,
                                Level: cat.ParentCategory.Level,
                                IsActive: true,
                                ParentCategory: null
                            );
                        }

                        return new CategoryInfoDto(
                            Id: cat.Id,
                            Name: cat.Name,
                            Slug: cat.Slug,
                            Level: cat.Level,
                            IsActive: cat.IsActive,
                            ParentCategory: parent
                        );
                    }
                    catch (Grpc.Core.RpcException)
                    {
                     
                        return null;
                    }
                });

            var results = await Task.WhenAll(tasks);
            categories = results.Where(r => r != null).Select(r => r!).ToList();
        }

        
        var shop = await shopServiceClient.GetShopByIdAsync(new GetShopByIdRequest()
        {
            ShopId = product.ShopId.ToString()
        });

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
            Categories: categories.Count > 0 ? categories.ToArray() : Array.Empty<CategoryInfoDto>()
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
            .FirstOrDefaultAsync(p => p.Id == id && p.ShopId == shopId);   

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
            .FirstOrDefaultAsync(p => p.Id == productId && p.ShopId == shopId);   
    
        if (product == null)
            throw new KeyNotFoundException($"Product {productId} not found in your shop");
    
        productRepository.Delete(product);
        var result = await productRepository.SaveChangesAsync();
        return result > 0;
    }
}