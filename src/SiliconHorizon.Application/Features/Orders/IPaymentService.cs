using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Orders.Dto;

namespace SiliconHorizon.Application.Features.Orders;

/// <summary>
/// Use case thanh toán và công nợ (PAY-01..05) — đi qua <c>usp_Payment_Record</c>, <c>usp_Payment_Refund</c>
/// và <c>usp_Customer_GetLedger</c>.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>PAY-02: callback cổng thanh toán idempotent theo <c>(Provider, ProviderTransactionId)</c> — gọi lại chỉ ghi một lần.</item>
/// <item>PAY-03: chuyển khoản do thu ngân xác nhận; thiếu/thừa tiền trả về <c>NeedsReview = true</c> để đối soát.</item>
/// <item>PAY-04: hoàn tiền là một bản ghi riêng trong <c>Payments</c>, không sửa giao dịch gốc.</item>
/// <item>PAY-05: tiền công nợ (phương thức Credit) được ghi vào <c>CustomerLedger</c> ngay khi ghi nhận thanh toán.</item>
/// </list>
/// </remarks>
public interface IPaymentService
{
    /// <summary>
    /// POST /api/v1/orders/{id}/payments — thu ngân xác nhận chuyển khoản / thu tiền (usp_Payment_Record).
    /// Phải có quyền theo chi nhánh của đơn.
    /// </summary>
    Task<RecordedPaymentDto> RecordAsync(long orderId, RecordPaymentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/v1/payments/callback/{provider} — cổng thanh toán gọi về (usp_Payment_Record).
    /// Không theo phạm vi chi nhánh (đây là luồng hệ thống của cổng, không phải thao tác của nhân viên).
    /// </summary>
    Task<RecordedPaymentDto> HandleCallbackAsync(string provider, PaymentCallbackRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/v1/orders/{id}/refunds — Quản lý/Admin hoàn tiền thủ công cho đơn đã hủy hoặc giao thất bại
    /// (usp_Payment_Refund, chỉ cho đơn trạng thái Cancelled/DeliveryFailed).
    /// </summary>
    Task<RefundResultDto> RefundAsync(long orderId, RefundRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/v1/customers/{id}/ledger — sao kê công nợ của khách (usp_Customer_GetLedger: số dư + bút toán).
    /// </summary>
    Task<CustomerLedgerDto> GetCustomerLedgerAsync(long customerId, PageRequest request, CancellationToken cancellationToken = default);
}
