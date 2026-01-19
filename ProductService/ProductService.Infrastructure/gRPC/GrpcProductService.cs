using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProductService.Core.Interfaces;
using ProductService.Infrastructure.Interfaces.Entities;
using ProductService.Shared.Protos.GrpcOrderService; // IProductRepository
using ProductService.Shared.Protos.GrpcProductService;

namespace ProductService.Infrastructure.gRPC;

public class GrpcProductService : ProductService.Shared.Protos.GrpcProductService.ProductService.ProductServiceBase
{
    private readonly IProductRepository _productRepository;
    private readonly IProductCategoryRepository _productCategoryRepository;
    private readonly OrderService.OrderServiceClient _orderClient;
    private readonly ILogger<GrpcProductService> _logger;

    public GrpcProductService(
        IProductRepository productRepository,
        IProductCategoryRepository productCategoryRepository,
        OrderService.OrderServiceClient orderClient,
        ILogger<GrpcProductService> logger)
    {
        _productRepository = productRepository;
        _productCategoryRepository = productCategoryRepository;
        _orderClient = orderClient;
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
                    Price = DecimalToMinorUnits(p.Price), // decimal -> int64 (копейки/центы)
                    StockQuantity = p.StockQuantity,
                    ShopId = p.ShopId.ToString()
                };

                if (p.ImageUrls is { Length: > 0 })
                    brief.ImageUrls.AddRange(p.ImageUrls);

                response.Products.Add(brief);
            }

            return response;
        }
        catch (RpcException)
        {
            throw;
        }
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
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateProductStock failed");
            throw new RpcException(new Status(StatusCode.Internal, "Internal server error"));
        }
    }

    public override async Task<GetCandidateProductIdsByProdIdsFromReviewsResponse>
        GetCandidateProductIdsByProdIdsFromReviews(
            GetCandidateProductIdsByProdIdsFromReviewsRequest request,
            ServerCallContext context)
    {
        if (request == null || request.ProductIdsFromReviews.Count == 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Product ids from reviews are required"));

        if (string.IsNullOrWhiteSpace(request.UserId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "User id is required"));

        if (!Guid.TryParse(request.UserId, out var userId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user id"));

        var parsedProductIds = request.ProductIdsFromReviews
            .Where(id => Guid.TryParse(id, out _))
            .Select(Guid.Parse)
            .Distinct()
            .ToArray();

        if (parsedProductIds.Length == 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "No valid product ids provided"));

        try
        {
            // 1. Получаем category IDs из product IDs (из отзывов)
            var categoryIds = await _productCategoryRepository.GetCategoryIdsByProductIdsAsync(parsedProductIds);

            if (categoryIds == null || !categoryIds.Any())
            {
                _logger.LogWarning(
                    "No categories found for productIds: {ProductIds}",
                    string.Join(",", parsedProductIds));
                return new GetCandidateProductIdsByProdIdsFromReviewsResponse();
            }

            // 2. Получаем все product IDs по найденным category IDs
            var candidateProductIds = await _productCategoryRepository
                .GetProductIdsByCategoryIdsAsync(categoryIds.ToArray());

            if (candidateProductIds == null || !candidateProductIds.Any())
            {
                _logger.LogWarning(
                    "No products found for categoryIds: {CategoryIds}",
                    string.Join(",", categoryIds));
                return new GetCandidateProductIdsByProdIdsFromReviewsResponse();
            }

            // 3. Получаем купленные продукты из OrderService
            var purchasedProductIds = await _orderClient.GetPurchasedProductIdsByUserIdAsync(
                new GetPurchasedProductIdsByUserIdRequest
                {
                    UserId = request.UserId
                });

            // 4. Фильтруем: исключаем уже купленные продукты (со статусами Delivered/Shipped/etc)
            var purchasedIds = purchasedProductIds.ProductIds
                .Where(id => Guid.TryParse(id, out _))
                .Select(Guid.Parse)
                .ToHashSet();

            var filteredCandidates = candidateProductIds
                .Where(id => !purchasedIds.Contains(id))
                .ToList();

            // 5. Формируем ответ
            var response = new GetCandidateProductIdsByProdIdsFromReviewsResponse();
            response.ProductIdsCandidate.AddRange(filteredCandidates.Select(id => id.ToString()));

            _logger.LogInformation(
                "GetCandidateProductIdsByProdIdsFromReviews: UserId={UserId}, Input={InputCount}, Categories={CategoryCount}, Candidates={CandidateCount}, Purchased={PurchasedCount}, Filtered={FilteredCount}",
                userId,
                parsedProductIds.Length,
                categoryIds.Count(),
                candidateProductIds.Count(),
                purchasedIds.Count,
                filteredCandidates.Count);

            return response;
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "GetCandidateProductIdsByProdIdsFromReviews failed for userId: {UserId}, productIds: {ProductIds}",
                userId,
                string.Join(",", parsedProductIds));

            throw new RpcException(
                new Status(StatusCode.Internal, $"An error occurred while processing the request: {ex.Message}"));
        }
    }
    
    public override async Task<GetAllProductsForIndexingResponse> GetAllProductsForIndexing(
        GetAllProductsForIndexingRequest request,
        ServerCallContext context)
    {
        try
        {
            var page = request.Page > 0 ? request.Page : 1;
            var pageSize = request.PageSize > 0 ? request.PageSize : 100;

            _logger.LogInformation("GetAllProductsForIndexing: page={Page}, pageSize={PageSize}", 
                page, pageSize);

            // Получаем продукты
            var products = await _productRepository.GetAllProductsAsync(page, pageSize);
        
            // Общее количество (для пагинации)
            var totalCount = await _productRepository.GetTotalCountAsync();

            var response = new GetAllProductsForIndexingResponse
            {
                TotalCount = totalCount
            };

            foreach (var product in products)
            {
                var productForIndexing = new ProductForIndexing
                {
                    Id = product.Id.ToString(),
                    Name = product.Name
                };

                // Добавляем картинки
                if (product.ImageUrls?.Any() == true)
                    productForIndexing.ImageUrls.AddRange(product.ImageUrls);

                // Добавляем ID категорий (без названий!)
                if (product.ProductCategories?.Any() == true)
                {
                    var categoryIds = product.ProductCategories
                        .Select(pc => pc.CategoryId.ToString())
                        .ToList();
                
                    productForIndexing.CategoryIds.AddRange(categoryIds);
                }

                response.Products.Add(productForIndexing);
            }

            _logger.LogInformation("Returning {Count} products out of {Total}", 
                products.Count, totalCount);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetAllProductsForIndexing failed");
            throw new RpcException(new Status(StatusCode.Internal, $"Internal error: {ex.Message}"));
        }
    }
}