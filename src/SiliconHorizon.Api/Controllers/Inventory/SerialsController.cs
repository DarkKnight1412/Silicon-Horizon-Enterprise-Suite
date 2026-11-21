using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Features.Inventory;
using SiliconHorizon.Application.Features.Inventory.Dto;

namespace SiliconHorizon.Api.Controllers.Inventory;

/// <summary>
/// Serial — GET /api/v1/serials/{serialNumber}, POST /api/v1/serials/{id}/status
/// (Database/README.md mục 6, SER-01..04).
/// </summary>
/// <remarks>
/// Khách hàng chỉ tra cứu được serial thuộc đơn của mình: API ép <c>@CustomerId</c> từ token,
/// KHÔNG nhận <c>customerId</c> từ query string.
/// </remarks>
[ApiController]
[Route("api/v1/serials")]
[Authorize]
[Produces("application/json")]
public class SerialsController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public SerialsController(IInventoryService inventoryService) => _inventoryService = inventoryService;

    /// <summary>
    /// GET /api/v1/serials/{serialNumber} — tra cứu serial: sản phẩm, bảo hành, đơn mua, lịch sử trạng thái
    /// (usp_Serial_Lookup, UC-07/WAR-02).
    /// </summary>
    /// <remarks>
    /// <c>productId</c> là tùy chọn, dùng khi cùng một số serial tồn tại ở nhiều sản phẩm (proc báo 400 nếu trùng
    /// mà không truyền). Quyền: NV, KH (hàng của mình), AD.
    /// </remarks>
    [HttpGet("{serialNumber}")]
    [Authorize(Roles = InventoryRoles.SerialLookup)]
    [ProducesResponseType(typeof(SerialLookupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SerialLookupDto>> Lookup(
        string serialNumber,
        [FromQuery] long? productId,
        CancellationToken cancellationToken)
        => Ok(await _inventoryService.LookupSerialAsync(serialNumber, productId, cancellationToken));

    /// <summary>
    /// POST /api/v1/serials/{id}/status — đổi trạng thái serial thủ công (usp_Serial_ChangeStatus).
    /// </summary>
    /// <remarks>
    /// Chỉ 3 chuyển đổi được phép: InStock(0) → Defective(7), Defective(7) → Scrapped(9),
    /// Defective(7) → InStock(0). Rời/vào InStock thì tồn thực tế của chi nhánh giảm/tăng 1 và
    /// <c>StockMovements</c> ghi bút toán Điều chỉnh thủ công (append-only). Bắt buộc có <c>reason</c>.
    /// <para>
    /// <c>{id}</c> là <c>SerialItemId</c> (lấy từ GET /serials/{serialNumber} hoặc danh sách serial của phiếu nhập).
    /// Quyền: QL, AD.
    /// </para>
    /// </remarks>
    [HttpPost("{id:long}/status")]
    [Authorize(Roles = InventoryRoles.Approvers)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeStatus(
        long id,
        [FromBody] ChangeSerialStatusRequest request,
        CancellationToken cancellationToken)
    {
        await _inventoryService.ChangeSerialStatusAsync(id, request, cancellationToken);

        return NoContent();
    }
}
