using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductService.Core.Interfaces;
using ProductService.Shared.DTO;
using Serilog;


namespace ProductService.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class ProductsController(IProductService productService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<GetProductDto>>> GetAllProducts(
        [FromQuery] bool loadFullImages = false)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var currentUserId))
            return Unauthorized(new { 
                Error         = "Не удалось определить пользователя.", 
                Exception     = "UnauthorizedAccess", 
                StackTrace    = string.Empty 
            });

        try
        {
            var products = await productService.GetAllProductsAsync(currentUserId, loadFullImages);
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
            Log.Error(ex, "Ошибка при получении всех продуктов для пользователя {UserId}", currentUserId);
            return StatusCode(500, new {
                Error      = ex.Message,
                Exception  = ex.GetType().Name,
                StackTrace = ex.StackTrace
            });
        }
    }

    [HttpGet("paginated")]
    public async Task<ActionResult<IEnumerable<GetProductDto>>> GetPaginatedProducts(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] bool loadFullImages = false,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] string? sortField = null,
        [FromQuery] string? sortDirection = "asc")
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var currentUserId))
            return Unauthorized(new { 
                Error         = "Не удалось определить пользователя.", 
                Exception     = "UnauthorizedAccess", 
                StackTrace    = string.Empty 
            });

        if (pageNumber < 1 || pageSize < 1)
            return BadRequest(new { 
                Error         = "Номер страницы и размер страницы должны быть больше 0.", 
                Exception     = "ArgumentOutOfRangeException", 
                StackTrace    = string.Empty 
            });

        try
        {
            var (products, totalCount) = await productService.GetPaginatedProductsAsync(
                currentUserId, pageNumber, pageSize, loadFullImages, categoryId, sortField, sortDirection);

            Response.Headers["X-Total-Count"] = totalCount.ToString();
            Response.Headers["X-Page-Number"] = pageNumber.ToString();
            Response.Headers["X-Page-Size"] = pageSize.ToString();

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
            Log.Error(ex, "Ошибка при получении пагинированных продуктов для пользователя {UserId}", currentUserId);
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
        [FromQuery] bool loadFullImages = false)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest(new { 
                Error      = "Search query cannot be empty.", 
                Exception  = "ArgumentException", 
                StackTrace = string.Empty 
            });

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var currentUserId))
            return Unauthorized(new { 
                Error         = "Не удалось определить пользователя.", 
                Exception     = "UnauthorizedAccess", 
                StackTrace    = string.Empty 
            });

        try
        {
            var products = await productService.SearchProduct(currentUserId, query, loadFullImages);
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
            Log.Error(ex, "Ошибка при поиске продуктов для пользователя {UserId}", currentUserId);
            return StatusCode(500, new {
                Error      = ex.Message,
                Exception  = ex.GetType().Name,
                StackTrace = ex.StackTrace
            });
        }
    }

    [HttpGet("{id:guid}", Name = "getProductById")]
    public async Task<ActionResult<GetProductDto>> GetProductById(
        Guid id,
        [FromQuery] bool loadFullImages = true)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var currentUserId))
            return Unauthorized(new { 
                Error         = "Не удалось определить пользователя.", 
                Exception     = "UnauthorizedAccess", 
                StackTrace    = string.Empty 
            });

        try
        {
            var product = await productService.GetProductByIdAsync(currentUserId, id, loadFullImages);
            if (product == null)
                return NotFound(new { 
                    Error      = $"Продукт с ID: {id} не найден.", 
                    Exception  = "NotFoundException", 
                    StackTrace = string.Empty 
                });

            return Ok(product);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при получении продукта {ProductId} для пользователя {UserId}", id, currentUserId);
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
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var currentUserId))
            return Unauthorized(new { 
                Error         = "Не удалось определить пользователя.", 
                Exception     = "UnauthorizedAccess", 
                StackTrace    = string.Empty 
            });

        try
        {
            Log.Information("ProductsController.CreateProduct: Пользователь {UserId} создаёт продукт {@Dto}", currentUserId, productDto);
            var created = await productService.CreateProductAsync(currentUserId, productDto);
            return CreatedAtRoute("getProductById", new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            Log.Warning(ex, "ProductsController.CreateProduct: Ошибка при создании продукта для пользователя {UserId}. DTO: {@Dto}", currentUserId, productDto);
            return BadRequest(new {
                Error      = ex.Message,
                Exception  = ex.GetType().Name,
                StackTrace = ex.StackTrace
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ProductsController.CreateProduct: Неожиданная ошибка для пользователя {UserId}. DTO: {@Dto}", currentUserId, productDto);
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
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var currentUserId))
            return Unauthorized(new { 
                Error         = "Не удалось определить пользователя.", 
                Exception     = "UnauthorizedAccess", 
                StackTrace    = string.Empty 
            });

        try
        {
            var success = await productService.UpdateProductAsync(currentUserId, id, productDto);
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
            Log.Error(ex, "ProductsController.UpdateProduct: Ошибка при обновлении продукта {ProductId} для пользователя {UserId}", id, currentUserId);
            return StatusCode(500, new {
                Error      = ex.Message,
                Exception  = ex.GetType().Name,
                StackTrace = ex.StackTrace
            });
        }
    }

    [Authorize("SuperAdminOrShopOwner")]
    [HttpDelete("{productId:guid}")]
    public async Task<IActionResult> DeleteProduct(Guid productId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var currentUserId))
            return Unauthorized(new { 
                Error         = "Не удалось определить пользователя.", 
                Exception     = "UnauthorizedAccess", 
                StackTrace    = string.Empty 
            });

        try
        {
            var success = await productService.DeleteProductAsync(currentUserId, productId);
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
            Log.Error(ex, "ProductsController.DeleteProduct: Ошибка при удалении продукта {ProductId} для пользователя {UserId}", productId, currentUserId);
            return StatusCode(500, new {
                Error      = ex.Message,
                Exception  = ex.GetType().Name,
                StackTrace = ex.StackTrace
            });
        }
    }
}