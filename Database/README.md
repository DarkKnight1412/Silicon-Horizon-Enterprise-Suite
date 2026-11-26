# DANH SÁCH API VÀ STORED PROCEDURE

**Hệ thống Bán hàng & Quản lý Doanh nghiệp Linh kiện Điện tử**

Tài liệu này liệt kê các API cần có cho dữ liệu hiện tại (64 bảng) và ánh xạ từng API sang stored procedure đã viết. Các file SQL đi kèm:

| Thứ tự chạy | File | Nội dung |
|---|---|---|
| 1 | `01_create_tables.sql` | Tạo bảng, khóa, chỉ mục, dữ liệu khởi tạo |
| 2 | `02_sp_foundation_catalog_cart.sql` | Sequence, hàm giá/voucher, tài khoản, sản phẩm, giỏ hàng |
| 3 | `03_sp_orders_payments.sql` | Đơn hàng, đóng gói, hủy, thanh toán |
| 4 | `04_sp_inventory_suppliers.sql` | Kho, nhập hàng, chuyển kho, kiểm kê, serial, nhà cung cấp |
| 5 | `05_sp_warranty_returns_commission.sql` | Bảo hành, đổi trả, hoa hồng |
| 6 | `06_sp_hr_audit_reports.sql` | Chấm công, nhật ký, báo cáo |
| 7 | `10_pc_builds.sql` | PC build sẵn: bảng `PcBuilds`/`PcBuildItems` + `usp_PcBuild_Search`/`GetById`/`AddToCart`/`BuilderCatalog` + 5 bộ mẫu |

Cả 5 file proc có thể chạy lại nhiều lần (`CREATE OR ALTER`). Phải chạy đủ 02 đến 06 trước khi dùng, vì proc ở file này gọi proc ở file khác (ví dụ hoàn tất đơn gọi tính hoa hồng).

Riêng `10_pc_builds.sql` **tự tạo bảng** (`IF OBJECT_ID ... IS NULL`) nên chạy được cả trên database **đã có dữ liệu** — nên chạy SAU `09_seed_sample_products.sql` để các bộ mẫu gắn được vào linh kiện mẫu.

---

## 1. QUY ƯỚC CHUNG

### 1.1. Chuẩn API

| Mục | Quy ước |
|---|---|
| Tiền tố | `/api/v1` (bên dưới viết gọn là `/api`) |
| Xác thực | JWT. Token chứa `userId`, `roles`, `customerId` (khách) hoặc `employeeId` + `branchId` (nhân viên) |
| Phân quyền | Do tầng API kiểm tra theo ma trận BRD mục 2.2. Proc chỉ nhận `@UserId` để ghi lịch sử |
| Phạm vi chi nhánh | Nhân viên chỉ thao tác trong chi nhánh của mình (trừ Admin). API ép `@BranchId` từ token, không tin giá trị client gửi |
| Dữ liệu của khách | Khách chỉ xem dữ liệu của mình: API ép `@CustomerId` từ token |
| Phân trang | `pageNumber`, `pageSize`. Các proc danh sách trả cột `TotalCount` |
| JSON trong proc | Các tham số `@ItemsJson`, `@SpecsJson`... dùng camelCase. API serialize từ DTO |
| Tiền, thời gian | VND số nguyên; thời gian UTC (ISO 8601) |

Ký hiệu quyền: **Public** không cần đăng nhập · **KH** khách hàng · **TN** thu ngân · **TV** tư vấn kỹ thuật · **TK** thủ kho · **QL** quản lý chi nhánh · **AD** admin · **NV** mọi nhân viên · **Job** tác vụ nền.

### 1.2. Mã lỗi từ stored procedure

| Số lỗi SQL | Ý nghĩa | HTTP |
|---|---|---|
| 50001 | Không tìm thấy | 404 |
| 50002 | Sai trạng thái / chuyển trạng thái không hợp lệ | 409 |
| 50003 | Không đủ tồn kho | 409 |
| 50004 | Dữ liệu không hợp lệ | 400 |
| 50005 | Trùng dữ liệu | 409 |
| 50006 | Vượt hạn mức, quá hạn đổi trả, hết lượt voucher | 422 |
| 1205 | Deadlock | Retry 2 đến 3 lần, rồi 503 |
| Khác | Lỗi hệ thống | 500 (ghi log, không lộ chi tiết) |

Thông điệp tiếng Việt trong `SqlException.Message` của các lỗi 5000x có thể trả thẳng cho người dùng.

### 1.3. Gợi ý gọi từ .NET (Dapper)

```csharp
// Gọi proc, đọc nhiều result set
using var multi = await conn.QueryMultipleAsync("dbo.usp_Order_Get",
    new { OrderId = id }, commandType: CommandType.StoredProcedure);
var header = await multi.ReadSingleAsync<OrderHeaderDto>();
var items  = (await multi.ReadAsync<OrderItemDto>()).ToList();
// ... theo thứ tự các result set nêu ở bảng bên dưới

// Truyền IP và User-Agent cho nhật ký (trước mỗi request dùng kết nối)
await conn.ExecuteAsync("EXEC sp_set_session_context @k, @v",
    new { k = "ip", v = httpContext.Connection.RemoteIpAddress?.ToString() });

// Ánh xạ lỗi
catch (SqlException ex) when (ex.Number is >= 50001 and <= 50006) {
    var status = ex.Number switch {
        50001 => 404, 50002 or 50003 or 50005 => 409, 50004 => 400, _ => 422 };
    return Problem(ex.Message, statusCode: status);
}
```

Với MediatR/CQRS: mỗi command/query gọi một proc tương ứng. Query dùng kết nối đọc, command dùng kết nối ghi (proc tự quản lý giao dịch, không bọc `TransactionScope` bên ngoài).

---

## 2. TÀI KHOẢN

| Method | Đường dẫn | Mô tả | Quyền | Stored procedure |
|---|---|---|---|---|
| POST | `/api/auth/register` | Khách đăng ký | Public | `usp_Auth_RegisterCustomer` |
| POST | `/api/auth/login` | Đăng nhập (API so khớp hash, phát JWT) | Public | `usp_Auth_GetUserByUsername`, `usp_Auth_UpdateLastLogin` |
| POST | `/api/auth/refresh` | Làm mới token | Đã đăng nhập | (không dùng proc) |
| GET | `/api/me` | Thông tin tài khoản hiện tại | Đã đăng nhập | `usp_Auth_GetUserByUsername` |

`usp_Auth_GetUserByUsername` trả 5 result set theo thứ tự: user, vai trò, quyền, nhân viên, khách hàng.

---

## 3. DANH MỤC SẢN PHẨM VÀ GIÁ

### 3.1. Công khai

| Method | Đường dẫn | Mô tả | Quyền | Stored procedure |
|---|---|---|---|---|
| GET | `/api/products` | Tìm kiếm, lọc chung và theo thông số, sắp xếp, phân trang | Public | `usp_Product_Search` |
| GET | `/api/products/{id}` | Chi tiết: header, ảnh, thông số, tồn từng chi nhánh | Public | `usp_Product_GetById` |
| POST | `/api/compatibility/check` | Cảnh báo tương thích linh kiện | Public | `usp_Compat_Check` |
| POST | `/api/vouchers/validate` | Kiểm tra mã giảm giá trước khi đặt | KH | `usp_Voucher_Validate` |
| GET | `/api/categories`, `/api/brands` | Danh sách danh mục, thương hiệu | Public | EF (đọc bảng) |

Ví dụ `GET /api/products?categoryCode=RAM&specFilters=...`: API chuyển `specFilters` thành `@SpecFiltersJson`, ví dụ `[{"field":"memoryType","value":"DDR5"},{"field":"capacityGbPerKit","value":"32"}]`. Sắp xếp: `newest`, `price_asc`, `price_desc`, `name`, `discount`. Sắp xếp "bán chạy" chưa có, cần bảng thống kê riêng.

### 3.2. Quản trị

| Method | Đường dẫn | Mô tả | Quyền | Stored procedure |
|---|---|---|---|---|
| POST | `/api/admin/products` | Tạo sản phẩm kèm thông số theo loại | AD | `usp_Product_Create` |
| PUT | `/api/admin/products/{id}` | Sửa sản phẩm (không đổi danh mục), ghi Audit nếu đổi giá | AD | `usp_Product_Update` |
| PATCH | `/api/admin/products/{id}/status` | Đổi trạng thái: nháp, đang bán, ngừng kinh doanh | AD | `usp_Product_SetStatus` |
| PUT | `/api/admin/products/{id}/prices/{groupId}` | Đặt giá theo nhóm khách (giá dưới vốn cần người duyệt) | AD, QL đề xuất | `usp_ProductPrice_Upsert` |
| POST | `/api/admin/products/images` | **Upload 1 ảnh sản phẩm** (multipart: `file` ≤ 5 MB jpg/jpeg/png/webp + `sku`) → lưu vào `wwwroot/images/products/`, trả `{ url }` | AD | — (ghi file; không qua proc) |

### 3.3. Quy ước đường dẫn ẢNH sản phẩm

**Ảnh do NHÂN VIÊN upload** (trang `/admin/products`): gọi `POST /api/admin/products/images` (multipart) →
API lưu file vào `src/SiliconHorizon.Api/wwwroot/images/products/` và phục vụ qua `UseStaticFiles()`
⇒ **thêm/sửa ảnh KHÔNG cần build lại web khách hàng**. Tên file do API sinh tự động, an toàn:
`<sku viết thường>-<8 ký tự ngẫu nhiên>.<ext>`.

Bảng `ProductImages.Url` (varchar(500)) chỉ lưu **chuỗi đường dẫn**:

| Loại ảnh | Giá trị lưu ở `ProductImages.Url` | File nằm ở |
|---|---|---|
| **Upload qua quản trị** (khuyến nghị) | **URL đầy đủ** trỏ về API — VD `http://localhost:5270/images/products/cpu-intel-i9-14900k-1a2b3c4d.jpg` (nếu deploy sau proxy: cấu hình `Storage:PublicBaseUrl`) | `src/SiliconHorizon.Api/wwwroot/images/products/` (KHÔNG commit — đã có trong `.gitignore`) |
| Ảnh đặt sẵn trong mã nguồn web | Đường dẫn **tương đối** theo origin web — VD `/images/products/cpu-intel-i9-14900k.jpg` | `frontend/public/images/products/` (tên file = **mã SKU viết thường**) |

| Quy ước chung | Giá trị |
|---|---|
| `SortOrder` | 0, 1, 2… theo thứ tự hiển thị |
| `IsPrimary` | đúng **một** dòng `= 1` cho mỗi sản phẩm (proc lấy ảnh theo `ORDER BY IsPrimary DESC, SortOrder`) |
| Định dạng cho phép | `jpg`, `jpeg`, `png`, `webp` — tối đa **5 MB**/ảnh |

Quy tắc kèm theo:

1. Form Admin (`/admin/products`) có **input chọn file** cho từng dòng ảnh: chọn ảnh ⇒ gọi API upload ⇒ tự điền URL vào dòng đó (**nhớ bấm Lưu** để ghi vào sản phẩm). Nút **“Theo SKU”** dùng cho trường hợp đặt ảnh sẵn trong mã nguồn web (điền `/images/products/<sku>.<ext>`).
2. `GET /api/products` chỉ trả **một** `imageUrl` (ảnh chính); `GET /api/products/{id}` trả đủ mảng `images`.
3. Ô nhập URL ảnh thủ công là `type="text"` (KHÔNG phải `type="url"`) vì đường dẫn tương đối không hợp lệ với kiểm tra URL của trình duyệt. Muốn dùng host ngoài/CDN thì nhập **URL tuyệt đối** (`https://...`).
4. Ảnh upload là **dữ liệu runtime**: đặt trong `wwwroot` của API và **không commit** vào git (mục tương ứng đã có trong `.gitignore`). Khi triển khai thật cần thư mục lưu bền (hoặc object storage) + sao lưu cùng dữ liệu.
5. Xoá ảnh trong form chỉ xoá **dòng dữ liệu** (`ProductImages`); **file vật lý trên đĩa chưa được dọn tự động** — việc dọn định kỳ nằm trong danh sách làm tiếp.

> **Việc nên làm tiếp:** dọn file mồ côi (ảnh đã bị xoá khỏi sản phẩm nhưng còn trên đĩa), giới hạn theo vai trò chi tiết hơn, và (khi lên production) chuyển lưu file sang object storage với `Storage:PublicBaseUrl` cấu hình theo domain công khai.

### 3.4. PC build sẵn (combo linh kiện)

Khách KHÔNG có chuyên môn không phải tự chọn từng linh kiện: cửa hàng bán sẵn các **bộ PC đã dựng**
(bảng `dbo.PcBuilds` + `dbo.PcBuildItems`, script `10_pc_builds.sql`). Khách xem bộ rồi bấm một nút là
**tất cả linh kiện của bộ vào giỏ** — backend thêm trong MỘT giao dịch và kiểm tra tồn của từng linh kiện.

| Method | Đường dẫn | Mô tả | Quyền | Stored procedure |
|---|---|---|---|---|
| GET | `/api/pc-builds` | Danh sách bộ (lọc `tier`, khoảng giá, `inStockOnly`, `featuredOnly`; sắp xếp; phân trang) | Public | `usp_PcBuild_Search` |
| GET | `/api/pc-builds/{id}` | Chi tiết bộ + danh sách linh kiện kèm giá và tồn hiện tại | Public | `usp_PcBuild_GetById` |
| POST | `/api/pc-builds/{id}/add-to-cart` | Thêm CẢ BỘ vào giỏ (body `{ "quantity": 1 }`); thiếu tồn ở 1 linh kiện ⇒ 409 và KHÔNG thêm gì | Public | `usp_PcBuild_AddToCart` |
| GET | `/api/pc-builds/builder/catalog` | **Danh mục TỰ BUILD PC**: 6 nhóm theo thứ tự chọn (bo mạch chủ → CPU → RAM → ổ cứng → nguồn → màn hình) + giá/tồn/ảnh + `specs` để giao diện tự lọc linh kiện phù hợp | Public | `usp_PcBuild_BuilderCatalog` |

Quy tắc quan trọng:

1. **Bộ KHÔNG có giá riêng.** Giá bộ (`partsPrice`) = tổng giá linh kiện theo `fn_ProductPrice`
   (bảng giá theo nhóm khách + khuyến mãi đang chạy) ⇒ số tiền hiển thị bằng đúng số tiền vào giỏ/đơn hàng.
   Muốn bán combo RẺ HƠN mua lẻ thì phải bổ sung cơ chế giảm giá ở tầng giỏ/đơn (`CartItems`/`OrderItems`
   hiện chưa có cột giảm giá).
2. `availableBuilds` = số bộ còn lắp được = `MIN(tồn khả dụng của linh kiện / số lượng cần)`.
3. `Tier`: 0 Văn phòng · 1 Gaming · 2 Đồ hoạ/Render · 3 Cao cấp (nhãn tiếng Việt nằm ở FE,
   `frontend/src/app/core/models/pc-build.model.ts`).
4. Thêm bộ ⇒ giỏ hàng nhận **mỗi linh kiện một dòng** (vẫn sửa số lượng / bỏ tick / xoá như bình thường);
   đơn hàng vì thế cũng gồm các dòng linh kiện — đúng với thực tế xuất kho & bảo hành từng món.
5. Hiện quản lý bộ bằng script SQL; **API quản trị CRUD cho bộ nằm trong danh sách làm tiếp**.
6. **TỰ BUILD PC** (tab thứ 3 ở `/shop`): khách chọn linh kiện theo thứ tự và giao diện **chỉ gợi ý linh kiện
   phù hợp** với lựa chọn trước đó, theo đúng 4 quy tắc mà `usp_Compat_Check` cũng kiểm tra:
   `socket` CPU = socket bo mạch · `memoryType` RAM = chuẩn RAM bo mạch · `ModulesPerKit × số kit ≤ RamSlots` ·
   `PowerW ≥ TDP CPU × 2 + 150 W` (công thức khuyến nghị của giao diện, chặt hơn quy tắc cảnh báo của proc).
   Chốt cấu hình ⇒ thêm MỌI linh kiện đã chọn vào giỏ bằng **một giao dịch** — xem `POST /api/cart/items/bulk` (mục 4).

---

## 4. GIỎ HÀNG

Chủ giỏ: khách đã đăng nhập (`customerId` từ token) hoặc khách vãng lai (`sessionToken` trong cookie/header).

| Method | Đường dẫn | Mô tả | Quyền | Stored procedure |
|---|---|---|---|---|
| GET | `/api/cart` | Xem giỏ, tự bỏ tick món hết hàng, giảm số lượng nếu tồn ít hơn | Public | `usp_Cart_Get` |
| POST | `/api/cart/items` | Thêm sản phẩm (cộng dồn hoặc đặt đúng số lượng) | Public | `usp_Cart_AddItem` |
| PATCH | `/api/cart/items/{productId}` | Đổi số lượng và/hoặc tick chọn | Public | `usp_Cart_UpdateItem` |
| DELETE | `/api/cart/items/{productId}` | Xóa dòng | Public | `usp_Cart_RemoveItem` |
| POST | `/api/cart/select-all` | Chọn / bỏ chọn tất cả | Public | `usp_Cart_SelectAll` |
| POST | `/api/cart/merge` | Gộp giỏ tạm vào giỏ tài khoản sau đăng nhập | KH | `usp_Cart_Merge` |
| POST | `/api/cart/items/bulk` | **Thêm NHIỀU sản phẩm trong MỘT giao dịch** — dùng cho tab "Tự build PC" (`items: [{ productId, quantity }]`); chỉ cần 1 dòng ngừng bán/thiếu tồn ⇒ 404/409 và KHÔNG thêm gì | Public | `usp_Cart_AddItems` |

`usp_Cart_Get` trả 3 result set: dòng giỏ hàng, tổng tiền các dòng được tick, cảnh báo (`OUT_OF_STOCK`, `QUANTITY_REDUCED`).

---

## 5. ĐƠN HÀNG VÀ THANH TOÁN

### 5.1. Khách hàng

| Method | Đường dẫn | Mô tả | Quyền | Stored procedure |
|---|---|---|---|---|
| POST | `/api/orders` | Đặt hàng từ các dòng đã tick trong giỏ | KH | `usp_Order_Create` (`@ItemsJson = NULL`, `Channel = 0`) |
| GET | `/api/orders` | Đơn của tôi | KH | `usp_Order_List` (ép `CustomerId`) |
| GET | `/api/orders/{id}` | Chi tiết đơn | KH (đơn của mình), NV | `usp_Order_Get` |
| POST | `/api/orders/{id}/cancel` | Khách hủy, chỉ khi `Pending` hoặc `PendingPayment` | KH | `usp_Order_Cancel` |

### 5.2. Nhân viên xử lý đơn

| Method | Đường dẫn | Mô tả | Quyền | Stored procedure |
|---|---|---|---|---|
| POST | `/api/pos/orders` | Tạo đơn tại quầy (`@ItemsJson`, `Channel = 1`) | TN, TV, QL | `usp_Order_Create` |
| GET | `/api/staff/orders` | Danh sách đơn của chi nhánh, lọc trạng thái, từ khóa | NV | `usp_Order_List` (ép `BranchId`) |
| POST | `/api/orders/{id}/confirm` | Xác nhận đơn (`Pending` sang `Confirmed`) | TN, QL | `usp_Order_Confirm` |
| POST | `/api/orders/{id}/start-packing` | Bắt đầu đóng gói | TK | `usp_Order_StartPacking` |
| POST | `/api/orders/{id}/serials` | Quét serial gán vào đơn | TK | `usp_Order_AssignSerial` |
| DELETE | `/api/orders/{id}/serials/{serialNumber}` | Gỡ serial gán nhầm (có lý do, Audit) | TK | `usp_Order_UnassignSerial` |
| POST | `/api/orders/{id}/ready` | Đóng gói xong: giao hàng hoặc chờ khách nhận | TK | `usp_Order_MarkReady` |
| POST | `/api/orders/{id}/delivery-failed` | Ghi nhận giao thất bại (bắt buộc lý do) | TN, TK | `usp_Order_MarkDeliveryFailed` |
| POST | `/api/orders/{id}/retry-delivery` | Giao lại | TN, TK | `usp_Order_RetryDelivery` |
| POST | `/api/orders/{id}/complete` | Giao thành công: trừ kho, bán serial, bảo hành, hoa hồng | TN, TK | `usp_Order_Complete` |
| POST | `/api/orders/{id}/cancel-approved` | Hủy đơn đã xác nhận trở đi (bắt buộc lý do) | QL, AD | `usp_Order_Cancel` |

Cùng proc `usp_Order_Cancel` phục vụ hai endpoint; khác nhau ở quyền (API kiểm tra: khách chỉ khi trạng thái 0 hoặc 1). Kết quả có `RefundRequired = 1` khi đơn đã thu tiền.

### 5.3. Thanh toán

| Method | Đường dẫn | Mô tả | Quyền | Stored procedure |
|---|---|---|---|---|
| POST | `/api/payments/callback/{provider}` | Callback cổng thanh toán (xác minh chữ ký ở API, idempotent) | Cổng TT | `usp_Payment_Record` |
| POST | `/api/orders/{id}/payments` | Thu ngân xác nhận chuyển khoản / thu tiền | TN, QL | `usp_Payment_Record` |
| POST | `/api/orders/{id}/refunds` | Hoàn tiền thủ công cho đơn đã hủy hoặc giao thất bại | QL, AD | `usp_Payment_Refund` |
| GET | `/api/customers/{id}/ledger` | Sao kê công nợ khách | KH (của mình), QL, AD | `usp_Customer_GetLedger` |

`usp_Payment_Record` trả `IsDuplicate = 1` khi cùng `(Provider, ProviderTransactionId)` được gửi lại; API vẫn trả 200 cho cổng. `NeedsReview = 1` khi tiền về cho đơn đã hủy hoặc thanh toán thừa, cần xử lý thủ công.

### 5.4. Tác vụ nền (không có endpoint)

| Tác vụ | Chu kỳ | Stored procedure |
|---|---|---|
| Hủy đơn quá hạn giữ hàng / hạn nhận hàng | Mỗi phút | `usp_Order_ExpireOverdue` |
| Cảnh báo tồn dưới mức tối thiểu | Mỗi giờ hoặc khi mở màn hình kho | `usp_Stock_GetLevels` với `@BelowMinOnly = 1` |

Dùng `BackgroundService` hoặc Hangfire. Proc idempotent nên chạy chồng hoặc chạy lại không gây hại.

---

## 6. KHO, SERIAL, NHÀ CUNG CẤP

| Method | Đường dẫn | Mô tả | Quyền | Stored procedure |
|---|---|---|---|---|
| GET | `/api/stock` | Tồn theo chi nhánh/sản phẩm, lọc dưới mức tối thiểu | TK, QL, AD | `usp_Stock_GetLevels` |
| PUT | `/api/stock/min` | Đặt mức tồn tối thiểu | QL, AD | `usp_Stock_SetMin` |
| GET | `/api/stock/movements` | Thẻ kho (lịch sử biến động) | TK, QL, AD | `usp_Stock_GetMovements` |
| POST | `/api/purchase-receipts` | Nhập kho kèm danh sách serial | TK | `usp_PurchaseReceipt_Create` |
| GET | `/api/purchase-receipts` | Danh sách phiếu nhập | TK, QL, AD | `usp_PurchaseReceipt_List` |
| GET | `/api/purchase-receipts/{id}` | Chi tiết phiếu nhập | TK, QL, AD | `usp_PurchaseReceipt_Get` |
| POST | `/api/stock-transfers` | Tạo phiếu chuyển kho (nháp) | TK | `usp_StockTransfer_Create` |
| GET | `/api/stock-transfers/{id}` | Chi tiết phiếu chuyển | TK, QL, AD | `usp_StockTransfer_Get` |
| POST | `/api/stock-transfers/{id}/ship` | Xuất đi (trừ tồn chi nhánh đi) | TK (chi nhánh đi) | `usp_StockTransfer_Ship` |
| POST | `/api/stock-transfers/{id}/receive` | Nhận hàng (cộng tồn chi nhánh đến) | TK (chi nhánh đến) | `usp_StockTransfer_Receive` |
| POST | `/api/stock-transfers/{id}/cancel` | Hủy phiếu nháp | TK, QL | `usp_StockTransfer_Cancel` |
| POST | `/api/stock-takes` | Tạo đợt kiểm kê | TK | `usp_StockTake_Create` |
| POST | `/api/stock-takes/{id}/counts` | Nhập số đếm, gửi duyệt | TK | `usp_StockTake_SubmitCounts` |
| POST | `/api/stock-takes/{id}/approve` | Duyệt hoặc từ chối, áp chênh lệch | QL | `usp_StockTake_Approve` |
| GET | `/api/stock-takes/{id}` | Chi tiết kiểm kê | TK, QL | `usp_StockTake_Get` |
| GET | `/api/serials/{serialNumber}` | Tra cứu serial: bảo hành, đơn mua, lịch sử | NV, KH (hàng của mình) | `usp_Serial_Lookup` |
| POST | `/api/serials/{id}/status` | Đổi trạng thái thủ công (lỗi, hủy, phục hồi) | QL, AD | `usp_Serial_ChangeStatus` |
| POST | `/api/supplier-returns` | Trả hàng nhà cung cấp | TK, QL | `usp_SupplierReturn_Create` |
| POST | `/api/suppliers/{id}/payments` | Ghi nhận thanh toán cho nhà cung cấp | QL, AD | `usp_SupplierPayment_Record` |
| GET | `/api/suppliers/{id}/ledger` | Công nợ phải trả | QL, AD | `usp_Supplier_GetLedger` |

---

## 7. BẢO HÀNH VÀ ĐỔI TRẢ

### 7.1. Bảo hành

| Method | Đường dẫn | Mô tả | Quyền | Stored procedure |
|---|---|---|---|---|
| GET | `/api/warranty/lookup?serial=` | Tra cứu bảo hành theo serial | NV, KH | `usp_Serial_Lookup` |
| POST | `/api/warranty-tickets` | Tiếp nhận bảo hành (tự từ chối nếu hết hạn) | TN, TV | `usp_Warranty_Receive` |
| POST | `/api/warranty-tickets/{id}/status` | Chuyển trạng thái: chẩn đoán, gửi hãng, sửa xong, đổi mới, từ chối, trả khách | TK, TV | `usp_Warranty_ChangeStatus` |
| POST | `/api/warranty-tickets/{id}/attachments` | Đính kèm ảnh/tài liệu | NV | `usp_Warranty_AddAttachment` |
| GET | `/api/warranty-tickets` | Danh sách phiếu (nhân viên theo chi nhánh, khách của mình) | NV, KH | `usp_Warranty_List` |
| GET | `/api/warranty-tickets/{id}` | Chi tiết phiếu | NV, KH (của mình) | `usp_Warranty_Get` |

### 7.2. Đổi trả

| Method | Đường dẫn | Mô tả | Quyền | Stored procedure |
|---|---|---|---|---|
| POST | `/api/orders/{id}/returns` | Khách tạo yêu cầu đổi trả | KH | `usp_Return_Create` |
| GET | `/api/returns` | Danh sách yêu cầu | NV, KH (của mình) | `usp_Return_List` |
| GET | `/api/returns/{id}` | Chi tiết yêu cầu | NV, KH (của mình) | `usp_Return_Get` |
| POST | `/api/returns/{id}/review` | Duyệt hoặc từ chối | QL, AD | `usp_Return_Review` |
| POST | `/api/returns/{id}/inspect` | Nhận hàng và kiểm tra từng dòng/serial | TK | `usp_Return_Inspect` |
| POST | `/api/returns/{id}/refund` | Hoàn tiền, cập nhật đơn, thu hồi hoa hồng | QL, AD | `usp_Return_Refund` |

---

## 8. NHÂN SỰ, HOA HỒNG, NHẬT KÝ

| Method | Đường dẫn | Mô tả | Quyền | Stored procedure |
|---|---|---|---|---|
| POST | `/api/schedules` | Xếp ca | QL, AD | `usp_Schedule_Assign` |
| DELETE | `/api/schedules/{id}` | Xóa ca chưa chấm công | QL, AD | `usp_Schedule_Remove` |
| GET | `/api/schedules` | Bảng xếp ca | NV (của mình), QL | `usp_Schedule_List` |
| POST | `/api/attendance/check-in` | Chấm công vào | NV | `usp_Attendance_CheckIn` |
| POST | `/api/attendance/check-out` | Chấm công ra | NV | `usp_Attendance_CheckOut` |
| POST | `/api/attendance/adjust` | Điều chỉnh công (bắt buộc lý do, Audit) | QL, AD | `usp_Attendance_Adjust` |
| GET | `/api/attendance?yearMonth=` | Bảng công tháng | NV (của mình), QL | `usp_Attendance_GetMonthly` |
| GET | `/api/commissions?yearMonth=` | Tổng hợp hoa hồng theo nhân viên | NV (của mình), QL, AD | `usp_Commission_Summary` |
| POST | `/api/admin/commissions/adjustments` | Điều chỉnh hoa hồng thủ công | AD | `usp_Commission_Adjust` |
| POST | `/api/admin/commission-periods/{id}/close` | Chốt kỳ hoa hồng | AD | `usp_Commission_ClosePeriod` |
| GET | `/api/audit-logs` | Tra cứu nhật ký (QL chỉ chi nhánh mình) | QL, AD | `usp_Audit_Search` |

---

## 9. BÁO CÁO

| Method | Đường dẫn | Mô tả | Quyền | Stored procedure |
|---|---|---|---|---|
| GET | `/api/reports/revenue` | Doanh thu, hoàn tiền, lợi nhuận gộp theo ngày/tháng/chi nhánh/danh mục | QL (chi nhánh), AD | `usp_Report_Revenue` |
| GET | `/api/reports/inventory` | Giá trị tồn, hàng tồn lâu không xuất | QL, AD, TK | `usp_Report_Inventory` |
| GET | `/api/reports/serial-status` | Số serial theo trạng thái | TK, QL, AD | `usp_Report_SerialStatus` |
| GET | `/api/reports/debt` | Công nợ phải thu và phải trả, phần quá hạn | AD | `usp_Report_Debt` |
| GET | `/api/reports/warranty` | Phiếu bảo hành theo trạng thái, sản phẩm lỗi nhiều, thời gian xử lý | QL, AD | `usp_Report_Warranty` |

---

## 10. API CRUD ĐƠN GIẢN (dùng EF Core, không cần stored procedure)

Các bảng này không có nghiệp vụ phức tạp, để EF Core xử lý cho nhanh. Vẫn ghi Audit khi sửa dữ liệu nhạy cảm (gọi `usp_Audit_Write`).

| Nhóm | Endpoint (đều dưới `/api/admin/...`) | Quyền |
|---|---|---|
| Danh mục, thương hiệu | `categories`, `brands` (GET, POST, PUT) | AD |
| Nhóm khách, bảng giá bậc thang | `customer-groups`, `products/{id}/price-tiers` | AD |
| Khuyến mãi, voucher | `promotions`, `vouchers` | AD |
| Quy tắc tương thích | `compatibility-rules` | AD |
| Chi nhánh, ca làm | `branches`, `shifts` | AD |
| Nhà cung cấp | `suppliers`, `suppliers/{id}/products` | AD, TK (xem) |
| Khách hàng, địa chỉ | `customers`, và `/api/me/addresses` cho khách tự quản lý | AD, KH |
| Nhân viên, tài khoản | `employees`, `users/{id}/roles`, `roles`, `permissions` | AD |
| Quy tắc hoa hồng | `commission-rules` | AD |
| Cấu hình hệ thống | `settings` (thời hạn giữ hàng, hạn đổi trả...) | AD |

Chú ý khi làm CRUD bằng EF: không xóa cứng bản ghi đã có phát sinh (dùng `IsActive` hoặc trạng thái); khi tạo/sửa nhân viên cần đồng bộ `Users`, `UserRoles`, `Employees`.

---

## 11. PROC KHÔNG GỌI TRỰC TIẾP TỪ API

| Proc | Vai trò |
|---|---|
| `fn_ProductPrice`, `fn_VoucherCheck`, `fn_FormatCode`, `fn_VnDate` | Hàm dùng chung (giá, voucher, sinh mã, ngày giờ Việt Nam) |
| `usp_Internal_StockMove` | Thay đổi tồn kho một sản phẩm và ghi bút toán |
| `usp_Internal_SetOrderStatus` | Đổi trạng thái đơn có kiểm tra chuyển hợp lệ |
| `usp_Internal_CancelOrder` | Hủy đơn: nhả tồn, gỡ serial, trả voucher, đảo công nợ |
| `usp_Internal_LogSerial` | Ghi lịch sử trạng thái serial |
| `usp_Internal_SaveSpecs` | Lưu thông số vào đúng bảng con |
| `usp_Internal_ResolveCart` | Tìm hoặc tạo giỏ hàng |
| `usp_Internal_AccrueCommission`, `usp_Internal_ClawbackCommission`, `usp_Internal_GetOpenPeriod` | Tính, thu hồi hoa hồng và tìm kỳ đang mở |
| `usp_Audit_Write` | Ghi nhật ký (API cũng có thể gọi trực tiếp cho thao tác CRUD nhạy cảm) |

---

## 12. GIỚI HẠN HIỆN TẠI VÀ VIỆC NÊN LÀM TIẾP

Các điểm sau là quyết định đơn giản hóa có chủ đích, cần bạn biết:

- **Chưa chạy thử trên SQL Server thật.** Script đã được kiểm tra thứ tự phụ thuộc và cú pháp bằng công cụ, nhưng cần chạy thử toàn bộ. Nên viết bộ test tích hợp cho các luồng: đặt đồng thời món cuối cùng, hủy đơn, hoàn tất đơn, đổi trả, bảo hành đổi mới.
- **Tương thích linh kiện** (`usp_Compat_Check`) viết cứng 4 quy tắc (socket, chuẩn RAM, số khe RAM, công suất nguồn theo TDP CPU), chưa đọc bảng `CompatibilityRules`. Chưa tính TDP card đồ họa vì chưa có bảng `Specs_GPU`.
- **Lọc theo thông số** hiện so sánh bằng, AND giữa các trường; chưa có khoảng (ví dụ dung lượng từ... đến...), chưa có nhiều giá trị cho cùng một trường.
- **Hoa hồng**: dùng một quy tắc phạm vi "toàn bộ" (`ScopeType = 0`) đang hiệu lực cho cả đơn. Quy tắc theo danh mục/sản phẩm và thưởng vượt chỉ tiêu (`TargetAmount`, `BonusRatePercent`) chưa được áp dụng trong proc.
- **Khuyến mãi**: đã áp giá tốt nhất theo phạm vi và thời hạn; chưa cưỡng chế `MaxQuantity` và `MaxPerCustomer`.
- **Công nợ**: kiểm tra hạn mức và nợ quá hạn khi đặt đơn mua chịu theo quy ước FIFO (thanh toán trừ khoản cũ nhất trước).
- **Kiểm kê** theo số lượng; sản phẩm quản lý serial bị lệch phải xử lý từng serial bằng `usp_Serial_ChangeStatus` rồi kiểm kê lại.
- **Hoàn tiền đổi trả**: hoàn theo mọi dòng đã được Quản lý duyệt, kể cả hàng kiểm tra không đạt (chuyển `Defective`). Nếu muốn từ chối hoàn tiền với hàng lỗi do người dùng, cần thêm bước chỉnh số tiền hoàn trước khi gọi `usp_Return_Refund` (proc đã cho phép truyền `@Amount` thấp hơn số dự kiến).
- **Trạng thái đơn `PendingPayment` của khách mua tại quầy** không áp dụng: đơn tại quầy luôn vào `Pending`.
- **Tìm kiếm văn bản** dùng `LIKE`. Khi dữ liệu lớn nên chuyển sang Full-Text Search hoặc dịch vụ tìm kiếm riêng.
- **PC build sẵn** (`10_pc_builds.sql`): bộ không có giá riêng (giá bộ = tổng giá linh kiện) và **chưa có API quản trị** để tạo/sửa bộ (hiện quản lý bằng script SQL); danh mục cũng chưa có card đồ hoạ rời nên các bộ mẫu dùng CPU có đồ hoạ tích hợp.

### Thứ tự đề xuất để làm tiếp

1. Chạy 6 file SQL trên một database thử, viết test tích hợp cho các luồng ở trên.
2. Dựng các API theo từng module, bắt đầu từ danh mục, giỏ hàng, đơn hàng (đường chính để bán được hàng).
3. Làm tác vụ nền hủy đơn quá hạn và cảnh báo tồn kho.
4. Bổ sung CRUD quản trị bằng EF Core.