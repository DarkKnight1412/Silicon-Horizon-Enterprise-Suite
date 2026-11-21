using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Inventory;
using SiliconHorizon.Application.Features.Inventory.Dto;

namespace SiliconHorizon.Api.Controllers.Inventory;

/// <summary>
/// Tồn kho — GET /api/v1/stock, PUT /api/v1/stock/min, GET /api/v1/stock/movements
/// (Database/README.md mục 6).
/// </summary>
/// <remarks>
/// API ép <c>@BranchId</c> từ token (BRD 2.2) và KHÔNG nhận <c>branchId</c> từ query string:
/// <list type="bullet">
/// <item>Nhân viên không gắn chi nhánh → 403.</item>
/// <item>Admin không gắn chi nhánh: xem tồn toàn hệ thống được, nhưng thẻ kho (proc bắt buộc có chi nhánh) → 403.</item>
/// </list>
/// </remarks>
[ApiController]
[Route("api/v1/stock")]
[Authorize]
[Produces("application/json")]
public class StockController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public StockController(IInventoryService inventoryService) => _inventoryService = inventoryService;

    /// <summary>
    /// GET /api/v1/stock — tồn theo chi nhánh/sản phẩm (usp_Stock_GetLevels).
    /// </summary>
    /// <remarks>
    /// Physical = số đơn vị đang ở kho, Reserved = đang giữ cho đơn chưa hoàn tất, Available = Physical - Reserved.
    /// <c>belowMinOnly=true</c> dùng cho cảnh báo tồn tối thiểu (INV-02/UC-34).
    /// <c>keyword</c> tìm theo tên sản phẩm (LIKE) hoặc đầu mã SKU, tối đa 100 ký tự.
    /// Quyền: TK, QL, AD.
    /// </remarks>
    [HttpGet]
    [Authorize(Roles = InventoryRoles.StockDesk)]
    [ProducesResponseType(typeof(PagedResult<StockLevelDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<StockLevelDto>>> GetLevels(
        [FromQuery] StockLevelQuery query,
        CancellationToken cancellationToken)
        => Ok(await _inventoryService.GetStockLevelsAsync(query, cancellationToken));

    /// <summary>
    /// PUT /api/v1/stock/min — đặt mức tồn tối thiểu theo chi nhánh + sản phẩm (usp_Stock_SetMin).
    /// </summary>
    /// <remarks>
    /// <c>minStock = null</c> để quay về dùng mặc định của sản phẩm (<c>Products.MinStockDefault</c>);
    /// giá trị âm bị từ chối (400). Quyền: QL, AD.
    /// </remarks>
    [HttpPut("min")]
    [Authorize(Roles = InventoryRoles.Approvers)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SetMinStock(
        [FromBody] SetMinStockRequest request,
        CancellationToken cancellationToken)
    {
        await _inventoryService.SetMinStockAsync(request, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// GET /api/v1/stock/movements — thẻ kho của một sản phẩm tại chi nhánh của người gọi
    /// (usp_Stock_GetMovements, INV-03).
    /// </summary>
    /// <remarks>
    /// <c>StockMovements</c> là sổ bất biến (append-only) nên chỉ có API đọc, không có API sửa/xóa bút toán.
    /// <c>fromDate</c>/<c>toDate</c> là UTC và <c>toDate</c> là chặn trên KHÔNG bao gồm.
    /// Quyền: TK, QL, AD (Admin phải gắn chi nhánh vì proc yêu cầu <c>@BranchId</c>).
    /// </remarks>
    [HttpGet("movements")]
    [Authorize(Roles = InventoryRoles.StockDesk)]
    [ProducesResponseType(typeof(PagedResult<StockMovementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<StockMovementDto>>> GetMovements(
        [FromQuery] StockMovementQuery query,
        CancellationToken cancellationToken)
        => Ok(await _inventoryService.GetStockMovementsAsync(query, cancellationToken));
}
