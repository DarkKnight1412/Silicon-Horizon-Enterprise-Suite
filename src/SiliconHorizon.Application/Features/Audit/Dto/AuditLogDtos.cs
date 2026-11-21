namespace SiliconHorizon.Application.Features.Audit.Dto;

// ─────────────────────────────────────────────────────────────────────────────
// DTO nhật ký hệ thống — khớp CHÍNH XÁC result set của dbo.usp_Audit_Search
// (Database/06_sp_hr_audit_reports.sql; Database/README.md mục 8, UC-29/AUD-04).
//
// Bảng AuditLogs là sổ append-only (chỉ INSERT; script gốc thu hồi quyền UPDATE/DELETE),
// nên module Nhật ký CHỈ có API đọc. Ghi nhật ký do proc nghiệp vụ gọi usp_Audit_Write
// (hoặc tầng API gọi trực tiếp cho thao tác CRUD nhạy cảm).
//
// Quy ước phạm vi (BRD 2.2): Quản lý chi nhánh chỉ thấy nhật ký của chi nhánh mình
// (ép @BranchId từ token), Admin xem toàn hệ thống (@BranchId = NULL).
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Một dòng nhật ký — result set của <c>dbo.usp_Audit_Search</c> (Id, UserId, Username, BranchId,
/// Action, EntityType, EntityId, OldValues, NewValues, Reason, IpAddress, CreatedAt, TotalCount).
/// </summary>
public sealed class AuditLogRow
{
    public long Id { get; set; }

    /// <summary>Người thực hiện — NULL với thao tác do hệ thống/tác vụ nền thực hiện.</summary>
    public long? UserId { get; set; }

    /// <summary>Tên đăng nhập (LEFT JOIN Users — NULL khi UserId NULL).</summary>
    public string? Username { get; set; }

    /// <summary>Chi nhánh gắn với thao tác — NULL với thao tác toàn hệ thống.</summary>
    public int? BranchId { get; set; }

    /// <summary>Mã hành động: PriceChanged, OrderCancelled, AttendanceAdjusted…</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Loại đối tượng: Order, Product, StockTake, Attendance, ReturnRequest…</summary>
    public string EntityType { get; set; } = string.Empty;

    public long EntityId { get; set; }

    /// <summary>Giá trị TRƯỚC khi sửa — JSON (nvarchar(max), có thể NULL).</summary>
    public string? OldValues { get; set; }

    /// <summary>Giá trị SAU khi sửa — JSON (nvarchar(max), có thể NULL).</summary>
    public string? NewValues { get; set; }

    /// <summary>Lý do (bắt buộc với các thao tác nhạy cảm như hủy đơn, điều chỉnh công).</summary>
    public string? Reason { get; set; }

    /// <summary>Địa chỉ IP của người thao tác (AUD-02).</summary>
    public string? IpAddress { get; set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Tổng số dòng khớp điều kiện lọc (dùng cho phân trang).</summary>
    public int TotalCount { get; set; }
}

/// <summary>Một dòng nhật ký trả ra API — GET /api/v1/audit-logs.</summary>
public class AuditLogDto
{
    public long Id { get; init; }

    /// <summary>Người thực hiện (NULL = hệ thống/tác vụ nền).</summary>
    public long? UserId { get; init; }

    public string? Username { get; init; }

    public int? BranchId { get; init; }

    public string Action { get; init; } = string.Empty;

    public string EntityType { get; init; } = string.Empty;

    public long EntityId { get; init; }

    /// <summary>JSON giá trị cũ (giữ nguyên dạng chuỗi để không làm mất dữ liệu khi JSON không hợp lệ).</summary>
    public string? OldValues { get; init; }

    /// <summary>JSON giá trị mới.</summary>
    public string? NewValues { get; init; }

    public string? Reason { get; init; }

    public string? IpAddress { get; init; }

    /// <summary>UTC (ISO 8601).</summary>
    public DateTime CreatedAt { get; init; }

    public static AuditLogDto FromRow(AuditLogRow row) => new()
    {
        Id = row.Id,
        UserId = row.UserId,
        Username = row.Username,
        BranchId = row.BranchId,
        Action = row.Action,
        EntityType = row.EntityType,
        EntityId = row.EntityId,
        OldValues = row.OldValues,
        NewValues = row.NewValues,
        Reason = row.Reason,
        IpAddress = row.IpAddress,
        CreatedAt = row.CreatedAt
    };
}

/// <summary>
/// Tham số GET /api/v1/audit-logs — tra cứu nhật ký (AUD-04).
/// </summary>
/// <remarks>
/// Mọi tham số đều tùy chọn (kết hợp AND như proc). Riêng <see cref="BranchId"/> chỉ có hiệu lực với
/// <b>Admin</b>: Quản lý chi nhánh luôn bị ép về chi nhánh trong token, nếu không sẽ đọc được nhật ký
/// của chi nhánh khác.
/// </remarks>
public class AuditLogQuery
{
    /// <summary>Loại đối tượng (Order, Product, Attendance…) — tối đa 50 ký tự (<c>@EntityType varchar(50)</c>).</summary>
    public string? EntityType { get; set; }

    /// <summary>Id đối tượng cụ thể.</summary>
    public long? EntityId { get; set; }

    /// <summary>Lọc theo người thực hiện (Users.Id).</summary>
    public long? UserId { get; set; }

    /// <summary>Chỉ Admin: lọc theo chi nhánh (bỏ trống = toàn hệ thống).</summary>
    public int? BranchId { get; set; }

    /// <summary>Mã hành động (OrderCancelled, AttendanceAdjusted…) — tối đa 60 ký tự (<c>@Action varchar(60)</c>).</summary>
    public string? Action { get; set; }

    /// <summary>UTC, chặn dưới BAO GỒM (>=).</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>UTC, chặn trên KHÔNG bao gồm (&lt;) — truyền đầu ngày kế tiếp để lấy trọn một ngày.</summary>
    public DateTime? ToDate { get; set; }

    public int PageNumber { get; set; } = 1;

    /// <summary>usp_Audit_Search tự cắt về tối đa 200.</summary>
    public int PageSize { get; set; } = 50;
}

