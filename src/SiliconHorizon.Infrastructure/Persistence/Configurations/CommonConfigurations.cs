using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SiliconHorizon.Domain.Entities.Common;

namespace SiliconHorizon.Infrastructure.Persistence.Configurations;

/// <summary>Cấu hình nhóm bảng chung: người dùng, phân quyền RBAC, chi nhánh, cấu hình hệ thống.</summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Username).HasColumnType("varchar(50)").IsRequired();
        builder.Property(x => x.Email).HasColumnType("varchar(150)");
        builder.Property(x => x.Phone).HasColumnType("varchar(20)");
        builder.Property(x => x.PasswordHash).HasColumnType("varchar(255)").IsRequired();
        builder.Property(x => x.UserType).IsRequired();          // 0 Customer, 1 Employee
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.LastLoginAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.UpdatedAt).HasColumnType("datetime2(0)");

        builder.HasIndex(x => x.Username).IsUnique();

        builder.HasMany(x => x.UserRoles)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId);
    }
}

/// <summary>Cấu hình bảng dbo.Roles.</summary>
public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasColumnType("varchar(50)").IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();

        builder.HasIndex(x => x.Code).IsUnique();
    }
}

/// <summary>Cấu hình bảng dbo.Permissions.</summary>
public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasColumnType("varchar(100)").IsRequired();

        builder.HasIndex(x => x.Code).IsUnique();
    }
}

/// <summary>Cấu hình bảng nối dbo.UserRoles (khóa chính tổ hợp).</summary>
public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles");
        builder.HasKey(x => new { x.UserId, x.RoleId });

        builder.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserId);
        builder.HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleId);
    }
}

/// <summary>Cấu hình bảng nối dbo.RolePermissions (khóa chính tổ hợp).</summary>
public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");
        builder.HasKey(x => new { x.RoleId, x.PermissionId });

        builder.HasOne(x => x.Role).WithMany(x => x.RolePermissions).HasForeignKey(x => x.RoleId);
        builder.HasOne(x => x.Permission).WithMany(x => x.RolePermissions).HasForeignKey(x => x.PermissionId);
    }
}

/// <summary>Cấu hình bảng dbo.Branches.</summary>
public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("Branches");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasColumnType("varchar(20)").IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Address).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Phone).HasColumnType("varchar(20)");
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.UpdatedAt).HasColumnType("datetime2(0)");

        builder.HasIndex(x => x.Code).IsUnique();
    }
}

/// <summary>Cấu hình bảng dbo.SystemSettings (khóa chính là cột [Key]).</summary>
public class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.ToTable("SystemSettings");
        builder.HasKey(x => x.Key);

        builder.Property(x => x.Key).HasColumnName("Key").HasColumnType("varchar(100)");
        builder.Property(x => x.Value).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(300);
        builder.Property(x => x.UpdatedAt).HasColumnType("datetime2(0)");
    }
}
