using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Features.Inventory;
using SiliconHorizon.Application.Features.Inventory.Dto;

namespace SiliconHorizon.Api.Controllers.Inventory;

/// <summary>
/// Chuyển kho nội bộ — POST /api/v1/stock-transfers, GET /{id}, POST /{id}/ship|receive|cancel
/// (Database/README.md mục 6, UC-14).
/// </summary>
/// <remarks>
/// Vòng đời: Nháp (0) → Đang chuyển (1) → Đã nhận (2); Nháp có thể bị hủy (3).
/// Hàng đang chuyển KHÔNG tính vào tồn của cả hai chi nhánh.
/// <para>
/// Chi nhánh ĐI ép từ token; <c>tobranchId</c> do client chọn. Xuất kho chỉ chi nhánh đi,
/// nhận hàng chỉ chi nhánh đến (Admin bỏ qua giới hạn chi nhánh).
/// </para>
/// </remarks>
[ApiController]
[Route("api/v1/stock-transfers")]
[Authorize]
[Produces("application/json")]
public class StockTransfersController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public StockTransfersController(IInventoryService inventoryService) => _inventoryService = inventoryService;

    /// <summary>
    /// POST /api/v1/stock-transfers — tạo phiếu chuyển kho ở trạng thái Nháp (usp_StockTransfer_Create).
    /// </summary>
    /// <remarks>
    /// Proc kiểm tra: chi nhánh đi khác chi nhánh đến, hàng quản lý serial phải liệt kê đủ serial
    /// đang <c>InStock</c> tại chi nhánh đi. Phiếu nháp CHƯA trừ tồn — chỉ trừ khi gọi <c>/ship</c>.
    /// Quyền: TK (chi nhánh đi).
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = InventoryRoles.StockOperations)]
    [ProducesResponseType(typeof(CreatedStockTransferDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreatedStockTransferDto>> Create(
        [FromBody] CreateStockTransferRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _inventoryService.CreateStockTransferAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = created.TransferId }, created);
    }

    /// <summary>
    /// GET /api/v1/stock-transfers/{id} — chi tiết phiếu chuyển (usp_StockTransfer_Get: header, dòng hàng, serial).
    /// </summary>
    /// <remarks>Chỉ xem được phiếu của chi nhánh đi hoặc chi nhánh đến (Admin xem tất cả). Quyền: TK, QL, AD.</remarks>
    [HttpGet("{id:long}")]
    [Authorize(Roles = InventoryRoles.StockDesk)]
    [ProducesResponseType(typeof(StockTransferDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockTransferDetailDto>> GetById(
        long id,
        CancellationToken cancellationToken)
        => Ok(await _inventoryService.GetStockTransferAsync(id, cancellationToken));

    /// <summary>
    /// POST /api/v1/stock-transfers/{id}/ship — xuất hàng đi (usp_StockTransfer_Ship).
    /// </summary>
    /// <remarks>
    /// Trừ tồn thực tế chi nhánh đi và chuyển serial InStock → InTransfer. Tồn khả dụng không đủ → 409.
    /// Chỉ phiếu Nháp mới xuất được. Quyền: TK (chi nhánh đi).
    /// </remarks>
    [HttpPost("{id:long}/ship")]
    [Authorize(Roles = InventoryRoles.StockOperations)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Ship(long id, CancellationToken cancellationToken)
    {
        await _inventoryService.ShipStockTransferAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// POST /api/v1/stock-transfers/{id}/receive — nhận hàng (usp_StockTransfer_Receive).
    /// </summary>
    /// <remarks>
    /// Cộng tồn thực tế chi nhánh đến, đổi <c>BranchId</c> của serial và chuyển serial InTransfer → InStock.
    /// Chỉ phiếu Đang chuyển mới nhận được. Quyền: TK (chi nhánh đến).
    /// </remarks>
    [HttpPost("{id:long}/receive")]
    [Authorize(Roles = InventoryRoles.StockOperations)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Receive(long id, CancellationToken cancellationToken)
    {
        await _inventoryService.ReceiveStockTransferAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// POST /api/v1/stock-transfers/{id}/cancel — hủy phiếu nháp (usp_StockTransfer_Cancel).
    /// </summary>
    /// <remarks>Phiếu đã xuất kho (Đang chuyển/Đã nhận) không hủy được → 409. Quyền: TK, QL.</remarks>
    [HttpPost("{id:long}/cancel")]
    [Authorize(Roles = InventoryRoles.StockDesk)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken)
    {
        await _inventoryService.CancelStockTransferAsync(id, cancellationToken);

        return NoContent();
    }
}
