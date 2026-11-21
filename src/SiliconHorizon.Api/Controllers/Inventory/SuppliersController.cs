using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Inventory;
using SiliconHorizon.Application.Features.Inventory.Dto;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Api.Controllers.Inventory;

/// <summary>
/// Nhà cung cấp — /api/v1/suppliers (Database/README.md mục 6 + nghiệp vụ SUP-01..03).
/// </summary>
/// <remarks>
/// <c>GET /api/v1/suppliers</c> và <c>POST /api/v1/suppliers</c> KHÔNG có trong bảng route của
/// Database/README.md mục 6 (bảng đó chỉ liệt kê <c>/supplier-returns</c>, <c>/{id}/payments</c>,
/// <c>/{id}/ledger</c>). Danh mục nhà cung cấp là bảng cấu hình nên được bổ sung theo đúng quy ước
/// "EF <c>Suppliers</c>" trong <c>src/SiliconHorizon.Application/Features/README.md</c>:
/// GET (TK, QL, AD), POST/PUT (AD — dữ liệu chủ như sản phẩm ở mục 3.2).
/// </remarks>
[ApiController]
[Route("api/v1/suppliers")]
[Authorize]
[Produces("application/json")]
public class SuppliersController : ControllerBase
{
    private readonly ISupplierService _supplierService;

    public SuppliersController(ISupplierService supplierService) => _supplierService = supplierService;

    /// <summary>
    /// GET /api/v1/suppliers — danh sách nhà cung cấp (phân trang, lọc theo mã/tên và trạng thái).
    /// </summary>
    /// <remarks>Quyền: TK, QL, AD (thủ kho cần danh sách để lập phiếu nhập).</remarks>
    [HttpGet]
    [Authorize(Roles = InventoryRoles.StockDesk)]
    [ProducesResponseType(typeof(PagedResult<SupplierDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<SupplierDto>>> List(
        [FromQuery] SupplierListQuery query,
        CancellationToken cancellationToken)
        => Ok(await _supplierService.ListAsync(query, cancellationToken));

    /// <summary>GET /api/v1/suppliers/{id} — chi tiết nhà cung cấp. Quyền: TK, QL, AD.</summary>
    [HttpGet("{id:long}")]
    [Authorize(Roles = InventoryRoles.StockDesk)]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupplierDto>> GetById(long id, CancellationToken cancellationToken)
        => Ok(await _supplierService.GetByIdAsync(id, cancellationToken));

    /// <summary>
    /// POST /api/v1/suppliers — tạo nhà cung cấp (SUP-01). Mã nhà cung cấp là duy nhất (409 nếu trùng).
    /// </summary>
    /// <remarks>
    /// <c>paymentTermDays</c> được proc nhập kho dùng để tính hạn trả (<c>SupplierLedger.DueDate</c>).
    /// Quyền: AD.
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = RoleCodes.Admin)]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SupplierDto>> Create(
        [FromBody] CreateSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _supplierService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// PUT /api/v1/suppliers/{id} — cập nhật thông tin/ngừng hợp tác nhà cung cấp (SUP-01).
    /// </summary>
    /// <remarks>
    /// Không đổi mã và không xóa cứng bản ghi đã phát sinh phiếu nhập/công nợ: đặt <c>isActive = false</c>
    /// thì <c>usp_PurchaseReceipt_Create</c> sẽ từ chối nhà cung cấp đó. Quyền: AD.
    /// </remarks>
    [HttpPut("{id:long}")]
    [Authorize(Roles = RoleCodes.Admin)]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupplierDto>> Update(
        long id,
        [FromBody] UpdateSupplierRequest request,
        CancellationToken cancellationToken)
        => Ok(await _supplierService.UpdateAsync(id, request, cancellationToken));

    /// <summary>
    /// POST /api/v1/suppliers/{id}/payments — ghi nhận thanh toán cho nhà cung cấp
    /// (usp_SupplierPayment_Record).
    /// </summary>
    /// <remarks>
    /// Proc khóa sổ công nợ và từ chối thanh toán vượt công nợ hiện tại (409). Trả về công nợ còn lại.
    /// Quyền: QL, AD.
    /// </remarks>
    [HttpPost("{id:long}/payments")]
    [Authorize(Roles = InventoryRoles.Approvers)]
    [ProducesResponseType(typeof(SupplierPaymentResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SupplierPaymentResultDto>> RecordPayment(
        long id,
        [FromBody] RecordSupplierPaymentRequest request,
        CancellationToken cancellationToken)
        => Ok(await _supplierService.RecordPaymentAsync(id, request, cancellationToken));

    /// <summary>
    /// GET /api/v1/suppliers/{id}/ledger — sao kê công nợ phải trả (usp_Supplier_GetLedger).
    /// </summary>
    /// <remarks>
    /// Trả <c>balance</c> (dương = còn nợ nhà cung cấp) và các dòng sổ:
    /// EntryType 0 Payable (tăng nợ), 1 Payment, 2 ReturnCredit (giảm nợ). Quyền: QL, AD.
    /// </remarks>
    [HttpGet("{id:long}/ledger")]
    [Authorize(Roles = InventoryRoles.Approvers)]
    [ProducesResponseType(typeof(SupplierLedgerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupplierLedgerDto>> GetLedger(
        long id,
        [FromQuery] SupplierLedgerQuery query,
        CancellationToken cancellationToken)
        => Ok(await _supplierService.GetLedgerAsync(id, query, cancellationToken));

}
