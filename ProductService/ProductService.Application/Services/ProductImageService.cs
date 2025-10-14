using Imagekit.Sdk;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using ProductService.Core.Interfaces;

namespace ProductService.Application.Services;

public class ProductImageService(IConfiguration configuration) : IProductImageService
{
    private readonly ImagekitClient _imagekitClient = new (configuration["ImageKit:PublicKey"], configuration["ImageKit:PrivateKey"], configuration["ImageKit:UrlEndPoint"]);
    
    public async  Task<string> UploadProductImageAsync(IFormFile imageData)
    {
        if (imageData == null || imageData.Length == 0)
            throw new ArgumentException("Image data is null or empty.", nameof(imageData));

        using var memoryStream = new MemoryStream();
        await imageData.CopyToAsync(memoryStream);
        var fileBytes = memoryStream.ToArray();

        var ob = new FileCreateRequest
        {
            file = fileBytes,
            fileName = Guid.NewGuid().ToString()
        };
        
        var result = await _imagekitClient.UploadAsync(ob);
        return result.url;
    }

    public Task<bool> DeleteProductImageAsync(string imageUrl)
    {
        throw new NotImplementedException();
    }
}