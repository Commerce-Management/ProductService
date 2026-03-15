namespace ProductService.Shared.DTO.DetailDtos;

public record GetProductDetailDto(
    string Id,
    string Name,
    string? Description,
    decimal Price,
    int StockQuantity,
    string[] ImageUrls,
    ShopInfoDto Shop,
    CategoryInfoDto[]? Categories
);