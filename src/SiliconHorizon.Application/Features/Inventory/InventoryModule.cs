using Microsoft.Extensions.DependencyInjection;

namespace SiliconHorizon.Application.Features.Inventory;

/// <summary>
/// Đăng ký DI của module Kho + Serial + Nhà cung cấp (Database/04_sp_inventory_suppliers.sql),
/// được <c>AddApplication()</c> gọi lại (quy ước: <c>Features/&lt;Module&gt;/&lt;Module&gt;Module.cs</c>).
/// </summary>
public static class InventoryModule
{
    public static IServiceCollection AddInventoryModule(this IServiceCollection services)
    {
        // Tồn kho, nhập kho, chuyển kho nội bộ, kiểm kê, serial (usp_Stock_*, usp_PurchaseReceipt_*,
        // usp_StockTransfer_*, usp_StockTake_*, usp_Serial_*).
        services.AddScoped<IInventoryService, InventoryService>();

        // Nhà cung cấp: CRUD bằng EF Core (bảng cấu hình Suppliers) + trả hàng/thanh toán/công nợ
        // qua usp_SupplierReturn_Create, usp_SupplierPayment_Record, usp_Supplier_GetLedger.
        services.AddScoped<ISupplierService, SupplierService>();

        // Chốt chặn phạm vi chi nhánh dùng chung cho hai service trên (ép BranchId từ token — BRD 2.2).
        services.AddScoped<InventoryAccessGuard>();

        return services;
    }
}
