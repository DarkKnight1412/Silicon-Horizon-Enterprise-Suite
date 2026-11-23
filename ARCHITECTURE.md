# KIẾN TRÚC SOLUTION — Apex Circuit Electronics Enterprise Suite

Tài liệu nghiệp vụ: `README.md` (BRD) và `DATABASE_ERD.md`. Tài liệu API + stored procedure: `Database/README.md`.

## 1. Các tầng và phụ thuộc

```
SiliconHorizon.Api            (ASP.NET Core Web API, .NET 8, Swagger)
   │  Phụ thuộc: Application + Infrastructure
   ├─ Controllers/            Controller theo module (Admin/, Catalog/, ...)
   ├─ Middleware/             ApiExceptionHandlingMiddleware (ánh xạ lỗi → HTTP)
   └─ Security/CurrentUser.cs ICurrentUser đọc từ JWT claim
        │
SiliconHorizon.Infrastructure (EF Core 8 + SQL Server, Dapper)
   │  Phụ thuộc: Application + Domain
   ├─ Persistence/            AppDbContext, Configurations, Repositories, AppDbContextFactory
   ├─ Sql/                    SqlConnectionFactory, StoredProcedureNames, SqlErrorTranslator
   └─ DependencyInjection.cs  services.AddInfrastructure(configuration)
        │
SiliconHorizon.Application    (use case/DTO/interface — KHÔNG tham chiếu EF Core/Dapper/ASP.NET)
   ├─ Abstractions/           Persistence (IRepository/IUnitOfWork), Security, Time, Data
   ├─ Common/                 PagedResult, AppException, SqlErrorCode
   └─ Features/               thư mục theo 14 nhóm API (Auth, Catalog, Cart, Orders, ...)
        │
SiliconHorizon.Domain         (entity + enum thuần, không package ngoài)
   ├─ Common/IAuditableEntity.cs
   ├─ Enums/                  mã hoá theo comment trong table_and_data.sql
   └─ Entities/               xem Entities/README.md (28/64 bảng ánh xạ EF)
```

Quy tắc phụ thuộc: **Api → Application/Infrastructure → Domain**. Không có chiều ngược lại.

## 2. Quyết định dữ liệu: EF Core hay Dapper?

| Loại | Công nghệ | Lý do |
|---|---|---|
| Bảng danh mục/cấu hình (28 bảng: Users, Roles, Products, Categories, Promotions, Customers, Employees, ...) | **EF Core** | CRUD đơn giản, không có nghiệp vụ phức tạp (mục 10 của `Database/README.md`) |
| Bảng giao dịch (36 bảng: Orders, Cart, Stock*, Serial*, Warranty*, Return*, AuditLogs, Specs_*) | **Dapper + stored procedure** | 90 proc đã tự quản lý giao dịch, khoá, chống bán vượt tồn (NFR-01) — không bọc `TransactionScope` bên ngoài |

`AppDbContext` **không** tự `EnsureCreated`/`Migrate` khi khởi động: schema thật do 6 file SQL trong `Database/` tạo.

## 3. Chuỗi kết nối

Khai báo tại `src/SiliconHorizon.Api/appsettings.json` → `ConnectionStrings:DefaultConnection`.

| Môi trường | Chuỗi kết nối |
|---|---|
| LocalDB (mặc định, không cần cài SQL Server) | `Server=(localdb)\MSSQLLocalDB;Database=SiliconHorizon;Trusted_Connection=True;MultipleActiveResultSets=True;TrustServerCertificate=True` |
| SQL Server/Express tại máy | `Server=localhost\SQLEXPRESS;Database=SiliconHorizon;Trusted_Connection=True;TrustServerCertificate=True` |
| Tài khoản SQL | `Server=localhost;Database=SiliconHorizon;User Id=sa;Password=<mật khẩu>;TrustServerCertificate=True` |

Ưu tiên bảo mật: đặt `ConnectionStrings__DefaultConnection` trong biến môi trường hoặc user-secrets thay vì commit mật khẩu thật.

## 4. Lệnh thường dùng

```powershell
# Build
dotnet build SiliconHorizon.sln

# Chạy API (Swagger tại http://localhost:5270/swagger)
dotnet run --project src/SiliconHorizon.Api

# Kiểm tra kết nối database
curl http://localhost:5270/api/v1/health/db
```

Nếu muốn dùng EF để sinh/kiểm tra model (cần cài tool một lần):

```powershell
dotnet tool install --global dotnet-ef --version 8.0.34

# Xem thông tin DbContext + model có dựng được không
dotnet ef dbcontext info --project src/SiliconHorizon.Infrastructure --startup-project src/SiliconHorizon.Api

# Tạo migration (chỉ dùng khi muốn EF quản lý schema; hiện schema do file SQL quản lý)
dotnet ef migrations add <Ten> --project src/SiliconHorizon.Infrastructure --startup-project src/SiliconHorizon.Api

# Sinh entity từ database đã chạy 6 file SQL (database-first)
dotnet ef dbcontext scaffold "<connection-string>" Microsoft.EntityFrameworkCore.SqlServer `
  --project src/SiliconHorizon.Infrastructure --startup-project src/SiliconHorizon.Api -o Persistence/Scaffolded
```

## 5. Ánh xạ lỗi: stored procedure → HTTP

| Mã SQL | Ý nghĩa | HTTP | Exception |
|---|---|---|---|
| 50001 | Không tìm thấy | 404 | `NotFoundException` |
| 50002 | Sai trạng thái | 409 | `ConflictException` |
| 50003 | Không đủ tồn kho | 409 | `InsufficientStockException` |
| 50004 | Dữ liệu không hợp lệ | 400 | `BadRequestException` |
| 50005 | Trùng dữ liệu | 409 | `ConflictException` |
| 50006 | Vượt hạn mức / quá hạn đổi trả | 422 | `BusinessRuleException` |
| 1205 | Deadlock | 503 | `AppException` (TODO: retry 2–3 lần) |

Thông điệp tiếng Việt của proc được trả thẳng cho người dùng (BRD cho phép).

## 6. Thêm một module mới (quy trình đề xuất)

1. `Application/Features/<Module>/` — DTO request/response + service gọi `ISqlConnectionFactory` (Dapper) cho proc, hoặc repository EF cho CRUD.
2. `Infrastructure/Sql/StoredProcedureNames.cs` — thêm tên proc của module.
3. `Api/Controllers/<Module>/` — controller mỏng: lấy `ICurrentUser` ép `customerId`/`branchId`, gọi service, trả DTO.
4. Ghi nhật ký: gọi `usp_Audit_Write` cho thao tác nhạy cảm; truyền IP/User-Agent bằng `SqlSessionContextExtensions.SetAuditContextAsync`.

## 7. Việc còn lại

| # | Việc | Ghi chú |
|---|---|---|
| 1 | Bật xác thực JWT (JwtBearer) + phân quyền theo ma trận RBAC BRD 2.2 | `CurrentUser` đã đọc sẵn claim `userId`, `roles`, `customerId`, `employeeId`, `branchId` |
| 2 | Bộ test tích hợp cho các luồng: đặt đồng thời món cuối, hủy đơn, hoàn tất đơn, đổi trả, bảo hành | theo khuyến nghị mục 12 của `Database/README.md` |
| 3 | Tác vụ nền: hủy đơn quá hạn giữ hàng, cảnh báo tồn tối thiểu (NFR-09, UC-33/UC-34) | |
| 4 | Bổ sung enum còn thiếu số hoá: `SerialItems.Status`, `WarrantyTickets.Status` | cần đọc lại định nghĩa bảng tương ứng |
| 5 | Dọn file sinh ra khi chạy: `_build.log` (nếu còn) | không commit |
