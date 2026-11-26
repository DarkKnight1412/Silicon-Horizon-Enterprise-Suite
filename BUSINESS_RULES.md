# QUY TẮC NGHIỆP VỤ ĐÃ XÁC NHẬN — Apex Circuit Electronics Enterprise Suite

> **Tài liệu sống** — cập nhật mỗi khi dự án nâng cấp.
> Nguồn: `README.md` (BRD, các mã yêu cầu `PRD/PRC/CART/ORD/PAY/INV/SUP/SER/WAR/RET/HR/KPI/AUD/NFR`)
> đối chiếu với mã nguồn backend (validators, guards, `*Roles.cs`) và stored procedure trong `Database/`.
> Tài liệu liên quan: `ARCHITECTURE.md` · `DATABASE.md` · `DATABASE_ERD.md`.

Quy ước: **X** toàn quyền · **R** chỉ xem · **C** tạo · **U** sửa · **A** duyệt · **—** không có quyền.
Quyền của nhân viên chỉ áp dụng **trong chi nhánh được gán**, trừ Admin.

---

## 1. VAI TRÒ VÀ MA TRẬN PHÂN QUYỀN (RBAC — BRD 2.2)

### 1.1. Nhóm đối tượng

| Nhóm | Mô tả | Vai trò kỹ thuật |
|---|---|---|
| **Khách hàng (Customer)** | Khách lẻ và khách sỉ (đại lý) | `Customer` |
| **Thu ngân** | Bán tại quầy, thu tiền | `Cashier` |
| **Tư vấn kỹ thuật** | Tư vấn, gắn vào đơn để tính hoa hồng, tiếp nhận bảo hành | `TechAdvisor` |
| **Thủ kho** | Nhập/xuất/chuyển/kiểm kê kho, quản lý serial | `Storekeeper` |
| **Quản lý chi nhánh** | Duyệt thao tác nhạy cảm, xếp ca | `Manager` |
| **Admin** | Cấu hình toàn hệ thống | `Admin` |

### 1.2. Ma trận chức năng

| Chức năng | KH | TN | TV | TK | QL | AD |
|---|---|---|---|---|---|---|
| Xem/lọc sản phẩm | R | R | R | R | R | X |
| Tạo/sửa sản phẩm, thuộc tính | — | — | — | — | — | X |
| Sửa giá bán, khuyến mãi | — | — | — | — | Đề xuất | X |
| Giỏ hàng, đặt hàng của mình | X | — | — | — | — | — |
| Tạo đơn tại quầy | — | C | C | — | C | X |
| Xác nhận / chuyển trạng thái đơn | Hủy khi Pending | U (đến giao xong) | R | U (đóng gói, xuất kho) | A | X |
| Hủy đơn đã xác nhận | — | — | — | — | A | X |
| Nhập kho, kiểm kê | — | — | — | C, U | A | X |
| Điều chỉnh tồn kho thủ công | — | — | — | Đề xuất | A | X |
| Chuyển kho nội bộ | — | — | — | C | A | X |
| Tiếp nhận / xử lý bảo hành | Tra cứu của mình | C | C, U | U | A | X |
| Duyệt đổi trả, hoàn tiền | — | Đề xuất | — | — | A | X |
| Quản lý nhân sự, phân quyền | — | — | — | — | R | X |
| Chấm công | Bản thân | Bản thân | Bản thân | Bản thân | Cả chi nhánh | X |
| Xem báo cáo doanh thu | — | — | Cá nhân | — | Chi nhánh | X |
| Xem báo cáo tồn kho | — | — | — | Chi nhánh | Chi nhánh | X |
| Xem Audit Log | — | — | — | — | Chi nhánh | X |

> Mọi thao tác **"A" (duyệt)** bắt buộc ghi Audit Log.

### 1.3. Nhóm quyền kỹ thuật (source)

- `Api/Constants/OrderRoleGroups.cs`: `SalesCounter`, `LedgerViewers`, ... và `*Roles.cs` trong từng module:
  `InventoryRoles` (StockDesk/StockOperations/Approvers/SerialLookup), `WarrantyRoles` (Viewers/Receive/Process/Attach),
  `ReturnsRoles` (Viewers/Review/Inspect/Refund), `ReportRoles`, `HrRoles` (ScheduleViewers/AttendanceViewers/Approvers),
  `CommissionRoles`, `AuditRoles`.
- `data.accessMode`: mặc định `roleOrPermission`; dùng `roles` (siết đúng vai trò) cho Audit/Debt/Ledger.

---

## 2. SẢN PHẨM VÀ THUỘC TÍNH

### 2.1. Mô hình dữ liệu Cha – Con (Table-Per-Type)

- Bảng cha `Products` chứa thông tin chung; các bảng con `Specs_*` liên kết **1-1** với `Products`.
- **PRD-01** Khóa chính của bảng con **đồng thời là khóa ngoại** trỏ về `Products.Id` (bảo đảm 1-1).
- **PRD-02** Mỗi sản phẩm thuộc đúng một `Category`; chỉ có bản ghi ở **đúng bảng con tương ứng**. Hệ thống chặn ở tầng nghiệp vụ.
- **PRD-03** Đổi Category của sản phẩm đã có phát sinh (đơn, nhập kho, serial) **không được phép** — chỉ tạo sản phẩm mới.
- **PRD-04** Khi tạo sản phẩm: chọn **Loại linh kiện** trước → hiện form thuộc tính tương ứng → lưu cha và con trong **cùng một giao dịch**.
- **PRD-05** Cho phép mở rộng loại linh kiện mới (nhóm ít dùng có thể lưu JSON).

### 2.2. Quy tắc sản phẩm

- **PRD-06** Trạng thái **Hết hàng** do hệ thống tự suy ra từ `Available = 0` toàn bộ chi nhánh; không nhập tay.
- **PRD-07** **Ngừng kinh doanh**: không cho đặt mới, vẫn tra cứu bảo hành và lịch sử.
- **PRD-08** Sản phẩm `requires_serial = true` bắt buộc quản lý serial; `false` chỉ quản lý theo số lượng.
- **PRD-09** Không xóa cứng sản phẩm đã có phát sinh; chỉ chuyển sang Ngừng kinh doanh.

### 2.3. Kiểm tra tương thích

- **PRD-10** Kiểm tra tương thích **ở mức cảnh báo** (không chặn đặt hàng): CPU↔Mainboard (socket), RAM↔Mainboard (chuẩn DDR, số thanh), tổng TDP ↔ công suất PSU (ngưỡng mặc định 80%).
- **PRD-11** Quy tắc tương thích là **dữ liệu cấu hình** (`CompatibilityRules`), không viết cứng rải rác.

### 2.4. Ghi chú thực tế từ mã nguồn

- Proc `usp_Compat_Check` hiện hard-code 4 quy tắc (socket, chuẩn RAM, số khe RAM, TDP/PSU),
  **chưa** đọc bảng `CompatibilityRules`; chưa tính TDP GPU (chưa có bảng `Specs_GPU`).

---

## 3. GIÁ VÀ KHUYẾN MÃI

- **PRC-01** Bảng giá theo nhóm khách: tối thiểu *Khách lẻ* và *Khách sỉ*. Khách chưa đăng nhập luôn dùng giá khách lẻ.
- **PRC-02** Giá bậc thang theo số lượng (bán sỉ), cấu hình theo sản phẩm/danh mục.
- **PRC-03** Khuyến mãi có thời hạn (ngày bắt đầu/kết thúc, phạm vi sản phẩm/thương hiệu/danh mục, mức giảm % hoặc tiền, giới hạn số lượng và số lần mỗi khách).
- **PRC-04** Voucher: giá trị đơn tối thiểu, thời hạn, số lần dùng, nhóm khách áp dụng, có/không cộng dồn.
- **PRC-05** Thứ tự ưu tiên: **Giá sỉ bậc thang → Khuyến mãi sản phẩm → Voucher**. Mặc định không cộng dồn hai khuyến mãi sản phẩm; chỉ voucher cộng dồn nếu cấu hình cho phép.
- **PRC-06** **Giá bán không được thấp hơn giá vốn** nếu không có phê duyệt của Quản lý/Admin.
- **PRC-07** Mọi thay đổi giá, khuyến mãi đều ghi Audit Log (giá cũ, giá mới, người sửa).
- **PRC-08** Bán sỉ – công nợ: khách sỉ có **hạn mức công nợ** và **kỳ hạn thanh toán**. Đơn vượt hạn mức hoặc khách có nợ quá hạn bị chặn ở bước xác nhận, cần Quản lý duyệt.

**Ghi chú thực tế:** proc đã áp giá tốt nhất theo phạm vi + thời hạn; **chưa** cưỡng chế `MaxQuantity`/`MaxPerCustomer`.

---

## 4. GIỎ HÀNG

- **CART-01** Giỏ chỉ làm việc với bảng cha `Products` (cho phép trộn RAM, CPU, màn hình, ổ cứng...).
- **CART-02** Khách chưa đăng nhập có giỏ tạm (theo mã phiên `X-Cart-Session`); khi đăng nhập, giỏ tạm **gộp** vào giỏ tài khoản (cùng sản phẩm thì cộng số lượng, giới hạn bởi tồn khả dụng) — `usp_Cart_Merge`.
- **CART-03** Mỗi dòng có cờ `is_selected` (mặc định bật khi mới thêm); có "Chọn tất cả" / bỏ chọn lẻ.
- **CART-04** Tổng tiền tạm tính **chỉ cộng dòng được tick**.
- **CART-05** Khi "Đặt hàng", **chỉ dòng được tick** chuyển thành đơn; dòng không tick giữ nguyên trong giỏ.
- **CART-06** Không cho tăng số lượng vượt `Available Stock`.
- **CART-07** Khi mở giỏ hoặc đặt hàng, kiểm tra lại tồn: hết hàng → **tự bỏ tick** + cảnh báo (không tự tick lại khi có hàng); tồn ít hơn số lượng → **tự giảm** về mức khả dụng và báo khách.
- **CART-08** Tồn kiểm tra theo **chi nhánh xuất hàng** phù hợp hình thức nhận hàng.
- **CART-09** Giá trong giỏ chỉ là **tạm tính**, luôn tính lại theo bảng giá hiện hành; nếu giá đổi, thông báo cho khách.
- **CART-10** Giá chốt là giá tại **thời điểm tạo đơn**, lưu snapshot.
- **CART-11** Giỏ hàng **không giữ tồn**; giữ hàng chỉ xảy ra khi tạo đơn.

---

## 5. ĐƠN HÀNG VÀ THANH TOÁN

### 5.1. Mô hình tồn kho

Mỗi SKU, tại mỗi chi nhánh, theo dõi ba số liệu:

```
Available = Physical − Reserved
```

- `Reserved` tăng khi đơn được tạo (giữ hàng), giảm khi đơn bị hủy hoặc giao xong.
- `Physical` chỉ giảm khi hàng **thực sự rời kho / giao thành công**.
- Ràng buộc CSDL: `CK (ReservedQty <= PhysicalQty)` trên `StockLevels`.

### 5.2. Hình thức nhận hàng

- **ORD-01** **Giao tận nơi**: chọn chi nhánh xuất theo quy tắc cấu hình (gần nhất có đủ hàng / kho mặc định); phí vận chuyển theo cấu hình.
- **ORD-02** **Nhận tại cửa hàng**: khách chọn chi nhánh nhận; tồn kiểm tra và giữ tại chi nhánh đó. Quá hạn nhận (mặc định 3 ngày) → đơn tự hủy, nhả tồn.
- **ORD-03** Đơn tại quầy dùng chi nhánh của nhân viên, có thể đi thẳng tới Hoàn tất khi thanh toán và giao hàng xảy ra cùng lúc.

### 5.3. Nguyên tắc dữ liệu đơn hàng

- **ORD-04** **Snapshot khi tạo đơn**: chi tiết đơn lưu cố định tên, SKU, đơn giá đã áp dụng, khuyến mãi/voucher, số lượng, thời hạn bảo hành. Thay đổi giá sau đó **không ảnh hưởng** đơn đã tạo.
- **ORD-05** Đơn lưu `Sales_Staff_ID` (nhân viên tư vấn) nếu có (bảng `OrderSalesStaff`).
- **ORD-06** Mã đơn hàng duy nhất, sinh tự động, không đoán được tuần tự với khách ngoài.

### 5.4. Vòng đời đơn hàng

```
[*] → PendingPayment   (tạo đơn: chuyển khoản / cổng TT)
[*] → Pending          (tạo đơn: COD / tại quầy)
PendingPayment → Pending          (đã nhận thanh toán)
PendingPayment → Cancelled        (quá hạn thanh toán / khách hủy)
Pending        → Confirmed        (nhân viên xác nhận)
Pending        → Cancelled        (khách hoặc nhân viên hủy)
Confirmed      → Packing          (bắt đầu đóng gói, quét serial)
Confirmed      → Cancelled        (Quản lý duyệt hủy)
Packing        → ReadyForPickup   (sẵn sàng — nhận tại cửa hàng)
Packing        → Shipping         (bàn giao vận chuyển)
Packing        → Cancelled        (Quản lý duyệt hủy, gỡ serial)
ReadyForPickup → Completed        (khách nhận hàng)
ReadyForPickup → Cancelled        (quá hạn nhận)
Shipping       → Completed        (giao thành công)
Shipping       → DeliveryFailed   (giao thất bại)
DeliveryFailed → Shipping         (giao lại)
DeliveryFailed → Cancelled        (hoàn về kho)
Completed      → PartiallyReturned / Returned     (đổi trả sau khi hoàn tất)
```

| Trạng thái | Ý nghĩa |
|---|---|
| PendingPayment | Chờ thanh toán (chuyển khoản / cổng). Đã giữ hàng, có đồng hồ đếm ngược |
| Pending | Đã tạo, chờ nhân viên xác nhận (COD vào thẳng đây) |
| Confirmed | Nhân viên đã kiểm tra thông tin, tồn kho, thanh toán |
| Packing | Đang lấy hàng, quét serial gán vào đơn |
| ReadyForPickup | Hàng đã chuẩn bị xong tại cửa hàng, chờ khách đến |
| Shipping | Đã bàn giao đơn vị vận chuyển |
| DeliveryFailed | Giao không thành công |
| Completed | Đã giao / đã nhận thành công |
| Cancelled | Đã hủy trước khi hoàn tất |
| PartiallyReturned / Returned | Có đổi trả sau khi hoàn tất |

**Quy tắc chuyển trạng thái**

- **ORD-07** Chỉ các chuyển vẽ trên sơ đồ mới hợp lệ; chuyển khác bị từ chối (cài trong `usp_Internal_SetOrderStatus`).
- **ORD-08** Mỗi lần chuyển lưu người thực hiện, thời điểm, lý do (bắt buộc với hủy và giao thất bại) — `OrderStatusHistory`.
- **ORD-09** Quyền chuyển trạng thái theo ma trận mục 1.2.

### 5.5. Tác động của từng bước lên tồn kho và serial

| Sự kiện | Physical | Reserved | Available | Serial |
|---|---|---|---|---|
| Tạo đơn (giữ hàng) | không đổi | **+ SL** | **− SL** | không đổi |
| Xác nhận đơn | không đổi | không đổi | không đổi | không đổi |
| Quét serial khi đóng gói | không đổi | không đổi | không đổi | `InStock` → `Allocated` |
| Giao thành công / khách nhận | **− SL** | **− SL** | không đổi | `Allocated` → `Sold` (kích hoạt BH) |
| Hủy **trước** khi giao | không đổi | **− SL** | **+ SL** | `Allocated` → `InStock` |
| Giao thất bại rồi hoàn về kho | không đổi | **− SL** | **+ SL** | `Allocated` → `InStock` |
| Trả hàng **sau** khi hoàn tất | **+ SL** (khi nhập lại kho) | không đổi | **+ SL** | `Sold` → `Returned` → theo kiểm tra |

- **ORD-10** Giữ hàng phải **an toàn khi nhiều người mua đồng thời**: hai khách không thể cùng giữ thành công món cuối cùng (proc dùng transaction + khoá — NFR-01).
- **ORD-11** **Timeout giữ hàng**: đơn `PendingPayment` quá hạn cấu hình (mặc định 30 phút với cổng TT, 24 giờ với chuyển khoản) **tự hủy** và nhả tồn — tác vụ nền (chưa cài đặt).
- **ORD-12** Đơn `Pending` COD không xác nhận quá hạn cấu hình (mặc định 24 giờ) → nhắc Quản lý xử lý; không tự hủy trừ khi cấu hình bật.
- **ORD-13** Sau khi đơn hoàn tất, **không được sửa chi tiết đơn**; mọi điều chỉnh đi qua đổi trả/hoàn tiền (có phê duyệt).

### 5.6. Thanh toán

- **PAY-01** Phương thức: **COD**, **Chuyển khoản**, **Cổng thanh toán**, **Tiền mặt/POS tại quầy**, **Công nợ** (chỉ khách sỉ có hạn mức).
- **PAY-02** Cổng thanh toán: callback phải **idempotent**, kiểm tra chữ ký, đối chiếu số tiền; chỉ tin callback/truy vấn từ cổng, không tin trình duyệt khách.
- **PAY-03** Chuyển khoản: đối soát theo nội dung chứa mã đơn; chuyển thiếu/thừa được đánh dấu xử lý.
- **PAY-04** Hoàn tiền: chỉ qua quy trình đổi trả/hủy được duyệt; hoàn đúng phương thức gốc nếu có thể; ghi giao dịch hoàn riêng, **không sửa giao dịch gốc** (`Payments.ParentPaymentId`).
- **PAY-05** Doanh thu chỉ ghi nhận khi đơn **Completed** và đã thanh toán (hoặc ghi nhận công nợ phải thu với khách sỉ).

---

## 6. QUẢN LÝ KHO THEO SỐ LƯỢNG

- **INV-01** Tồn kho theo dõi **theo từng chi nhánh** và từng SKU: Tổng nhập, Tổng xuất, Physical, Reserved, Available.
- **INV-02** **Cảnh báo tồn tối thiểu (Min-stock)**: cấu hình theo SKU và chi nhánh; khi `Available` thấp hơn ngưỡng, cảnh báo Thủ kho và Quản lý.
- **INV-03** Mọi biến động tồn kho sinh một **bút toán kho (stock movement) bất biến** (`StockMovements` — chỉ INSERT): loại, số lượng, chi nhánh, chứng từ gốc, người thực hiện, thời điểm. **Số tồn là kết quả cộng dồn, không sửa trực tiếp.**
- **INV-04** **Điều chỉnh tồn thủ công** (kiểm kê lệch, hư hỏng) bắt buộc có lý do và **phải được Quản lý duyệt**.
- **INV-05** **Kiểm kê**: lập phiếu theo chi nhánh; so sánh số đếm với hệ thống (hàng có serial thì so khớp danh sách serial); chênh lệch tạo phiếu điều chỉnh chờ duyệt.
- **INV-06** **Chuyển kho nội bộ**: Thủ kho tạo phiếu chuyển (chi nhánh đi → đến); hàng **đang chuyển (`InTransfer`)** không tính vào tồn khả dụng cả hai chi nhánh; chi nhánh nhận xác nhận mới ghi nhận nhập. Hàng có serial thì serial đi theo phiếu chuyển.

### 6.1. Loại bút toán kho (StockMovementType)

`Receipt(0)` · `Reserve(1)` · `Release(2)` · `Issue(3)` · `TransferOut(4)` · `TransferIn(5)` ·
`StockTakeAdjust(6)` · `ManualAdjust(7)` · `CustomerReturn(8)` · `SupplierReturn(9)`.

---

## 7. NHẬP KHO VÀ NHÀ CUNG CẤP

- **SUP-01** Quản lý **Nhà cung cấp**: thông tin liên hệ, mã số thuế, điều khoản thanh toán, danh sách sản phẩm cung cấp (`SupplierProducts`).
- **INV-07** **Phiếu nhập kho** gồm: số lượng, đơn giá nhập, chi nhánh nhận. Với sản phẩm `requires_serial`, nhân viên **phải nhập/quét đủ danh sách serial** khớp số lượng; thiếu một serial thì không cho hoàn tất phiếu.
- **INV-08** Serial phải **duy nhất** trong toàn hệ thống (theo SKU hoặc toàn cục theo cấu hình). Nhập trùng serial bị chặn và báo rõ serial trùng.
- **INV-09** **Giá vốn**: tính theo phương pháp cấu hình (mặc định bình quân gia quyền); hàng có serial có thể lưu giá nhập từng serial. Giá vốn phục vụ báo cáo lợi nhuận và ràng buộc PRC-06.
- **SUP-02** **Công nợ nhà cung cấp** (`SupplierLedger`): ghi nhận phải trả theo phiếu nhập, thanh toán từng phần, theo dõi hạn thanh toán.
- **SUP-03** **Trả hàng nhà cung cấp** (`SupplierReturns`): lập phiếu trả (hàng lỗi, bảo hành hãng), trừ tồn, serial chuyển `ReturnedToSupplier`, giảm công nợ tương ứng.

Trạng thái phiếu nhập (`PurchaseReceiptStatus`): `Draft(0)` · `Completed(1)` · `Cancelled(2)`.
Loại bút toán công nợ NCC: `Payable(0)` · `Payment(1)` · `ReturnCredit(2)`.

---

## 8. VÒNG ĐỜI SERIAL

```
[*] → InStock            (Nhập kho)
InStock     → Allocated  (Gán vào đơn – đóng gói)
Allocated   → InStock    (Hủy đơn / gỡ gán nhầm)
Allocated   → Sold       (Giao thành công)
InStock     → InTransfer (Chuyển kho)
InTransfer  → InStock    (Chi nhánh nhận xác nhận)
InStock     → Defective  (Phát hiện lỗi trong kho)
Sold        → Returned   (Khách trả hàng)
Returned    → InStock    (Kiểm tra đạt, bán lại)
Returned    → Defective  (Kiểm tra không đạt)
Sold        → RMA        (Tiếp nhận bảo hành)
RMA         → Sold       (Sửa xong, trả khách)
RMA         → Replaced   (Đổi mới – serial mới thay thế)
RMA         → Defective  (Không sửa được, không đổi)
Defective   → ReturnedToSupplier (Trả nhà cung cấp)
Defective   → Scrapped   (Hủy bỏ)
```

| Trạng thái | Ý nghĩa | Tính vào Available |
|---|---|---|
| InStock | Trong kho, sẵn sàng bán | Có |
| Allocated | Đã gán cho đơn, chưa giao | Không |
| InTransfer | Đang chuyển giữa chi nhánh | Không |
| Sold | Đã bán, đang trong tay khách | Không |
| RMA | Đang bảo hành tại cửa hàng / gửi hãng | Không |
| Returned | Khách trả, chờ kiểm tra | Không |
| Replaced | Đã được thay bằng serial khác (kết thúc) | Không |
| Defective | Lỗi, chờ xử lý | Không |
| ReturnedToSupplier / Scrapped | Đã trả NCC / đã hủy (kết thúc) | Không |

**Quy tắc**

- **SER-01** Chỉ serial ở `InStock` mới được gán vào đơn.
- **SER-02** Mỗi serial chỉ gán cho **một đơn tại một thời điểm**. Gỡ gán trước khi giao, ghi Audit Log.
- **SER-03** Mọi chuyển trạng thái serial lưu lịch sử (`SerialStatusHistory`).
- **SER-04** **Tra cứu bảo hành bằng serial**: xem SKU, ngày mua, đơn hàng, khách hàng, thời hạn bảo hành còn lại, lịch sử bảo hành.

---

## 9. BẢO HÀNH ĐIỆN TỬ

- **WAR-01** Thời hạn bảo hành **bắt đầu từ ngày giao thành công** (chuyển serial sang `Sold`), không phải ngày đặt hàng. Thời hạn lấy từ snapshot lúc đặt (ORD-04).
- **WAR-02** Khách tra cứu bảo hành bằng serial hoặc trong lịch sử đơn hàng.
- **WAR-03** Hàng không có serial (`requires_serial = false`) bảo hành theo **đơn hàng** (cần mã đơn).

### 9.1. Quy trình bảo hành

```
[*] → Received            (Tiếp nhận – tạo phiếu)
Received     → Rejected          (Không đủ điều kiện)
Received     → Diagnosing        (Kiểm tra sơ bộ)
Diagnosing   → SentToVendor      (Gửi hãng / NCC)
Diagnosing   → Repaired          (Sửa tại cửa hàng)
SentToVendor → Repaired          (Hãng sửa xong)
SentToVendor → Replaced          (Hãng đổi mới)
Repaired / Replaced / Rejected → ReturnedToCustomer
```

Trạng thái phiếu (`WarrantyTickets.Status`): `Received(0)` · `Diagnosing(1)` · `SentToVendor(2)` ·
`Repaired(3)` · `Replaced(4)` · `Rejected(5)` · `ReturnedToCustomer(6)`.

- **WAR-04** **Tiếp nhận**: quét serial, hệ thống kiểm tra còn hạn hay không và hiển thị thông tin liên quan; ghi tình trạng thực tế, phụ kiện; sinh **phiếu tiếp nhận** có mã để khách theo dõi.
- **WAR-05** **Từ chối bảo hành** khi: hết hạn; tem/serial rách hoặc sửa; hư hỏng do tác động vật lý/vào nước; không đúng điều kiện. **Lý do từ chối bắt buộc nhập** và khách được thông báo.
- **WAR-06** **Đổi mới**: serial cũ → `Replaced`, serial mới → `Sold` và gắn vào phiếu. **Thời hạn bảo hành của serial mới = thời hạn còn lại của serial cũ** (mặc định), lưu liên kết serial cũ ↔ mới (`ReplacedBySerialId`).
- **WAR-07** Mỗi phiếu có lịch sử trạng thái, người xử lý, thông báo cho khách khi đổi trạng thái chính.
- **WAR-08** Chi phí bảo hành ngoài chính sách được ghi nhận thanh toán riêng (`Fee`).

---

## 10. ĐỔI TRẢ

- **RET-01** Hạn đổi trả cấu hình theo nhóm sản phẩm (ví dụ 7 ngày kể từ ngày giao), kèm điều kiện (nguyên seal, đủ phụ kiện, không lỗi do người dùng).
- **RET-02** Đổi trả có thể **một phần** hoặc toàn bộ; đơn chuyển `PartiallyReturned` hoặc `Returned`.
- **RET-03** Hàng trả về vào trạng thái `Returned` chờ kiểm tra: đạt → `InStock` (cộng lại Physical và Available); không đạt → `Defective`.
- **RET-04** Mọi đổi trả và hoàn tiền **do Quản lý duyệt**, ghi Audit Log.
- **RET-05** Đổi trả làm phát sinh **thu hồi hoa hồng** (xem mục 12).

### 10.1. Vòng đời yêu cầu đổi trả

`Requested(0)` → `Approved(1)` → `GoodsReceived(3)` → `Refunded(4)` → `Closed(5)`; nhánh `Rejected(2)`.

Quy trình 3 bước (`ReturnsController`):
1. `POST /returns/{id}/review` — QL/AD duyệt hoặc từ chối (từ chối bắt buộc lý do), chỉ khi đang Chờ duyệt.
2. `POST /returns/{id}/inspect` — TK/AD nhận hàng & kiểm tra từng dòng (`InspectionResult`: Pending/Pass/Fail).
3. `POST /returns/{id}/refund` — QL/AD hoàn tiền (tạo giao dịch hoàn riêng + thu hồi hoa hồng).

---

## 11. NHÂN SỰ VÀ VẬN HÀNH

- **HR-01** Hồ sơ gồm thông tin cá nhân, chức vụ (Thu ngân, Tư vấn kỹ thuật, Thủ kho, Quản lý), chi nhánh làm việc, trạng thái (đang làm / nghỉ việc).
- **HR-02** Nhân viên được gán **nhóm quyền** theo ma trận mục 1.2. Chuyển chi nhánh phải cập nhật phạm vi truy cập.
- **HR-03** Nhân viên nghỉ việc: **vô hiệu hóa tài khoản** ngay; **không xóa hồ sơ** (còn gắn đơn hàng, Audit Log, hoa hồng) — `Employees.Status = Terminated`.
- **HR-04** Phân ca: Ca sáng, Ca chiều, Full-time (cấu hình khung giờ); Quản lý chi nhánh xếp ca (`EmployeeSchedules`).
- **HR-05** Ghi nhận check-in / check-out tại cửa hàng (`Attendances`); có quy định chống gian lận.
- **HR-06** Xử lý ngoại lệ: quên check-in/out, đi muộn, về sớm, làm thêm giờ → Quản lý duyệt điều chỉnh (lưu lý do, Audit Log) — `Source = ManualAdjust`.

`EmployeePosition`: `Cashier(0)` · `TechAdvisor(1)` · `Storekeeper(2)` · `Manager(3)`.
`EmployeeStatus`: `Active(0)` · `Terminated(1)`.
`AttendanceSource`: `Device(0)` · `ManualAdjust(1)`.

---

## 12. HOA HỒNG VÀ KPI BÁN HÀNG

- **KPI-01** Mỗi đơn có `Sales_Staff_ID`. Doanh số và hoa hồng tính **khi đơn `Completed`**, không tính ở bước tạo đơn.
- **KPI-02** **Đơn khách tự đặt online** (không qua tư vấn): `Sales_Staff_ID` để trống và **không tính hoa hồng**, trừ khi khách nhập mã nhân viên giới thiệu (nếu bật).
- **KPI-03** **Nhiều nhân viên cùng tư vấn**: chia tỷ lệ hoa hồng (tổng 100%); mặc định một người chịu trách nhiệm chính (`OrderSalesStaff.ShareRate`).
- **KPI-04** **Thu hồi hoa hồng (Clawback)**: khi đơn đã tính hoa hồng bị đổi trả/hoàn tiền, hoa hồng tương ứng **trừ vào kỳ gần nhất** (có thể âm), theo tỷ lệ phần hàng bị trả.
- **KPI-05** Công thức hoa hồng cấu hình được: theo % doanh thu, % lợi nhuận gộp, theo nhóm sản phẩm, thưởng vượt chỉ tiêu. Công thức áp dụng lưu cùng kỳ tính để truy vết.
- **KPI-06** **Chốt kỳ**: cuối tháng tổng hợp, Quản lý duyệt; sau khi chốt **không sửa trực tiếp** — điều chỉnh phải qua bút toán điều chỉnh ở kỳ sau.

`CommissionScopeType`: `All(0)` · `Category(1)` · `Product(2)`.
`CommissionBasisType`: `Revenue(0)` · `GrossProfit(1)`.
`CommissionPeriodStatus`: `Open(0)` · `Closed(1)`.
`CommissionEntryType`: `Earn(0)` · `Clawback(1)` · `Adjustment(2)`.

**Ràng buộc:** mỗi nhân viên chỉ hưởng hoa hồng **một lần** trên một đơn
(`UQ (EmployeeId, OrderId) WHERE EntryType = 0`).
**Ghi chú thực tế:** proc hiện dùng một quy tắc phạm vi "toàn bộ" (`ScopeType = 0`); quy tắc theo danh mục/sản phẩm và thưởng vượt chỉ tiêu (`TargetAmount`, `BonusRatePercent`) **chưa** áp dụng trong proc.

---

## 13. NHẬT KÝ HỆ THỐNG (AUDIT LOG)

- **AUD-01** Ghi nhận ít nhất các thao tác nhạy cảm:
  - Sửa giá bán, giá sỉ, khuyến mãi, voucher.
  - Hủy đơn, đổi trả, hoàn tiền, đảo trạng thái đơn.
  - Điều chỉnh tồn kho, duyệt phiếu kiểm kê, gỡ gán serial, thay đổi trạng thái serial thủ công.
  - Đổi quyền, tạo/khóa tài khoản, thay đổi hồ sơ nhân viên.
- **AUD-02** Mỗi bản ghi gồm: **ai** (user), **làm gì** (hành động), **đối tượng** (loại và mã), **giá trị cũ và mới**, **thời điểm**, **IP/thiết bị**, **chi nhánh**, lý do (nếu có).
- **AUD-03** Log là **chỉ ghi thêm (append-only)**: không ai kể cả Admin sửa/xóa. Có chính sách lưu trữ (tối thiểu theo cấu hình, ví dụ 2 năm).
- **AUD-04** Quản lý chỉ xem log của chi nhánh mình; Admin xem toàn bộ. Có tìm kiếm và lọc theo người, thời gian, loại hành động.

**Ràng buộc kỹ thuật:** bảng `AuditLogs` thu hồi quyền UPDATE/DELETE với mọi tài khoản ứng dụng (xem cuối `table_and_data.sql`).

---

## 14. YÊU CẦU PHI CHỨC NĂNG (NFR)

| Mã | Yêu cầu |
|---|---|
| NFR-01 | **Toàn vẹn dữ liệu kho**: giữ hàng, trừ kho, gán serial trong giao dịch; chịu được đặt đồng thời mà không bán vượt tồn |
| NFR-02 | **Hiệu năng**: trang danh sách/lọc phản hồi < 2 giây (điều kiện bình thường); số người dùng đồng thời chốt với doanh nghiệp |
| NFR-03 | **Bảo mật**: xác thực mạnh cho nhân viên, mật khẩu băm an toàn, phiên có thời hạn, chống truy cập vượt quyền theo chi nhánh, không lưu dữ liệu thẻ |
| NFR-04 | **Sao lưu & phục hồi**: sao lưu định kỳ, có kế hoạch và thử nghiệm phục hồi |
| NFR-05 | **Khả năng truy vết**: mọi thay đổi nghiệp vụ quan trọng truy ngược được người và thời điểm |
| NFR-06 | **Khả năng mở rộng**: thêm loại linh kiện, chi nhánh, phương thức thanh toán mà không sửa lõi |
| NFR-07 | **Khả dụng**: thời gian ngừng cho phép và cửa sổ bảo trì thống nhất với doanh nghiệp |
| NFR-08 | **Đa ngôn ngữ/định dạng**: hiển thị tiếng Việt, tiền VND, múi giờ Việt Nam |
| NFR-09 | **Tác vụ nền**: hủy đơn quá hạn giữ hàng, cảnh báo tồn tối thiểu, nhắc việc định kỳ; có giám sát, chạy lại an toàn (idempotent) |

---

## 15. BẢO MẬT VÀ PHẠM VI DỮ LIỆU

- **Mật khẩu**: PBKDF2-HMAC-SHA256, 210.000 vòng; tối thiểu 8 ký tự.
- **JWT**: token chứa `userId`, `roles`, `customerId`/`employeeId`, `branchId`; phiên có thời hạn
  (AccessToken 30 phút, Refresh 14 ngày — cấu hình trong `appsettings.json`).
- **Phạm vi chi nhánh**: API **ép** `branchId` từ token (nhân viên), không tin client.
- **Phạm vi khách**: API **ép** `customerId` từ token; khách chỉ xem dữ liệu của mình.
- **Sai đăng nhập**: sai mật khẩu / không tồn tại / bị khóa → **cùng một thông điệp 401** (không lộ tài khoản tồn tại).
- **Vai trò mặc định**: `POST /api/auth/register` chỉ tạo được vai trò **Customer**.
- Tài khoản kiểm thử: xem `TAI_KHOAN_KIEM_THU.md` (chỉ dùng cho dev).

---

## 16. ĐIỂM CẦN DOANH NGHIỆP XÁC NHẬN (giá trị mặc định đề xuất)

1. Kho tính tồn **theo từng chi nhánh** (đề xuất) hay một kho tổng?
2. Thời hạn giữ hàng theo phương thức thanh toán (30 phút / 24 giờ / 3 ngày nhận tại cửa hàng).
3. Serial duy nhất **theo SKU** hay **toàn hệ thống**.
4. Bảo hành khi đổi mới: **kế thừa thời hạn còn lại** (đề xuất) hay tính lại từ đầu.
5. Công thức hoa hồng và việc tính hoa hồng cho đơn online có mã giới thiệu.
6. Chính sách đổi trả: số ngày, danh mục không được đổi trả.
7. Bán sỉ có công nợ triển khai ngay giai đoạn 1 hay để sau.
8. Phương pháp tính giá vốn (bình quân gia quyền hay theo serial/lô).
9. Mức tích hợp thanh toán và vận chuyển cần có ở giai đoạn đầu.

---

## 17. LỊCH SỬ CẬP NHẬT

| Ngày | Thay đổi |
|---|---|
| 26/11/2026 | Tạo bản tổng hợp quy tắc nghiệp vụ từ BRD + đối chiếu mã nguồn (RBAC, sản phẩm, giá, giỏ, đơn, kho, serial, bảo hành, đổi trả, HR, KPI, audit, NFR) |





