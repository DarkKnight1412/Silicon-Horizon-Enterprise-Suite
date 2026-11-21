# MIDDLEWARE VÀ EXCEPTION HANDLING

Chốt **thứ tự pipeline middleware** và **cách xử lý lỗi thống nhất** cho toàn hệ thống.

## 1. Thứ tự pipeline (bắt buộc giữ nguyên trong `Program.cs`)

| # | Middleware | Vì sao ở vị trí này |
|---|---|---|
| 1 | `CorrelationIdMiddleware` (tự viết) | Đặt trước tiên để **mọi** log/lỗi phía sau đều có `correlationId`; đọc `X-Correlation-Id` của client, nếu không có thì dùng W3C `TraceId` |
| 2 | `UseExceptionHandler()` | Bao trọn mọi middleware phía sau → không lỗi nào thoát ra ngoài; dùng `IExceptionHandler` thay vì middleware tự viết |
| 3 | `UseStatusCodePages()` | Bổ sung body ProblemDetails cho các lỗi **không sinh exception**: 404, 401, 403, 405 |
| 4 | `RequestLoggingMiddleware` (tự viết) | Log sau khi response hoàn tất (`Response.OnCompleted`) nên `StatusCode` luôn là giá trị cuối cùng; nằm trong exception handler nên request lỗi vẫn được log |
| 5 | `UseHsts` + `UseHttpsRedirection` | Chỉ bật ngoài Development — bật ở local sẽ gây vòng lặp chuyển hướng |
| 6 | `UseCors(CustomerWeb)` | Phải **trước** authentication/authorization (preflight `OPTIONS` không có token) và trước endpoints |
| 7 | `UseAuthentication` → `UseAuthorization` | Bật cùng lúc với JWT; authorization luôn sau authentication. Quyền theo token + RBAC BRD 2.2 |
| 8 | `UseSwagger` / `UseSwaggerUI` | Chỉ ở Development — không lộ API doc ở Production |
| 9 | `MapControllers()` + `MapGet` health | Điểm cuối pipeline; `WebApplication` tự gắn `UseRouting` phía trước |

**Luật:** không thêm middleware mới vào giữa khối 1–4 mà không cập nhật tài liệu này; mọi middleware tự viết phải **chịu được exception** (không `try/catch` rồi `throw ex` làm mất stack trace).

## 2. Exception handling — thành phần

| Thành phần | File | Vai trò |
|---|---|---|
| `ApiExceptionHandler : IExceptionHandler` | `Api/Exceptions/ApiExceptionHandler.cs` | Dịch exception → mã HTTP + thông điệp; ghi log theo mức phù hợp |
| `AddExceptionHandler<ApiExceptionHandler>()` · `AddProblemDetails(...)` | `Api/DependencyInjection.cs` | Đăng ký handler + bổ sung `instance`, `traceId`, `correlationId` vào mọi lỗi |
| `SqlErrorTranslator` | `Infrastructure/Sql/SqlErrorTranslator.cs` | Dịch lỗi 5000x của stored procedure → `AppException` |
| `AppException` + lớp con | `Application/Common/Exceptions/AppException.cs` | Kiểu lỗi nghiệp vụ dùng xuyên tầng |
| `ApiExceptionHandlingMiddleware` | `Api/Middleware/` | **ĐÃ THAY THẾ** bởi handler trên (xóa được) |

## 3. Bảng ánh xạ lỗi

| Nguồn lỗi | Điều kiện | HTTP | `detail` | Log level |
|---|---|---|---|---|
| `AppException` | bất kỳ | theo `StatusCode` của exception | `Code` | Error nếu ≥ 500, còn lại Warning |
| `SqlException` 50001 | không tìm thấy | 404 | `SQL-50001` | Warning |
| `SqlException` 50002 | sai trạng thái | 409 | `SQL-50002` | Warning |
| `SqlException` 50003 | không đủ tồn kho | 409 | `SQL-50003` | Warning |
| `SqlException` 50004 | dữ liệu không hợp lệ | 400 | `SQL-50004` | Warning |
| `SqlException` 50005 | trùng dữ liệu | 409 | `SQL-50005` | Warning |
| `SqlException` 50006 | vượt hạn mức / quá hạn đổi trả | 422 | `SQL-50006` | Warning |
| `SqlException` 1205 | deadlock | 503 | `SQL-1205` | Error |
| `SqlException` khác | lỗi cấu hình/kết nối/hệ thống | 500 | `SQL-<number>` | Error + stack trace trong log |
| `OperationCanceledException` (client hủy) | `RequestAborted` | 499 | `CLIENT-CLOSED` | Debug |
| Exception khác | — | 500 | null | Error + stack trace trong log |

## 4. Hợp đồng lỗi trả cho client (ProblemDetails)

```json
{
  "type": "https://httpstatuses.com/409",
  "title": "Không đủ tồn kho cho SKU RAM-KING-16G",
  "status": 409,
  "detail": "SQL-50003",
  "instance": "/api/v1/orders",
  "traceId": "00-8f3c...-01",
  "correlationId": "0HN7..."
}
```

**Luật:**
1. `title` = thông điệp tiếng Việt **do stored procedure hoặc use case** đưa ra → trả thẳng cho người dùng (được BRD cho phép).
2. Lỗi hệ thống **không lộ chi tiết kỹ thuật**: luôn là `"Lỗi hệ thống, vui lòng liên hệ quản trị viên."`, chi tiết chỉ nằm trong log.
3. Mọi lỗi **phải** có `correlationId` + `traceId` để tra log (NFR-05).
4. Response header luôn có `X-Correlation-Id` đi kèm.

## 5. Log vận hành

- Một dòng log cho mỗi request: `HTTP {Method} {Path} => {StatusCode} ({ElapsedMs} ms, user=..., correlationId=...)`.
- Bỏ qua `/api/v1/health`, `/swagger` để không nhiễu log (xem `RequestLoggingMiddleware.IgnoredPathPrefixes`).
- Lỗi có stack trace chỉ ở mức `Error`; lỗi nghiệp vụ chỉ ghi `Warning` để dễ phân biệt sự cố thật.
- Nhật ký **nghiệp vụ** (ai sửa gì, giá trị cũ/mới) là việc của `usp_Audit_Write` → bảng `AuditLogs`, **không** thay thế bằng log ứng dụng.

## 6. Việc còn lại (đã xác định, chưa làm)

| # | Việc | Ghi chú |
|---|---|---|
| 1 | Retry 2–3 lần khi deadlock (SQL 1205) | Có thể dùng `EnableRetryOnFailure` cho EF; với Dapper cần policy bọc `ISqlExecutor` (Polly hoặc vòng lặp thủ công) |
| 2 | Rate limiting | `AddRateLimiter` + policy cho `/api/auth/login` (NFR-03) — chống dò mật khẩu; đặt trước authentication |
| 3 | ProblemDetails cho lỗi validate (400) | Thêm extension `errors` (dictionary field → message) khi có validator |
| 4 | `UseHttpLogging` cho môi trường debug | Chỉ dùng ở Development để xem header/body |
| 5 | Health check nâng cao | `AddHealthChecks().AddDbContextCheck<AppDbContext>()` + `/health/ready`, `/health/live` |
| 6 | ~~Xóa `ApiExceptionHandlingMiddleware`~~ | **Đã xóa** — thay bằng `ApiExceptionHandler` |

## 7. Ghi nhận từ lần verify đầu tiên (đã chạy thật)

| # | Phát hiện | Xử lý |
|---|---|---|
| 1 | Body ProblemDetails **rỗng** khi client gửi `Accept: */*` (PowerShell `Invoke-WebRequest`) | Đúng chuẩn content negotiation của `ProblemDetailsService`: client phải nhận JSON (`Accept: application/json` / `application/problem+json`) mới có body. Khi test API lỗi nên dùng `curl.exe -i -H "Accept: application/json"` (PowerShell 5.1 còn không hiển thị body của response lỗi) |
| 2 | Lỗi SQL kèm `EnableRetryOnFailure` bị bọc trong `RetryLimitExceededException` → **không dịch được mã 5000x** (thiếu `detail`) | `ApiExceptionHandler.Map` **mở dần inner exception** để tìm `AppException`/`SqlException`. Đã verify: 500 trả `"detail":"SQL-4060"` |
| 3 | Truy vấn tới database không tồn tại mất ~5,8 giây (retry 3 lần ⇒ 4 lần thử) | Chấp nhận được với lỗi cấu hình; muốn phản hồi nhanh hơn khi sai connection string thì giảm `EnableRetryOnFailure(3)` → `(2)` trong `Infrastructure/DependencyInjection.cs` |

**Raw response đã ghi lại được (dùng làm mẫu hợp đồng lỗi):**

```http
HTTP/1.1 404 Not Found
Content-Type: application/problem+json
X-Correlation-Id: dfacf69286402a7e4af71724051fc5d5

{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.5","title":"Not Found","status":404,
 "instance":"/api/v1/khong-ton-tai","traceId":"00-dfacf6...-daf886f4ade5dbae-00",
 "correlationId":"dfacf69286402a7e4af71724051fc5d5"}
```

```http
HTTP/1.1 500 Internal Server Error
Content-Type: application/problem+json
Cache-Control: no-cache,no-store
X-Correlation-Id: test-500d
{"type":"https://httpstatuses.com/500","title":"Lỗi hệ thống, vui lòng liên hệ quản trị viên.","status":500,
 "detail":"SQL-4060","instance":"/api/v1/admin/categories",
 "traceId":"00-6f8bdcb6a0327ca8fa725625f84c22bf-650268886497d839-00",
 "correlationId":"test-500d"}
```

Log tương ứng (một dòng/request, status cuối cùng chính xác):

```
HTTP GET /api/v1/admin/categories => 500 (5819 ms, user=anonymous, correlationId=test-500d)
HTTP GET /api/v1/khong-ton-tai    => 404 (4 ms,    user=anonymous, correlationId=dfacf6...)
```

