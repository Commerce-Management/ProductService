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
    List<string> CategoryIds
);
