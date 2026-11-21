using Microsoft.EntityFrameworkCore;
using SiliconHorizon.Application.Abstractions.Persistence;
using SiliconHorizon.Domain.Entities.Catalog;
using SiliconHorizon.Domain.Entities.Commission;
using SiliconHorizon.Domain.Entities.Common;
using SiliconHorizon.Domain.Entities.Customers;
using SiliconHorizon.Domain.Entities.Hr;
using SiliconHorizon.Domain.Entities.Inventory;
using SiliconHorizon.Domain.Entities.Pricing;

namespace SiliconHorizon.Infrastructure.Persistence;

/// <summary>
/// DbContext EF Core với SQL Server.
/// <para>
/// Chỉ ánh xạ các bảng <b>cấu hình/danh mục</b> (28 bảng — xem <c>Domain/Entities/README.md</c>).
/// Các bảng giao dịch (đơn hàng, giỏ, kho, serial, bảo hành, đổi trả...) đi qua stored procedure + Dapper
/// vì proc đã tự bảo đảm giao dịch và chống bán vượt tồn (NFR-01).
/// </para>
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    // ── Module chung / phân quyền ─────────────────────────────────────────────
    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<Branch> Branches => Set<Branch>();

    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    // ── Danh mục sản phẩm ────────────────────────────────────────────────────
    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Brand> Brands => Set<Brand>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    public DbSet<CompatibilityRule> CompatibilityRules => Set<CompatibilityRule>();

    // ── Giá và khuyến mãi ────────────────────────────────────────────────────
    public DbSet<CustomerGroup> CustomerGroups => Set<CustomerGroup>();

    public DbSet<ProductPrice> ProductPrices => Set<ProductPrice>();

    public DbSet<PriceTier> PriceTiers => Set<PriceTier>();

    public DbSet<Promotion> Promotions => Set<Promotion>();

    public DbSet<Voucher> Vouchers => Set<Voucher>();

    // ── Khách hàng ───────────────────────────────────────────────────────────
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();

    // ── Nhân sự ──────────────────────────────────────────────────────────────
    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<Shift> Shifts => Set<Shift>();

    public DbSet<EmployeeSchedule> EmployeeSchedules => Set<EmployeeSchedule>();

    public DbSet<Attendance> Attendances => Set<Attendance>();

    // ── Nhà cung cấp ─────────────────────────────────────────────────────────
    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<SupplierProduct> SupplierProducts => Set<SupplierProduct>();

    // ── Hoa hồng ─────────────────────────────────────────────────────────────
    public DbSet<CommissionRule> CommissionRules => Set<CommissionRule>();

    public DbSet<CommissionPeriod> CommissionPeriods => Set<CommissionPeriod>();

    public DbSet<CommissionEntry> CommissionEntries => Set<CommissionEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Mọi cấu hình nằm trong Persistence/Configurations/*.cs (IEntityTypeConfiguration).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
