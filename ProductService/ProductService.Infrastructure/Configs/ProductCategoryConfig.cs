using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductService.Core.Entities;

namespace ProductService.Infrastructure.Configs;

public class ProductCategoryConfig : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> builder)
    {
        builder.HasKey(pc => pc.Id);

        builder.HasIndex(pc => new { pc.ProductId, pc.CategoryId })
            .IsUnique();

        builder.Property(pc => pc.ProductId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(pc => pc.CategoryId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.HasOne(pc => pc.Product)
            .WithMany(p => p.ProductCategories)
            .HasForeignKey(pc => pc.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}