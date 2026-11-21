using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Audit.Dto;

namespace SiliconHorizon.Application.Features.Audit;

/// <summary>
/// Use case Nhật ký hệ thống (UC-29 / AUD-04). <b>Chỉ đọc</b>: bảng <c>AuditLogs</c> là sổ append-only,
/// việc ghi do <c>usp_Audit_Write</c> thực hiện (proc nghiệp vụ gọi, hoặc tầng API gọi trực tiếp cho
/// thao tác CRUD nhạy cảm) — không có endpoint sửa/xóa nhật ký.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Tra cứu nhật ký có phân trang — GET /api/v1/audit-logs (usp_Audit_Search).
    /// Quản lý chi nhánh chỉ thấy nhật ký của chi nhánh mình; Admin thấy toàn hệ thống (BRD 2.2).
    /// </summary>
    Task<PagedResult<AuditLogDto>> SearchAsync(
        AuditLogQuery query,
        CancellationToken cancellationToken = default);
}
