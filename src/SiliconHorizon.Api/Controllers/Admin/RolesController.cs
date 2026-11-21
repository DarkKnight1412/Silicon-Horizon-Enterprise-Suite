using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Admin;
using SiliconHorizon.Application.Features.Admin.Dto;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Api.Controllers.Admin;

/// <summary>
/// CRUD vai trò + gán quyền cho vai trò — <c>dbo.Roles</c> và bảng nối <c>dbo.RolePermissions</c>
/// (Database/README.md mục 10: <c>/api/admin/roles</c>).
/// </summary>
/// <remarks>
/// Quyền: chỉ <b>Admin</b>. Bảng <c>Roles</c> không có cột trạng thái nên không có endpoint DELETE
/// (đang được <c>UserRoles</c>/<c>RolePermissions</c> tham chiếu). Mã vai trò khớp <c>RoleCodes</c> dùng
/// trong <c>[Authorize(Roles = ...)]</c>.
/// </remarks>
[ApiController]
[Route("api/v1/admin/roles")]
[Authorize(Roles = RoleCodes.Admin)]
[Produces("application/json")]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService) => _roleService = roleService;

    /// <summary>GET /api/v1/admin/roles?pageNumber=&amp;pageSize= — kèm số quyền đang gán.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<RoleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<RoleDto>>> GetAll(
        [FromQuery] PageRequest page,
        CancellationToken cancellationToken)
        => Ok(await _roleService.GetPagedAsync(page, cancellationToken));

    /// <summary>GET /api/v1/admin/roles/{id}</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoleDto>> GetById(int id, CancellationToken cancellationToken)
        => Ok(await _roleService.GetByIdAsync(id, cancellationToken));

    /// <summary>POST /api/v1/admin/roles — mã vai trò duy nhất (409 nếu trùng).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RoleDto>> Create(
        [FromBody] CreateRoleRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _roleService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>PUT /api/v1/admin/roles/{id} — đổi tên vai trò (không đổi mã).</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoleDto>> Update(
        int id,
        [FromBody] UpdateRoleRequest request,
        CancellationToken cancellationToken)
        => Ok(await _roleService.UpdateAsync(id, request, cancellationToken));

    /// <summary>
    /// PUT /api/v1/admin/roles/{id}/permissions — thay thế toàn bộ quyền của vai trò
    /// (khóa chính tổ hợp <c>RoleId + PermissionId</c>; danh sách rỗng = thu hồi hết quyền).
    /// </summary>
    [HttpPut("{id:int}/permissions")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoleDto>> SetPermissions(
        int id,
        [FromBody] SetRolePermissionsRequest request,
        CancellationToken cancellationToken)
        => Ok(await _roleService.SetPermissionsAsync(id, request, cancellationToken));
}
