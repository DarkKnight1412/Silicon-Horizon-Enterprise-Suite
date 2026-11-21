using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Admin;
using SiliconHorizon.Application.Features.Admin.Dto;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Api.Controllers.Admin;

/// <summary>
/// CRUD quyền — <c>dbo.Permissions</c> (Database/README.md mục 10: <c>/api/admin/permissions</c>).
/// </summary>
/// <remarks>
/// Quyền: chỉ <b>Admin</b>. Bảng chỉ có <c>Id</c> và <c>Code</c>; mã khớp <c>PermissionCodes</c> và là giá trị
/// dùng cho <c>[RequirePermission("...")]</c>. Không có endpoint DELETE (đang được <c>RolePermissions</c> tham chiếu).
/// </remarks>
[ApiController]
[Route("api/v1/admin/permissions")]
[Authorize(Roles = RoleCodes.Admin)]
[Produces("application/json")]
public class PermissionsController : ControllerBase
{
    private readonly IPermissionService _permissionService;

    public PermissionsController(IPermissionService permissionService) => _permissionService = permissionService;

    /// <summary>GET /api/v1/admin/permissions?keyword=&amp;pageNumber=&amp;pageSize=</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<PermissionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<PermissionDto>>> GetAll(
        [FromQuery] PermissionListQuery query,
        CancellationToken cancellationToken)
        => Ok(await _permissionService.GetPagedAsync(query, cancellationToken));

    /// <summary>GET /api/v1/admin/permissions/{id}</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(PermissionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PermissionDto>> GetById(int id, CancellationToken cancellationToken)
        => Ok(await _permissionService.GetByIdAsync(id, cancellationToken));

    /// <summary>POST /api/v1/admin/permissions — mã quyền duy nhất (409 nếu trùng).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PermissionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PermissionDto>> Create(
        [FromBody] CreatePermissionRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _permissionService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>PUT /api/v1/admin/permissions/{id} — sửa mã quyền (mã phải duy nhất).</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(PermissionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PermissionDto>> Update(
        int id,
        [FromBody] UpdatePermissionRequest request,
        CancellationToken cancellationToken)
        => Ok(await _permissionService.UpdateAsync(id, request, cancellationToken));
}
