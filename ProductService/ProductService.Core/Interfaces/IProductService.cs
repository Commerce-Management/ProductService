using ProductService.Shared.DTO;

namespace ProductService.Core.Interfaces;

public interface IProductService
{
    Task<IEnumerable<GetProductDto>> GetAllProductsAsync(int page);
    Task<IEnumerable<GetProductDto>> SearchProduct(string parameter, int page);
    Task<GetProductDto> GetProductByIdAsync(Guid productId);
    Task<IEnumerable<GetProductDto>> GetAllShopProductsAsync(Guid shopId, int page);
    Task<GetProductDto> CreateProductAsync(Guid userId, CreateProductDto productDto);
    Task<bool> UpdateProductAsync(Guid userId, Guid productId, UpdateProductDto productDto);
    Task<bool> DeleteProductAsync(Guid userId, Guid shopId, Guid productId);
}