using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Admin;
using SiliconHorizon.Application.Features.Admin.Dto;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Api.Controllers.Admin;

/// <summary>
/// CRUD thương hiệu — <c>dbo.Brands</c> (Database/README.md mục 10: <c>/api/admin/brands</c>).
/// Danh sách công khai cho web khách hàng đã có ở <c>GET /api/v1/categories</c>, <c>GET /api/v1/brands</c>.
/// </summary>
/// <remarks>Quyền: chỉ <b>Admin</b>. "Xóa" = PATCH trạng thái, không xóa cứng (thương hiệu đã gắn sản phẩm).</remarks>
[ApiController]
[Route("api/v1/admin/brands")]
[Authorize(Roles = RoleCodes.Admin)]
[Produces("application/json")]
public class BrandsController : ControllerBase
{
    private readonly IBrandService _brandService;

    public BrandsController(IBrandService brandService) => _brandService = brandService;

    /// <summary>GET /api/v1/admin/brands?keyword=&amp;isActive=&amp;pageNumber=&amp;pageSize=</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<BrandDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<BrandDto>>> GetAll(
        [FromQuery] BrandListQuery query,
        CancellationToken cancellationToken)
        => Ok(await _brandService.GetPagedAsync(query, cancellationToken));

    /// <summary>GET /api/v1/admin/brands/{id}</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(BrandDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BrandDto>> GetById(int id, CancellationToken cancellationToken)
        => Ok(await _brandService.GetByIdAsync(id, cancellationToken));

    /// <summary>POST /api/v1/admin/brands — tên thương hiệu duy nhất (409 nếu trùng).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(BrandDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BrandDto>> Create(
        [FromBody] CreateBrandRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _brandService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>PUT /api/v1/admin/brands/{id} — đổi tên thương hiệu.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(BrandDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BrandDto>> Update(
        int id,
        [FromBody] UpdateBrandRequest request,
        CancellationToken cancellationToken)
        => Ok(await _brandService.UpdateAsync(id, request, cancellationToken));

    /// <summary>PATCH /api/v1/admin/brands/{id}/status — ngừng/cho dùng lại thương hiệu.</summary>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(
        int id,
        [FromBody] SetBrandStatusRequest request,
        CancellationToken cancellationToken)
    {
        await _brandService.SetStatusAsync(id, request, cancellationToken);

        return NoContent();
    }
}
