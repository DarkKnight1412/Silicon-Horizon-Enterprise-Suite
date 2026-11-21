using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SiliconHorizon.Domain.Entities.Catalog;

namespace SiliconHorizon.Infrastructure.Persistence.Configurations;

/// <summary>Cấu hình nhóm bảng danh mục: Categories, Brands, Products, ProductImages, CompatibilityRules.</summary>
public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasColumnType("varchar(30)").IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SpecType).IsRequired();           // 0 Generic ... 6 PSU
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.UpdatedAt).HasColumnType("datetime2(0)");

        builder.HasIndex(x => x.Code).IsUnique();

        builder.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng dbo.Brands.</summary>
public class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("Brands");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.UpdatedAt).HasColumnType("datetime2(0)");

        builder.HasIndex(x => x.Name).IsUnique();
    }
}

/// <summary>Cấu hình bảng dbo.Products.</summary>
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Sku).HasColumnType("varchar(50)").IsRequired();
        builder.Property(x => x.Name).HasMaxLength(250).IsRequired();
        builder.Property(x => x.Slug).HasColumnType("varchar(300)").IsRequired();
        builder.Property(x => x.ShortDescription).HasMaxLength(500);
        builder.Property(x => x.Description).HasColumnType("nvarchar(max)");
        builder.Property(x => x.LastImportPrice).HasPrecision(18, 0);
        builder.Property(x => x.ListPrice).HasPrecision(18, 0).IsRequired();
        builder.Property(x => x.RequiresSerial).HasDefaultValue(true);
        builder.Property(x => x.WarrantyTerms).HasMaxLength(1000);
        builder.Property(x => x.Status).IsRequired();              // 0 Draft, 1 Active, 2 Discontinued
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.UpdatedAt).HasColumnType("datetime2(0)");

        builder.HasIndex(x => x.Sku).IsUnique();
        builder.HasIndex(x => x.Slug).IsUnique();

        builder.HasOne(x => x.Brand).WithMany(x => x.Products).HasForeignKey(x => x.BrandId);
        builder.HasOne(x => x.Category).WithMany(x => x.Products).HasForeignKey(x => x.CategoryId);
    }
}

/// <summary>Cấu hình bảng dbo.ProductImages (FK ON DELETE CASCADE theo script gốc).</summary>
public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("ProductImages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Url).HasColumnType("varchar(500)").IsRequired();

        builder.HasOne(x => x.Product)
            .WithMany(x => x.Images)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Cấu hình bảng dbo.CompatibilityRules (2 khóa ngoại cùng trỏ về Categories).</summary>
public class CompatibilityRuleConfiguration : IEntityTypeConfiguration<CompatibilityRule>
{
    public void Configure(EntityTypeBuilder<CompatibilityRule> builder)
    {
        builder.ToTable("CompatibilityRules");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.LeftField).HasColumnType("varchar(50)").IsRequired();
        builder.Property(x => x.RightField).HasColumnType("varchar(50)").IsRequired();
        builder.Property(x => x.Operator).HasColumnType("varchar(10)").IsRequired();  // EQ | LTE | GTE
        builder.Property(x => x.Severity).IsRequired();                          // 0 Info, 1 Warning
        builder.Property(x => x.IsActive).HasDefaultValue(true);

        builder.HasOne(x => x.LeftCategory)
            .WithMany()
            .HasForeignKey(x => x.LeftCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RightCategory)
            .WithMany()
            .HasForeignKey(x => x.RightCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
