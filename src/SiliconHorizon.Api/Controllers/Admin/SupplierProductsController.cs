using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Features.Admin;
using SiliconHorizon.Application.Features.Admin.Dto;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Api.Controllers.Admin;

/// <summary>
/// CRUD liên kết nhà cung cấp ↔ sản phẩm — bảng nối <c>dbo.SupplierProducts</c>
/// (Database/README.md mục 10, dòng "Nhà cung cấp": <c>suppliers/{id}/products</c>).
/// </summary>
/// <remarks>
/// Quyền: chỉ <b>Admin</b>. Danh mục nhà cung cấp (<c>/api/v1/suppliers</c>) đã có ở module Inventory;
/// endpoint ở đây hoàn thiện phần bảng nối (mã SKU theo nhà cung cấp) để lập phiếu nhập đối chiếu được.
/// </remarks>
[ApiController]
[Route("api/v1/admin/suppliers/{supplierId:long}/products")]
[Authorize(Roles = RoleCodes.Admin)]
[Produces("application/json")]
public class SupplierProductsController : ControllerBase
{
    private readonly ISupplierProductService _supplierProductService;

    public SupplierProductsController(ISupplierProductService supplierProductService)
        => _supplierProductService = supplierProductService;

    /// <summary>GET /api/v1/admin/suppliers/{supplierId}/products — sản phẩm đang gắn với nhà cung cấp.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SupplierProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<SupplierProductDto>>> GetAll(
        long supplierId,
        CancellationToken cancellationToken)
        => Ok(await _supplierProductService.GetBySupplierAsync(supplierId, cancellationToken));

    /// <summary>GET /api/v1/admin/suppliers/{supplierId}/products/{productId}</summary>
    [HttpGet("{productId:long}")]
    [ProducesResponseType(typeof(SupplierProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupplierProductDto>> GetById(
        long supplierId,
        long productId,
        CancellationToken cancellationToken)
        => Ok(await _supplierProductService.GetByIdAsync(supplierId, productId, cancellationToken));

    /// <summary>POST /api/v1/admin/suppliers/{supplierId}/products — trùng cặp khóa (supplierId, productId) trả 409.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(SupplierProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SupplierProductDto>> Create(
        long supplierId,
        [FromBody] UpsertSupplierProductRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _supplierProductService.CreateAsync(supplierId, request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { supplierId, productId = created.ProductId }, created);
    }

    /// <summary>PUT /api/v1/admin/suppliers/{supplierId}/products/{productId} — sửa mã SKU nhà cung cấp.</summary>
    [HttpPut("{productId:long}")]
    [ProducesResponseType(typeof(SupplierProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupplierProductDto>> Update(
        long supplierId,
        long productId,
        [FromBody] UpsertSupplierProductRequest request,
        CancellationToken cancellationToken)
        => Ok(await _supplierProductService.UpdateAsync(supplierId, productId, request, cancellationToken));

    /// <summary>DELETE /api/v1/admin/suppliers/{supplierId}/products/{productId} — gỡ liên kết.</summary>
    [HttpDelete("{productId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        long supplierId,
        long productId,
        CancellationToken cancellationToken)
    {
        await _supplierProductService.DeleteAsync(supplierId, productId, cancellationToken);

        return NoContent();
    }
}
