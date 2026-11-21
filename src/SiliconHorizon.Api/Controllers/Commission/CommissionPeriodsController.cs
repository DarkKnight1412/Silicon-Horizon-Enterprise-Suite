using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Features.Commission;

namespace SiliconHorizon.Api.Controllers.Commission;

/// <summary>
/// Chốt kỳ hoa hồng — POST /api/v1/admin/commission-periods/{id}/close
/// (Database/README.md mục 8, usp_Commission_ClosePeriod, UC-28, chỉ Admin).
/// </summary>
/// <remarks>
/// Sau khi chốt, kỳ không điều chỉnh được nữa (chốt lại → 409) và mọi bút toán mới (hoa hồng phát sinh,
/// Clawback, điều chỉnh) tự rơi vào kỳ đang mở kế tiếp — dữ liệu đã chốt là bất biến (KPI-05/KPI-06).
/// <para>
/// <c>{id}</c> là <c>PeriodId</c> (lấy từ <c>period.periodId</c> của GET /api/v1/commissions).
/// </para>
/// </remarks>
[ApiController]
[Route("api/v1/admin/commission-periods")]
[Authorize(Roles = CommissionRoles.Admin)]
[Produces("application/json")]
public class CommissionPeriodsController : ControllerBase
{
    private readonly ICommissionService _commissionService;

    public CommissionPeriodsController(ICommissionService commissionService) => _commissionService = commissionService;

    /// <summary>
    /// POST /api/v1/admin/commission-periods/{id}/close — chốt kỳ hoa hồng
    /// (usp_Commission_ClosePeriod: ghi <c>ClosedBy</c>/<c>ClosedAt</c> + nhật ký AUD-01).
    /// </summary>
    /// <remarks>
    /// Kỳ không tồn tại → 404; kỳ đã chốt → 409. Quyền: AD.
    /// </remarks>
    [HttpPost("{id:int}/close")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Close(int id, CancellationToken cancellationToken)
    {
        await _commissionService.ClosePeriodAsync(id, cancellationToken);

        return NoContent();
    }
}