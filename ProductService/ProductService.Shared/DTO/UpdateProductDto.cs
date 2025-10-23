using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ProductService.Shared.DTO;

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