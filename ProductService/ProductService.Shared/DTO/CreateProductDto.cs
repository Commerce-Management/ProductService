using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System;
using ProductService.Shared.Validation;

namespace ProductService.Shared.DTO;

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
    List<string> CategoryIds,
    
    // New fields for custom bags (made nullable to not break existing product creation)
    string? DesignData,
    string? PreviewImage,
    Guid? UserId // Use Guid? for nullable Guid
);
