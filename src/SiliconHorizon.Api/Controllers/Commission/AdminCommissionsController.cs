using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Features.Commission;
using SiliconHorizon.Application.Features.Commission.Dto;

namespace SiliconHorizon.Api.Controllers.Commission;

/// <summary>
/// Điều chỉnh hoa hồng thủ công — POST /api/v1/admin/commissions/adjustments
/// (Database/README.md mục 8, usp_Commission_Adjust, chỉ Admin).
/// </summary>
/// <remarks>
/// Bút toán điều chỉnh luôn được ghi vào kỳ hoa hồng <b>đang mở</b> của ngày hiện tại: nếu kỳ của tháng hiện tại
/// đã chốt thì proc tự chuyển sang tháng kế tiếp còn mở (<c>usp_Internal_GetOpenPeriod</c>), nhờ đó dữ liệu của
/// kỳ đã chốt không bị sửa (KPI-05/KPI-06). Số tiền phải khác 0 và bắt buộc có lý do; mọi thao tác ghi nhật ký AUD-01.
/// </remarks>
[ApiController]
[Route("api/v1/admin/commissions")]
[Authorize(Roles = CommissionRoles.Admin)]
[Produces("application/json")]
public class AdminCommissionsController : ControllerBase
{
    private readonly ICommissionService _commissionService;

    public AdminCommissionsController(ICommissionService commissionService) => _commissionService = commissionService;

    /// <summary>
    /// POST /api/v1/admin/commissions/adjustments — tạo bút toán điều chỉnh hoa hồng (EntryType = 2)
    /// cho một nhân viên, gắn với một đơn hàng (usp_Commission_Adjust).
    /// </summary>
    /// <remarks>
    /// Trả về <c>entryId</c> của bút toán vừa tạo (kèm <c>reason</c> đã lưu trong nhật ký). Dùng khi cần
    /// cộng/trừ hoa hồng ngoài công thức tự động (thưởng nóng, xử lý khiếu nại...).
    /// Quyền: AD.
    /// </remarks>
    [HttpPost("adjustments")]
    [ProducesResponseType(typeof(CommissionAdjustmentResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommissionAdjustmentResultDto>> Adjust(
        [FromBody] AdjustCommissionRequest request,
        CancellationToken cancellationToken)
        => Ok(await _commissionService.AdjustAsync(request, cancellationToken));
}