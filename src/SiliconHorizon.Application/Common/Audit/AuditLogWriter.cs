using System.Text.Json;
using SiliconHorizon.Application.Abstractions.Data;
using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Application.Common.Sql;

namespace SiliconHorizon.Application.Common.Audit;

/// <summary>
/// Hiện thực <see cref="IAuditLogWriter"/> qua <c>dbo.usp_Audit_Write</c>.
/// </summary>
/// <remarks>
/// Tham số truyền vào proc đúng như khai báo trong Database/02_sp_foundation_catalog_cart.sql:
/// <c>@UserId, @BranchId, @Action, @EntityType, @EntityId, @OldValues, @NewValues, @Reason, @IpAddress, @UserAgent</c>.
/// <para>
/// <b>Không</b> truyền <c>@IpAddress</c>/<c>@UserAgent</c> từ dữ liệu client: lấy từ <see cref="ICurrentUser"/>
/// (đọc header/IP của request hiện tại). Nếu để null, proc tự lấy từ <c>SESSION_CONTEXT</c> do
/// <c>DapperSqlExecutor</c> đã set trước mỗi lần gọi proc.
/// </para>
/// </remarks>
public sealed class AuditLogWriter : IAuditLogWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>usp_Audit_Write <c>@Action varchar(60)</c>.</summary>
    private const int ActionMaxLength = 60;

    /// <summary>usp_Audit_Write <c>@EntityType varchar(50)</c>.</summary>
    private const int EntityTypeMaxLength = 50;

    /// <summary>usp_Audit_Write <c>@Reason nvarchar(300)</c>.</summary>
    private const int ReasonMaxLength = 300;

    /// <summary>AuditLogs.IpAddress <c>varchar(45)</c> (đủ cho IPv6).</summary>
    private const int IpAddressMaxLength = 45;

    /// <summary>AuditLogs.UserAgent <c>nvarchar(300)</c>.</summary>
    private const int UserAgentMaxLength = 300;

    private readonly ISqlExecutor _sqlExecutor;
    private readonly ICurrentUser _currentUser;

    public AuditLogWriter(ISqlExecutor sqlExecutor, ICurrentUser currentUser)
    {
        _sqlExecutor = sqlExecutor;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public Task WriteAsync(
        string action,
        string entityType,
        long entityId,
        object? oldValues = null,
        object? newValues = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("Nhật ký phải có hành động (action).", nameof(action));
        }

        if (string.IsNullOrWhiteSpace(entityType))
        {
            throw new ArgumentException("Nhật ký phải có loại đối tượng (entityType).", nameof(entityType));
        }

        return _sqlExecutor.ExecuteAsync(
            StoredProcedureNames.AuditWrite,
            new
            {
                UserId = _currentUser.UserId,
                BranchId = _currentUser.BranchId,
                Action = Truncate(action.Trim(), ActionMaxLength),
                EntityType = Truncate(entityType.Trim(), EntityTypeMaxLength),
                EntityId = entityId,
                OldValues = Serialize(oldValues),
                NewValues = Serialize(newValues),
                Reason = Truncate(reason, ReasonMaxLength),
                IpAddress = Truncate(_currentUser.IpAddress, IpAddressMaxLength),
                UserAgent = Truncate(_currentUser.UserAgent, UserAgentMaxLength)
            },
            cancellationToken);
    }

    /// <summary>
    /// Chuỗi đơn giản được giữ nguyên; đối tượng phức tạp serialize JSON camelCase (quy ước mục 1.1
    /// của Database/README.md). <c>@OldValues</c>/<c>@NewValues</c> là <c>nvarchar(max)</c> nên không cắt.
    /// </summary>
    private static string? Serialize(object? value) => value switch
    {
        null => null,
        string text => string.IsNullOrWhiteSpace(text) ? null : text.Trim(),
        _ => JsonSerializer.Serialize(value, JsonOptions)
    };

    /// <summary>Cắt bớt theo độ dài cột/varchar tham số để không làm proc lỗi vì dữ liệu quá dài.</summary>
    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();

        return text.Length <= maxLength ? text : text[..maxLength];
    }
}
