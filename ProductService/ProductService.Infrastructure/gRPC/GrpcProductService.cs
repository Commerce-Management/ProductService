using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProductService.Core.Interfaces;
using ProductService.Infrastructure.Interfaces.Entities; // IProductRepository
using ProductService.Shared.Protos.GrpcProductService;

namespace ProductService.Infrastructure.gRPC;

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
