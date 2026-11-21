using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Features.Pricing;
using SiliconHorizon.Application.Features.Pricing.Dto;

namespace SiliconHorizon.Api.Controllers.Pricing;

/// <summary>
/// Endpoint công khai của module Pricing: kiểm tra mã giảm giá trước khi đặt hàng
/// (Database/README.md mục 3.1 — <c>POST /api/vouchers/validate</c>, quyền <b>KH</b>).
/// </summary>
/// <remarks>
/// Cho phép cả khách vãng lai (<c>[AllowAnonymous]</c>): khi đó <c>customerId</c> là <b>tùy chọn</b>
/// và <c>usp_Voucher_Validate</c> sẽ tự dùng nhóm khách mặc định (<c>CustomerGroups.IsDefault = 1</c>).
/// Khách đã đăng nhập thì <c>customerId</c> bị ép từ token — xem <c>VoucherService.ValidateAsync</c>.
/// </remarks>
[ApiController]
[Route("api/v1/vouchers")]
[AllowAnonymous]
[Produces("application/json")]
public class VouchersController : ControllerBase
{
    private readonly IVoucherService _voucherService;

    public VouchersController(IVoucherService voucherService) => _voucherService = voucherService;

    /// <summary>
    /// POST /api/v1/vouchers/validate — kiểm tra mã, trả về số tiền được giảm (usp_Voucher_Validate).
    /// </summary>
    /// <remarks>
    /// Mã sai vẫn trả 200 với <c>isValid = false</c> kèm <c>message</c> tiếng Việt (thông điệp của
    /// <c>fn_VoucherCheck</c>): ngoài thời hạn, hết lượt, sai nhóm khách, chưa đủ giá trị đơn tối thiểu,
    /// hoặc không dùng cùng sản phẩm đang khuyến mãi.
    /// </remarks>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(VoucherValidationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VoucherValidationDto>> Validate(
        [FromBody] ValidateVoucherRequest request,
        CancellationToken cancellationToken)
        => Ok(await _voucherService.ValidateAsync(request, cancellationToken));
}
