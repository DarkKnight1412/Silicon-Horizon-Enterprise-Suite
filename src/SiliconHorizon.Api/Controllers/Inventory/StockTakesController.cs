using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Features.Inventory;
using SiliconHorizon.Application.Features.Inventory.Dto;

namespace SiliconHorizon.Api.Controllers.Inventory;

/// <summary>
/// Kiểm kê — POST /api/v1/stock-takes, POST /{id}/counts, POST /{id}/approve, GET /{id}
/// (Database/README.md mục 6, UC-15/UC-21).
/// </summary>
/// <remarks>
/// Vòng đời: Đang kiểm kê (1) → Chờ duyệt (2) → Đã duyệt (3) / Đã từ chối (4).
/// Đợt kiểm kê luôn thuộc chi nhánh của người mở; chỉ chi nhánh đó (hoặc Admin) thao tác được.
/// </remarks>
[ApiController]
[Route("api/v1/stock-takes")]
[Authorize]
[Produces("application/json")]
public class StockTakesController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public StockTakesController(IInventoryService inventoryService) => _inventoryService = inventoryService;

    /// <summary>
    /// POST /api/v1/stock-takes — mở đợt kiểm kê cho chi nhánh trong token (usp_StockTake_Create).
    /// </summary>
    /// <remarks>
    /// Không có tham số: chi nhánh ép từ token. Proc sinh mã <c>KK...</c> và đặt trạng thái Đang kiểm kê.
    /// Quyền: TK.
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = InventoryRoles.StockOperations)]
    [ProducesResponseType(typeof(CreatedStockTakeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CreatedStockTakeDto>> Create(CancellationToken cancellationToken)
    {
        var created = await _inventoryService.CreateStockTakeAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = created.StockTakeId }, created);
    }

    /// <summary>
    /// POST /api/v1/stock-takes/{id}/counts — nhập số đếm, có thể gửi duyệt luôn
    /// (usp_StockTake_SubmitCounts).
    /// </summary>
    /// <remarks>
    /// <c>finalize = true</c> để chuyển sang Chờ duyệt (khóa nhập tiếp). Proc ghi <c>SystemQty</c> theo tồn
    /// hiện tại nên nên kiểm kê lúc ít phát sinh xuất/nhập. Tồn CHƯA thay đổi ở bước này.
    /// Quyền: TK (chi nhánh sở hữu đợt kiểm kê).
    /// </remarks>
    [HttpPost("{id:long}/counts")]
    [Authorize(Roles = InventoryRoles.StockOperations)]
    [ProducesResponseType(typeof(IReadOnlyList<StockTakeCountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IReadOnlyList<StockTakeCountDto>>> SubmitCounts(
        long id,
        [FromBody] SubmitStockTakeCountsRequest request,
        CancellationToken cancellationToken)
        => Ok(await _inventoryService.SubmitStockTakeCountsAsync(id, request, cancellationToken));

    /// <summary>
    /// POST /api/v1/stock-takes/{id}/approve — duyệt (áp chênh lệch vào tồn) hoặc từ chối
    /// (usp_StockTake_Approve, INV-04).
    /// </summary>
    /// <remarks>
    /// <b>Quy tắc lệch hàng serial</b>: nếu có dòng <c>requiresSerial</c> mà <c>diffQty ≠ 0</c>, proc từ chối
    /// duyệt và yêu cầu xử lý TỪNG SERIAL bằng <c>POST /api/v1/serials/{id}/status</c> rồi kiểm kê lại —
    /// API không tự bù số lượng cho hàng serial. Mọi lần duyệt/từ chối đều ghi nhật ký AUD-01 kèm ghi chú.
    /// Quyền: QL (Admin thực hiện được thay).
    /// </remarks>
    [HttpPost("{id:long}/approve")]
    [Authorize(Roles = InventoryRoles.Approvers)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(
        long id,
        [FromBody] ApproveStockTakeRequest request,
        CancellationToken cancellationToken)
    {
        await _inventoryService.ApproveStockTakeAsync(id, request, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// GET /api/v1/stock-takes/{id} — chi tiết đợt kiểm kê kèm chênh lệch từng dòng (usp_StockTake_Get).
    /// </summary>
    /// <remarks>Quyền: TK, QL (chi nhánh sở hữu đợt kiểm kê), AD.</remarks>
    [HttpGet("{id:long}")]
    [Authorize(Roles = InventoryRoles.StockDesk)]
    [ProducesResponseType(typeof(StockTakeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockTakeDetailDto>> GetById(
        long id,
        CancellationToken cancellationToken)
        => Ok(await _inventoryService.GetStockTakeAsync(id, cancellationToken));
}
