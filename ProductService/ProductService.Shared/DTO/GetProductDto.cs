namespace ProductService.Shared.DTO;

public record GetProductDto(
    string Id,
    string Name,
    string Description,
    decimal Price,
    int StockQuantity,
    string[] ImageUrls,
    // List<CategoryNameDto>? Categories,
    string? DesignData,    // JSON с данными 3D модели
    string? PreviewImage,  // Base64 превью
    string? Status,        // Draft, Ordered
    Guid? UserId          // ID пользователя, создавшего дизайн
);