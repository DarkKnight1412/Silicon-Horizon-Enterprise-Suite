using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Returns;
using SiliconHorizon.Application.Features.Returns.Dto;

namespace SiliconHorizon.Api.Controllers.Returns;

/// <summary>
/// Đổi trả — Database/README.md mục 7.2 (RET-01..05, UC-08/UC-18/UC-20).
/// </summary>
/// <remarks>
/// Controller mỏng: chỉ nhận tham số và gọi service. Mọi quy tắc dữ liệu nằm ở
/// <c>SiliconHorizon.Application.Features.Returns</c>:
/// <list type="bullet">
/// <item>Khách tạo yêu cầu bằng <c>POST /api/v1/orders/{id}/returns</c> (đúng route trong tài liệu);
/// API ép <c>customerId</c> từ token, chi nhánh do proc lấy theo đơn.</item>
/// <item>Vượt phạm vi → 403; không tồn tại → 404; sai bước / quá hạn đổi trả / số lượng vượt số đã mua → 409.</item>
/// </list>
/// </remarks>
[ApiController]
[Route("api/v1/returns")]
[Authorize(Roles = ReturnsRoles.Viewers)]
[Produces("application/json")]
public class ReturnsController : ControllerBase
{
    private readonly IReturnService _returnService;

    public ReturnsController(IReturnService returnService) => _returnService = returnService;

    /// <summary>
    /// POST /api/v1/orders/{id}/returns — khách tạo yêu cầu đổi trả cho đơn của mình (usp_Return_Create).
    /// </summary>
    /// <remarks>
    /// Chỉ áp dụng cho đơn đã hoàn tất (7 Completed / 9 PartiallyReturned) và trong hạn đổi trả
    /// (<c>SystemSettings['return.window.days']</c>, mặc định 7 ngày — quá hạn → 409). Yêu cầu được tạo ở trạng
    /// thái Requested (0) và trả kèm tiền hoàn dự kiến. Hàng quản lý theo serial phải liệt kê đủ serial.
    /// Quyền: KH.
    /// </remarks>
    [HttpPost("/api/v1/orders/{orderId:long}/returns")]
    [Authorize(Roles = ReturnsRoles.Create)]
    [ProducesResponseType(typeof(CreatedReturnDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreatedReturnDto>> Create(
        long orderId,
        [FromBody] CreateReturnRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _returnService.CreateAsync(orderId, request, cancellationToken);

        return Created($"/api/v1/returns/{created.ReturnRequestId}", created);
    }

    /// <summary>
    /// GET /api/v1/returns — danh sách yêu cầu đổi trả (usp_Return_List).
    /// </summary>
    /// <remarks>
    /// Nhân viên xem yêu cầu của chi nhánh mình (Admin xem toàn hệ thống), khách chỉ xem yêu cầu của mình.
    /// Lọc theo <c>status</c> và phân trang <c>pageNumber</c>, <c>pageSize</c> (tối đa 100).
    /// Quyền: NV, KH (của mình), AD.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ReturnListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<ReturnListItemDto>>> List(
        [FromQuery] ReturnListQuery query,
        CancellationToken cancellationToken)
        => Ok(await _returnService.ListAsync(query, cancellationToken));

    /// <summary>
    /// GET /api/v1/returns/{id} — chi tiết yêu cầu kèm các dòng hàng trả (usp_Return_Get).
    /// Quyền: NV, KH (yêu cầu của mình), AD.
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ReturnRequestDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReturnRequestDetailDto>> Get(long id, CancellationToken cancellationToken)
        => Ok(await _returnService.GetAsync(id, cancellationToken));

    /// <summary>
    /// POST /api/v1/returns/{id}/review — Quản lý duyệt hoặc từ chối (usp_Return_Review, RET-04).
    /// </summary>
    /// <remarks>
    /// Chỉ duyệt/từ chối được yêu cầu đang ở trạng thái Requested (proc báo 50002 → 409 nếu khác).
    /// <c>approve = false</c> bắt buộc kèm <c>note</c> (lý do từ chối) và được ghi nhật ký AUD-01.
    /// Quyền: QL, AD.
    /// </remarks>
    [HttpPost("{id:long}/review")]
    [Authorize(Roles = ReturnsRoles.Review)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Review(
        long id,
        [FromBody] ReviewReturnRequest request,
        CancellationToken cancellationToken)
    {
        await _returnService.ReviewAsync(id, request, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// POST /api/v1/returns/{id}/inspect — thủ kho nhận hàng và kiểm tra từng dòng/serial
    /// (usp_Return_Inspect, RET-03).
    /// </summary>
    /// <remarks>
    /// Chỉ thực hiện được với yêu cầu đã Approved. Phải nhập kết quả cho <b>tất cả</b> dòng của yêu cầu.
    /// Dòng đạt: serial → InStock (cộng tồn thực tế); dòng không đạt: serial → Defective (không vào tồn bán được).
    /// Sau thao tác này yêu cầu chuyển sang GoodsReceived (3) và mới hoàn tiền được.
    /// Quyền: TK, AD.
    /// </remarks>
    [HttpPost("{id:long}/inspect")]
    [Authorize(Roles = ReturnsRoles.Inspect)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Inspect(
        long id,
        [FromBody] InspectReturnRequest request,
        CancellationToken cancellationToken)
    {
        await _returnService.InspectAsync(id, request, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// POST /api/v1/returns/{id}/refund — hoàn tiền, cập nhật đơn và thu hồi hoa hồng
    /// (usp_Return_Refund, RET-05).
    /// </summary>
    /// <remarks>
    /// Chỉ hoàn tiền sau khi hàng đã được nhận &amp; kiểm tra (GoodsReceived). Số tiền hoàn không vượt quá số
    /// dự kiến của yêu cầu; phần vượt quá số đã thu chỉ được cấn trừ công nợ với đơn mua chịu.
    /// Đơn chuyển sang PartiallyReturned (9) hoặc Returned (10); hoa hồng của đơn bị <b>thu hồi theo tỷ lệ</b>
    /// số tiền đã hoàn (bút toán Clawback — KPI-04).
    /// Quyền: QL, AD.
    /// </remarks>
    [HttpPost("{id:long}/refund")]
    [Authorize(Roles = ReturnsRoles.Refund)]
    [ProducesResponseType(typeof(ReturnRefundResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReturnRefundResultDto>> Refund(
        long id,
        [FromBody] RefundReturnRequest request,
        CancellationToken cancellationToken)
        => Ok(await _returnService.RefundAsync(id, request, cancellationToken));
}