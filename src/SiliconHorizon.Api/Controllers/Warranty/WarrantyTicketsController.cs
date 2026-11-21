using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Warranty;
using SiliconHorizon.Application.Features.Warranty.Dto;

namespace SiliconHorizon.Api.Controllers.Warranty;

/// <summary>
/// Phiếu bảo hành — GET/POST /api/v1/warranty-tickets (Database/README.md mục 7.1, WAR-04..WAR-07, UC-16/UC-17).
/// </summary>
/// <remarks>
/// Controller mỏng: chỉ nhận tham số và gọi service. Mọi quy tắc dữ liệu nằm ở
/// <c>SiliconHorizon.Application.Features.Warranty</c>:
/// <list type="bullet">
/// <item>API LUÔN ép <c>branchId</c> (nhân viên) và <c>customerId</c> (khách) từ token — endpoint không nhận
/// hai giá trị này từ client; tài khoản nhân viên chưa gắn chi nhánh → 403.</item>
/// <item>Vượt phạm vi → 403; phiếu/khách không tồn tại → 404; chuyển trạng thái sai (proc báo 50002) → 409.</item>
/// </list>
/// </remarks>
[ApiController]
[Route("api/v1/warranty-tickets")]
[Authorize(Roles = WarrantyRoles.Viewers)]
[Produces("application/json")]
public class WarrantyTicketsController : ControllerBase
{
    private readonly IWarrantyService _warrantyService;

    public WarrantyTicketsController(IWarrantyService warrantyService) => _warrantyService = warrantyService;

    /// <summary>
    /// POST /api/v1/warranty-tickets — tiếp nhận bảo hành (usp_Warranty_Receive).
    /// </summary>
    /// <remarks>
    /// Hàng có serial: truyền <c>serialNumber</c> (serial phải đang Sold); hàng không serial: truyền
    /// <c>orderItemId</c> của đơn đã hoàn tất. Hết hạn bảo hành (WAR-05) hoặc có <c>rejectReason</c> thì proc
    /// tạo phiếu ở trạng thái Rejected (5) và trả về kèm <c>rejectionReason</c> — khi đó <c>rejected = true</c>.
    /// Quyền: TN, TV, AD.
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = WarrantyRoles.Receive)]
    [ProducesResponseType(typeof(ReceivedWarrantyTicketDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReceivedWarrantyTicketDto>> Receive(
        [FromBody] ReceiveWarrantyTicketRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _warrantyService.ReceiveAsync(request, cancellationToken);

        return Created($"/api/v1/warranty-tickets/{created.TicketId}", created);
    }

    /// <summary>
    /// GET /api/v1/warranty-tickets — danh sách phiếu (usp_Warranty_List).
    /// </summary>
    /// <remarks>
    /// Nhân viên xem phiếu của chi nhánh mình (Admin xem toàn hệ thống), khách chỉ xem phiếu của mình.
    /// Lọc theo <c>status</c>, <c>keyword</c> (mã phiếu / số serial khớp đầu, SĐT khách) và phân trang
    /// <c>pageNumber</c>, <c>pageSize</c> (tối đa 100).
    /// Quyền: NV, KH, AD.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<WarrantyTicketListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<WarrantyTicketListItemDto>>> List(
        [FromQuery] WarrantyTicketListQuery query,
        CancellationToken cancellationToken)
        => Ok(await _warrantyService.ListAsync(query, cancellationToken));

    /// <summary>
    /// GET /api/v1/warranty-tickets/{id} — chi tiết phiếu kèm lịch sử trạng thái và đính kèm (usp_Warranty_Get).
    /// Quyền: NV, KH (phiếu của mình), AD.
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(WarrantyTicketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WarrantyTicketDetailDto>> Get(long id, CancellationToken cancellationToken)
        => Ok(await _warrantyService.GetAsync(id, cancellationToken));

    /// <summary>
    /// POST /api/v1/warranty-tickets/{id}/status — chuyển trạng thái phiếu (usp_Warranty_ChangeStatus).
    /// </summary>
    /// <remarks>
    /// Đích hợp lệ: 1 Chẩn đoán, 2 Gửi hãng, 3 Sửa xong, 4 Đổi mới (bắt buộc kèm <c>replacementSerialNumber</c>),
    /// 5 Từ chối (bắt buộc kèm <c>rejectionReason</c>), 6 Trả khách. Cặp chuyển đổi sai → 409.
    /// Quyền: TK, TV, AD.
    /// </remarks>
    [HttpPost("{id:long}/status")]
    [Authorize(Roles = WarrantyRoles.Process)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeStatus(
        long id,
        [FromBody] ChangeWarrantyStatusRequest request,
        CancellationToken cancellationToken)
    {
        await _warrantyService.ChangeStatusAsync(id, request, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// POST /api/v1/warranty-tickets/{id}/attachments — đính kèm ảnh/tài liệu (usp_Warranty_AddAttachment).
    /// Quyền: NV (mọi nhân viên), AD.
    /// </summary>
    [HttpPost("{id:long}/attachments")]
    [Authorize(Roles = WarrantyRoles.Attach)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddAttachment(
        long id,
        [FromBody] AddWarrantyAttachmentRequest request,
        CancellationToken cancellationToken)
    {
        await _warrantyService.AddAttachmentAsync(id, request, cancellationToken);

        return NoContent();
    }
}