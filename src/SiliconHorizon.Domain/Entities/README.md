# Domain entities — trạng thái ánh xạ 64 bảng

Nguồn sự thật: `Database/table_and_data.sql` (64 bảng) và `Database/README.md` (API ↔ stored procedure).

## Quyết định thiết kế

- **EF Core** quản lý các bảng *cấu hình/danh mục* (mục 10 của `Database/README.md`): không có nghiệp vụ phức tạp, CRUD trực tiếp.
- **Dapper + stored procedure** quản lý các bảng *giao dịch* (đơn hàng, giỏ, kho, serial, bảo hành, đổi trả...): proc đã tự quản lý giao dịch, khóa và chống bán vượt tồn (NFR-01).
- Vì vậy **không phải bảng nào cũng cần entity EF**. Bảng chỉ dùng qua proc sẽ được truy vấn bằng Dapper vào DTO ở tầng Application.

## Đã có entity EF (28 bảng)

| Module | Bảng | File |
|---|---|---|
| Chung / Auth | `Users`, `Roles`, `Permissions`, `UserRoles`, `RolePermissions`, `Branches`, `SystemSettings` | `Entities/Common/AuthEntities.cs` |
| Danh mục | `Categories`, `Brands`, `Products`, `ProductImages`, `CompatibilityRules` | `Entities/Catalog/CatalogEntities.cs` |
| Giá & KM | `CustomerGroups`, `ProductPrices`, `PriceTiers`, `Promotions`, `Vouchers` | `Entities/Pricing/PricingEntities.cs` |
| Khách hàng | `Customers`, `CustomerAddresses` | `Entities/Customers/CustomerEntities.cs` |
| Nhân sự | `Employees`, `Shifts`, `EmployeeSchedules`, `Attendances` | `Entities/Hr/HrEntities.cs` |
| Nhà cung cấp | `Suppliers`, `SupplierProducts` | `Entities/Inventory/SupplierEntities.cs` |
| Hoa hồng | `CommissionRules`, `CommissionPeriods`, `CommissionEntries` | `Entities/Commission/CommissionEntities.cs` |

## Chưa có entity — sẽ dùng Dapper + proc (36 bảng)

| Module | Bảng | Truy cập qua |
|---|---|---|
| Thông số kỹ thuật | `Specs_CPU`, `Specs_RAM`, `Specs_Storage`, `Specs_Monitor`, `Specs_Monitor_Ports`, `Specs_Mainboard`, `Specs_PSU` | `usp_Internal_SaveSpecs`, `usp_Product_GetById` |
| Giỏ hàng | `Carts`, `CartItems` | `usp_Cart_*` |
| Đơn hàng & thanh toán | `Orders`, `OrderItems`, `OrderStatusHistory`, `OrderSalesStaff`, `VoucherUsages`, `Payments`, `CustomerLedger` | `usp_Order_*`, `usp_Payment_*`, `usp_Internal_SetOrderStatus` |
| Đổi trả | `ReturnRequests`, `ReturnItems` | `usp_Return_*` |
| Kho & serial | `StockLevels`, `StockMovements`, `PurchaseReceipts`, `PurchaseReceiptItems`, `SerialItems`, `SerialStatusHistory`, `StockTransfers`, `StockTransferItems`, `StockTransferSerials`, `StockTakes`, `StockTakeItems`, `SupplierReturns`, `SupplierReturnItems`, `SupplierLedger` | `usp_Inventory_*`, `usp_StockTake_*`, `usp_Transfer_*`, `usp_Serial_*`, `usp_Internal_StockMove` |
| Bảo hành | `WarrantyTickets`, `WarrantyTicketHistory`, `WarrantyTicketAttachments` | `usp_Warranty_*` |
| Nhật ký | `AuditLogs` | `usp_Audit_Write`, `usp_Audit_Search` (append-only) |

## Việc còn lại

1. Bổ sung enum còn thiếu số hoá theo script: `SerialItems.Status`, `WarrantyTickets.Status`, `Users.UserType` (đã ghi dạng `byte`).
2. Nếu muốn EF cho cả bảng giao dịch (đọc/báo cáo), chạy scaffold đúng schema thay vì viết tay:
   `dotnet ef dbcontext scaffold "<connection-string>" Microsoft.EntityFrameworkCore.SqlServer -o Entities/Scaffolded`
