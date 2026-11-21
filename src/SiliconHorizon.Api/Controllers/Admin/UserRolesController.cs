using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Features.Admin;
using SiliconHorizon.Application.Features.Admin.Dto;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Api.Controllers.Admin;

/// <summary>
/// Gán vai trò cho người dùng — bảng nối <c>dbo.UserRoles</c> (khóa chính tổ hợp <c>UserId + RoleId</c>)
/// theo Database/README.md mục 10: <c>/api/admin/users/{id}/roles</c>.
/// </summary>
/// <remarks>
/// Quyền: chỉ <b>Admin</b>. Gán vai trò là thao tác nhạy cảm → mọi thay đổi ghi nhật ký <c>usp_Audit_Write</c>.
/// Sử dụng <c>PUT</c> khi muốn đặt lại toàn bộ tập vai trò, <c>POST</c> khi chỉ cần gán thêm (idempotent).
/// </remarks>
[ApiController]
[Route("api/v1/admin/users/{userId:long}/roles")]
[Authorize(Roles = RoleCodes.Admin)]
[Produces("application/json")]
public class UserRolesController : ControllerBase
{
    private readonly IUserRoleService _userRoleService;

    public UserRolesController(IUserRoleService userRoleService) => _userRoleService = userRoleService;

    /// <summary>GET /api/v1/admin/users/{userId}/roles — danh sách vai trò hiện có của tài khoản.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserRoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<UserRoleDto>>> GetRoles(
        long userId,
        CancellationToken cancellationToken)
        => Ok(await _userRoleService.GetRolesAsync(userId, cancellationToken));

    /// <summary>
    /// PUT /api/v1/admin/users/{userId}/roles — thay thế toàn bộ vai trò của tài khoản
    /// (gửi <c>roleIds</c> rỗng để thu hồi hết vai trò).
    /// </summary>
    [HttpPut]
    [ProducesResponseType(typeof(IReadOnlyList<UserRoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<UserRoleDto>>> SetRoles(
        long userId,
        [FromBody] SetUserRolesRequest request,
        CancellationToken cancellationToken)
        => Ok(await _userRoleService.SetRolesAsync(userId, request, cancellationToken));

    /// <summary>POST /api/v1/admin/users/{userId}/roles/{roleId} — gán thêm một vai trò (đã có thì bỏ qua).</summary>
    [HttpPost("{roleId:int}")]
    [ProducesResponseType(typeof(IReadOnlyList<UserRoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<UserRoleDto>>> AssignRole(
        long userId,
        int roleId,
        CancellationToken cancellationToken)
        => Ok(await _userRoleService.AssignRoleAsync(userId, roleId, cancellationToken));
}
