using Microsoft.Extensions.DependencyInjection;
using SiliconHorizon.Application.Common.Audit;
using SiliconHorizon.Application.Features.Admin;
using SiliconHorizon.Application.Features.Audit;
using SiliconHorizon.Application.Features.Auth;
using SiliconHorizon.Application.Features.Cart;
using SiliconHorizon.Application.Features.Catalog;
using SiliconHorizon.Application.Features.Commission;
using SiliconHorizon.Application.Features.Customers;
using SiliconHorizon.Application.Features.Hr;
using SiliconHorizon.Application.Features.Inventory;
using SiliconHorizon.Application.Features.Orders;
using SiliconHorizon.Application.Features.PcBuilds;
using SiliconHorizon.Application.Features.Pricing;
using SiliconHorizon.Application.Features.Reports;
using SiliconHorizon.Application.Features.Returns;
using SiliconHorizon.Application.Features.Warranty;

namespace SiliconHorizon.Application;

/// <summary>
/// Composition root của tầng Application: chỉ đăng ký use case của từng module.
/// Thêm module mới = thêm đúng một dòng dưới đây; chi tiết nằm trong
/// <c>Features/&lt;Module&gt;/&lt;Module&gt;Module.cs</c> (quy ước thống nhất).
/// <para>
/// Vòng đời mặc định: <b>Scoped</b> — vì service dùng repository/executor cũng là Scoped
/// (xem DEPENDENCY_INJECTION.md mục 3).
/// </para>
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // ── Module đã có use case ───────────────────────────────────────────
        services.AddAuthModule();
        services.AddCatalogModule();
        services.AddCartModule();
        services.AddPcBuildModule();  // bộ PC dựng sẵn — khách thêm cả bộ vào giỏ (usp_PcBuild_*)
        services.AddOrdersModule();
        services.AddInventoryModule();      // kho, nhập/chuyển/kiểm kê, serial, nhà cung cấp
        services.AddWarrantyModule();       // usp_Warranty_* (bảo hành, tra cứu bảo hành theo serial)
        services.AddReturnsModule();        // usp_Return_* (đổi trả)
        services.AddCommissionModule();     // usp_Commission_* (hoa hồng, điều chỉnh, chốt kỳ)
        services.AddHrModule();             // usp_Schedule_*, usp_Attendance_* (xếp ca, chấm công)
        services.AddAuditModule();          // usp_Audit_Search (tra cứu nhật ký — bảng chỉ đọc)
        services.AddReportModule();         // usp_Report_* (doanh thu, tồn kho, serial, công nợ, bảo hành)
        services.AddPricingModule();        // giá, khuyến mãi, voucher (usp_Voucher_Validate + EF)
        services.AddCustomerModule();       // khách hàng, sổ địa chỉ (EF + ép customerId từ token)
        services.AddAdminModule();          // CRUD quản trị còn lại (mục 10 của Database/README.md)

        // ── Tiện ích dùng chung cho CRUD quản trị ───────────────────────────
        // Ghi nhật ký thao tác nhạy cảm (đổi giá, đổi cấu hình, tạo/sửa nhân viên, phân quyền...)
        // bằng cách gọi trực tiếp usp_Audit_Write — dùng chung cho module Pricing/Admin/Customers.
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();

        return services;
    }
}
