using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Orders;
using SiliconHorizon.Application.Features.Orders.Dto;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Api.Controllers.Orders;

/// <summary>
/// Quản trị đơn hàng — GET /api/v1/admin/orders.
/// </summary>
/// <remarks>
/// Dành cho Admin xem và tra soát toàn hệ thống: gọi <c>usp_Order_List</c> với <c>@BranchId = NULL</c> và
/// <c>@CustomerId = NULL</c>. Endpoint KHÔNG nhận <c>branchId</c> từ query (quy ước phạm vi chi nhánh ở
/// Database/README.md mục 1.1); muốn xem theo từng chi nhánh thì dùng GET /api/v1/staff/orders với tài khoản
/// của chi nhánh đó.
/// </remarks>
[ApiController]
[Route("api/v1/admin/orders")]
[Authorize(Roles = RoleCodes.Admin)]
[Produces("application/json")]
public class AdminOrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public AdminOrdersController(IOrderService orderService) => _orderService = orderService;

    /// <summary>
    /// GET /api/v1/admin/orders — toàn bộ đơn hàng, lọc theo trạng thái / khoảng thời gian / từ khóa (usp_Order_List).
    /// </summary>
    /// <remarks>
    /// Dùng cho đối soát, xử lý đơn thừa tiền (needsReview) hoặc tìm đơn theo mã/SĐT.
    /// Quyền: AD · mã quyền đề xuất: chưa có mã riêng cho xem đơn toàn hệ thống.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OrderListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<OrderListItemDto>>> List(
        [FromQuery] OrderListRequest request,
        CancellationToken cancellationToken)
        => Ok(await _orderService.ListAllAsync(request, cancellationToken));
}
