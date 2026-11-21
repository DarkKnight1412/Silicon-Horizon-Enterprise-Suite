using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Inventory;
using SiliconHorizon.Application.Features.Inventory.Dto;

namespace SiliconHorizon.Api.Controllers.Inventory;

/// <summary>
/// Nhập kho — POST /api/v1/purchase-receipts, GET /api/v1/purchase-receipts[/{id}]
/// (Database/README.md mục 6).
/// </summary>
/// <remarks>
/// Chi nhánh nhận hàng luôn là chi nhánh của thủ kho trong token (không có <c>branchId</c> trong request).
/// Nhân viên chỉ xem được phiếu nhập của chi nhánh mình; Admin xem tất cả.
/// </remarks>
[ApiController]
[Route("api/v1/purchase-receipts")]
[Authorize]
[Produces("application/json")]
public class PurchaseReceiptsController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public PurchaseReceiptsController(IInventoryService inventoryService) => _inventoryService = inventoryService;

    /// <summary>
    /// POST /api/v1/purchase-receipts — nhập kho kèm danh sách serial (usp_PurchaseReceipt_Create, UC-13).
    /// </summary>
    /// <remarks>
    /// Proc tạo phiếu ở trạng thái Hoàn tất trong MỘT giao dịch: tạo phiếu + dòng hàng, sinh serial
    /// (hàng <c>requiresSerial</c> phải kèm đủ serial, không trùng), cộng tồn thực tế, ghi bút toán
    /// <c>StockMovements</c> loại Nhập kho và ghi tăng công nợ phải trả theo <c>PaymentTermDays</c> của nhà cung cấp.
    /// <para>Lỗi nghiệp vụ: serial trùng/đã tồn tại → 409; thiếu serial hoặc số lượng/đơn giá sai → 400.</para>
    /// Quyền: TK (Admin thực hiện được thay).
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = InventoryRoles.StockOperations)]
    [ProducesResponseType(typeof(CreatedPurchaseReceiptDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreatedPurchaseReceiptDto>> Create(
        [FromBody] CreatePurchaseReceiptRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _inventoryService.CreatePurchaseReceiptAsync(request, cancellationToken);

        // Trả 201 kèm mã phiếu để client mở chi tiết tại GET /api/v1/purchase-receipts/{id}.
        return CreatedAtAction(nameof(GetById), new { id = created.ReceiptId }, created);
    }

    /// <summary>
    /// GET /api/v1/purchase-receipts — danh sách phiếu nhập của chi nhánh (usp_PurchaseReceipt_List).
    /// </summary>
    /// <remarks>
    /// Lọc theo <c>supplierId</c>, khoảng <c>fromDate</c>/<c>toDate</c> (UTC, chặn trên không bao gồm).
    /// Phân trang <c>pageNumber</c>/<c>pageSize</c> (proc cắt tối đa 100). Quyền: TK, QL, AD.
    /// </remarks>
    [HttpGet]
    [Authorize(Roles = InventoryRoles.StockDesk)]
    [ProducesResponseType(typeof(PagedResult<PurchaseReceiptListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<PurchaseReceiptListItemDto>>> List(
        [FromQuery] PurchaseReceiptListQuery query,
        CancellationToken cancellationToken)
        => Ok(await _inventoryService.ListPurchaseReceiptsAsync(query, cancellationToken));

    /// <summary>
    /// GET /api/v1/purchase-receipts/{id} — chi tiết phiếu nhập kèm serial đã sinh
    /// (usp_PurchaseReceipt_Get: header, dòng hàng, serial).
    /// </summary>
    /// <remarks>Phiếu của chi nhánh khác → 403; không tồn tại → 404. Quyền: TK, QL, AD.</remarks>
    [HttpGet("{id:long}")]
    [Authorize(Roles = InventoryRoles.StockDesk)]
    [ProducesResponseType(typeof(PurchaseReceiptDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PurchaseReceiptDetailDto>> GetById(
        long id,
        CancellationToken cancellationToken)
        => Ok(await _inventoryService.GetPurchaseReceiptAsync(id, cancellationToken));
}
