# KIẾN TRÚC HỆ THỐNG — Apex Circuit Electronics Enterprise Suite

> **Tài liệu sống** — cập nhật mỗi khi dự án nâng cấp.
> Tài liệu liên quan: `BUSINESS_RULES.md` (quy tắc nghiệp vụ) · `DATABASE.md` (dữ liệu) ·
> `README.md` (BRD) · `DATABASE_ERD.md` (use case + ERD chi tiết) · `Database/README.md` (API ↔ stored procedure).

| Thông tin | Giá trị |
|---|---|
| Nền tảng Backend | .NET 8 (ASP.NET Core Web API) |
| Nền tảng Frontend | Angular 22 (standalone, lazy loading) + TailwindCSS 4 |
| Cơ sở dữ liệu | Microsoft SQL Server |
| Xác thực | JWT Bearer + Google Sign-In |
| Cập nhật gần nhất | 26/11/2026 (nhánh `dev_cus`, commit `89213cdd` + thay đổi chưa commit) |

---

## 1. TỔNG QUAN

Hệ thống là một ứng dụng **web đơn khối (monolith) phân lớp** gồm 2 phần tách biệt:

- **Backend** — một solution .NET 8 theo kiến trúc Clean Architecture (4 tầng), phục vụ REST API
  tại tiền tố `/api/v1` (xác thực `/api`).
- **Frontend** — một ứng dụng Angular SPA (Single Page Application) tiêu thụ API qua proxy khi dev.

Toàn bộ nghiệp vụ phức tạp (đặt hàng, giữ/trừ tồn, gán serial, bảo hành, đổi trả, hoa hồng)
được cài đặt trong **stored procedure SQL Server**; backend chỉ là lớp điều phối mỏng.

---

## 2. CÁC TẦNG VÀ QUY TẮC PHỤ THUỘC

```
┌──────────────────────────────────────────────────────────────────────┐
│  Frontend — Angular 22 SPA (frontend/)                               │
│    core/ (auth, http, models, services) · features/ · layout/ · shared/│
└───────────────────────────────┬──────────────────────────────────────┘
                                │ HTTP/JSON (JWT Bearer) — /api/v1/*
┌───────────────────────────────▼──────────────────────────────────────┐
│  SiliconHorizon.Api — ASP.NET Core Web API (.NET 8, Swagger)         │
│    ├─ Controllers/   Controller mỏng theo 15 module                  │
│    ├─ Middleware/    CorrelationId · RequestLogging · SwaggerVersion  │
│    ├─ Security/      CurrentUser, AuthenticationSetup, Permission auth│
│    ├─ Constants/     OrderRoleGroups (nhóm quyền nghiệp vụ)           │
│    ├─ Exceptions/    ApiExceptionHandler (lỗi → ProblemDetails)       │
│    └─ Storage/       ProductImageStorage (lưu ảnh lên wwwroot)       │
│                    │                                                  │
│        ┌───────────┴────────────┐                                    │
│        ▼                        ▼                                    │
│  SiliconHorizon.Application   SiliconHorizon.Infrastructure          │
│   (use case / DTO / interface) (EF Core 8 + Dapper + SQL Server)      │
│        │                        │                                    │
│        └───────────┬────────────┘                                    │
│                    ▼                                                 │
│         SiliconHorizon.Domain  (entity + enum thuần, không dependency)│
└───────────────────────────────┬──────────────────────────────────────┘
                                │
┌───────────────────────────────▼──────────────────────────────────────┐
│  SQL Server — 66 bảng + ~90 stored procedure + 4 hàm dùng chung       │
└──────────────────────────────────────────────────────────────────────┘
```

**Quy tắc phụ thuộc (bắt buộc):**

```
Api  →  Application + Infrastructure  →  Domain
```

- `Domain` **không** tham chiếu bất kỳ package nào (chỉ `net8.0`, code thuần).
- `Application` **không** tham chiếu EF Core / Dapper / ASP.NET Core (chỉ `Microsoft.Extensions.DependencyInjection.Abstractions` cho DI extension).
- `Infrastructure` tham chiếu `Application` + `Domain`, cài đặt các abstraction (EF Core, Dapper, SQL Server, PBKDF2, JWT, Google token).
- `Api` là điểm vào, đăng ký DI và middleware.

---

## 3. BACKEND (.NET 8)

### 3.1. Các project trong solution

| Project | Vai trò | Package chính |
|---|---|---|
| `SiliconHorizon.Api` | Web API, controllers, middleware, Swagger | `Microsoft.AspNetCore.Authentication.JwtBearer`, `Swashbuckle.AspNetCore` |
| `SiliconHorizon.Application` | Use case, DTO, interface, validation | `FluentValidation` (validators) |
| `SiliconHorizon.Domain` | Entity EF, enum nghiệp vụ | (không có) |
| `SiliconHorizon.Infrastructure` | EF Core, Dapper, SQL, bảo mật | `Microsoft.EntityFrameworkCore.SqlServer`, `Dapper` |

### 3.2. Tầng Api — Controller theo 15 module

```
Controllers/
├─ Auth/          AuthController                 (login, register, google, refresh, /api/me)
├─ Catalog/       ProductsController, CatalogReferenceController, CompatibilityController
├─ Cart/          CartController
├─ Orders/        OrdersController, StaffOrdersController, PosOrdersController, AdminOrdersController
├─ Payments/      PaymentsController, CustomerLedgerController
├─ Inventory/     StockController, PurchaseReceiptsController, StockTransfersController,
│                 StockTakesController, SerialsController, SuppliersController, SupplierReturnsController
├─ Warranty/      WarrantyTicketsController, WarrantyLookupController
├─ Returns/       ReturnsController
├─ Pricing/       VouchersController
├─ PcBuilds/      PcBuildsController
├─ Hr/            SchedulesController, AttendanceController
├─ Commission/    CommissionsController, AdminCommissionsController, CommissionPeriodsController
├─ Audit/         AuditLogsController
├─ Reports/       ReportsController
├─ Customers/     CustomersController, MyAddressesController
└─ Admin/         AdminProductsController, CategoriesController, BrandsController, BranchesController,
                  EmployeesController, RolesController, PermissionsController, UserRolesController,
                  ShiftsController, PromotionsController, VouchersAdminController, PriceTiersController,
                  CustomerGroupsController, CommissionRulesController, CompatibilityRulesController,
                  SettingsController, SupplierProductsController
```

**Đặc điểm controller:** mỏng; chỉ (1) đọc `ICurrentUser` để ép `customerId`/`branchId`/`employeeId`
từ token, (2) gọi service ở `Application`, (3) trả DTO. **Không** chứa truy vấn SQL.

### 3.3. Middleware & cross-cutting

| Thành phần | File | Chức năng |
|---|---|---|
| Correlation ID | `Middleware/CorrelationIdMiddleware.cs` | Gắn/truyền `X-Correlation-Id` mỗi request |
| Request logging | `Middleware/RequestLoggingMiddleware.cs` | Ghi log request/response có cấu trúc |
| Swagger version | `Middleware/SwaggerDocumentVersionMiddleware.cs` | Phục vụ tài liệu OpenAPI |
| Exception handler | `Exceptions/ApiExceptionHandler.cs` | Ánh xạ `AppException` → HTTP + `ProblemDetails` |
| Phân quyền | `Security/PermissionAuthorizationHandler.cs`, `PermissionRequirement.cs` | Policy dựa trên mã quyền |
| CORS | `Constants/CorsPolicyNames.cs` | Đọc `Cors:AllowedOrigins` |

### 3.4. Tầng Application

```
Application/
├─ Abstractions/
│   ├─ Data/        ISqlConnectionFactory, ISqlExecutor
│   ├─ Persistence/ IRepository, IUnitOfWork
│   ├─ Security/    ICurrentUser, IJwtTokenService, IPasswordHasher,
│   │               IGoogleTokenValidator, AuthClaimTypes
│   └─ Time/        IDateTimeProvider
├─ Common/
│   ├─ Audit/       IAuditLogWriter, AuditLogWriter
│   ├─ Errors/      SqlErrorCode  (50001..50006, 1205)
│   ├─ Exceptions/  AppException + NotFound/Conflict/BadRequest/BusinessRule/InsufficientStock
│   ├─ Models/      PagedResult<T>
│   ├─ Persistence/ PagedListBuilder
│   ├─ Sql/         StoredProcedureNames  (hằng số tên proc)
│   └─ Validation/  InputGuard
└─ Features/        <Module>/ (Service + I<Module>Service + Dto/ + Validators/ + <Module>Module.cs)
```

Mỗi module trong `Features/` theo cấu trúc thống nhất:
`<Module>Service.cs` (use case) · `I<Module>Service.cs` · `Dto/*.cs` · `Validators/*.cs` ·
`<Module>Module.cs` (đăng ký DI) · `*Roles.cs` (nhóm quyền) · `*AccessGuard.cs` (kiểm tra phạm vi).

### 3.5. Tầng Domain

- `Entities/` — entity EF Core: `Common/Auth`, `Catalog`, `Pricing`, `Customers`, `Hr`,
  `Inventory/Supplier`, `Commission`. Xem `DATABASE.md` mục 4.
- `Enums/` — `CatalogEnums`, `SalesEnums`, `ServiceEnums`, `InventoryEnums`, `HrEnums`, `CommissionEnums`.
- `Common/IAuditableEntity.cs` — đánh dấu entity có audit (Created/Updated).

### 3.6. Tầng Infrastructure

```
Infrastructure/
├─ Persistence/   AppDbContext, AppDbContextFactory, Configurations/*, Repositories/EfRepository
├─ Sql/           SqlConnectionFactory, StoredProcedureNames, SqlErrorTranslator
├─ Security/      JwtTokenService, Pbkdf2PasswordHasher, GoogleIdTokenValidator
└─ DependencyInjection.cs   services.AddInfrastructure(configuration)
```

### 3.7. Xác thực & phân quyền

- **Đăng nhập**: `POST /api/auth/login`, `POST /api/auth/register` (chỉ Customer),
  `POST /api/auth/google` (xác minh Google ID token qua `IGoogleTokenValidator`),
  `POST /api/auth/refresh`, `GET /api/me`.
- **Mật khẩu**: băm **PBKDF2-HMAC-SHA256, 210.000 vòng**, định dạng `pbkdf2-sha256$<iter>$<salt>$<hash>`.
- **JWT claims**: `userId`, `roles`, `customerId` (khách), `employeeId` + `branchId` (nhân viên).
  `Api/Security/CurrentUser.cs` đọc các claim này.
- **Phạm vi**: nhân viên chỉ thao tác trong chi nhánh của mình (API ép `branchId` từ token);
  khách chỉ xem dữ liệu của mình (ép `customerId`). **Không tin giá trị client gửi lên.**
- **Nhóm quyền nghiệp vụ**: `Api/Constants/OrderRoleGroups.cs` (ví dụ `SalesCounter`, `LedgerViewers`)
  và các `*Roles.cs` trong từng module (`InventoryRoles`, `WarrantyRoles`, `ReturnsRoles`,
  `ReportRoles`, `HrRoles`, `CommissionRoles`, `AuditRoles`).

### 3.8. Ánh xạ lỗi: stored procedure → HTTP

| Mã SQL | Ý nghĩa | HTTP | Exception Application |
|---|---|---|---|
| 50001 | Không tìm thấy | 404 | `NotFoundException` |
| 50002 | Sai trạng thái | 409 | `ConflictException` |
| 50003 | Không đủ tồn kho | 409 | `InsufficientStockException` |
| 50004 | Dữ liệu không hợp lệ | 400 | `BadRequestException` |
| 50005 | Trùng dữ liệu | 409 | `ConflictException` |
| 50006 | Vượt hạn mức / quá hạn | 422 | `BusinessRuleException` |
| 1205 | Deadlock | 503 | `AppException` (khuyến nghị retry 2–3 lần) |
| Khác | Lỗi hệ thống | 500 | (ghi log, không lộ chi tiết) |

Thông điệp tiếng Việt của proc được trả thẳng cho người dùng (BRD cho phép).
Xem `Infrastructure/Sql/SqlErrorTranslator.cs` và `Application/Common/Errors/SqlErrorCode.cs`.

### 3.9. Lưu trữ ảnh sản phẩm

- `Api/Storage/ProductImageStorage.cs` cài đặt `IProductImageStorage` (Application).
- Ảnh lưu trong `Api/wwwroot/images/products/`, phục vụ qua static files.
- `POST /api/admin/products/images` upload → trả URL đầy đủ để ghi vào `ProductImages.Url`.
- Cấu hình: `Storage:ProductImagesFolder`, `Storage:PublicBaseUrl` (appsettings).

---

## 4. FRONTEND (Angular 22)

### 4.1. Công nghệ & cấu trúc thư mục

| Thành phần | Lựa chọn |
|---|---|
| Framework | Angular 22 (standalone components, `loadComponent`/`loadChildren`) |
| State | Angular Signals (`AuthStore`, `CartStore`, `BranchStore`) |
| Styling | TailwindCSS 4 (PostCSS) + SCSS toàn cục |
| HTTP | `provideHttpClient` + functional interceptors |
| Build | `@angular/build` (esbuild) |

```
frontend/src/
├─ app/
│   ├─ app.config.ts / app.routes.ts     (cấu hình + bảng route gốc)
│   ├─ core/
│   │   ├─ auth/       auth.guard, auth.store, guest.guard, permission.guard,
│   │   │              role.guard, staff.guard
│   │   ├─ http/       api.service, auth.interceptor, error.interceptor, http-context
│   │   ├─ models/     auth, branch, cart, category, common, order, paged-result,
│   │   │              pc-build, product, spec, status-classes, voucher, api-error
│   │   └─ services/   branch.store, cart.store, confirm.service, loading.service, notification.service
│   ├─ features/       admin, audit, auth, cart, catalog, commission, dashboard, errors, hr,
│   │                  inventory, landing, orders, payments, pc-builds, reports, returns,
│   │                  staff-orders, suppliers, warranty
│   ├─ layout/         auth-layout, main-layout, public-layout, nav-menu
│   └─ shared/         components/ (confirm-dialog, empty-state, pagination, spec-fields,
                        spinner, toast-host) · directives/ · validators/
├─ environments/       environment.ts (apiBaseUrl, googleClientId)
└─ public/             images/ (products, builds), icons
```

### 4.2. Routing & 3 khung giao diện

`app.routes.ts` tách 3 khung theo vai trò (xem `BAO_CAO_TIEN_DO.md` để có danh sách màn hình):

| Khung | Layout | Bảo vệ | Phạm vi |
|---|---|---|---|
| Khách hàng | `PublicLayout` | (không) | `/`, `/shop`, `/cart`, `/catalog`, `/orders`, `/about`, `/development` |
| Xác thực | `AuthLayout` | `guestGuard` | `/login`, `/register` |
| Nội bộ | `MainLayout` | `staffGuard` | `/dashboard`, `/admin`, `/inventory`, `/suppliers`, `/warranty`, `/returns`, `/payments`, `/staff-orders`, `/hr`, `/commission`, `/audit`, `/reports` |

Mỗi feature có file `*.routes.ts` riêng, nạp lazy. Trang lỗi `/403`, `/404` + wildcard `**`.

### 4.3. Auth store & guards

- `AuthStore` (signals): giữ phiên đăng nhập, `homePath` (điều hướng theo vai trò), `canAccess(roles, permissions)`.
- Guards: `authGuard` (đăng nhập) · `guestGuard` (chặn khi đã đăng nhập) · `staffGuard` (khu vực nội bộ) ·
  `roleGuard` (`data.roles`) · `permissionGuard` (`data.permissions`) · `pendingChangesGuard` (cảnh báo rời form).
- `data.accessMode`: `roleOrPermission` (mặc định) hoặc `roles` (siết đúng vai trò, dùng cho Audit/Debt/Ledger).
- Menu sidebar (`layout/nav-menu.ts`) lọc qua `AuthStore.canAccess` — khớp logic guard.

### 4.4. HTTP & interceptors

- `core/http/api.service.ts` — wrapper gọi API (base URL từ `environment.apiBaseUrl`).
- `auth.interceptor.ts` — gắn `Authorization: Bearer <token>` và header giỏ khách vãng lai `X-Cart-Session`.
- `error.interceptor.ts` — chuẩn hoá lỗi về `ApiError` + thông báo.
- `http-context.ts` — cờ tuỳ chọn cho request (ví dụ bỏ qua spinner/loading).
- Dev dùng `proxy.conf.json` để chuyển `/api` sang backend `http://localhost:5270`.

### 4.5. Quy ước feature (FE)

Mỗi feature có: `*-api.service.ts` (gọi HTTP), `*.model.ts` (DTO + nhãn tiếng Việt),
`*.routes.ts`, và các component màn hình (mỗi màn 1 thư mục `.ts` + `.html`).
DTO ở FE khớp 1-1 với DTO backend.

---

## 5. CHIẾN LƯỢC TRUY CẬP DỮ LIỆU (EF Core vs Dapper)

| Loại bảng | Công nghệ | Số bảng | Lý do |
|---|---|---|---|
| Danh mục / cấu hình (Users, Roles, Products, Categories, Promotions, Customers, Employees, ...) | **EF Core** | 28 | CRUD đơn giản, không nghiệp vụ phức tạp |
| Giao dịch (Orders, Cart, Stock*, Serial*, Warranty*, Return*, AuditLogs, Specs_*, PcBuilds) | **Dapper + stored procedure** | 38 | Proc tự quản lý giao dịch, khoá, chống bán vượt tồn (NFR-01) |

- `AppDbContext` **không** `EnsureCreated`/`Migrate` khi khởi động — schema do các file SQL trong `Database/` tạo.
- Không bọc `TransactionScope` ngoài proc (proc đã tự quản lý transaction).
- `ISqlConnectionFactory` mở kết nối; `SetAuditContextAsync` truyền IP/User-Agent cho nhật ký trước khi gọi proc.

---

## 6. CROSS-CUTTING

| Chủ đề | Cách làm |
|---|---|
| **Audit log** | `IAuditLogWriter` + proc `usp_Audit_Write`; bảng `AuditLogs` append-only. Mọi thao tác "A" (duyệt) bắt buộc ghi log |
| **Logging** | `RequestLoggingMiddleware` + `CorrelationIdMiddleware`; log level cấu hình trong `appsettings.json` |
| **Validation** | FluentValidation ở `Features/*/Validators`, `InputGuard` kiểm tra đầu vào |
| **Phân trang** | `PagedResult<T>` + `PagedListBuilder`; proc danh sách trả cột `TotalCount` |
| **Thời gian** | `IDateTimeProvider` (UTC lưu trữ, hiển thị theo múi giờ VN) |
| **Tiền tệ** | VND số nguyên (`decimal(18,0)`), không có phần thập phân |
| **Đa chi nhánh** | Mọi thực thể nghiệp vụ gắn `BranchId`; API ép từ token |

---

## 7. QUY ƯỚC CODE

1. **Thêm module mới**:
   1. `Application/Features/<Module>/` — DTO + service (Dapper cho proc / EF cho CRUD).
   2. `Application/Common/Sql/StoredProcedureNames.cs` — thêm tên proc.
   3. `Api/Controllers/<Module>/` — controller mỏng, ép `customerId`/`branchId` từ `ICurrentUser`.
   4. Ghi nhật ký bằng `usp_Audit_Write` cho thao tác nhạy cảm.
   5. FE: thêm feature + `*.routes.ts` + khai báo menu trong `nav-menu.ts`.
2. Tên bảng/cột: `PascalCase`; proc: `usp_<Module>_<Action>`; hàm: `fn_<...>`.
3. Comment/quy ước nghiệp vụ ghi bằng tiếng Việt, tham chiếu mã yêu cầu BRD.

---

## 8. MÔI TRƯỜNG & LỆNH CHẠY

```powershell
# Backend
dotnet build SiliconHorizon.sln
dotnet run --project src/SiliconHorizon.Api      # Swagger: http://localhost:5270/swagger
curl http://localhost:5270/api/v1/health/db      # kiểm tra kết nối DB

# Frontend
cd frontend
npm install
npm start                                        # http://localhost:4200 (proxy /api -> :5270)
npm run build
```

**Chuỗi kết nối** khai báo tại `src/SiliconHorizon.Api/appsettings.json` → `ConnectionStrings:DefaultConnection`
(dev hiện tại dùng `BRD_DEV`). Ưu tiên biến môi trường `ConnectionStrings__DefaultConnection` thay vì commit mật khẩu thật.

**Cấu hình khác trong appsettings.json:** `Jwt` (Issuer/Audience/SigningKey/AccessTokenMinutes/RefreshTokenDays),
`Storage` (thư mục ảnh), `Google:ClientId`, `Swagger:OpenApiVersion`, `Cors:AllowedOrigins`.

---

## 9. VIỆC CÒN LẠI / ROADMAP KỸ THUẬT

| # | Việc | Ghi chú |
|---|---|---|
| 1 | Bộ test tích hợp các luồng trọng yếu (đặt đồng thời món cuối, hủy/hoàn tất đơn, đổi trả, bảo hành) | chưa có project test |
| 2 | Tác vụ nền: hủy đơn quá hạn giữ hàng, cảnh báo tồn tối thiểu (NFR-09) | chưa có `BackgroundService` |
| 3 | UI quản trị còn thiếu (nhân viên, vai trò/quyền, khuyến mãi/voucher, cấu hình, quy tắc, ca) | backend đã có controller |
| 4 | API quản trị PC build | hiện quản lý bằng script SQL |
| 5 | Bổ sung enum còn thiếu: `SerialItems.Status`, `WarrantyTickets.Status` | |
| 6 | Viết `CREATE INDEX` cho truy vấn nóng; cân nhắc Full-Text Search thay `LIKE` | khi dữ liệu lớn |

---

## 10. LỊCH SỬ CẬP NHẬT

| Ngày | Thay đổi |
|---|---|
| 26/11/2026 | Tạo bản kiến trúc đầy đủ: bổ sung xác thực JWT + Google, PC Builds, upload ảnh, middleware, chiến lược EF/Dapper, quy ước FE |




