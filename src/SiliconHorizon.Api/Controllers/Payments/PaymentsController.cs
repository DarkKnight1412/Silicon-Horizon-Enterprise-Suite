using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Features.Orders;
using SiliconHorizon.Application.Features.Orders.Dto;

namespace SiliconHorizon.Api.Controllers.Payments;

/// <summary>
/// Callback từ cổng thanh toán — POST /api/v1/payments/callback/{provider} (Database/README.md mục 5.3).
/// </summary>
/// <remarks>
/// <para>
/// <b>Public theo thiết kế:</b> cổng thanh toán gọi trực tiếp, không có JWT, nên endpoint để
/// <c>[AllowAnonymous]</c>. ⚠️ Trước khi chạy thật cần bật <b>xác minh chữ ký/biên lai của cổng</b> (PAY-02)
/// và cấu hình khóa của từng cổng — hiện chưa có phần cấu hình này trong hệ thống, và API cũng không tin
/// kết quả trả về từ trình duyệt của khách (chỉ tin callback).
/// </para>
/// <para>
/// Idempotent theo <c>(provider, providerTransactionId)</c>: cổng gửi lại cùng giao dịch thì API vẫn trả 200
/// nhưng chỉ ghi nhận một lần (<c>isDuplicate = true</c>), nhờ đó cổng không phải gửi lại nhiều lần.
/// </para>
/// </remarks>
[ApiController]
[Route("api/v1/payments")]
[AllowAnonymous]
[Produces("application/json")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService) => _paymentService = paymentService;

    /// <summary>
    /// POST /api/v1/payments/callback/{provider} — cổng thanh toán báo đã thu tiền (usp_Payment_Record).
    /// </summary>
    /// <remarks>
    /// <c>provider</c> là mã cổng (tối đa 30 ký tự, ví dụ <c>vnpay</c>, <c>momo</c>). Body cần <c>orderId</c>
    /// hoặc <c>orderCode</c> (mã đơn trong nội dung giao dịch) + <c>amount</c> + <c>providerTransactionId</c>.
    /// <c>method</c> mặc định 2 (Cổng thanh toán); chỉ nhận 1 (Chuyển khoản) hoặc 2.
    /// </remarks>
    /// <response code="200">
    /// Ghi nhận thành công hoặc giao dịch đã tồn tại. <c>needsReview = true</c> khi tiền về cho đơn đã hủy
    /// hoặc thanh toán thừa — cần đối soát/hoàn tiền thủ công.
    /// </response>
    /// <response code="400">Thiếu tham số, số tiền không hợp lệ hoặc phương thức không dùng callback.</response>
    /// <response code="404">Không tìm thấy đơn hàng theo <c>orderId</c>/<c>orderCode</c>.</response>
    /// <response code="409">Không ghi nhận được giao dịch (giữ chỗ cho lỗi nghiệp vụ khác từ proc).</response>
    [HttpPost("callback/{provider}")]
    [ProducesResponseType(typeof(RecordedPaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RecordedPaymentDto>> Callback(
        string provider,
        [FromBody] PaymentCallbackRequest request,
        CancellationToken cancellationToken)
        => Ok(await _paymentService.HandleCallbackAsync(provider, request, cancellationToken));
}
