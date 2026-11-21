using SiliconHorizon.Application.Abstractions.Data;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Common.Sql;
using SiliconHorizon.Application.Features.Audit.Dto;

namespace SiliconHorizon.Application.Features.Audit;

/// <summary>
/// Hiện thực use case Nhật ký hệ thống qua stored procedure
/// (Database/06_sp_hr_audit_reports.sql, nhóm <c>usp_Audit_*</c>; Database/README.md mục 8).
/// </summary>
/// <remarks>
/// Nguyên tắc nghiệp vụ:
/// <list type="bullet">
/// <item><b>Nhật ký chỉ đọc</b> — không có API sửa/xóa; proc gốc còn thu hồi quyền UPDATE/DELETE trên bảng
/// <c>AuditLogs</c> để bảo đảm tính toàn vẹn (AUD-01..04).</item>
/// <item><b>Quản lý chỉ thấy chi nhánh mình</b>: <c>@BranchId</c> ép từ token, không nhận từ client;
/// Admin không truyền gì thì xem toàn hệ thống (<c>@BranchId = NULL</c>).</item>
/// <item>Kết quả gồm cả <c>OldValues</c>/<c>NewValues</c> (JSON) và <c>IpAddress</c> để tra soát
/// "ai đổi gì, từ máy nào" (AUD-02).</item>
/// <item><c>fromDate</c>/<c>toDate</c> là UTC; chặn trên KHÔNG bao gồm (proc dùng <c>&lt;</c>).</item>
/// </list>
/// </remarks>
public sealed class AuditService : IAuditService
{
    /// <summary>@EntityType varchar(50).</summary>
    private const int EntityTypeMaxLength = 50;

    /// <summary>@Action varchar(60).</summary>
    private const int ActionMaxLength = 60;

    /// <summary>usp_Audit_Search tự cắt @PageSize về tối đa 200.</summary>
    private const int PageSizeMax = 200;

    private readonly ISqlExecutor _sqlExecutor;
    private readonly AuditAccessGuard _guard;

    public AuditService(ISqlExecutor sqlExecutor, AuditAccessGuard guard)
    {
        _sqlExecutor = sqlExecutor;
        _guard = guard;
    }

    /// <inheritdoc />
    public async Task<PagedResult<AuditLogDto>> SearchAsync(
        AuditLogQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsurePositive(query.EntityId, "entityId");
        EnsurePositive(query.UserId, "userId");
        EnsurePositive(query.BranchId, "branchId");

        // Quản lý bị ép @BranchId từ token; chỉ Admin được truyền giá trị lọc (null = toàn hệ thống).
        var branchId = _guard.ResolveBranchScope(query.BranchId);

        if (query.FromDate is { } fromDate && query.ToDate is { } toDate && toDate <= fromDate)
        {
            throw new BadRequestException("toDate phải lớn hơn fromDate (toDate là chặn trên KHÔNG bao gồm).");
        }

        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = Math.Clamp(query.PageSize, 1, PageSizeMax);

        var rows = await _sqlExecutor.QueryAsync<AuditLogRow>(
            StoredProcedureNames.AuditSearch,
            new
            {
                EntityType = NormalizeOptional(query.EntityType, EntityTypeMaxLength, "entityType"),
                EntityId = query.EntityId,
                UserId = query.UserId,
                BranchId = branchId,
                Action = NormalizeOptional(query.Action, ActionMaxLength, "action"),
                FromDate = query.FromDate,
                ToDate = query.ToDate,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            cancellationToken);

        return PagedResult<AuditLogDto>.Create(
            rows.Select(AuditLogDto.FromRow).ToList(),
            pageNumber,
            pageSize,
            rows.Count > 0 ? rows[0].TotalCount : 0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tiện ích nội bộ
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Chuẩn hóa chuỗi lọc tùy chọn: rỗng → <c>null</c> (proc hiểu là không lọc), quá dài → 400.</summary>
    private static string? NormalizeOptional(string? value, int maxLength, string fieldName)
    {
        var text = value?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (text.Length > maxLength)
        {
            throw new BadRequestException($"{fieldName} tối đa {maxLength} ký tự.");
        }

        return text;
    }

    /// <summary>Các tham số id của proc là <c>bigint</c>/<c>int</c> dương; 0 hoặc âm là dữ liệu sai (400).</summary>
    private static void EnsurePositive(long? value, string fieldName)
    {
        if (value.HasValue && value.Value <= 0)
        {
            throw new BadRequestException($"{fieldName} phải là số dương.");
        }
    }

    /// <summary>Bản dùng cho <c>int?</c> (<c>branchId</c>) — cùng quy tắc với bản <c>long?</c> ở trên.</summary>
    private static void EnsurePositive(int? value, string fieldName)
    {
        if (value.HasValue && value.Value <= 0)
        {
            throw new BadRequestException($"{fieldName} phải là số dương.");
        }
    }
}
