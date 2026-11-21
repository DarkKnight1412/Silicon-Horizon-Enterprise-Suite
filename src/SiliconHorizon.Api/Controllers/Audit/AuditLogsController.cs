using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Audit;
using SiliconHorizon.Application.Features.Audit.Dto;

namespace SiliconHorizon.Api.Controllers.Audit;

/// <summary>
/// Nhật ký hệ thống — GET /api/v1/audit-logs (Database/README.md mục 8, UC-29 / AUD-04).
/// </summary>
/// <remarks>
/// <b>Chỉ đọc</b>: bảng <c>AuditLogs</c> là sổ append-only, không có endpoint sửa/xóa; việc ghi do
/// <c>usp_Audit_Write</c> thực hiện bên trong các proc nghiệp vụ. Quản lý chi nhánh chỉ thấy nhật ký
/// của chi nhánh mình (API ép <c>@BranchId</c> từ token), Admin xem toàn hệ thống.
/// </remarks>
[ApiController]
[Route("api/v1/audit-logs")]
[Authorize(Roles = AuditRoles.Viewers)]
[Produces("application/json")]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditService _auditService;

    public AuditLogsController(IAuditService auditService) => _auditService = auditService;

    /// <summary>
    /// GET /api/v1/audit-logs — tra cứu nhật ký có phân trang (usp_Audit_Search).
    /// </summary>
    /// <remarks>
    /// Lọc theo <c>entityType</c>, <c>entityId</c>, <c>userId</c>, <c>action</c> và khoảng thời gian
    /// <c>fromDate</c>/<c>toDate</c> (UTC, chặn trên KHÔNG bao gồm). <c>branchId</c> chỉ có hiệu lực với Admin;
    /// Quản lý chi nhánh luôn bị ép về chi nhánh trong token. Quyền: QL (chi nhánh mình), AD.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AuditLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> Search(
        [FromQuery] AuditLogQuery query,
        CancellationToken cancellationToken)
        => Ok(await _auditService.SearchAsync(query, cancellationToken));
}
