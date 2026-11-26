# Features — cấu trúc module theo API

Mỗi nhóm API trong `Database/README.md` tương ứng một thư mục ở đây. Trong mỗi thư mục sẽ có:
`<Module>Service.cs` (use case), `Dto/` (request/response), `Validators/` (kiểm tra đầu vào),
và `<Module>Sql.cs` (hằng số tên stored procedure + hàm gọi qua Dapper) nếu module dùng proc.

| Thư mục | Nhóm API (tiền tố) | Nguồn dữ liệu |
|---|---|---|
| `Auth` | `/api/auth/login`, `/register`, `/refresh`, `/api/me` | `usp_Auth_*` + EF `Users/Roles/Permissions` |
| `Catalog` | `/api/products`, `/api/categories`, `/api/brands`, `/api/compatibility/check`, `/api/admin/products` | `usp_Product_*`, `usp_Compat_Check` + EF danh mục |
| `Pricing` | `/api/vouchers/validate`, `/api/admin/price-tiers`, `promotions`, `vouchers`, `customer-groups` | EF + `usp_Voucher_Validate`, `usp_ProductPrice_Upsert` |
| `Cart` | `/api/cart`, `/api/cart/items`, `/select-all`, `/merge` | `usp_Cart_*` |
| `PcBuilds` | `/api/pc-builds`, `/api/pc-builds/{id}`, `/pc-builds/{id}/add-to-cart` (PC build sẵn — combo linh kiện) | `usp_PcBuild_*` (Database/10_pc_builds.sql) |
| `Orders` | `/api/orders`, `/orders/{id}/confirm`, `/pack`, `/ship`, `/cancel`, `/complete`, `admin/orders` | `usp_Order_*`, `usp_Internal_SetOrderStatus` |
| `Payments` | `/api/orders/{id}/payments`, callback cổng thanh toán | `usp_Payment_*` |
| `Inventory` | `/api/inventory/*`, nhập kho, chuyển kho, kiểm kê, serial, nhà cung cấp | `usp_Inventory_*`, `usp_Transfer_*`, `usp_Serial_*` + EF `Suppliers` |
| `Warranty` | `/api/warranty*`, tra cứu theo serial | `usp_Warranty_*` |
| `Returns` | `/api/returns`, `/review`, `/inspect`, `/refund` | `usp_Return_*` |
| `Customers` | `/api/customers`, `/api/me/addresses`, `/api/customers/{id}/ledger` | EF `Customers/CustomerAddresses` + `usp_Customer_GetLedger` |
| `Hr` | `/api/schedules`, `/api/attendance/*`, `/api/commissions` | `usp_Schedule_*`, `usp_Attendance_*`, EF `Employees/Shifts` |
| `Commission` | `/api/admin/commissions/adjustments`, `/commission-periods/{id}/close` | `usp_Commission_*` + EF `CommissionRules` |
| `Audit` | `/api/audit-logs` | `usp_Audit_Write`, `usp_Audit_Search` |
| `Reports` | `/api/reports/revenue`, `/inventory`, `/serial-status`, `/debt`, `/warranty` | `usp_Report_*` |
| `Admin` | `/api/admin/brands`, `/compatibility-rules`, `/branches`, `/shifts`, `/settings`, `/commission-rules`, `/employees`, `/roles`, `/permissions`, `/users/{id}/roles`, `/suppliers/{id}/products` | EF `Brands/CompatibilityRules/Branches/Shifts/SystemSettings/CommissionRules/Employees/Users/Roles/Permissions/UserRoles/RolePermissions/SupplierProducts` + `usp_Audit_Write` |

## Quy ước chung khi viết use case

- Không tin dữ liệu client: `customerId`, `employeeId`, `branchId` luôn lấy từ `ICurrentUser` (token).
- Tiền là `decimal(18,0)` VND; thời gian là UTC (`IDateTimeProvider.UtcNow`).
- Mọi thay đổi dữ liệu nhạy cảm phải ghi nhật ký qua `usp_Audit_Write` (AUD-01..04).
- Lỗi SQL 5000x ném ra từ proc được ánh xạ sang HTTP bằng `SqlErrorCode.ToHttpStatusCode` + middleware ở tầng API.
