# Product Service - Выделенная информация

## 1. Сущность Product

```1:20:ProductService.Core/Entities/Product.cs
namespace ProductService.Core.Entities;

public class Product : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public decimal Price { get; set; }
    
    public int StockQuantity { get; set; }  

    public string[] ImageUrls { get; set; } = Array.Empty<string>();
    
    public Guid ShopId { get; set; } 
    
    public virtual ICollection<ProductCategory> ProductCategories { get; set; } = new List<ProductCategory>();
}
```

**Описание сущности:**
- `Id` (Guid) - уникальный идентификатор продукта
- `Name` (string) - название продукта (обязательное поле)
- `Description` (string?) - описание продукта (необязательное поле)
- `Price` (decimal) - цена продукта
- `StockQuantity` (int) - количество товара на складе
- `ImageUrls` (string[]) - массив URL изображений продукта
- `ShopId` (Guid) - идентификатор магазина, которому принадлежит продукт
- `ProductCategories` (ICollection<ProductCategory>) - коллекция категорий продукта

---

## 2. ProductRepository

### Интерфейс IProductRepository

```6:13:ProductService.Infrastructure/Interfaces/Entities/IProductRepository.cs
public interface IProductRepository : IRepository<Product>
{
    public Task<ICollection<Product>> GetAllProductsAsync(int page, int pageSize);
    public Task<Product?> GetProductByIdAsync(Guid id);
    public Task<IEnumerable<Product>> GetAllShopProductsAsync(Guid shopId, int page, int pageSize);
    public Task<IEnumerable<Product>> GetProductsByIdAsync(Guid[] productIds);
    IQueryable<Product> GetQueryableEntities();
}
```

### Реализация ProductRepository

```10:54:ProductService.Infrastructure/Repositories/Entities/ProductRepository.cs
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

    public IQueryable<Product> GetQueryableEntities() => GetProductQuery();
}
```

**Методы репозитория:**
- `GetAllProductsAsync` - получение всех продуктов с пагинацией
- `GetAllShopProductsAsync` - получение всех продуктов конкретного магазина с пагинацией
- `GetProductByIdAsync` - получение продукта по ID
- `GetProductsByIdAsync` - получение нескольких продуктов по массиву ID
- `GetQueryableEntities` - получение запроса для дальнейшей фильтрации

---

## 3. ProductService

### Интерфейс IProductService

```6:16:ProductService.Core/Interfaces/IProductService.cs
public interface IProductService
{
    Task<IEnumerable<GetProductDto>> GetAllProductsAsync(int page);
    Task<IEnumerable<GetProductDto>> SearchProduct(string parameter, int page);
    Task<GetProductDto> GetProductByIdAsync(Guid productId);
    Task<IEnumerable<GetProductDto>> GetAllShopProductsAsync(Guid shopId, int page);
    Task<GetProductDto> CreateProductAsync(Guid userId, CreateProductDto productDto);
    Task<GetProductDetailDto> GetDetailProductByIdAsync(Guid productId);
    Task<bool> UpdateProductAsync(Guid userId, Guid productId, UpdateProductDto productDto);
    Task<bool> DeleteProductAsync(Guid userId, Guid shopId, Guid productId);
}
```

### Реализация ProductService (только методы для работы с продуктом)

```27:66:ProductService.Application/Services/ProductService.cs
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
```

```120:191:ProductService.Application/Services/ProductService.cs
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
```

```194:276:ProductService.Application/Services/ProductService.cs
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
```

```280:319:ProductService.Application/Services/ProductService.cs
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
```

**Методы сервиса:**
- `GetAllProductsAsync` - получение всех продуктов с пагинацией
- `SearchProduct` - поиск продуктов по параметру (название или описание)
- `GetProductByIdAsync` - получение продукта по ID
- `GetAllShopProductsAsync` - получение всех продуктов магазина
- `GetDetailProductByIdAsync` - получение детальной информации о продукте (с данными магазина и категории)
- `CreateProductAsync` - создание нового продукта
- `UpdateProductAsync` - обновление продукта
- `DeleteProductAsync` - удаление продукта

---

## 4. DTO (Data Transfer Objects)

### GetProductDto - основной DTO для возврата продукта

```3:11:ProductService.Shared/DTO/GetProductDto.cs
public record GetProductDto(
    string Id,
    string Name,
    string Description,
    decimal Price,
    int StockQuantity,
    string[] ImageUrls
    // List<CategoryNameDto>? Categories,
);
```

### GetProductDetailDto - детальный DTO с информацией о магазине и категории

```3:12:ProductService.Shared/DTO/DetailDtos/GetProductDetailDto.cs
public record GetProductDetailDto(
    string Id,
    string Name,
    string? Description,
    decimal Price,
    int StockQuantity,
    string[] ImageUrls,
    ShopInfoDto Shop,
    CategoryInfoDto Category
);
```

### ShopInfoDto - информация о магазине

```2:6:ProductService.Shared/DTO/DetailDtos/ShopInfoDto.cs
public record ShopInfoDto(
    string Id,
    string Name,
    string OwnerId
);
```

### CategoryInfoDto - информация о категории

```2:9:ProductService.Shared/DTO/DetailDtos/CategoryInfoDto.cs
public record CategoryInfoDto(
    string Id,
    string Name,
    string? Slug,
    int Level,
    bool IsActive,
    CategoryInfoDto? ParentCategory = null
);
```

### CreateProductDto - DTO для создания продукта

```9:27:ProductService.Shared/DTO/CreateProductDto.cs
public record CreateProductDto(
    [Required]
    string ShopId, 
    
    [Required]
    string Name,

    [Required]
    string Description,
    
    [AllowedExtensions([".jpg", ".png"])]
    [MaxFileSize(10 * 1024 * 1024)]
    IFormFile[]? Images,
    decimal Price,
    int StockQuantity,
    
    [Required]
    List<string> CategoryIds
);
```

### UpdateProductDto - DTO для обновления продукта

```6:21:ProductService.Shared/DTO/UpdateProductDto.cs
public record UpdateProductDto(
    [Required]
    string ShopId,
    
    string Name,
    
    string Description,
    
    decimal Price,
    
    int StockQuantity, 
    
    IFormFile[]? NewImages,
    List<string>? RemoveImageUrls,
    
    List<string>? CategoryIds);
```

---

## 5. Protofile для gRPC Product Service

```1:39:ProductService.Shared/Protos/Product.proto
syntax = "proto3";

option csharp_namespace = "ProductService.Shared.Protos.GrpcProductService";
package product;

service ProductService {
  rpc GetProductsByIds (GetProductsByIdsRequest) returns (GetProductsByIdsResponse);
  rpc UpdateProductStock (UpdateProductStockRequest) returns (UpdateProductStockResponse);
}

message GetProductsByIdsRequest {
  repeated string product_ids = 1;
}

message GetProductsByIdsResponse {
  repeated ProductBrief products = 1;
}

message ProductBrief {
  string id = 1;
  string name = 2;
  int64 price = 3;
  int32 stock_quantity = 4;
  repeated string image_urls = 5;
  string shop_id = 6;
}

message UpdateProductStockRequest {
  repeated StockUpdate updates = 1;
}

message StockUpdate {
  string product_id = 1;
  int32 quantity = 2;
}

message UpdateProductStockResponse {
  bool success = 1;
}
```

---

## 6. gRPC Product Service Implementation

```9:129:ProductService.Infrastructure/gRPC/GrpcProductService.cs
public class GrpcProductService : ProductService.Shared.Protos.GrpcProductService.ProductService.ProductServiceBase
{
    private readonly IProductRepository _productRepository;
    private readonly ILogger<GrpcProductService> _logger;

    public GrpcProductService(IProductRepository productRepository, ILogger<GrpcProductService> logger)
    {
        _productRepository = productRepository;
        _logger = logger;
    }

    public override async Task<GetProductsByIdsResponse> GetProductsByIds(
        GetProductsByIdsRequest request,
        ServerCallContext context)
    {
        if (request is null)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Request is required"));

        if (request.ProductIds.Count == 0)
            return new GetProductsByIdsResponse(); // пустой ответ — норм

        var parsedIds = request.ProductIds
            .Where(id => Guid.TryParse(id, out _))
            .Select(Guid.Parse)
            .Distinct()
            .ToArray();

        if (parsedIds.Length == 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "No valid product IDs were provided"));

        try
        {
            var products = await _productRepository.GetProductsByIdAsync(parsedIds);

            var response = new GetProductsByIdsResponse();

            foreach (var p in products)
            {
                var brief = new ProductBrief
                {
                    Id = p.Id.ToString(),
                    Name = p.Name ?? string.Empty,
                    Price = DecimalToMinorUnits(p.Price),    // decimal -> int64 (копейки/центы)
                    StockQuantity = p.StockQuantity,
                    ShopId = p.ShopId.ToString()
                };

                if (p.ImageUrls is { Length: > 0 })
                    brief.ImageUrls.AddRange(p.ImageUrls);

                response.Products.Add(brief);
            }

            return response;
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetProductsByIds failed for ids: {Ids}", string.Join(",", parsedIds));
            throw new RpcException(new Status(StatusCode.Internal, "An error occurred while processing your request"));
        }
    }

    private static long DecimalToMinorUnits(decimal price)
    {
        var rounded = decimal.Round(price, 2, MidpointRounding.AwayFromZero);
        return (long)(rounded * 100m);
    }
    
    public override async Task<UpdateProductStockResponse> UpdateProductStock(
        UpdateProductStockRequest request,
        ServerCallContext context)
    {
        if (request == null || request.Updates.Count == 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Updates are required"));

        try
        {
            // Парсим список
            var updates = request.Updates
                .Where(u => Guid.TryParse(u.ProductId, out _))
                .ToDictionary(
                    u => Guid.Parse(u.ProductId),
                    u => u.Quantity
                );

            // Тянем продукты из репозитория
            var products = await _productRepository.GetProductsByIdAsync(updates.Keys.ToArray());

            // Проверяем остатки
            foreach (var product in products)
            {
                if (!updates.TryGetValue(product.Id, out var decrease))
                    continue;

                if (product.StockQuantity < decrease)
                    throw new RpcException(new Status(StatusCode.FailedPrecondition,
                        $"Not enough stock for product {product.Name}"));
            }

            // Если всё ок — уменьшаем
            foreach (var product in products)
            {
                if (updates.TryGetValue(product.Id, out var decrease))
                {
                    product.StockQuantity -= decrease;
                    _productRepository.Update(product);
                }
            }

            await _productRepository.SaveChangesAsync();
            return new UpdateProductStockResponse { Success = true };
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateProductStock failed");
            throw new RpcException(new Status(StatusCode.Internal, "Internal server error"));
        }
    }
}
```

**gRPC методы:**
- `GetProductsByIds` - получение краткой информации о продуктах по массиву ID
- `UpdateProductStock` - обновление количества товара на складе (уменьшение остатков)
