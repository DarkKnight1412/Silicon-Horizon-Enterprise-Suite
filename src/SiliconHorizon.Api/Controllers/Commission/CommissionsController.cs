using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Features.Commission;
using SiliconHorizon.Application.Features.Commission.Dto;

namespace SiliconHorizon.Api.Controllers.Commission;

/// <summary>
/// Tổng hợp hoa hồng — GET /api/v1/commissions?yearMonth= (Database/README.md mục 8, KPI-01..06).
/// </summary>
/// <remarks>
/// API ép phạm vi xem theo vai trò (BRD 2.2): nhân viên chỉ xem hoa hồng của mình (ép <c>EmployeeId</c> từ token),
/// Quản lý chi nhánh xem trong chi nhánh mình (ép <c>BranchId</c> từ token), Admin xem toàn hệ thống và có thể
/// lọc theo <c>employeeId</c>/<c>branchId</c>. Giá trị <c>employeeId</c>/<c>branchId</c> do vai trò khác gửi lên
/// bị bỏ qua.
/// </remarks>
[ApiController]
[Route("api/v1/commissions")]
[Authorize(Roles = CommissionRoles.Viewers)]
[Produces("application/json")]
public class CommissionsController : ControllerBase
{
    private readonly ICommissionService _commissionService;

    public CommissionsController(ICommissionService commissionService) => _commissionService = commissionService;

    /// <summary>
    /// GET /api/v1/commissions?yearMonth=2026-11 — tổng hợp hoa hồng theo nhân viên và chi tiết từng bút toán
    /// (usp_Commission_Summary, 3 result set: kỳ hoa hồng, tổng hợp theo nhân viên, chi tiết bút toán).
    /// </summary>
    /// <remarks>
    /// <c>yearMonth</c> bắt buộc theo định dạng <c>yyyy-MM</c>. Tháng chưa phát sinh hoa hồng thì <c>period = null</c>
    /// và hai danh sách rỗng. Bút toán Clawback (do đổi trả) có <c>amount</c> âm và kèm <c>returnRequestId</c>.
    /// Quyền: NV (của mình), QL (chi nhánh mình), AD.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(CommissionSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CommissionSummaryDto>> GetSummary(
        [FromQuery] CommissionSummaryQuery query,
        CancellationToken cancellationToken)
        => Ok(await _commissionService.GetSummaryAsync(query, cancellationToken));
}