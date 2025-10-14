using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ProductService.Shared.Validation;

public class AllowedExtensionsAttribute(string[] extensions) : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName);
            if (!extensions.Contains(extension.ToLower()))
            {
                return new ValidationResult($"File extension {extension} is not allowed.");
            }
        }
        return ValidationResult.Success;
    }
}