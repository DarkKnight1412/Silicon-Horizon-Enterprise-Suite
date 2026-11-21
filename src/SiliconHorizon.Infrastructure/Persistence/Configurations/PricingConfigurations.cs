using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SiliconHorizon.Domain.Entities.Pricing;

namespace SiliconHorizon.Infrastructure.Persistence.Configurations;

/// <summary>Cấu hình nhóm giá & khuyến mãi: CustomerGroups, ProductPrices, PriceTiers, Promotions, Vouchers.</summary>
public class CustomerGroupConfiguration : IEntityTypeConfiguration<CustomerGroup>
{
    public void Configure(EntityTypeBuilder<CustomerGroup> builder)
    {
        builder.ToTable("CustomerGroups");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasColumnType("varchar(30)").IsRequired();   // RETAIL, WHOLESALE
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.IsDefault).HasDefaultValue(false);

        builder.HasIndex(x => x.Code).IsUnique();
    }
}

/// <summary>Cấu hình bảng dbo.ProductPrices (mỗi cặp Product + CustomerGroup là duy nhất).</summary>
public class ProductPriceConfiguration : IEntityTypeConfiguration<ProductPrice>
{
    public void Configure(EntityTypeBuilder<ProductPrice> builder)
    {
        builder.ToTable("ProductPrices");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Price).HasPrecision(18, 0).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.UpdatedAt).HasColumnType("datetime2(0)");

        builder.HasIndex(x => new { x.ProductId, x.CustomerGroupId }).IsUnique();

        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
        builder.HasOne(x => x.CustomerGroup).WithMany(x => x.ProductPrices).HasForeignKey(x => x.CustomerGroupId);
    }
}

/// <summary>Cấu hình bảng dbo.PriceTiers (giá bậc thang theo số lượng, MinQuantity > 1).</summary>
public class PriceTierConfiguration : IEntityTypeConfiguration<PriceTier>
{
    public void Configure(EntityTypeBuilder<PriceTier> builder)
    {
        builder.ToTable("PriceTiers");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Price).HasPrecision(18, 0).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.UpdatedAt).HasColumnType("datetime2(0)");

        builder.HasIndex(x => new { x.ProductId, x.CustomerGroupId, x.MinQuantity }).IsUnique();

        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
        builder.HasOne(x => x.CustomerGroup).WithMany().HasForeignKey(x => x.CustomerGroupId);
    }
}

/// <summary>Cấu hình bảng dbo.Promotions.</summary>
public class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        builder.ToTable("Promotions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DiscountValue).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.StartAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.EndAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.UpdatedAt).HasColumnType("datetime2(0)");
    }
}

/// <summary>Cấu hình bảng dbo.Vouchers.</summary>
public class VoucherConfiguration : IEntityTypeConfiguration<Voucher>
{
    public void Configure(EntityTypeBuilder<Voucher> builder)
    {
        builder.ToTable("Vouchers");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasColumnType("varchar(40)").IsRequired();
        builder.Property(x => x.DiscountValue).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.MinOrderAmount).HasPrecision(18, 0);
        builder.Property(x => x.MaxDiscountAmount).HasPrecision(18, 0);
        builder.Property(x => x.StartAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.EndAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.UsedCount).HasDefaultValue(0);
        builder.Property(x => x.IsStackable).HasDefaultValue(false);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.UpdatedAt).HasColumnType("datetime2(0)");

        builder.HasIndex(x => x.Code).IsUnique();

        builder.HasOne(x => x.CustomerGroup).WithMany().HasForeignKey(x => x.CustomerGroupId);
    }
}
