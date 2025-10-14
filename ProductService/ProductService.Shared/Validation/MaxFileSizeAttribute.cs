using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ProductService.Shared.Validation;

public class MaxFileSizeAttribute(int maxFileSize) : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is IFormFile file)
        {
            if (file.Length > maxFileSize)
                return new ValidationResult($"File size cannot exceed {maxFileSize} bytes.");
        }
        return ValidationResult.Success;
    }
}