namespace ProductService.Core.Entities;

public class ProductCategory : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Guid CategoryId { get; set; }

    public Product Product { get; set; } = null!;
}