using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SiliconHorizon.Domain.Entities.Customers;
using SiliconHorizon.Domain.Entities.Inventory;

namespace SiliconHorizon.Infrastructure.Persistence.Configurations;

/// <summary>Cấu hình nhóm khách hàng và nhà cung cấp.</summary>
public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Phone).HasColumnType("varchar(20)").IsRequired();
        builder.Property(x => x.Email).HasColumnType("varchar(150)");
        builder.Property(x => x.TaxCode).HasColumnType("varchar(20)");
        builder.Property(x => x.CreditLimit).HasPrecision(18, 0).HasDefaultValue(0m);
        builder.Property(x => x.PaymentTermDays).HasDefaultValue(0);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.UpdatedAt).HasColumnType("datetime2(0)");

        builder.HasOne(x => x.CustomerGroup)
            .WithMany()
            .HasForeignKey(x => x.CustomerGroupId);
    }
}

/// <summary>Cấu hình bảng dbo.CustomerAddresses.</summary>
public class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddress>
{
    public void Configure(EntityTypeBuilder<CustomerAddress> builder)
    {
        builder.ToTable("CustomerAddresses");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RecipientName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Phone).HasColumnType("varchar(20)").IsRequired();
        builder.Property(x => x.Province).HasMaxLength(100).IsRequired();
        builder.Property(x => x.District).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Ward).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Street).HasMaxLength(250).IsRequired();

        builder.HasOne(x => x.Customer)
            .WithMany(x => x.Addresses)
            .HasForeignKey(x => x.CustomerId);
    }
}

/// <summary>Cấu hình bảng dbo.Suppliers.</summary>
public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasColumnType("varchar(30)").IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.TaxCode).HasColumnType("varchar(20)");
        builder.Property(x => x.Phone).HasColumnType("varchar(20)");
        builder.Property(x => x.Address).HasMaxLength(300);
        builder.Property(x => x.PaymentTermDays).HasDefaultValue(0);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.UpdatedAt).HasColumnType("datetime2(0)");

        builder.HasIndex(x => x.Code).IsUnique();
    }
}

/// <summary>Cấu hình bảng nối dbo.SupplierProducts.</summary>
public class SupplierProductConfiguration : IEntityTypeConfiguration<SupplierProduct>
{
    public void Configure(EntityTypeBuilder<SupplierProduct> builder)
    {
        builder.ToTable("SupplierProducts");
        builder.HasKey(x => new { x.SupplierId, x.ProductId });

        builder.Property(x => x.SupplierSku).HasColumnType("varchar(50)");

        builder.HasOne(x => x.Supplier).WithMany(x => x.Products).HasForeignKey(x => x.SupplierId);
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
    }
}
