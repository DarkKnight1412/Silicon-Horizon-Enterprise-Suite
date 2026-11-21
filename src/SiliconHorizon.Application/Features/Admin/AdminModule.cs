using Microsoft.Extensions.DependencyInjection;

namespace SiliconHorizon.Application.Features.Admin;

/// <summary>
/// Đăng ký DI của module Admin — nhóm CRUD quản trị còn lại ở mục 10 của Database/README.md
/// (thương hiệu, quy tắc tương thích, chi nhánh, ca làm, cấu hình hệ thống, quy tắc hoa hồng,
/// nhân viên và phân quyền tài khoản). Được <c>AddApplication()</c> gọi lại.
/// </summary>
/// <remarks>
/// Vòng đời <b>Scoped</b>: các service dùng repository EF Core, <c>ISqlExecutor</c>, <c>ICurrentUser</c>
/// và <c>IPasswordHasher</c> theo vòng đời request (xem DEPENDENCY_INJECTION.md mục 3).
/// </remarks>
public static class AdminModule
{
    public static IServiceCollection AddAdminModule(this IServiceCollection services)
    {
        // Danh mục, thương hiệu (bổ sung cho CategoryService đã có ở module Catalog).
        services.AddScoped<IBrandService, BrandService>();

        // Quy tắc tương thích linh kiện (PRD-11) — dữ liệu chủ để chuyển usp_Compat_Check sang đọc bảng.
        services.AddScoped<ICompatibilityRuleService, CompatibilityRuleService>();

        // Chi nhánh và danh mục ca làm (dữ liệu chủ cho tồn kho, đơn hàng, xếp ca).
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<IShiftService, ShiftService>();

        // Cấu hình hệ thống (thời hạn giữ hàng, hạn đổi trả...) — khóa chính là cột [Key].
        services.AddScoped<ISystemSettingService, SystemSettingService>();

        // Quy tắc hoa hồng (KPI-01..03).
        services.AddScoped<ICommissionRuleService, CommissionRuleService>();

        // Nhân viên: đồng bộ Users + UserRoles + Employees.
        services.AddScoped<IEmployeeService, EmployeeService>();

        // Phân quyền: vai trò, quyền, và gán vai trò cho tài khoản.
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IUserRoleService, UserRoleService>();

        // Bảng nối nhà cung cấp ↔ sản phẩm (hoàn thiện dòng "suppliers, suppliers/{id}/products" ở mục 10).
        services.AddScoped<ISupplierProductService, SupplierProductService>();

        return services;
    }
}
