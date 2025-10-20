namespace ProductService.Shared.DTO;

public record GetProductDto(
    string Id,
    string Name,
    string Description,
    decimal Price,
    int StockQuantity,
    string[] ImageUrls
    // List<CategoryNameDto>? Categories,
);