using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Features.Catalog;
using SiliconHorizon.Application.Features.Catalog.Dto;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Api.Controllers.Admin;

/// <summary>
/// Quản trị sản phẩm (PRD-01..09, PRC-01/06/07) — Database/README.md mục 3.2.
/// <para>
/// Quyền theo vai trò tại từng action (không đặt ở cấp class, vì endpoint giá cho phép cả Quản lý chi nhánh).
/// Khi đã seed bảng Permissions, siết thêm <c>[RequirePermission(PermissionCodes.CatalogManage)]</c>.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/admin/products")]
[Authorize]
[Produces("application/json")]
public class AdminProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public AdminProductsController(IProductService productService) => _productService = productService;

    /// <summary>POST /api/v1/admin/products — tạo sản phẩm kèm thông số theo loại và ảnh (UC-25).</summary>
    [HttpPost]
    [Authorize(Roles = RoleCodes.Admin)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var productId = await _productService.CreateAsync(request, cancellationToken);

        // Trả về 201 kèm đường dẫn chi tiết sản phẩm vừa tạo.
        return Created($"/api/v1/products/{productId}", new { productId });
    }

    /// <summary>PUT /api/v1/admin/products/{id} — sửa sản phẩm (không đổi danh mục), thay toàn bộ thông số/ảnh.</summary>
    [HttpPut("{id:long}")]
    [Authorize(Roles = RoleCodes.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(
        long id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        await _productService.UpdateAsync(id, request, cancellationToken);

        return NoContent();
    }

    /// <summary>PATCH /api/v1/admin/products/{id}/status — 0 Draft, 1 Active, 2 Discontinued.</summary>
    [HttpPatch("{id:long}/status")]
    [Authorize(Roles = RoleCodes.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetStatus(
        long id,
        [FromBody] SetProductStatusRequest request,
        CancellationToken cancellationToken)
    {
        await _productService.SetStatusAsync(id, request, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// PUT /api/v1/admin/products/{id}/prices/{groupId} — đặt giá theo nhóm khách.
    /// Giá thấp hơn giá vốn chỉ được lưu khi người gọi là Quản lý chi nhánh/Admin (proc kiểm tra).
    /// </summary>
    [HttpPut("{id:long}/prices/{groupId:int}")]
    [Authorize(Roles = RoleCodes.Approvers)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpsertPrice(
        long id,
        int groupId,
        [FromBody] UpsertProductPriceRequest request,
        CancellationToken cancellationToken)
    {
        await _productService.UpsertPriceAsync(id, groupId, request, cancellationToken);

        return NoContent();
    }
}
