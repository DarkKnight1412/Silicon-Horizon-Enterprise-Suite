using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Api.Constants;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Orders;
using SiliconHorizon.Application.Features.Orders.Dto;

namespace SiliconHorizon.Api.Controllers.Payments;

/// <summary>
/// Công nợ khách hàng — GET /api/v1/customers/{id}/ledger (Database/README.md mục 5.3).
/// </summary>
/// <remarks>
/// Thuộc nhóm thanh toán vì sao kê công nợ chính là các bút toán sinh ra từ đơn mua chịu (method = 4) và
/// từ giao dịch thanh toán (bảng <c>CustomerLedger</c>).
/// </remarks>
[ApiController]
[Route("api/v1/customers")]
[Authorize(Roles = OrderRoleGroups.LedgerViewers)]
[Produces("application/json")]
public class CustomerLedgerController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public CustomerLedgerController(IPaymentService paymentService) => _paymentService = paymentService;

    /// <summary>
    /// GET /api/v1/customers/{id}/ledger — sao kê công nợ: số dư, hạn mức và danh sách bút toán
    /// (usp_Customer_GetLedger — 2 result set), có phân trang.
    /// </summary>
    /// <remarks>
    /// Khách chỉ xem được sao kê của chính mình (API ép <c>customerId</c> từ token, vượt phạm vi → 403);
    /// Quản lý chi nhánh và Admin xem được khách hàng bất kỳ — proc không có tham số lọc theo chi nhánh nên
    /// phạm vi chi nhánh của Quản lý không được áp dụng ở đây.
    /// Quyền: KH (của mình), QL, AD.
    /// </remarks>
    [HttpGet("{id:long}/ledger")]
    [ProducesResponseType(typeof(CustomerLedgerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerLedgerDto>> GetLedger(
        long id,
        [FromQuery] PageRequest request,
        CancellationToken cancellationToken)
        => Ok(await _paymentService.GetCustomerLedgerAsync(id, request, cancellationToken));
}
