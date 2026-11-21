using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Features.Inventory;
using SiliconHorizon.Application.Features.Inventory.Dto;

namespace SiliconHorizon.Api.Controllers.Inventory;

/// <summary>
/// Trả hàng nhà cung cấp — POST /api/v1/supplier-returns (Database/README.md mục 6, SUP-02).
/// </summary>
/// <remarks>
/// Chi nhánh trả hàng ép từ token. Serial trả phải đang ở trạng thái Defective (7) tại chi nhánh đó;
/// hàng không quản lý serial thì proc trừ tồn thực tế. <c>creditAmount &gt; 0</c> sẽ ghi giảm công nợ phải trả.
/// </remarks>
[ApiController]
[Route("api/v1/supplier-returns")]
[Authorize]
[Produces("application/json")]
public class SupplierReturnsController : ControllerBase
{
    private readonly ISupplierService _supplierService;

    public SupplierReturnsController(ISupplierService supplierService) => _supplierService = supplierService;

    /// <summary>
    /// POST /api/v1/supplier-returns — tạo phiếu trả hàng nhà cung cấp (usp_SupplierReturn_Create).
    /// </summary>
    /// <remarks>
    /// Proc (một giao dịch): kiểm tra serial thuộc chi nhánh và đang lỗi, chuyển serial sang
    /// ReturnedToSupplier (8) + ghi <c>SerialStatusHistory</c>; hàng không serial bị trừ tồn thực tế với
    /// bút toán <c>StockMovements</c> loại Trả nhà cung cấp (9); nếu <c>creditAmount &gt; 0</c> thì ghi
    /// <c>SupplierLedger</c> EntryType 2 với số tiền âm.
    /// Quyền: TK, QL.
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = InventoryRoles.StockDesk)]
    [ProducesResponseType(typeof(CreatedSupplierReturnDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreatedSupplierReturnDto>> Create(
        [FromBody] CreateSupplierReturnRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _supplierService.CreateSupplierReturnAsync(request, cancellationToken);

        // Chưa có endpoint GET chi tiết phiếu trả nên trả 201 kèm mã phiếu để client hiển thị kết quả.
        return StatusCode(StatusCodes.Status201Created, created);
    }
}
