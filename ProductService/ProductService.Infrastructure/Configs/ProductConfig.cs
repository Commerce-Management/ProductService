using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductService.Core.Entities;

namespace ProductService.Infrastructure.Configs;

public class ProductConfig : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ProductID");

        builder.Property(e => e.Name)
            .HasMaxLength(100)
            .HasColumnType("text");

        builder.Property(e => e.Description)
            .HasMaxLength(500)
            .HasColumnType("text");

        builder.Property(e => e.Price)
            .HasColumnType("decimal(10,2)");

        builder.Property(p => p.StockQuantity)
            .IsRequired()
            .HasColumnType("integer");

        // ImageUrls — теперь массив text[]
        builder.Property(e => e.ImageUrls)
            .IsRequired()
            .HasColumnType("text[]");

        builder.Property(p => p.DesignData)
            .HasColumnType("jsonb")
            .IsRequired(false);

        builder.Property(p => p.PreviewImage)
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(p => p.Status)
            .HasMaxLength(50)
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(p => p.UserId)
            .IsRequired(false)
            .HasColumnType("uuid");
    }
}