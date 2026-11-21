# XÁC THỰC (AUTHENTICATION) VÀ PHÂN QUYỀN (AUTHORIZATION)

Chốt cơ chế đăng nhập/token, cách mã hoá quyền và cách API ép phạm vi dữ liệu — theo BRD mục 2 và
`Database/README.md` mục 1.1, 2.

## 1. Luồng xác thực

```
POST /api/auth/register  → usp_Auth_RegisterCustomer (hash PBKDF2 ở API, proc chỉ ghi dữ liệu)
POST /api/auth/login     → usp_Auth_GetUserByUsername (5 result set) → API so khớp hash → phát JWT
                           + usp_Auth_UpdateLastLogin
POST /api/auth/refresh   → đọc refresh token (kiểm tra chữ ký + audience riêng) → đọc lại quyền từ DB → phát cặp token mới
GET  /api/me             → usp_Auth_GetUserByUsername (theo claim name) → trả vai trò, quyền, phạm vi chi nhánh
Các API khác             → Authorization: Bearer <access token>
```

Sai mật khẩu / tài khoản bị khoá / không tồn tại → **cùng một thông điệp 401** để không lộ tài khoản có tồn tại.

## 2. Claim trong token

| Claim | Nguồn | Dùng ở đâu |
|---|---|---|
| `sub`, `userId` | `Users.Id` | `ICurrentUser.UserId`, ghi nhật ký |
| `name` (ClaimTypes.Name) | `Users.Username` | `ICurrentUser.Username`, gọi lại `usp_Auth_GetUserByUsername` cho `/api/me` |
| `role` (ClaimTypes.Role, nhiều giá trị) | `usp_Auth_GetUserByUsername` (result set 2) | `[Authorize(Roles = ...)]`, `User.IsInRole` |
| `permission` (nhiều giá trị) | result set 3 (đã gộp từ `RolePermissions`) | `[RequirePermission("...")]` |
| `customerId` | result set 5 | ép phạm vi dữ liệu của khách |
| `employeeId`, `branchId`, `employeePosition` | result set 4 | ép phạm vi chi nhánh (BRD 2.2) |
| `userType` | `Users.UserType` (0 khách, 1 nhân viên) | phân nhánh nghiệp vụ |
| `token_type` | `access` / `refresh` | chặn dùng refresh token thay access token |
| `iss`, `aud`, `iat`, `exp` | `JwtOptions` | kiểm tra hiệu lực |

Tên claim khai báo một chỗ duy nhất: `Application/Abstractions/Security/AuthClaimTypes.cs`.
`MapInboundClaims = false` ở cả nơi phát hành và nơi kiểm tra → claim giữ nguyên tên, không bị .NET đổi tên.

## 3. Cấu hình

```json
"Jwt": {
  "Issuer": "SiliconHorizon",
  "Audience": "SiliconHorizon.Api",
  "SigningKey": "DEV-ONLY-CHANGE-ME-32-CHARS-MINIMUM-0001",
  "AccessTokenMinutes": 30,
  "RefreshTokenDays": 14
}
```

| Quy tắc | Chi tiết |
|---|---|
| Thuật toán | HS256, khóa đối xứng **tối thiểu 32 ký tự** — `JwtOptions` dùng `[MinLength(32)]` + `ValidateOnStart()` nên app **không khởi động** nếu khóa ngắn |
| Khóa thật | **Không commit**. Đặt qua biến môi trường `Jwt__SigningKey` hoặc user-secrets; `appsettings.json` chỉ chứa giá trị DEV |
| Thời hạn | Access 30 phút (cấu hình được), refresh 14 ngày |
| Audience | Refresh token dùng `Audience + ":refresh"` → **không thể** dùng refresh token để gọi API |
| ClockSkew | 30 giây (mặc định của .NET là 5 phút) |

## 4. Phân quyền — 2 tầng

### 4.1. Theo vai trò (đang dùng được ngay)

Vai trò đã có seed trong `table_and_data.sql`: `Customer`, `Cashier`, `TechAdvisor`, `Storekeeper`, `Manager`, `Admin`
(hằng số: `RoleCodes`). Cách dùng:

```csharp
[Authorize(Roles = RoleCodes.Admin)]            // chỉ Admin
[Authorize(Roles = RoleCodes.Approvers)]        // Manager hoặc Admin
[Authorize(Roles = RoleCodes.Staff)]            // mọi nhân viên (ký hiệu NV trong BRD 2.2)
```

### 4.2. Theo mã quyền (đã code sẵn, cần seed dữ liệu)

```csharp
[RequirePermission(PermissionCodes.OrderCancelApprove)]
public Task<IActionResult> Cancel(...) { ... }
```

Cơ chế: `RequirePermissionAttribute` sinh policy `permission:<mã>` → `PermissionPolicyProvider` tạo policy động →
`PermissionAuthorizationHandler` so khớp với claim `permission` trong token.

> ⚠️ **Việc còn thiếu ở database:** `table_and_data.sql` **chưa seed** bảng `Permissions` và `RolePermissions`,
> nên hiện **chưa ai có claim quyền** — dùng `[RequirePermission]` sẽ trả 403 cho mọi người, kể cả Admin.
> Admin không tự động có mọi quyền; quyền phải được gán tường minh cho từng vai trò.

SQL mẫu (chạy sau khi đã chạy 6 file SQL, cần bạn duyệt riêng):

```sql
INSERT INTO dbo.Permissions (Code) VALUES
    ('catalog.manage'), ('price.manage'), ('promotion.manage'),
    ('order.cancel.approve'), ('stock.adjust.approve'), ('serial.manage'),
    ('warranty.receive'), ('warranty.process'),
    ('return.review'), ('return.inspect'), ('return.refund'),
    ('hr.schedule.manage'), ('hr.attendance.adjust'),
    ('commission.manage'), ('audit.view'), ('settings.manage');

-- Admin: toàn quyền; Manager: nhóm duyệt trong chi nhánh
INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id FROM dbo.Roles r CROSS JOIN dbo.Permissions p WHERE r.Code = 'Admin';

INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id FROM dbo.Roles r JOIN dbo.Permissions p
    ON p.Code IN ('order.cancel.approve', 'stock.adjust.approve', 'return.review', 'return.refund',
                  'hr.schedule.manage', 'hr.attendance.adjust', 'warranty.process', 'audit.view')
WHERE r.Code = 'Manager';
```

## 5. Refresh token — chiến lược và giới hạn

| Khía cạnh | Hiện tại |
|---|---|
| Định dạng | JWT ký HS256, audience riêng, **không chứa vai trò/quyền** |
| Thu hồi | **Chưa hỗ trợ revoke/rotate** (không có bảng lưu token). Đổi quyền có hiệu lực: ngay với refresh mới, còn access token tối đa bằng `AccessTokenMinutes` |
| Muốn thu hồi ngay | Thêm bảng `RefreshTokens (Id, UserId, TokenHash, ExpiresAt, RevokedAt, ReplacedByTokenId)` + kiểm tra khi refresh; cần migration EF Core và cập nhật `AuthService.RefreshAsync` |
| Nâng cấp an ninh | Bật rotation: mỗi lần refresh phát token mới và vô hiệu token cũ; phát hiện reuse → revoke cả họ token |

## 6. Mật khẩu

| Hạng mục | Giá trị |
|---|---|
| Thuật toán | **PBKDF2-HMACSHA256**, 210.000 vòng, salt 16 byte ngẫu nhiên, key 32 byte (`Pbkdf2PasswordHasher`) |
| Định dạng lưu | `pbkdf2-sha256$<iterations>$<base64Salt>$<base64Hash>` (~90 ký tự, vừa `Users.PasswordHash varchar(255)`) |
| Lý do chọn | Có sẵn trong .NET (không thêm package), tham số nằm trong chuỗi băm nên **nâng số vòng sau này vẫn kiểm tra được mật khẩu cũ** |
| So sánh | `CryptographicOperations.FixedTimeEquals` (chống tấn công theo thời gian) |
| Ràng buộc đầu vào | Tối thiểu 8 ký tự (`MinimumPasswordLength` trong `AuthService`) |
| Không bao giờ | Trả `PasswordHash` ra API — `AuthUserRow.PasswordHash` chỉ dùng nội bộ trong `AuthService` |

## 7. Phạm vi dữ liệu (bắt buộc tuân thủ khi viết use case)

| Đối tượng | Quy tắc | Lấy từ đâu |
|---|---|---|
| Nhân viên | Chỉ thao tác trong chi nhánh được gán (trừ Admin). **API ép `branchId` từ token**, không nhận tham số `branchId` do client gửi | `ICurrentUser.BranchId` |
| Khách hàng | Chỉ xem dữ liệu của mình. **API ép `customerId` từ token** | `ICurrentUser.CustomerId` |
| Vượt phạm vi | Ném `ForbiddenException` (403) hoặc trả 404 để không lộ sự tồn tại của dữ liệu | `Application/Common/Exceptions` |
| Ghi nhật ký | Truyền `@UserId` cho proc (qua `sp_set_session_context`) | `SqlSessionContextExtensions` |

## 8. Bảng endpoint ↔ quyền (theo Database/README.md)

| Nhóm API | Vai trò tối thiểu | Mã quyền (khi đã seed) |
|---|---|---|
| `/api/auth/register`, `/api/auth/login`, `/api/auth/refresh` | Public (ẩn danh, tự bảo vệ bằng chữ ký token) | — |
| `/api/me` | Đã đăng nhập | — |
| `/api/products`, `/api/categories`, `/api/brands`, `/api/compatibility/check` | Public | — |
| `/api/cart*` | Public (giỏ khách vãng lai theo `sessionToken`) | — |
| `/api/orders` (tạo/đổi trạng thái), `/api/orders/{id}/pack`, `/ship`, `/complete` | NV (`Staff`) | `order.confirm`, `order.pack`, `order.ship` |
| hủy đơn / duyệt đổi trả / hoàn tiền | `Approvers` | `order.cancel.approve`, `return.review`, `return.refund` |
| nhập kho, chuyển kho, kiểm kê, serial | `Storekeeper` hoặc `Admin` | `stock.receipt`, `stock.transfer`, `stock.take`, `serial.manage` |
| duyệt điều chỉnh tồn | `Approvers` | `stock.adjust.approve` |
| bảo hành (tiếp nhận / xử lý) | `SalesDesk` / `Storekeeper` | `warranty.receive`, `warranty.process` |
| `/api/v1/admin/**` (sản phẩm, giá, khuyến mãi, khách, nhân sự, cấu hình) | `Admin` | `catalog.manage`, `price.manage`, `promotion.manage`, `hr.schedule.manage`, `settings.manage` |
| `/api/audit-logs` | `Manager` (chi nhánh mình), `Admin` | `audit.view` |
| `/api/reports/*` | `Manager`, `Admin` (tồn kho: thêm `Storekeeper`) | — |

## 9. Phản hồi lỗi xác thực/phân quyền

| Tình huống | HTTP | Body |
|---|---|---|
| Không gửi token tới endpoint `[Authorize]` | **401** | ProblemDetails (từ `UseStatusCodePages`) + header `WWW-Authenticate: Bearer` |
| Token sai chữ ký / hết hạn / sai audience (ví dụ dùng refresh token) | **401** | ProblemDetails |
| Đăng nhập sai mật khẩu, tài khoản bị khoá | **401** | `title: "Tên đăng nhập hoặc mật khẩu không đúng."`, `detail: "AUTH-INVALID-CREDENTIALS"` |
| Refresh token không hợp lệ/hết hạn | **401** | `detail: "AUTH-REFRESH-INVALID"` |
| Đã đăng nhập nhưng thiếu vai trò/quyền | **403** | ProblemDetails (mặc định của ASP.NET) |
| Truy cập dữ liệu ngoài phạm vi chi nhánh/khách | **403** | `detail: "AUTH-FORBIDDEN"` |

## 10. Bản đồ file

| Việc | File |
|---|---|
| Cấu hình JwtBearer + policy provider/handler | `Api/Security/AuthenticationSetup.cs` |
| Đọc claim → người gọi hiện tại | `Api/Security/CurrentUser.cs` |
| Quyền theo mã (requirement/attribute/provider) | `Api/Security/PermissionRequirement.cs` |
| Kiểm tra quyền khi request | `Api/Security/PermissionAuthorizationHandler.cs` |
| Endpoint tài khoản | `Api/Controllers/Auth/AuthController.cs` |
| Use case + gọi proc | `Application/Features/Auth/AuthService.cs` |
| Tên claim, vai trò, mã quyền | `Application/Abstractions/Security/AuthClaimTypes.cs`, `Application/Security/RoleCodes.cs`, `Application/Security/PermissionCodes.cs` |
| Phát hành/đọc token, băm mật khẩu | `Infrastructure/Security/JwtTokenService.cs`, `Pbkdf2PasswordHasher.cs`, `JwtOptions.cs` |

## 11. Việc còn lại

| # | Việc | Ghi chú |
|---|---|---|
| 1 | **Seed `Permissions` + `RolePermissions`** (SQL mẫu ở mục 4.2) | điều kiện để dùng `[RequirePermission]` |
| 2 | Bảng `RefreshTokens` + rotation/revoke | xem mục 5 |
| 3 | Rate limiting `/api/auth/login` (NFR-03) | chống dò mật khẩu; đặt trước authentication |
| 4 | Khoá tài khoản sau N lần sai + ghi `AuditLogs` | cần bảng đếm hoặc dùng `AuditLogs` |
| 5 | Endpoint đổi/đặt lại mật khẩu, nhân viên Admin tạo tài khoản | dùng `IPasswordHasher` đã có |
| 6 | `[AllowAnonymous]` tường minh cho endpoint Public nếu sau này bật fallback policy | hiện chưa bật (xem ghi chú ở `AuthenticationSetup`) |
| 7 | Bật HTTPS + `RequireHttpsMetadata = true` ở production | đã tự động theo môi trường |


## 12. Ghi chú phiên bản (đã verify bằng chạy thật)

| # | Phát hiện | Chi tiết |
|---|---|---|
| 1 | `Microsoft.IdentityModel.JsonWebTokens` **8.x** khác 7.x ở 3 điểm — viết theo 7.x sẽ build fail | (a) `TokenValidationResult.ClaimsPrincipal` → **`ClaimsIdentity`**; (b) `ValidateToken(...)` đồng bộ **đã bỏ**, chỉ còn `ValidateTokenAsync(...)` (overload 3 tham số nhận `BaseConfiguration` chứ không phải `CancellationToken` → truyền hủy qua `.WaitAsync(ct)`); (c) `MapInboundClaims` **bỏ khỏi** `TokenValidationParameters`, chuyển thành thuộc tính của handler (`new JsonWebTokenHandler { MapInboundClaims = false }`). Vì vậy `IJwtTokenService.ReadRefreshTokenAsync` là **async** |
| 2 | Chọn đúng version | `Microsoft.AspNetCore.Authentication.JwtBearer 8.0.34` (8.0.x mới nhất của dòng .NET 8) + `Microsoft.IdentityModel.JsonWebTokens 8.23.0` (mới nhất ⇒ không bao giờ bị lỗi downgrade NU1605) |
| 3 | Lỗi validate model của `[ApiController]` **đã** trả ProblemDetails kèm `errors` | Không cần viết thêm code (mục 3 trong "việc còn lại" của MIDDLEWARE_AND_ERROR_HANDLING.md coi như xong) |
| 4 | Test API bằng PowerShell 5.1 | `curl.exe -d '{...}'` **bị nuốt dấu ngoặc kép** → body hỏng, server trả 400. Phải ghi JSON ra file (UTF-8 **không BOM**) và dùng `curl.exe --data-binary "@file"` |

**Đã kiểm chứng bằng chạy thật:**

```http
GET /api/v1/health                          → 200 (vẫn public)
GET /api/me                                 → 401 + WWW-Authenticate: Bearer
GET /api/v1/admin/categories                → 401 + WWW-Authenticate: Bearer
GET /api/v1/admin/categories (+Bearer rác)   → 401 + WWW-Authenticate: Bearer error="invalid_token"

POST /api/auth/refresh (token rác)           → 401
{"type":"https://httpstatuses.com/401","title":"Phiên đăng nhập không hợp lệ hoặc đã hết hạn.",
 "status":401,"detail":"AUTH-REFRESH-INVALID","instance":"/api/auth/refresh","correlationId":"..."}

POST /api/auth/login (chưa có database)      → 500
{"status":500,"title":"Lỗi hệ thống, vui lòng liên hệ quản trị viên.","detail":"SQL-4060",
 "instance":"/api/auth/login","correlationId":"..."}
```
