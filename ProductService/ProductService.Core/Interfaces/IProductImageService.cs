using Microsoft.AspNetCore.Http;

namespace ProductService.Core.Interfaces;

public interface IProductImageService
{
    public Task<string> UploadProductImageAsync(IFormFile imageData);
    public Task<bool> DeleteProductImageAsync(string imageUrl);
}