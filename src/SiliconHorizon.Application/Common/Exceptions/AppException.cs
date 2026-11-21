namespace SiliconHorizon.Application.Common.Exceptions;

/// <summary>
/// Lỗi nghiệp vụ. Thông điệp tiếng Việt của các lỗi 5000x từ stored procedure
/// (Database/README.md mục 1.2) có thể trả thẳng cho người dùng.
/// </summary>
public class AppException : Exception
{
    public AppException(string message, int statusCode = 400, string? code = null)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    /// <summary>Mã HTTP tương ứng (404, 409, 400, 422, 500...).</summary>
    public int StatusCode { get; }

    /// <summary>Mã nghiệp vụ tuỳ chọn để client xử lý (ví dụ SQL-50003).</summary>
    public string? Code { get; }
}

/// <summary>Không tìm thấy dữ liệu (SQL 50001).</summary>
public sealed class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message, 404, "SQL-50001") { }
}

/// <summary>
/// Chưa xác thực / sai thông tin đăng nhập. Thông điệp phải chung chung để không lộ tài khoản có tồn tại hay không.
/// </summary>
public sealed class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "Tên đăng nhập hoặc mật khẩu không đúng.")
        : base(message, 401, "AUTH-INVALID-CREDENTIALS") { }

    public UnauthorizedException(string message, string code) : base(message, 401, code) { }
}

/// <summary>
/// Đã xác thực nhưng không đủ quyền (ví dụ khách truy cập dữ liệu của khách khác,
/// hoặc nhân viên thao tác ngoài chi nhánh được gán — BRD 2.2).
/// </summary>
public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message = "Bạn không có quyền thực hiện thao tác này.")
        : base(message, 403, "AUTH-FORBIDDEN") { }
}

/// <summary>Sai trạng thái / chuyển trạng thái không hợp lệ (SQL 50002) hoặc trùng dữ liệu (SQL 50005).</summary>
public sealed class ConflictException : AppException
{
    public ConflictException(string message, string? code = "SQL-50002") : base(message, 409, code) { }
}

/// <summary>Không đủ tồn kho (SQL 50003) — luôn 409 để client có thể thử lại với số lượng khác.</summary>
public sealed class InsufficientStockException : AppException
{
    public InsufficientStockException(string message) : base(message, 409, "SQL-50003") { }
}

/// <summary>Dữ liệu không hợp lệ (SQL 50004).</summary>
public sealed class BadRequestException : AppException
{
    public BadRequestException(string message) : base(message, 400, "SQL-50004") { }
}

/// <summary>Vượt hạn mức, quá hạn đổi trả, hết lượt voucher (SQL 50006).</summary>
public sealed class BusinessRuleException : AppException
{
    public BusinessRuleException(string message) : base(message, 422, "SQL-50006") { }
}
