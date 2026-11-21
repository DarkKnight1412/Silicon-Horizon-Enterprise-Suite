using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Orders;
using SiliconHorizon.Application.Features.Orders.Dto;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Api.Controllers.Orders;

/// <summary>
/// Danh sách đơn cho nhân viên — GET /api/v1/staff/orders (Database/README.md mục 5.2).
/// </summary>
/// <remarks>
/// API ép <c>@BranchId</c> từ token (BRD 2.2: nhân viên chỉ thấy đơn của chi nhánh mình), không nhận
/// <c>branchId</c> từ query string; tài khoản nhân viên chưa gắn chi nhánh → 403.
/// </remarks>
[ApiController]
[Route("api/v1/staff/orders")]
[Authorize(Roles = RoleCodes.Staff + "," + RoleCodes.Admin)]
[Produces("application/json")]
public class StaffOrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public StaffOrdersController(IOrderService orderService) => _orderService = orderService;

    /// <summary>
    /// GET /api/v1/staff/orders — danh sách đơn của chi nhánh, lọc theo trạng thái / khoảng thời gian / từ khóa
    /// (usp_Order_List).
    /// </summary>
    /// <remarks>
    /// <c>fromDate</c>/<c>toDate</c> là UTC và <c>toDate</c> là chặn trên không bao gồm (nên truyền đầu ngày kế tiếp).
    /// <c>keyword</c> tìm theo mã đơn (khớp đầu mã), SĐT hoặc tên khách, tối đa 50 ký tự.
    /// Phân trang <c>pageNumber</c>, <c>pageSize</c> (tối đa 100).
    /// Quyền: NV (mọi nhân viên), AD.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OrderListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<OrderListItemDto>>> List(
        [FromQuery] OrderListRequest request,
        CancellationToken cancellationToken)
        => Ok(await _orderService.ListBranchAsync(request, cancellationToken));
}
