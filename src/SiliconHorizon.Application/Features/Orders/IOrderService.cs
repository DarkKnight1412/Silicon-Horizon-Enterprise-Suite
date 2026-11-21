using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Orders.Dto;

namespace SiliconHorizon.Application.Features.Orders;

/// <summary>
/// Use case đơn hàng (ORD-01..13) — mọi thao tác đi qua stored procedure trong
/// Database/03_sp_orders_payments.sql, không truy vấn bảng trực tiếp.
/// </summary>
/// <remarks>
/// Phạm vi dữ liệu do tầng API quyết định và service ép lại (xem <see cref="OrderAccessGuard"/>):
/// khách luôn bị ép <c>CustomerId</c> từ token, nhân viên luôn bị ép <c>BranchId</c> từ token.
/// Trạng thái và các chuyển trạng thái hợp lệ (ORD-07) do <c>usp_Internal_SetOrderStatus</c> kiểm tra; vi phạm → 409.
/// </remarks>
public interface IOrderService
{
    /// <summary>
    /// POST /api/v1/orders — khách đặt hàng từ các dòng đã tick trong giỏ (usp_Order_Create,
    /// <c>@ItemsJson = NULL</c>, <c>@Channel = 0</c>); đơn chuyển khoản/cổng thanh toán vào PendingPayment kèm hạn giữ hàng.
    /// </summary>
    Task<CreatedOrderDto> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/v1/pos/orders — thu ngân/tư vấn tạo đơn tại quầy (usp_Order_Create, <c>@Channel = 1</c>,
    /// chi nhánh ép từ token theo ORD-03).
    /// </summary>
    Task<CreatedOrderDto> CreatePosAsync(CreatePosOrderRequest request, CancellationToken cancellationToken = default);

    /// <summary>GET /api/v1/orders — đơn của chính khách đang đăng nhập (usp_Order_List, ép <c>CustomerId</c>).</summary>
    Task<PagedResult<OrderListItemDto>> ListMineAsync(OrderListRequest request, CancellationToken cancellationToken = default);

    /// <summary>GET /api/v1/staff/orders — đơn của chi nhánh nhân viên (usp_Order_List, ép <c>BranchId</c>).</summary>
    Task<PagedResult<OrderListItemDto>> ListBranchAsync(OrderListRequest request, CancellationToken cancellationToken = default);

    /// <summary>GET /api/v1/admin/orders — toàn bộ đơn của hệ thống (usp_Order_List, <c>BranchId = NULL</c>).</summary>
    Task<PagedResult<OrderListItemDto>> ListAllAsync(OrderListRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/v1/orders/{id} — chi tiết đơn: header, dòng hàng, serial, thanh toán, lịch sử trạng thái,
    /// nhân viên tư vấn (usp_Order_Get — 6 result set) kèm kiểm tra phạm vi.
    /// </summary>
    Task<OrderDetailDto> GetByIdAsync(long orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/v1/orders/{id}/cancel — khách tự hủy (usp_Order_Cancel) chỉ khi đơn ở PendingPayment hoặc Pending.
    /// </summary>
    Task<OrderCancelResultDto> CancelByCustomerAsync(long orderId, CancelOrderRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/v1/orders/{id}/cancel-approved — Quản lý/Admin hủy đơn từ Confirmed trở đi (usp_Order_Cancel),
    /// trả về <c>RefundRequired</c> để biết phải hoàn tiền.
    /// </summary>
    Task<OrderCancelResultDto> CancelApprovedAsync(long orderId, CancelOrderRequest request, CancellationToken cancellationToken = default);

    /// <summary>POST /api/v1/orders/{id}/confirm — Pending → Confirmed (usp_Order_Confirm).</summary>
    Task ConfirmAsync(long orderId, CancellationToken cancellationToken = default);

    /// <summary>POST /api/v1/orders/{id}/start-packing — Confirmed → Packing (usp_Order_StartPacking).</summary>
    Task StartPackingAsync(long orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/v1/orders/{id}/serials — quét serial gán vào đơn đang đóng gói (usp_Order_AssignSerial),
    /// trả tiến độ quét của từng dòng cần serial.
    /// </summary>
    Task<IReadOnlyList<OrderSerialProgressDto>> AssignSerialAsync(long orderId, AssignSerialRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// DELETE /api/v1/orders/{id}/serials/{serialNumber} — gỡ serial gán nhầm, bắt buộc lý do (usp_Order_UnassignSerial).
    /// </summary>
    Task UnassignSerialAsync(long orderId, string serialNumber, string reason, long? productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/v1/orders/{id}/ready — đóng gói xong (usp_Order_MarkReady): yêu cầu đã quét đủ serial,
    /// đơn nhận tại cửa hàng nhận thêm hạn đến lấy (ORD-02), đơn giao tận nơi chuyển sang Shipping.
    /// </summary>
    Task<OrderStatusChangeDto> MarkReadyAsync(long orderId, CancellationToken cancellationToken = default);

    /// <summary>POST /api/v1/orders/{id}/delivery-failed — ghi nhận giao thất bại, bắt buộc lý do (usp_Order_MarkDeliveryFailed).</summary>
    Task MarkDeliveryFailedAsync(long orderId, DeliveryFailedRequest request, CancellationToken cancellationToken = default);

    /// <summary>POST /api/v1/orders/{id}/retry-delivery — giao lại: DeliveryFailed → Shipping (usp_Order_RetryDelivery).</summary>
    Task RetryDeliveryAsync(long orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/v1/orders/{id}/complete — giao thành công/khách nhận hàng (usp_Order_Complete): trừ tồn thực tế,
    /// serial → Sold và bắt đầu bảo hành (WAR-01), ghi nhận tiền COD/tiền mặt, tính hoa hồng (KPI-01 — gọi
    /// <c>usp_Internal_AccrueCommission</c>).
    /// </summary>
    /// <remarks>
    /// Hoa hồng chỉ phát sinh ở bước hoàn tất; việc THU HỒI hoa hồng khi trả hàng nằm ở luồng đổi trả
    /// (<c>usp_Return_Refund</c> gọi <c>usp_Internal_ClawbackCommission</c>), không thuộc module này.
    /// </remarks>
    Task CompleteAsync(long orderId, CancellationToken cancellationToken = default);
}
