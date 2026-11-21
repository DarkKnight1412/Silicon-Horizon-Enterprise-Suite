using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Api.Constants;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Orders;
using SiliconHorizon.Application.Features.Orders.Dto;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Api.Controllers.Orders;

/// <summary>
/// Đơn hàng — Database/README.md mục 5.1 (khách) và 5.2 (nhân viên xử lý), cùng các endpoint thanh toán
/// gắn theo đơn ({id}/payments, {id}/refunds) ở mục 5.3.
/// </summary>
/// <remarks>
/// Controller mỏng: chỉ nhận tham số và gọi service. Mọi quy tắc dữ liệu nằm ở
/// <c>SiliconHorizon.Application.Features.Orders</c>:
/// <list type="bullet">
/// <item>API LUÔN ép <c>customerId</c> (khách) và <c>branchId</c> (nhân viên) từ token — endpoint không nhận hai giá trị này từ client.</item>
/// <item>vượt phạm vi → 403; sai trạng thái (proc báo 50002) → 409; không đủ tồn (50003) → 409.</item>
/// </list>
/// </remarks>
[ApiController]
[Route("api/v1/orders")]
[Authorize]
[Produces("application/json")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly IPaymentService _paymentService;

    public OrdersController(IOrderService orderService, IPaymentService paymentService)
    {
        _orderService = orderService;
        _paymentService = paymentService;
    }

    /// <summary>
    /// POST /api/v1/orders — khách đặt hàng từ các dòng đã tick trong giỏ (usp_Order_Create, <c>@ItemsJson = NULL</c>,
    /// <c>@Channel = 0</c>).
    /// </summary>
    /// <remarks>
    /// Proc chốt giá và snapshot vào đơn (ORD-04), áp voucher, giữ hàng an toàn khi đặt đồng thời (ORD-10) rồi
    /// xóa các dòng đã đặt khỏi giỏ (CART-05). Đơn chuyển khoản/cổng thanh toán vào PendingPayment kèm ExpiresAt
    /// (ORD-11); COD và công nợ vào thẳng Pending.
    /// Quyền: KH (khách đã đăng nhập) · mã quyền đề xuất <c>order.create</c>.
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = RoleCodes.Customer)]
    [ProducesResponseType(typeof(CreatedOrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreatedOrderDto>> Create(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _orderService.CreateAsync(request, cancellationToken);

        return Created($"/api/v1/orders/{created.OrderId}", created);
    }

    /// <summary>
    /// GET /api/v1/orders — danh sách đơn của chính khách đang đăng nhập (usp_Order_List, ép <c>@CustomerId</c> từ token).
    /// Quyền: KH.
    /// </summary>
    /// <remarks>
    /// Lọc theo <c>status</c>, <c>fromDate</c>/<c>toDate</c> (UTC, <c>toDate</c> là chặn trên không bao gồm),
    /// <c>keyword</c> (mã đơn / SĐT / tên khách) và phân trang <c>pageNumber</c>, <c>pageSize</c> (tối đa 100).
    /// </remarks>
    [HttpGet]
    [Authorize(Roles = RoleCodes.Customer)]
    [ProducesResponseType(typeof(PagedResult<OrderListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<OrderListItemDto>>> ListMine(
        [FromQuery] OrderListRequest request,
        CancellationToken cancellationToken)
        => Ok(await _orderService.ListMineAsync(request, cancellationToken));

    /// <summary>
    /// GET /api/v1/orders/{id} — chi tiết đơn: header, dòng hàng, serial đã gán, giao dịch thanh toán,
    /// lịch sử trạng thái và nhân viên tư vấn (usp_Order_Get — 6 result set).
    /// </summary>
    /// <remarks>
    /// Quyền: KH (chỉ đơn của mình), NV (chỉ đơn chi nhánh mình), AD. Vượt phạm vi → 403; không có đơn → 404.
    /// </remarks>
    [HttpGet("{id:long}")]
    [Authorize(Roles = OrderRoleGroups.OrderViewers)]
    [ProducesResponseType(typeof(OrderDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetailDto>> GetById(long id, CancellationToken cancellationToken)
        => Ok(await _orderService.GetByIdAsync(id, cancellationToken));

    /// <summary>
    /// POST /api/v1/orders/{id}/cancel — khách tự hủy đơn (usp_Order_Cancel), bắt buộc lý do (ORD-08).
    /// </summary>
    /// <remarks>
    /// Chỉ cho phép khi đơn ở PendingPayment (0) hoặc Pending (1); trạng thái khác → 409 (ORD-07 và Database/README.md mục 5.1).
    /// Proc nhả tồn đang giữ, gỡ serial về InStock, trả lượt voucher và đảo công nợ.
    /// Đơn đã thu tiền trả về <c>refundRequired = true</c> — dùng POST /api/v1/orders/{id}/refunds để hoàn.
    /// Quyền: KH (đơn của mình).
    /// </remarks>
    [HttpPost("{id:long}/cancel")]
    [Authorize(Roles = RoleCodes.Customer)]
    [ProducesResponseType(typeof(OrderCancelResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderCancelResultDto>> Cancel(
        long id,
        [FromBody] CancelOrderRequest request,
        CancellationToken cancellationToken)
        => Ok(await _orderService.CancelByCustomerAsync(id, request, cancellationToken));

    // ─────────────────────────────────────────────────────────────────────────
    // Nhân viên xử lý đơn (Database/README.md mục 5.2)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// POST /api/v1/orders/{id}/confirm — xác nhận đơn: Pending → Confirmed (usp_Order_Confirm).
    /// Quyền: TN, TV, QL, AD · mã quyền đề xuất <c>order.confirm</c>.
    /// </summary>
    /// <remarks>Nhân viên chỉ thao tác được đơn thuộc chi nhánh mình (403 nếu không); sai trạng thái → 409.</remarks>
    [HttpPost("{id:long}/confirm")]
    [Authorize(Roles = OrderRoleGroups.SalesCounter)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Confirm(long id, CancellationToken cancellationToken)
    {
        await _orderService.ConfirmAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// POST /api/v1/orders/{id}/start-packing — bắt đầu đóng gói: Confirmed → Packing (usp_Order_StartPacking).
    /// Quyền: TK, AD · mã quyền đề xuất <c>order.pack</c>.
    /// </summary>
    [HttpPost("{id:long}/start-packing")]
    [Authorize(Roles = OrderRoleGroups.Warehouse)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> StartPacking(long id, CancellationToken cancellationToken)
    {
        await _orderService.StartPackingAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// POST /api/v1/orders/{id}/serials — quét serial gán vào đơn đang đóng gói (usp_Order_AssignSerial).
    /// </summary>
    /// <remarks>
    /// Trả tiến độ quét theo từng dòng hàng cần serial (<c>assigned</c>/<c>quantity</c> và còn lại <c>remaining</c>).
    /// Proc kiểm tra: đơn phải ở Packing (409), serial phải InStock (409), đúng chi nhánh xuất hàng (409),
    /// chưa quét quá số lượng của dòng (422).
    /// Quyền: TK, AD · mã quyền đề xuất <c>order.pack</c>, <c>serial.manage</c>.
    /// </remarks>
    [HttpPost("{id:long}/serials")]
    [Authorize(Roles = OrderRoleGroups.Warehouse)]
    [ProducesResponseType(typeof(IReadOnlyList<OrderSerialProgressDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyList<OrderSerialProgressDto>>> AssignSerial(
        long id,
        [FromBody] AssignSerialRequest request,
        CancellationToken cancellationToken)
        => Ok(await _orderService.AssignSerialAsync(id, request, cancellationToken));

    /// <summary>
    /// DELETE /api/v1/orders/{id}/serials/{serialNumber} — gỡ serial gán nhầm (usp_Order_UnassignSerial, SER-02).
    /// </summary>
    /// <remarks>
    /// Bắt buộc lý do (<c>reason</c>) — proc ghi SerialStatusHistory và Audit. <c>productId</c> chỉ cần khi cùng
    /// số serial xuất hiện ở nhiều sản phẩm trong đơn. Serial trở về InStock.
    /// Quyền: TK, AD.
    /// </remarks>
    [HttpDelete("{id:long}/serials/{serialNumber}")]
    [Authorize(Roles = OrderRoleGroups.Warehouse)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UnassignSerial(
        long id,
        string serialNumber,
        [FromQuery] string reason,
        [FromQuery] long? productId,
        CancellationToken cancellationToken)
    {
        await _orderService.UnassignSerialAsync(id, serialNumber, reason, productId, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// POST /api/v1/orders/{id}/ready — đóng gói xong (usp_Order_MarkReady): nhận tại cửa hàng → ReadyForPickup (4),
    /// giao tận nơi → Shipping (5).
    /// </summary>
    /// <remarks>
    /// Yêu cầu đã quét đủ serial cho mọi dòng cần serial (thiếu → 409). Với đơn nhận tại cửa hàng, proc đặt
    /// <c>ExpiresAt</c> theo cấu hình <c>pickup.hold.days</c> (ORD-02); quá hạn job tự hủy và nhả tồn.
    /// Quyền: TK, AD.
    /// </remarks>
    [HttpPost("{id:long}/ready")]
    [Authorize(Roles = OrderRoleGroups.Warehouse)]
    [ProducesResponseType(typeof(OrderStatusChangeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderStatusChangeDto>> MarkReady(long id, CancellationToken cancellationToken)
        => Ok(await _orderService.MarkReadyAsync(id, cancellationToken));

    /// <summary>
    /// POST /api/v1/orders/{id}/delivery-failed — ghi nhận giao thất bại: Shipping → DeliveryFailed
    /// (usp_Order_MarkDeliveryFailed); bắt buộc lý do (ORD-08).
    /// Quyền: TN, TV, TK, AD.
    /// </summary>
    [HttpPost("{id:long}/delivery-failed")]
    [Authorize(Roles = OrderRoleGroups.Delivery)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarkDeliveryFailed(
        long id,
        [FromBody] DeliveryFailedRequest request,
        CancellationToken cancellationToken)
    {
        await _orderService.MarkDeliveryFailedAsync(id, request, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// POST /api/v1/orders/{id}/retry-delivery — giao lại: DeliveryFailed → Shipping (usp_Order_RetryDelivery).
    /// Quyền: TN, TV, TK, AD.
    /// </summary>
    [HttpPost("{id:long}/retry-delivery")]
    [Authorize(Roles = OrderRoleGroups.Delivery)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RetryDelivery(long id, CancellationToken cancellationToken)
    {
        await _orderService.RetryDeliveryAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// POST /api/v1/orders/{id}/complete — giao thành công / khách nhận hàng: ReadyForPickup hoặc Shipping → Completed
    /// (usp_Order_Complete).
    /// </summary>
    /// <remarks>
    /// Proc trừ tồn thực tế và tồn đang giữ, chuyển serial đã gán sang Sold và bắt đầu tính bảo hành (WAR-01),
    /// ghi nhận tiền COD/tiền mặt còn thiếu (PAY-01) và tính hoa hồng (KPI-01).
    /// Đơn chuyển khoản/cổng thanh toán chưa thu đủ → 409; chưa gán đủ serial → 409.
    /// Quyền: TN, TV, TK, AD · mã quyền đề xuất <c>order.ship</c>.
    /// </remarks>
    [HttpPost("{id:long}/complete")]
    [Authorize(Roles = OrderRoleGroups.Delivery)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Complete(long id, CancellationToken cancellationToken)
    {
        await _orderService.CompleteAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// POST /api/v1/orders/{id}/cancel-approved — Quản lý/Admin hủy đơn đã xác nhận trở đi (usp_Order_Cancel),
    /// bắt buộc lý do (ORD-08, UC-20).
    /// </summary>
    /// <remarks>
    /// Hủy được từ Pending (1), Confirmed (2), Packing (3), ReadyForPickup (4), DeliveryFailed (6) → Cancelled (8);
    /// proc nhả tồn, gỡ serial, trả lượt voucher và đảo công nợ. Trạng thái không hủy được (đang giao / đã hoàn tất) → 409.
    /// <c>refundRequired = true</c> nghĩa là đơn đã thu tiền, phải hoàn qua POST /api/v1/orders/{id}/refunds (PAY-04).
    /// Quyền: QL, AD · mã quyền đề xuất <c>order.cancel.approve</c>.
    /// </remarks>
    [HttpPost("{id:long}/cancel-approved")]
    [Authorize(Roles = OrderRoleGroups.Approval)]
    [ProducesResponseType(typeof(OrderCancelResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderCancelResultDto>> CancelApproved(
        long id,
        [FromBody] CancelOrderRequest request,
        CancellationToken cancellationToken)
        => Ok(await _orderService.CancelApprovedAsync(id, request, cancellationToken));

    // ─────────────────────────────────────────────────────────────────────────
    // Thanh toán gắn theo đơn (Database/README.md mục 5.3)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// POST /api/v1/orders/{id}/payments — thu ngân xác nhận chuyển khoản / thu tiền (usp_Payment_Record).
    /// </summary>
    /// <remarks>
    /// Proc cộng <c>PaidAmount</c> và cập nhật <c>PaymentStatus</c>; khi thu đủ tiền cho đơn PendingPayment thì tự
    /// chuyển sang Pending và xóa hạn giữ hàng. Thanh toán công nợ (method = 4) ghi thêm bút toán vào
    /// <c>CustomerLedger</c> (PAY-05). Chuyển thiếu/thừa tiền trả về <c>needsReview = true</c> để đối soát (PAY-03).
    /// Quyền: TN, TV, QL, AD.
    /// </remarks>
    [HttpPost("{id:long}/payments")]
    [Authorize(Roles = OrderRoleGroups.SalesCounter)]
    [ProducesResponseType(typeof(RecordedPaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RecordedPaymentDto>> RecordPayment(
        long id,
        [FromBody] RecordPaymentRequest request,
        CancellationToken cancellationToken)
        => Ok(await _paymentService.RecordAsync(id, request, cancellationToken));

    /// <summary>
    /// POST /api/v1/orders/{id}/refunds — hoàn tiền thủ công cho đơn đã hủy hoặc giao thất bại (usp_Payment_Refund).
    /// </summary>
    /// <remarks>
    /// Chỉ áp dụng cho đơn Cancelled (8) hoặc DeliveryFailed (6); hoàn tiền do đổi trả đi qua quy trình đổi trả
    /// (usp_Return_Refund). Bản ghi hoàn là giao dịch mới trỏ về giao dịch gốc, không sửa giao dịch gốc (PAY-04);
    /// số tiền không được vượt quá số đã thanh toán (422 khi vượt).
    /// Quyền: QL, AD (hoàn tiền phải được duyệt) · mã quyền đề xuất <c>order.cancel.approve</c>
    /// (bảng Permissions chưa có mã riêng cho hoàn tiền).
    /// <para>
    /// Lưu ý hoa hồng: đơn chỉ được tính hoa hồng khi đã Completed, mà hoàn tiền thủ công chỉ áp dụng cho đơn
    /// Cancelled/DeliveryFailed nên không cần thu hồi hoa hồng ở đây — thu hồi hoa hồng do luồng đổi trả lo
    /// (<c>usp_Return_Refund</c> → <c>usp_Internal_ClawbackCommission</c>).
    /// </para>
    /// </remarks>
    [HttpPost("{id:long}/refunds")]
    [Authorize(Roles = OrderRoleGroups.Approval)]
    [ProducesResponseType(typeof(RefundResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RefundResultDto>> Refund(
        long id,
        [FromBody] RefundRequest request,
        CancellationToken cancellationToken)
        => Ok(await _paymentService.RefundAsync(id, request, cancellationToken));
}
