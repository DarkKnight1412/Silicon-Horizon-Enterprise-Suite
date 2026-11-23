# BỘ TÀI KHOẢN KIỂM THỬ

Dùng cho môi trường **dev** (database hiện có, ví dụ `BRD_DEV`). Mật khẩu lưu dạng **PBKDF2-HMACSHA256** (210.000 vòng) — đúng thuật toán của `Pbkdf2PasswordHasher` trong API.

> ⚠️ **Chỉ để kiểm thử. Đổi mật khẩu trước khi dùng thật.** Không dùng bộ này ở môi trường production.

## 1. Danh sách tài khoản

| # | Tên đăng nhập | Mật khẩu | Vai trò | Loại | Chi nhánh | Chức vụ | Dùng để kiểm thử |
|---|---|---|---|---|---|---|---|
| 1 | `admin` | `Admin@123456` | **Admin** | Nhân viên | CN01 | Quản lý (3) | Toàn quyền: `/api/v1/admin/**`, cấu hình, nhân sự, chốt hoa hồng, nhật ký |
| 2 | `manager` | `Manager@123456` | **Manager** | Nhân viên | CN01 | Quản lý (3) | Duyệt hủy đơn, duyệt điều chỉnh kho, duyệt/hoàn tiền đổi trả, xếp ca, điều chỉnh công |
| 3 | `cashier` | `Cashier@123456` | **Cashier** | Nhân viên | CN01 | Thu ngân (0) | Xác nhận đơn, giao hàng, đề xuất hủy, thu tiền (`/staff-orders`) |
| 4 | `tech` | `Tech@123456` | **TechAdvisor** | Nhân viên | CN01 | Tư vấn KT (1) | Tiếp nhận bảo hành, tạo đơn tại quầy (POS), xem serial |
| 5 | `store` | `Store@123456` | **Storekeeper** | Nhân viên | CN01 | Thủ kho (2) | Nhập/chuyển/kiểm kê kho, serial, đóng gói, kiểm tra hàng trả |
| 6 | `customer` | `Customer@123456` | **Customer** | Khách hàng | — | — | Giỏ hàng, đặt hàng, theo dõi đơn, yêu cầu đổi trả, tra cứu bảo hành |

Tất cả nhân viên trên đều thuộc chi nhánh **CN01** (`branchId` được ép từ token — chỉ `admin` chọn được chi nhánh khác trên UI).

## 2. Cách nạp tài khoản vào database
Chạy **theo thứ tự** (không tạo database mới — trỏ đúng DB hiện có):
```powershell
$sqlcmd = "C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\SQLCMD.EXE"
$db = "BRD_DEV"

# (1) quyền + vai trò + chi nhánh + tài khoản admin
# ⚠️ BẮT BUỘC có cờ -I (QUOTED_IDENTIFIER ON): bảng Users có filtered index (unique Email)
#    thiếu -I sẽ lỗi "INSERT failed because the following SET options have incorrect settings: 'QUOTED_IDENTIFIER'"
& $sqlcmd -S localhost -d $db -E -I -f 65001 -i "D:\CARRER\CODE_TEST\Silicon-Horizon-Enterprise-Suite\Database\07_seed_admin_user_and_permissions.sql"

# (2) bộ 6 tài khoản kiểm thử (gồm cả admin, chạy lại an toàn)
& $sqlcmd -S localhost -d $db -E -I -f 65001 -i "D:\CARRER\CODE_TEST\Silicon-Horizon-Enterprise-Suite\Database\08_seed_test_accounts.sql"
```
Kiểm tra nhanh:
```powershell
& $sqlcmd -S localhost -d $db -E -Q "SELECT Username, UserType FROM dbo.Users ORDER BY Id"
```

## 3. Đăng nhập
- **Swagger (backend)**: http://localhost:5270/swagger/index.html → `POST /api/auth/login` với
  ```json
  { "username": "admin", "password": "Admin@123456" }
  ```
  → copy `accessToken` → bấm **Authorize** → dán `Bearer <accessToken>`.
- **Giao diện (frontend)**: http://localhost:4200/login

## 4. Ghi chú kỹ thuật
- Mật khẩu ≥ 8 ký tự (ràng buộc trong `AuthService`).
- Hash PBKDF2 gồm: `pbkdf2-sha256$<iterations>$<base64 salt>$<base64 hash>` — dài ~90 ký tự, vừa `Users.PasswordHash varchar(255)`.
- Sai mật khẩu / tài khoản không tồn tại / bị khoá đều trả **cùng một thông điệp 401** (không lộ tài khoản có tồn tại).
- Muốn tạo tài khoản khác: dùng `POST /api/auth/register` (chỉ tạo được vai trò **Customer**), hoặc thêm dòng vào bảng `@Seed` trong `08_seed_test_accounts.sql` kèm hash sinh bằng API/tool PBKDF2 cùng tham số.
