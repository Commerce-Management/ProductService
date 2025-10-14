using ProductService.Shared.DTO;

namespace ProductService.Core.Interfaces;

public interface IProductService
{
    Task<IEnumerable<GetProductDto>> GetAllProductsAsync(Guid currentUserId, bool loadFullImages = false);
    Task<(IEnumerable<GetProductDto> Products, int TotalCount)> GetPaginatedProductsAsync(
        Guid currentUserId,
        int pageNumber,
        int pageSize,
        bool loadFullImages = false,
        Guid? categoryId = null,
        string? sortField = null,
        string? sortDirection = "asc");
    Task<IEnumerable<GetProductDto>> SearchProduct(Guid currentUserId, string searchTerm, bool loadFullImages = false);
    Task<GetProductDto> GetProductByIdAsync(Guid currentUserId, Guid id, bool loadFullImages = true);
    Task<GetProductDto> CreateProductAsync(Guid currentUserId, CreateProductDto productDto);
    Task<bool> UpdateProductAsync(Guid currentUserId, Guid id, UpdateProductDto productDto);
    Task<bool> DeleteProductAsync(Guid currentUserId, Guid productId);
}