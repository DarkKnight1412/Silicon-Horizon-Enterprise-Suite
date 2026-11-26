# CẤU TRÚC VÀ QUAN HỆ DỮ LIỆU — Apex Circuit Electronics Enterprise Suite

> **Tài liệu sống** — cập nhật mỗi khi dự án nâng cấp.
> Nguồn sự thật: `Database/table_and_data.sql` (bảng + dữ liệu khởi tạo), các file `02..06_sp_*.sql` (stored procedure),
> `Database/10_pc_builds.sql` (bảng PC build), `Database/README.md` (API ↔ proc).
> Tài liệu liên quan: `DATABASE_ERD.md` (DDL cột đầy đủ + use case) · `ARCHITECTURE.md` · `BUSINESS_RULES.md`.

| Thông tin | Giá trị |
|---|---|
| Hệ QTCSDL | Microsoft SQL Server |
| Số bảng | **66** (64 trong `table_and_data.sql` + `PcBuilds`/`PcBuildItems`) |
| Stored procedure | **~90 proc** (`usp_*`) + 4 hàm dùng chung (`fn_*`) |
| Naming | `PascalCase` cho bảng/cột; `dbo` schema |
| Migrations | Không dùng EF migration — schema do file SQL quản lý |

---

## 1. TỔNG QUAN

Schema được thiết kế cho mô hình **bán lẻ + bán sỉ linh kiện máy tính** với các đặc trưng:

- **Đa chi nhánh**: hầu hết thực thể nghiệp vụ gắn `BranchId`; tồn kho theo `(BranchId, ProductId)`.
- **Cha – Con (Table-Per-Type)** cho sản phẩm: bảng cha `Products` + các bảng `Specs_*` (1-1).
- **Tồn kho bất biến**: số tồn là kết quả cộng dồn của `StockMovements` (chỉ INSERT).
- **Serial theo từng đơn vị**: `SerialItems` + `SerialStatusHistory`.
- **Giao dịch do stored procedure quản lý**: transaction + khoá + chống bán vượt tồn.

---

## 2. QUY ƯỚC CHUNG

| Mục | Quy ước |
|---|---|
| Schema | `dbo` |
| Đặt tên | Bảng/cột `PascalCase`; proc `usp_<Module>_<Action>`; hàm `fn_<...>` |
| Khóa chính | `Id` (`bigint IDENTITY` cho bảng giao dịch, `int` cho danh mục nhỏ) |
| Khóa ngoại | `<ThựcThể>Id`, ví dụ `ProductId`, `BranchId`, `OrderId` |
| Tiền | `decimal(18,0)` — **VND số nguyên**, không phần thập phân |
| Tỷ lệ % | `decimal(5,2)` (0–100) |
| Thời gian | `datetime2` lưu **UTC** (hiển thị theo múi giờ VN); ngày: `date`, giờ: `time` |
| Mã nghiệp vụ | `<Entity>Code` `varchar(20..30)`, cột `UQ` (VD `OrderCode`, `ReceiptCode`) |
| Trạng thái số hoá | `tinyint` (xem mục 6) |
| Đồng thời | Cột `RowVersion` (`rowversion`) trên `Orders`, `StockLevels`, `SerialItems` |
| Xóa mềm | Dùng `IsActive` / `Status` thay vì xóa cứng bản ghi có phát sinh |
| Chỉ mục lọc | Nhiều `UNIQUE` dạng `WHERE <cột> IS NOT NULL` (unique filtered index) |
| Audit | Cột `CreatedAt/CreatedBy/UpdatedAt/UpdatedBy` ở bảng nghiệp vụ chính |

> ⚠️ Khi chạy script tạo bảng cần cờ `-I` (QUOTED_IDENTIFIER ON) do có filtered index trên `Users`.

---

## 3. PHÂN LOẠI BẢNG THEO CÔNG NGHỆ TRUY CẬP

| Nhóm | Công nghệ | Số bảng | Ví dụ |
|---|---|---|---|
| Danh mục / cấu hình | **EF Core** | 28 | `Users`, `Roles`, `Products`, `Categories`, `Customers`, `Employees`, `Promotions`, `Vouchers` |
| Giao dịch / nghiệp vụ | **Dapper + stored procedure** | 38 | `Orders`, `CartItems`, `StockLevels`, `SerialItems`, `WarrantyTickets`, `ReturnRequests`, `AuditLogs`, `PcBuilds` |

Chi tiết bảng nào EF / bảng nào Dapper: `src/SiliconHorizon.Domain/Entities/README.md`.

---

## 4. DANH MỤC BẢNG THEO MODULE

### 4.1. Xác thực, phân quyền & hệ thống (7 bảng)

| Bảng | Vai trò | Cột khoá / đáng chú ý |
|---|---|---|
| `Users` | Tài khoản đăng nhập | `Username` UQ, `Email` (UQ lọc), `PasswordHash varchar(255)`, `UserType tinyint`, `IsActive` |
| `Roles` | Vai trò (Admin, Manager, Cashier, TechAdvisor, Storekeeper, Customer) | `Code` UQ |
| `Permissions` | Mã quyền chi tiết | `Code` UQ |
| `UserRoles` | Gán vai trò cho user | PK `(UserId, RoleId)` |
| `RolePermissions` | Gán quyền cho vai trò | PK `(RoleId, PermissionId)` |
| `Branches` | Chi nhánh | `Code` UQ, `IsActive` |
| `SystemSettings` | Cấu hình hệ thống (thời hạn giữ hàng, hạn đổi trả...) | `Key` UQ, `Value` |

### 4.2. Danh mục hàng hoá & thông số kỹ thuật (12 bảng)

| Bảng | Vai trò | Cột khoá / đáng chú ý |
|---|---|---|
| `Categories` | Danh mục (cây cha-con) | `ParentId` FK→`Categories`, `SpecType tinyint` |
| `Brands` | Thương hiệu | `Code`/`Slug` UQ |
| `Products` | Bảng cha sản phẩm | `Sku` UQ, `CategoryId`, `BrandId`, `RequiresSerial`, `WarrantyMonths`, `MinStock`, `Status tinyint`, `CostPrice`, `ListPrice` |
| `ProductImages` | Ảnh sản phẩm (1-N) | `ProductId` FK (ON DELETE CASCADE), `Url`, `SortOrder`, `IsPrimary` |
| `Specs_CPU` | Thông số CPU | PK=`ProductId` FK→`Products`; `Socket`, `Cores`, `Threads`, `TdpW`, `HasIntegratedGpu` |
| `Specs_RAM` | Thông số RAM | `MemoryType`, `CapacityGbPerKit`, `ModulesPerKit (CK IN 1,2,4)`, `BusMhz`, `CasLatency` |
| `Specs_Storage` | Thông số ổ cứng | `StorageType`, `Interface`, `FormFactor`, `CapacityGb` |
| `Specs_Monitor` | Thông số màn hình | `SizeInch`, `Resolution`, `PanelType`, `RefreshRateHz` |
| `Specs_Monitor_Ports` | Cổng kết nối màn hình | PK `(ProductId, PortType)` |
| `Specs_Mainboard` | Thông số mainboard | `Socket`, `Chipset`, `MemoryType`, `RamSlots`, `FormFactor` |
| `Specs_PSU` | Thông số nguồn | `PowerW`, `EfficiencyRating`, `ModularType` |
| `CompatibilityRules` | Quy tắc tương thích (cấu hình) | `LeftCategoryId`, `RightCategoryId`, `LeftField`, `RightField`, `Operator`, `Severity` |

> Quan hệ 1-1 cha-con: cột `ProductId` của mỗi `Specs_*` **vừa là PK vừa là FK** trỏ `Products.Id` (PRD-01).
> Lưu ý: chưa có `Specs_GPU` và `Specs_Case` (sẽ bổ sung khi mở rộng danh mục GPU/vỏ máy).

### 4.3. Giá và khuyến mãi (6 bảng)

| Bảng | Vai trò | Cột khoá / đáng chú ý |
|---|---|---|
| `CustomerGroups` | Nhóm khách (RETAIL, WHOLESALE) | `Code` UQ, `IsDefault` (UQ lọc) |
| `ProductPrices` | Giá theo nhóm khách | UQ `(ProductId, CustomerGroupId)`, `Price >= 0` |
| `PriceTiers` | Giá bậc thang theo số lượng | UQ `(ProductId, CustomerGroupId, MinQuantity)`, `MinQuantity > 1` |
| `Promotions` | Khuyến mãi có thời hạn | `ScopeType`, `ScopeId`, `DiscountType`, `DiscountValue`, `StartAt/EndAt (EndAt > StartAt)`, `MaxQuantity`, `MaxPerCustomer`, `IsActive` |
| `Vouchers` | Mã giảm giá | `Code` UQ, `MinOrderAmount`, `MaxDiscountAmount`, `MaxUses`, `UsedCount`, `CustomerGroupId`, `IsStackable`, `IsActive` |
| `VoucherUsages` | Lịch sử dùng voucher | UQ `(VoucherId, OrderId)` |

### 4.4. Khách hàng (2 bảng)

| Bảng | Vai trò | Cột khoá / đáng chú ý |
|---|---|---|
| `Customers` | Khách hàng (lẻ/sỉ) | `UserId` FK (UQ lọc), `CustomerGroupId`, `CreditLimit`, `PaymentTermDays`, `IsActive` |
| `CustomerAddresses` | Sổ địa chỉ giao hàng | `CustomerId` FK, `IsDefault` (UQ lọc theo `CustomerId`), `Province/District/Ward/Street` |

### 4.5. Giỏ hàng, đơn hàng, thanh toán (8 bảng)

| Bảng | Vai trò | Cột khoá / đáng chú ý |
|---|---|---|
| `Carts` | Giỏ hàng (theo khách hoặc phiên) | `CustomerId` (UQ lọc) **hoặc** `SessionToken` (UQ lọc); `CK (CustomerId IS NOT NULL OR SessionToken IS NOT NULL)` |
| `CartItems` | Dòng giỏ | `CartId` FK (CASCADE), `ProductId`, UQ `(CartId, ProductId)`, `Quantity > 0`, `IsSelected` |
| `Orders` | Đơn hàng | `OrderCode` UQ, `CustomerId`, `BranchId`, `Channel`, `FulfillmentType`, `Status`, `SubTotal/DiscountTotal/ShippingFee/GrandTotal (CK)`, `PaidAmount`, `PaymentMethod`, `PaymentStatus`, `VoucherId`, `ExpiresAt`, `CompletedAt`, `RowVersion` |
| `OrderItems` | Dòng đơn (snapshot) | `OrderId` FK, `ProductId`, UQ `(OrderId, ProductId)`, `Sku/ProductName/UnitPrice/WarrantyMonths/RequiresSerial` (snapshot), `LineTotal (CK)` |
| `OrderStatusHistory` | Lịch sử trạng thái đơn | `OrderId`, `FromStatus`, `ToStatus`, `Reason`, `ChangedBy`, `ChangedAt` |
| `OrderSalesStaff` | Nhân viên tư vấn theo đơn | PK `(OrderId, EmployeeId)`, `ShareRate (0-100)`, `IsPrimary` |
| `Payments` | Giao dịch thu/hoàn | `OrderId`, `PaymentType`, `Method`, `Amount > 0`, `Status`, `Provider`, `ProviderTransactionId`, `ParentPaymentId` (hoàn trỏ giao dịch gốc) |
| `CustomerLedger` | Công nợ khách | `CustomerId`, `EntryType (Charge/Payment/Adjustment)`, `Amount` (dương: tăng nợ) |

### 4.6. Kho, serial, nhà cung cấp (16 bảng)

| Bảng | Vai trò | Cột khoá / đáng chú ý |
|---|---|---|
| `Suppliers` | Nhà cung cấp | `Code` UQ, điều khoản thanh toán |
| `SupplierProducts` | SP mà NCC cung cấp | quan hệ nhiều-nhiều `Suppliers ↔ Products` |
| `StockLevels` | Tồn kho theo chi nhánh/SKU | PK/UQ `(BranchId, ProductId)`, `PhysicalQty`, `ReservedQty`, `AvailableQty`, `MinStock`, `CK (ReservedQty <= PhysicalQty)`, `RowVersion` |
| `StockMovements` | Bút toán kho (**bất biến, chỉ INSERT**) | `BranchId`, `ProductId`, `MovementType`, `PhysicalDelta`, `ReservedDelta`, `RefType/RefId`, `CreatedBy` |
| `PurchaseReceipts` | Phiếu nhập kho | `ReceiptCode` UQ, `SupplierId`, `BranchId`, `Status`, `TotalAmount`, `ReceivedAt` |
| `PurchaseReceiptItems` | Dòng phiếu nhập | `ReceiptId` FK, UQ `(ReceiptId, ProductId)`, `Quantity > 0`, `UnitCost >= 0` |
| `SerialItems` | Đơn vị hàng vật lý | UQ `(ProductId, SerialNumber)`, `Status`, `BranchId`, `PurchaseReceiptItemId`, `UnitCost`, `OrderItemId`, `WarrantyStartDate/EndDate`, `ReplacedBySerialId`, `RowVersion`, `CK (Status <> 3 OR OrderItemId IS NOT NULL)` |
| `SerialStatusHistory` | Lịch sử trạng thái serial | `SerialItemId`, `FromStatus/ToStatus`, `RefType/RefId`, `ChangedBy`, `ChangedAt` |
| `StockTransfers` | Phiếu chuyển kho | `TransferCode` UQ, `FromBranchId`, `ToBranchId`, `Status`, `CK (FromBranchId <> ToBranchId)` |
| `StockTransferItems` | Dòng phiếu chuyển | `TransferId` FK, UQ `(TransferId, ProductId)`, `Quantity > 0` |
| `StockTransferSerials` | Serial theo phiếu chuyển | PK `(TransferItemId, SerialItemId)` |
| `StockTakes` | Đợt kiểm kê | `StockTakeCode` UQ, `BranchId`, `Status`, `ApprovedBy`, `ApprovedAt` |
| `StockTakeItems` | Dòng kiểm kê | `StockTakeId` FK, UQ `(StockTakeId, ProductId)`, `SystemQty`, `CountedQty`, `DiffQty` (computed PERSISTED) |
| `SupplierReturns` | Phiếu trả hàng NCC | `ReturnCode` UQ, `SupplierId`, `BranchId`, `Status` |
| `SupplierReturnItems` | Dòng trả NCC | `SupplierReturnId` FK, `ProductId`, `SerialItemId` (NULL nếu không serial), `Quantity > 0` |
| `SupplierLedger` | Công nợ NCC | `SupplierId`, `PurchaseReceiptId`, `SupplierReturnId`, `EntryType (Payable/Payment/ReturnCredit)`, `Amount`, `DueDate` |

### 4.7. Bảo hành và đổi trả (5 bảng)

| Bảng | Vai trò | Cột khoá / đáng chú ý |
|---|---|---|
| `WarrantyTickets` | Phiếu bảo hành | `TicketCode` UQ, `SerialItemId` **hoặc** `OrderItemId`, `CustomerId`, `BranchId`, `Status`, `IssueDescription`, `RejectionReason`, `ReplacementSerialId`, `VendorSentAt/VendorReturnedAt`, `Fee`, `CK (SerialItemId IS NOT NULL OR OrderItemId IS NOT NULL)` |
| `WarrantyTicketHistory` | Lịch sử trạng thái phiếu BH | `TicketId`, `FromStatus/ToStatus`, `Note`, `ChangedBy`, `ChangedAt` |
| `WarrantyTicketAttachments` | Tài liệu đính kèm | `TicketId` FK, `Url` |
| `ReturnRequests` | Yêu cầu đổi trả | `ReturnCode` UQ, `OrderId`, `CustomerId`, `BranchId`, `Status`, `Reason`, `RefundAmount`, `ApprovedBy/ApprovedAt` |
| `ReturnItems` | Dòng hàng trả | `ReturnRequestId` FK, `OrderItemId`, `SerialItemId` (NULL nếu không serial), `Quantity > 0`, `InspectionResult (0 Pending / 1 Pass / 2 Fail)` |

### 4.8. Nhân sự, chấm công, hoa hồng, nhật ký (8 bảng)

| Bảng | Vai trò | Cột khoá / đáng chú ý |
|---|---|---|
| `Employees` | Nhân viên | `EmployeeCode` UQ, `UserId` FK (UQ), `FullName`, `Position`, `BranchId`, `Status`, `HireDate`, `TerminatedDate` |
| `Shifts` | Ca làm việc (MORNING/AFTERNOON/FULL) | `Code` UQ, `StartTime`, `EndTime` |
| `EmployeeSchedules` | Xếp ca | UQ `(EmployeeId, WorkDate, ShiftId)`, `BranchId` |
| `Attendances` | Chấm công | UQ lọc `(EmployeeId, ScheduleId)`, `CheckInAt/CheckOutAt`, `Source`, `AdjustedBy`, `AdjustReason`, `CK (CheckOutAt >= CheckInAt)` |
| `CommissionRules` | Quy tắc hoa hồng | `ScopeType`, `ScopeId`, `BasisType`, `RatePercent (0-100)`, `TargetAmount`, `BonusRatePercent`, `EffectiveFrom/To` |
| `CommissionPeriods` | Kỳ hoa hồng (YYYY-MM) | `YearMonth` UQ, `Status`, `ClosedBy`, `ClosedAt` |
| `CommissionEntries` | Bút toán hoa hồng | `PeriodId`, `EmployeeId`, `OrderId`, `ReturnRequestId`, `RuleId`, `EntryType`, `BaseAmount`, `RatePercent`, `Amount` (âm khi Clawback); UQ lọc `(EmployeeId, OrderId) WHERE EntryType = 0` |
| `AuditLogs` | Nhật ký (**append-only**) | `UserId`, hành động, đối tượng, `OldValues/NewValues`, thời điểm, IP/thiết bị, `BranchId`, lý do; thu hồi quyền UPDATE/DELETE |

### 4.9. PC build sẵn (2 bảng — `10_pc_builds.sql`)

| Bảng | Vai trò | Cột khoá / đáng chú ý |
|---|---|---|
| `PcBuilds` | Bộ PC dựng sẵn | `Code` UQ, `Slug` UQ, `Name`, `Tier (CK 0-3: Văn phòng/Gaming/Đồ hoạ/Cao cấp)`, `Summary`, `ImageUrl`, `IsFeatured`, `SortOrder`, `IsActive` |
| `PcBuildItems` | Linh kiện trong bộ | `PcBuildId` FK (CASCADE), `ProductId` FK, `Quantity > 0`, UQ `(PcBuildId, ProductId)`, `SortOrder` |

> 2 bảng này **tự tạo** trong script (`IF OBJECT_ID ... IS NULL`) nên chạy được trên DB đã có dữ liệu.
> 2 bảng này **tự tạo** trong script (`IF OBJECT_ID ... IS NULL`) nên chạy được trên DB đã có dữ liệu.
> Bộ không có giá riêng — **giá bộ = tổng giá linh kiện**.

---

## 5. QUAN HỆ DỮ LIỆU

### 5.1. Danh mục hàng hoá & giá

```
Categories (cha-con) ──< Products >── Brands
Products ──< ProductImages
Products ──1..1── Specs_CPU | Specs_RAM | Specs_Storage | Specs_Monitor | Specs_Mainboard | Specs_PSU
Specs_Monitor ──< Specs_Monitor_Ports
Categories ──< CompatibilityRules
CustomerGroups ──< ProductPrices >── Products
CustomerGroups ──< PriceTiers  >── Products
Vouchers ──< VoucherUsages
```

### 5.2. Khách hàng, giỏ, đơn, thanh toán

```
Users 1..1 Customers         Users 1..1 Employees
Users >──< Roles (UserRoles) Roles >──< Permissions (RolePermissions)
CustomerGroups ──< Customers ──< CustomerAddresses
Customers 1..1 Carts ──< CartItems >── Products
Customers ──< Orders >── Branches
Orders ──< OrderItems >── Products
Orders ──< OrderStatusHistory
Orders ──< OrderSalesStaff >── Employees
Orders ──< Payments
Orders ──< VoucherUsages
Customers ──< CustomerLedger
```

### 5.3. Kho, serial, nhà cung cấp

```
Branches ──< StockLevels >── Products          (tồn theo chi nhánh)
Branches ──< StockMovements >── Products        (bút toán bất biến)
Suppliers ──< PurchaseReceipts >── Branches
PurchaseReceipts ──< PurchaseReceiptItems >── Products
PurchaseReceiptItems ──< SerialItems >── Products
OrderItems ──< SerialItems                       (serial gán vào đơn)
SerialItems ──< SerialStatusHistory
SerialItems 1..1 SerialItems                     (serial thay thế — ReplacedBySerialId)
Suppliers >──< Products (SupplierProducts)
Suppliers ──< SupplierLedger
Branches ──< StockTransfers >── Branches (From/To)
StockTransfers ──< StockTransferItems ──< StockTransferSerials >── SerialItems
Branches ──< StockTakes ──< StockTakeItems >── Products
Suppliers ──< SupplierReturns ──< SupplierReturnItems >── SerialItems
```

### 5.4. Bảo hành, đổi trả, hoa hồng, HR

```
SerialItems ──< WarrantyTickets >── Customers
OrderItems  ──< WarrantyTickets                 (hàng không serial — WAR-03)
WarrantyTickets ──< WarrantyTicketHistory / WarrantyTicketAttachments
WarrantyTickets ──1 SerialItems (ReplacementSerialId)
Orders ──< ReturnRequests ──< ReturnItems >── OrderItems / SerialItems
Employees ──< EmployeeSchedules >── Shifts
Employees ──< Attendances
CommissionRules ──< CommissionEntries
CommissionPeriods ──< CommissionEntries >── Employees / Orders / ReturnRequests
Users ──< AuditLogs
PcBuilds ──< PcBuildItems >── Products
```

---

## 6. ENUM VÀ GIÁ TRỊ SỐ HOÁ (nguồn: `Domain/Enums` + comment trong SQL)

| Bảng.Cột | Enum (C#) | Giá trị |
|---|---|---|
| `Users.UserType` | (byte) | 0 Nhân viên, 1 Khách hàng |
| `Categories.SpecType` | `SpecType` | 0 Generic, 1 Cpu, 2 Ram, 3 Storage, 4 Monitor, 5 Mainboard, 6 Psu |
| `Products.Status` | `ProductStatus` | 0 Draft, 1 Active, 2 Discontinued |
| `Promotions.DiscountType`, `Vouchers.DiscountType` | `DiscountType` | 0 Percent, 1 Amount |
| `Promotions.ScopeType` | `PromotionScopeType` | 0 Product, 1 Brand, 2 Category |
| `CompatibilityRules.Severity` | `CompatibilitySeverity` | 0 Info, 1 Warning |
| `Orders.Channel` | `OrderChannel` | 0 Online, 1 POS |
| `Orders.FulfillmentType` | `FulfillmentType` | 0 Delivery, 1 Pickup |
| `Orders.Status` | `OrderStatus` | 0 PendingPayment, 1 Pending, 2 Confirmed, 3 Packing, 4 ReadyForPickup, 5 Shipping, 6 DeliveryFailed, 7 Completed, 8 Cancelled, 9 PartiallyReturned, 10 Returned |
| `Orders.PaymentMethod` | `PaymentMethod` | 0 COD, 1 BankTransfer, 2 Gateway, 3 Cash, 4 Credit |
| `Orders.PaymentStatus` | `PaymentStatus` | 0 Unpaid, 1 PartiallyPaid, 2 Paid, 3 Refunded |
| `Payments.PaymentType` | `PaymentType` | 0 Payment, 1 Refund |
| `CustomerLedger.EntryType` | `LedgerEntryType` | 0 Charge, 1 Payment, 2 Adjustment |
| `StockMovements.MovementType` | `StockMovementType` | 0 Receipt, 1 Reserve, 2 Release, 3 Issue, 4 TransferOut, 5 TransferIn, 6 StockTakeAdjust, 7 ManualAdjust, 8 CustomerReturn, 9 SupplierReturn |
| `PurchaseReceipts.Status` | `PurchaseReceiptStatus` | 0 Draft, 1 Completed, 2 Cancelled |
| `SerialItems.Status` | (byte theo comment) | 0 InStock, 1 Allocated, 2 InTransfer, 3 Sold, 4 RMA, 5 Returned, 6 Replaced, 7 Defective, 8 ReturnedToSupplier, 9 Scrapped |
| `StockTransfers.Status` | (byte) | 0 Draft, 1 InTransit, 2 Received, 3 Cancelled |
| `StockTakes.Status` | (byte) | 0 Draft, 1 Counting, 2 PendingApproval, 3 Approved, 4 Rejected |
| `SupplierReturns.Status` | (byte) | 0 Draft, 1 Completed, 2 Cancelled |
| `SupplierLedger.EntryType` | (byte) | 0 Payable, 1 Payment, 2 ReturnCredit |
| `WarrantyTickets.Status` | (byte theo comment) | 0 Received, 1 Diagnosing, 2 SentToVendor, 3 Repaired, 4 Replaced, 5 Rejected, 6 ReturnedToCustomer |
| `ReturnRequests.Status` | `ReturnRequestStatus` | 0 Requested, 1 Approved, 2 Rejected, 3 GoodsReceived, 4 Refunded, 5 Closed |
| `ReturnItems.InspectionResult` | (byte) | 0 Pending, 1 Pass, 2 Fail |
| `Employees.Position` | `EmployeePosition` | 0 Cashier, 1 TechAdvisor, 2 Storekeeper, 3 Manager |
| `Employees.Status` | `EmployeeStatus` | 0 Active, 1 Terminated |
| `Attendances.Source` | `AttendanceSource` | 0 Device, 1 ManualAdjust |
| `CommissionRules.ScopeType` | `CommissionScopeType` | 0 All, 1 Category, 2 Product |
| `CommissionRules.BasisType` | `CommissionBasisType` | 0 Revenue, 1 GrossProfit |
| `CommissionPeriods.Status` | `CommissionPeriodStatus` | 0 Open, 1 Closed |
| `CommissionEntries.EntryType` | `CommissionEntryType` | 0 Earn, 1 Clawback, 2 Adjustment |
| `PcBuilds.Tier` | (byte) | 0 Văn phòng, 1 Gaming, 2 Đồ hoạ/Render, 3 Cao cấp |

---

## 7. RÀNG BUỘC VÀ CHỈ MỤC ĐÁNG CHÚ Ý

### 7.1. Ràng buộc CHECK nghiệp vụ

| Bảng | Ràng buộc |
|---|---|
| `Orders` | `GrandTotal = SubTotal - DiscountTotal + ShippingFee` |
| `OrderItems` | `LineTotal = UnitPrice * Quantity - DiscountAmount`; `Quantity > 0` |
| `StockLevels` | `ReservedQty <= PhysicalQty` |
| `SerialItems` | `Status <> 3 (Sold) OR OrderItemId IS NOT NULL` |
| `StockTransfers` | `FromBranchId <> ToBranchId` |
| `Attendances` | `CheckOutAt >= CheckInAt` |
| `Promotions` / `Vouchers` | `EndAt > StartAt`; `DiscountValue > 0` |
| `PriceTiers` | `MinQuantity > 1` |
| `Specs_RAM` | `ModulesPerKit IN (1,2,4)` |
| `PcBuilds` | `Tier BETWEEN 0 AND 3` |

### 7.2. UNIQUE INDEX (lọc — chỉ áp dụng khi cột không NULL)

| Bảng | Cột | Ghi chú |
|---|---|---|
| `Users` | `Email` | unique filtered |
| `Customers` | `UserId` | unique filtered |
| `Carts` | `CustomerId`, `SessionToken` | unique filtered từng cột |
| `CustomerAddresses` | `CustomerId` | unique filtered WHERE `IsDefault = 1` |
| `Attendances` | `(EmployeeId, ScheduleId)` | unique filtered |
| `Categories` / `CustomerGroups` | `IsDefault = 1` | unique filtered — chỉ một mặc định |
| `CommissionEntries` | `(EmployeeId, OrderId)` WHERE `EntryType = 0` | mỗi NV chỉ hưởng 1 lần/đơn |

### 7.3. Bảng bất biến (immutable)

- `StockMovements` — **chỉ INSERT**, số tồn cộng dồn từ bút toán (INV-03).
- `AuditLogs` — **chỉ INSERT**, thu hồi quyền UPDATE/DELETE với mọi tài khoản ứng dụng (AUD-03).
- `SerialStatusHistory`, `OrderStatusHistory`, `WarrantyTicketHistory` — lịch sử chỉ ghi thêm.

### 7.4. Cột computed / đồng thời

- `StockTakeItems.DiffQty` = `CountedQty - SystemQty` (computed, PERSISTED).
- `RowVersion` trên `Orders`, `StockLevels`, `SerialItems` (optimistic concurrency).

---

## 8. STORED PROCEDURE VÀ HÀM DÙNG CHUNG

### 8.1. Hàm dùng chung (`fn_*`)

| Hàm | Vai trò |
|---|---|
| `fn_ProductPrice` | Tính giá bán theo nhóm khách + bậc thang + khuyến mãi |
| `fn_VoucherCheck` | Kiểm tra hợp lệ voucher |
| `fn_FormatCode` | Sinh mã chứng từ (đơn, phiếu...) |
| `fn_VnDate` | Ngày giờ theo múi giờ Việt Nam |

### 8.2. Nhóm proc theo domain

| Tiền tố | Domain | Ví dụ |
|---|---|---|
| `usp_Cart_*` | Giỏ hàng | Resolve, Add, UpdateQty, Select, Merge |
| `usp_Order_*` | Đơn hàng | Create, Get, List, Confirm, StartPacking, Complete, Cancel |
| `usp_Payment_*` | Thanh toán | RecordPayment, Refund, Ledger |
| `usp_Inventory_*`, `usp_StockTake_*`, `usp_Transfer_*`, `usp_Serial_*` | Kho & serial | Receipt, Move, Transfer, StockTake, ChangeSerialStatus |
| `usp_Supplier*_*` | Nhà cung cấp | Payable, Return, Ledger |
| `usp_Warranty_*` | Bảo hành | Receive, ChangeStatus, Attach |
| `usp_Return_*` | Đổi trả | Create, Review, Inspect, Refund |
| `usp_Commission_*` | Hoa hồng | Accrue, Clawback, Period close |
| `usp_Hr_*`, `usp_Attendance_*` | Nhân sự | Schedule, CheckIn/Out, Adjust |
| `usp_Audit_*` | Nhật ký | Write, Search |
| `usp_Report_*` | Báo cáo | Revenue, Inventory, SerialStatus, Debt, Warranty |
| `usp_PcBuild_*` | PC build | Search, GetById, AddToCart, BuilderCatalog |
| `usp_Compat_Check` | Tương thích | Kiểm tra tương thích linh kiện |
| `usp_Internal_*` | Nội bộ (không gọi trực tiếp từ API) | `StockMove`, `SetOrderStatus`, `CancelOrder`, `LogSerial`, `SaveSpecs`, `ResolveCart`, `AccrueCommission`, `ClawbackCommission`, `GetOpenPeriod` |

> API có thể gọi trực tiếp `usp_Audit_Write` cho thao tác CRUD nhạy cảm.

---

## 9. FILE SQL VÀ THỨ TỰ CHẠY

| Thứ tự | File | Nội dung |
|---|---|---|
| 1 | `Database/table_and_data.sql` | Tạo bảng, khóa, chỉ mục, dữ liệu khởi tạo (64 bảng) |
| 2 | `Database/02_sp_foundation_catalog_cart.sql` | Sequence, hàm giá/voucher, tài khoản, sản phẩm, giỏ hàng |
| 3 | `Database/03_sp_orders_payments.sql` | Đơn hàng, đóng gói, hủy, thanh toán |
| 4 | `Database/04_sp_inventory_suppliers.sql` | Kho, nhập hàng, chuyển kho, kiểm kê, serial, NCC |
| 5 | `Database/05_sp_warranty_returns_commission.sql` | Bảo hành, đổi trả, hoa hồng |
| 6 | `Database/06_sp_hr_audit_reports.sql` | Chấm công, nhật ký, báo cáo |
| 7 | `Database/07_seed_admin_user_and_permissions.sql` | Quyền + vai trò + chi nhánh + tài khoản admin |
| 8 | `Database/08_seed_test_accounts.sql` | Bộ 6 tài khoản kiểm thử |
| 9 | `Database/09_seed_sample_products.sql` | Dữ liệu sản phẩm mẫu |
| 10 | `Database/10_pc_builds.sql` | Bảng `PcBuilds`/`PcBuildItems` + proc + 5 bộ mẫu (**tự tạo bảng**, chạy sau 09) |

> Các file proc dùng `CREATE OR ALTER` — chạy lại an toàn. **Phải chạy đủ `02`→`06`** trước khi dùng
> vì proc file này gọi proc file khác. Khi chạy script có filtered index cần cờ `-I` (QUOTED_IDENTIFIER ON).

---

## 10. DỮ LIỆU MẪU & TÀI KHOẢN

- **Tài khoản kiểm thử** (dev): xem `TAI_KHOAN_KIEM_THU.md`
  — `admin`, `manager`, `cashier`, `tech`, `store` (chi nhánh CN01), `customer`.
- **Sản phẩm mẫu**: `Database/09_seed_sample_products.sql` (kèm ảnh mẫu trong `frontend/public/images/products/`).
- **PC build mẫu**: 5 bộ trong `Database/10_pc_builds.sql`.

---

## 11. ĐIỂM CẦN LƯU Ý / KHOẢNG TRỐNG DỮ LIỆU

| # | Điểm | Ghi chú |
|---|---|---|
| 1 | Chưa có bảng `Specs_GPU`, `Specs_Case` | Kiểm tra tương thích chưa tính TDP GPU |
| 2 | Serial chưa có UQ toàn cục | Hiện `UQ (ProductId, SerialNumber)` — đổi thành `UQ (SerialNumber)` nếu chốt duy nhất toàn hệ thống |
| 3 | `SerialItems.Status`, `WarrantyTickets.Status` chưa có enum C# | Đang dùng `byte` theo comment SQL |
| 4 | Chưa ràng buộc cha-con `Products` ↔ `Specs_*` ở tầng CSDL | Kiểm tra ở tầng ứng dụng (PA: thêm `SpecType` để cưỡng chế bằng FK) |
| 5 | `usp_Compat_Check` hard-code 4 quy tắc | Chưa đọc `CompatibilityRules` |
| 6 | Tìm kiếm dùng `LIKE` | Cân nhắc Full-Text Search khi dữ liệu lớn |

---

## 12. LỊCH SỬ CẬP NHẬT

| Ngày | Thay đổi |
|---|---|
| 26/11/2026 | Tạo bản tổng hợp cấu trúc & quan hệ dữ liệu: 66 bảng theo 9 module, quan hệ, enum số hoá, ràng buộc/chỉ mục, proc/hàm, thứ tự file SQL |






