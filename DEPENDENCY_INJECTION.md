# XÁC ĐỊNH DEPENDENCY INJECTION

Tài liệu này chốt **cách đăng ký và vòng đời (lifetime)** của mọi service trong solution.

## 1. Composition root

Chỉ có **một** nơi gọi đăng ký: `Program.cs`

```csharp
builder.Host.UseDefaultServiceProvider(o => { o.ValidateScopes = true; o.ValidateOnBuild = ...; });
builder.Services.AddApiServices(builder.Configuration);   // SiliconHorizon.Api/DependencyInjection.cs
```

`AddApiServices` gọi theo thứ tự từ trong ra ngoài:

```
AddApiServices(configuration)          Api/DependencyInjection.cs          (composition root)
 ├─ AddApplication()                   Application/DependencyInjection.cs  (use case theo module)
 │   └─ AddCatalogModule()             Features/Catalog/CatalogModule.cs
 ├─ AddInfrastructure(configuration)   Infrastructure/DependencyInjection.cs (EF Core, Dapper, repository)
 └─ thành phần của Api: AddHttpContextAccessor, ICurrentUser, AddControllers, AddSwaggerGen
```

## 2. Bảng đăng ký & vòng đời (kết quả "xác định")

| # | Service (abstraction) | Implementation | Lifetime | Tầng | Lý do chọn |
|---|---|---|---|---|---|
| 1 | `AppDbContext` | `AppDbContext` | **Scoped** | Infrastructure | Một đơn vị công việc mỗi request; giữ change tracker |
| 2 | `IReadRepository<>` | `EfReadRepository<>` | **Scoped** | Infrastructure | Phụ thuộc `AppDbContext` |
| 3 | `IWriteRepository<>` | `EfWriteRepository<>` | **Scoped** | Infrastructure | Phụ thuộc `AppDbContext` |
| 4 | `IUnitOfWork` | `EfUnitOfWork` | **Scoped** | Infrastructure | Bọc `AppDbContext`, quản lý transaction |
| 5 | `ISqlConnectionFactory` | `SqlConnectionFactory` | **Singleton** | Infrastructure | Chỉ giữ chuỗi kết nối, không trạng thái; mỗi lần gọi mở connection mới |
| 6 | `ISqlExecutor` | `DapperSqlExecutor` | **Scoped** | Infrastructure | Phụ thuộc `ICurrentUser` (Scoped) để set IP/User-Agent cho `sp_set_session_context` |
| 7 | `IDateTimeProvider` | `SystemDateTimeProvider` | **Singleton** | Infrastructure | Không trạng thái, chỉ đọc đồng hồ + timezone |
| 8 | `ICurrentUser` | `CurrentUser` | **Scoped** | Api | Đọc `HttpContext` của request hiện tại |
| 9 | `IHttpContextAccessor` | (framework) | Singleton | Api | Do ASP.NET cung cấp sẵn |
| 10 | `ICategoryService` | `CategoryService` | **Scoped** | Application | Use case phụ thuộc repository/executor Scoped |
| 11 | Controllers | — | Transient | Api | MVC tự quản lý |
| 12 | `IValidator<T>` (nếu thêm FluentValidation) | — | **Scoped** | Application | Có thể phụ thuộc service Scoped |

## 3. Luật bắt buộc

1. **Scoped không được nằm trong Singleton.** `ValidateScopes = true` sẽ chặn ngay khi chạy.
2. **Không inject `AppDbContext` vào Hosted Service** (Singleton) — phải tạo scope: `IServiceScopeFactory.CreateScope()`.
3. **Không capture `ICurrentUser` vào Singleton** (giá trị sẽ bị "dính" của request đầu tiên).
4. Service của module **luôn đăng ký ở `Features/<Module>/<Module>Module.cs`**, không rải rác trong `Program.cs`.
5. Tầng Application **không** tham chiếu EF Core/Dapper/ASP.NET — chỉ qua interface (`IReadRepository`, `ISqlExecutor`, `ICurrentUser`, `IDateTimeProvider`).
6. Vòng đời mặc định cho use case mới: **Scoped**. Chỉ dùng Singleton khi service thực sự không trạng thái và không phụ thuộc Scoped.
7. `ISqlConnectionFactory` là Singleton nhưng **connection luôn được mở/đóng trong phạm vi một lời gọi** — không cache connection.

## 4. Quy ước thêm một module mới

```
1. Features/<Module>/ICatalogService.cs ...            (interface use case)
2. Features/<Module>/<Module>Service.cs                (implementation, chỉ phụ thuộc abstraction)
3. Features/<Module>/<Module>Module.cs                 (AddXxxModule() — đăng ký Scoped)
4. Application/DependencyInjection.cs                  (bỏ comment đúng 1 dòng: services.AddXxxModule();)
5. Api/Controllers/<Module>/...Controller.cs           (chỉ inject IXxxService)
```

## 5. Kiểm tra DI tự động

`Program.cs` bật:

```csharp
builder.Host.UseDefaultServiceProvider((context, options) =>
{
    options.ValidateScopes = true;                                  // mọi môi trường
    options.ValidateOnBuild = context.HostingEnvironment.IsDevelopment(); // dev: dựng thử toàn bộ service
});
```

→ Nếu quên đăng ký service, hoặc vòng đời sai, ứng dụng **không khởi động được** kèm thông báo chỉ rõ loại nào thiếu — thay vì lỗi lúc người dùng gọi API.

## 6. Sự cố thường gặp

| Thông báo | Nguyên nhân | Cách sửa |
|---|---|---|
| `Unable to resolve service for type 'X'` | Chưa đăng ký | Thêm vào `AddXxxModule()` hoặc `AddInfrastructure()` |
| `Cannot consume scoped service 'AppDbContext' from singleton 'Y'` | Singleton phụ thuộc Scoped | Đổi `Y` sang Scoped, hoặc dùng `IServiceScopeFactory` |
| `A circular dependency was detected` | A → B → A | Tách phần dùng chung sang service thứ ba |
| `A second operation was started on this context` | Dùng song song 2 luồng trên cùng `DbContext` | Chạy tuần tự, hoặc tạo scope riêng cho mỗi luồng |

## 7. Còn lại (đăng ký khi implement)

| Hạng mục | Cách làm dự kiến |
|---|---|
| `JwtOptions` (issuer, audience, key, thời hạn) | `services.Configure<JwtOptions>(configuration.GetSection("Jwt"))` + `AddAuthentication().AddJwtBearer(...)` |
| CORS cho web khách hàng | `AddCors` + policy đặt trong `AddApiServices` |
| Job nền (hủy đơn quá hạn, cảnh báo tồn tối thiểu — NFR-09) | `AddHostedService<...>` (Singleton) + tự tạo scope bên trong |
| Cache đọc (danh mục, bảng giá) | `AddMemoryCache` (Singleton) hoặc Redis; cache ở tầng Application qua interface |
| Rate limit / Health check nâng cao | `AddRateLimiter`, `AddHealthChecks().AddDbContextCheck<AppDbContext>()` |
