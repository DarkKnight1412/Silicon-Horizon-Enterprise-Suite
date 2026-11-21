using System.Data;

namespace SiliconHorizon.Application.Abstractions.Data;

/// <summary>
/// Mở kết nối SQL Server cho Dapper để gọi stored procedure.
/// Theo tài liệu: query dùng kết nối đọc, command dùng kết nối ghi; proc tự quản lý giao dịch.
/// </summary>
public interface ISqlConnectionFactory
{
    IDbConnection CreateConnection();

    Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}
