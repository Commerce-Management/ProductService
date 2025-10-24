using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductService.Core.Interfaces;
using ProductService.Shared.DTO;
using ProductService.Shared.DTO.DetailDtos;
using Serilog;


namespace ProductService.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class ProductsController(IProductService productService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<GetProductDto>>> GetAllProducts([FromQuery] int page = 1, [FromQuery] int limit = 10)
    {
        try
        {
            var result = await productService.GetAllProductsAsync(page);
            if (result.Any()) return Ok(result);

            return NotFound("Products not found.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при получении всех продуктов");
            return StatusCode(500, new {
                Error      = ex.Message,
                Exception  = ex.GetType().Name,
                StackTrace = ex.StackTrace
            });
        }
    }
    
    
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<GetProductDto>>> SearchProduct(
        [FromQuery] string query,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest(new { 
                Error      = "Search query cannot be empty.", 
                Exception  = "ArgumentException", 
                StackTrace = string.Empty 
            });
        try
        {
            var products = await productService.SearchProduct(query, page);
            if (!products.Any())
                return NotFound(new { 
                    Error      = "Продукты не найдены.", 
                    Exception  = "NotFoundException", 
                    StackTrace = string.Empty 
                });
    
            return Ok(products);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при поиске продуктов");
            return StatusCode(500, new {
                Error      = ex.Message,
                Exception  = ex.GetType().Name,
                StackTrace = ex.StackTrace
            });
        }
    }

    [HttpGet("shop")]
    public async Task<ActionResult<IEnumerable<GetProductDto>>> GetAllShopProducts([FromQuery] Guid shopId, [FromQuery] int page = 1, [FromQuery] int limit = 10)
    {
        try
        {
            var result = await productService.GetAllShopProductsAsync(shopId, page);
            if (result.Any()) return Ok(result);

            return NotFound("Shop company products not found");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Internal server error occurred.");
            return StatusCode(500, new { Error = "An unexpected error occurred. Please try again later." });
        }        
    }

    [HttpGet("{id:guid}", Name = "getProductById")]
    public async Task<ActionResult<GetProductDto>> GetProductById(Guid id)
    {
        try
        {
            var product = await productService.GetProductByIdAsync(id);
            
            if (product == null)
                return NotFound(new { 
                    Error      = $"Product with ID: {id} not found.", 
                    Exception  = "NotFoundException", 
                    StackTrace = string.Empty 
                });
    
            return Ok(product);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при получении продукта {ProductId}", id);
            return StatusCode(500, new {
                Error      = ex.Message,
                Exception  = ex.GetType().Name,
                StackTrace = ex.StackTrace
            });
        }
    }
    
    [HttpGet("{id:guid}/detail", Name = "getDetailedProductById")]
    public async Task<ActionResult<GetProductDetailDto>> GetDetailProductById(Guid id)
    {
        try
        {
            var product = await productService.GetDetailProductByIdAsync(id);
            
            if (product == null)
                return NotFound(new { 
                    Error      = $"Product with ID: {id} not found.", 
                    Exception  = "NotFoundException", 
                    StackTrace = string.Empty 
                });
    
            return Ok(product);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при получении продукта {ProductId}", id);
            return StatusCode(500, new {
                Error      = ex.Message,
                Exception  = ex.GetType().Name,
                StackTrace = ex.StackTrace
            });
        }
    }

    [Authorize("SuperAdminOrShopOwner")]
    [HttpPost]
    public async Task<ActionResult<GetProductDto>> CreateProduct([FromForm] CreateProductDto productDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { 
                Error      = "Валидация не пройдена.", 
                Exception  = "ModelStateInvalid", 
                StackTrace = string.Empty 
            });

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { 
                Error         = "Не удалось определить пользователя.", 
                Exception     = "UnauthorizedAccess", 
                StackTrace    = string.Empty 
            });

        try
        {
            Log.Information("ProductsController.CreateProduct: Пользователь {UserId} создаёт продукт {@Dto}", userId, productDto);
            var created = await productService.CreateProductAsync(userId, productDto);
            return CreatedAtRoute("getProductById", new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            Log.Warning(ex, "ProductsController.CreateProduct: Ошибка при создании продукта для пользователя {UserId}. DTO: {@Dto}", userId, productDto);
            return BadRequest(new {
                Error      = ex.Message,
                Exception  = ex.GetType().Name,
                StackTrace = ex.StackTrace
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ProductsController.CreateProduct: Неожиданная ошибка для пользователя {UserId}. DTO: {@Dto}", userId, productDto);
            return StatusCode(500, new {
                Error      = ex.Message,
                Exception  = ex.GetType().Name,
                StackTrace = ex.StackTrace
            });
        }
    }

    [Authorize("SuperAdminOrShopOwner")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateProduct(Guid id, [FromForm] UpdateProductDto productDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { 
                Error      = "Валидация не пройдена.", 
                Exception  = "ModelStateInvalid", 
                StackTrace = string.Empty 
            });
    
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { 
                Error         = "Не удалось определить пользователя.", 
                Exception     = "UnauthorizedAccess", 
                StackTrace    = string.Empty 
            });
    
        try
        {
            var success = await productService.UpdateProductAsync(userId, id, productDto);
            if (!success)
                return NotFound(new { 
                    Error      = $"Продукт с ID: {id} не найден или доступ запрещён.", 
                    Exception  = "NotFoundException", 
                    StackTrace = string.Empty 
                });
    
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new {
                Error      = ex.Message,
                Exception  = ex.GetType().Name,
                StackTrace = ex.StackTrace
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ProductsController.UpdateProduct: Ошибка при обновлении продукта {ProductId} для пользователя {UserId}", id, userId);
            return StatusCode(500, new {
                Error      = ex.Message,
                Exception  = ex.GetType().Name,
                StackTrace = ex.StackTrace
            });
        }
    }
    
    [Authorize("SuperAdminOrShopOwner")]
    [HttpDelete("{productId:guid}")]
    public async Task<IActionResult> DeleteProduct(Guid shopId,Guid productId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { 
                Error         = "Не удалось определить пользователя.", 
                Exception     = "UnauthorizedAccess", 
                StackTrace    = string.Empty 
            });
    
        try
        {
            var success = await productService.DeleteProductAsync(userId, shopId, productId);
            if (!success)
                return NotFound(new { 
                    Error      = $"Продукт с ID: {productId} не найден или доступ запрещён.", 
                    Exception  = "NotFoundException", 
                    StackTrace = string.Empty 
                });
    
            return NoContent();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ProductsController.DeleteProduct: Ошибка при удалении продукта {ProductId} для пользователя {UserId}", productId, userId);
            return StatusCode(500, new {
                Error      = ex.Message,
                Exception  = ex.GetType().Name,
                StackTrace = ex.StackTrace
            });
        }
    }
}