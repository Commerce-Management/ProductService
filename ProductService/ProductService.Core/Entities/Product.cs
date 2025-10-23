namespace ProductService.Core.Entities;

public class Product : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public decimal Price { get; set; }
    
    public int StockQuantity { get; set; }  

    public string[] ImageUrls { get; set; } = Array.Empty<string>();
    
    public Guid ShopId { get; set; } 
    
    public virtual ICollection<ProductCategory> ProductCategories { get; set; } = new List<ProductCategory>();
}