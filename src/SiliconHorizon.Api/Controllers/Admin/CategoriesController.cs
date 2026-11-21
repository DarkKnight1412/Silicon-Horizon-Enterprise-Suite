using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Catalog;
using SiliconHorizon.Application.Features.Catalog.Dto;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Api.Controllers.Admin;

/// <summary>
/// CRUD danh mục sản phẩm — controller mỏng: nhận request, gọi use case, trả DTO.
/// Nghiệp vụ và truy cập dữ liệu nằm trong <see cref="ICategoryService"/> (EF Core cho bảng cấu hình).
/// <para>Quyền: chỉ <b>Admin</b> (BRD 2.2 — nhóm "Tạo/sửa sản phẩm, thuộc tính": X cho Admin).</para>
/// <para>
/// Khi bảng Permissions/RolePermissions đã được seed, siết tiếp bằng mã quyền:
/// <c>[RequirePermission(PermissionCodes.CatalogManage)]</c>.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/admin/categories")]
[Authorize(Roles = RoleCodes.Admin)]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService) => _categoryService = categoryService;

    /// <summary>GET /api/v1/admin/categories?pageNumber=1&amp;pageSize=20</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CategoryDto>>> GetAll(
        [FromQuery] PageRequest page,
        CancellationToken cancellationToken)
        => Ok(await _categoryService.GetPagedAsync(page, cancellationToken));

    /// <summary>GET /api/v1/admin/categories/{id}</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDto>> GetById(int id, CancellationToken cancellationToken)
        => Ok(await _categoryService.GetByIdAsync(id, cancellationToken));

    /// <summary>POST /api/v1/admin/categories</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<CategoryDto>> Create(
        [FromBody] CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _categoryService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>PATCH /api/v1/admin/categories/{id}/status</summary>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(
        int id,
        [FromBody] SetCategoryStatusRequest request,
        CancellationToken cancellationToken)
    {
        await _categoryService.SetStatusAsync(id, request, cancellationToken);

        return NoContent();
    }
}
