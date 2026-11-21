using System.Data;
using Dapper;
using SiliconHorizon.Application.Abstractions.Security;

namespace SiliconHorizon.Infrastructure.Sql;

/// <summary>
/// Truyền IP và User-Agent vào phiên SQL Server để các proc ghi nhật ký lấy được thiết bị (AUD-02):
/// <c>EXEC sys.sp_set_session_context @key, @value</c> — xem Database/README.md mục 1.3.
/// Gọi hàm này ngay sau khi mở kết nối và trước khi thực thi proc ghi dữ liệu.
/// </summary>
public static class SqlSessionContextExtensions
{
    public static async Task SetAuditContextAsync(
        this IDbConnection connection,
        ICurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(currentUser);

        await SetValueAsync(connection, "ip", currentUser.IpAddress, cancellationToken);
        await SetValueAsync(connection, "userAgent", currentUser.UserAgent, cancellationToken);

        if (currentUser.UserId is { } userId)
        {
            await SetValueAsync(connection, "userId", userId.ToString(), cancellationToken);
        }
    }

    private static async Task SetValueAsync(
        IDbConnection connection,
        string key,
        string? value,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "EXEC sys.sp_set_session_context @key = @Key, @value = @Value",
            new { Key = key, Value = value },
            cancellationToken: cancellationToken));
    }
}
