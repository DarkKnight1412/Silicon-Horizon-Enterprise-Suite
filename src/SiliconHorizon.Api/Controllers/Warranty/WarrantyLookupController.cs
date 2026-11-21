using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Features.Inventory;
using SiliconHorizon.Application.Features.Inventory.Dto;

namespace SiliconHorizon.Api.Controllers.Warranty;

/// <summary>
/// Tra cứu bảo hành theo serial — GET /api/v1/warranty/lookup?serial= (Database/README.md mục 7.1, WAR-02).
/// </summary>
/// <remarks>
/// Route này dùng đúng một stored procedure là <c>usp_Serial_Lookup</c> — cùng proc với
/// <c>GET /api/v1/serials/{serialNumber}</c> của module Kho/Serial (Database/README.md mục 6).
/// Vì vậy controller chỉ là lớp mỏng tái sử dụng <see cref="IInventoryService"/> thay vì viết lại logic tra cứu,
/// và dùng chung nhóm quyền <see cref="InventoryRoles.SerialLookup"/> (NV, KH, AD).
/// <para>
/// Khách hàng chỉ tra cứu được serial thuộc đơn của mình: API ép <c>@CustomerId</c> từ token bên trong service,
/// KHÔNG nhận <c>customerId</c> từ query string.
/// </para>
/// </remarks>
[ApiController]
[Route("api/v1/warranty")]
[Authorize]
[Produces("application/json")]
public class WarrantyLookupController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public WarrantyLookupController(IInventoryService inventoryService) => _inventoryService = inventoryService;

    /// <summary>
    /// GET /api/v1/warranty/lookup?serial= — tra cứu tình trạng bảo hành của một serial (usp_Serial_Lookup).
    /// </summary>
    /// <remarks>
    /// Trả về thông tin serial (ngày bán, hạn bảo hành, số ngày còn lại), lịch sử trạng thái và các phiếu bảo hành
    /// đã lập cho serial đó. <c>productId</c> là tùy chọn, dùng khi cùng một số serial tồn tại ở nhiều sản phẩm
    /// (proc báo 400 nếu trùng mà không truyền).
    /// Quyền: NV, KH (hàng của mình), AD.
    /// </remarks>
    [HttpGet("lookup")]
    [Authorize(Roles = InventoryRoles.SerialLookup)]
    [ProducesResponseType(typeof(SerialLookupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SerialLookupDto>> Lookup(
        [FromQuery] string? serial,
        [FromQuery] long? productId,
        CancellationToken cancellationToken)
        => Ok(await _inventoryService.LookupSerialAsync(serial ?? string.Empty, productId, cancellationToken));
}