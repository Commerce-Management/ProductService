namespace ProductService.Shared.DTO.DetailDtos;
public record CategoryInfoDto(
    string Id,
    string Name,
    string? Slug,
    int Level,
    bool IsActive,
    CategoryInfoDto? ParentCategory = null
);