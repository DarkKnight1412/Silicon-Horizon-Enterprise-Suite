using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Api.Constants;
using SiliconHorizon.Application.Features.Orders;
using SiliconHorizon.Application.Features.Orders.Dto;

namespace SiliconHorizon.Api.Controllers.Orders;

/// <summary>
/// Bán hàng tại quầy — POST /api/v1/pos/orders (Database/README.md mục 5.2).
/// </summary>
/// <remarks>
/// Đơn tại quầy dùng chi nhánh của nhân viên (ORD-03): API ép <c>@BranchId</c> và <c>@UserId</c> từ token,
/// endpoint KHÔNG nhận <c>branchId</c> từ client. Danh sách sản phẩm bắt buộc truyền trực tiếp
/// (<c>@ItemsJson</c>, <c>@Channel = 1</c>) vì khách tại quầy có thể không dùng giỏ hàng online.
/// </remarks>
[ApiController]
[Route("api/v1/pos/orders")]
[Authorize(Roles = OrderRoleGroups.SalesCounter)]
[Produces("application/json")]
public class PosOrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public PosOrdersController(IOrderService orderService) => _orderService = orderService;

    /// <summary>
    /// POST /api/v1/pos/orders — tạo đơn tại quầy (usp_Order_Create với <c>@Channel = 1</c>).
    /// </summary>
    /// <remarks>
    /// Trả 201 kèm thông tin đơn vừa tạo. Đơn COD/tiền mặt/tiền mặt-POS vào thẳng Pending (1);
    /// chuyển khoản/cổng thanh toán vào PendingPayment (0) kèm hạn giữ hàng.
    /// Không truyền <c>salesStaff</c> thì proc gán nhân viên tạo đơn 100% (KPI-02); nếu truyền thì tổng
    /// <c>shareRate</c> phải bằng 100, sai → 400.
    /// Quyền: TN, TV, QL, AD · mã quyền đề xuất <c>order.create</c>.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(CreatedOrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreatedOrderDto>> Create(
        [FromBody] CreatePosOrderRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _orderService.CreatePosAsync(request, cancellationToken);

        return Created($"/api/v1/orders/{created.OrderId}", created);
    }
}
