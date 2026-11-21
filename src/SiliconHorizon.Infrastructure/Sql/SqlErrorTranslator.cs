using Microsoft.Data.SqlClient;
using SiliconHorizon.Application.Common.Errors;
using SiliconHorizon.Application.Common.Exceptions;

namespace SiliconHorizon.Infrastructure.Sql;

/// <summary>
/// Chuyển lỗi từ stored procedure (mã 50001..50006 và 1205) thành <see cref="AppException"/>
/// để tầng API trả đúng mã HTTP (Database/README.md mục 1.2).
/// Thông điệp tiếng Việt do proc ném ra được giữ nguyên và trả thẳng cho người dùng.
/// </summary>
public static class SqlErrorTranslator
{
    /// <summary>Trả về null nếu không phải lỗi nghiệp vụ đã biết (lỗi hệ thống → 500).</summary>
    public static AppException? Translate(SqlException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.Number switch
        {
            SqlErrorCode.NotFound => new NotFoundException(exception.Message),
            SqlErrorCode.InvalidState => new ConflictException(exception.Message, "SQL-50002"),
            SqlErrorCode.InsufficientStock => new InsufficientStockException(exception.Message),
            SqlErrorCode.InvalidData => new BadRequestException(exception.Message),
            SqlErrorCode.Duplicate => new ConflictException(exception.Message, "SQL-50005"),
            SqlErrorCode.BusinessRuleViolation => new BusinessRuleException(exception.Message),
            SqlErrorCode.Deadlock => new AppException(
                "Hệ thống đang bận, vui lòng thử lại sau ít giây.", 503, "SQL-1205"),
            _ => null
        };
    }
}
