using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Admin.Dto;

namespace SiliconHorizon.Application.Features.Admin;

/// <summary>
/// Use case vai trò — CRUD bằng EF Core trên <c>dbo.Roles</c> và gán quyền qua bảng nối
/// <c>dbo.RolePermissions</c> (Database/README.md mục 10: <c>/api/admin/roles</c>).
/// </summary>
public interface IRoleService
{
    /// <summary>GET /api/v1/admin/roles — danh sách vai trò kèm số quyền đang gán.</summary>
    Task<PagedResult<RoleDto>> GetPagedAsync(PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>GET /api/v1/admin/roles/{id} — chi tiết vai trò; không tồn tại → 404.</summary>
    Task<RoleDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>POST /api/v1/admin/roles — tạo vai trò; <c>code</c> duy nhất (<c>UQ_Roles_Code</c>) → trùng 409.</summary>
    Task<RoleDto> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken = default);

    /// <summary>PUT /api/v1/admin/roles/{id} — đổi tên vai trò (không đổi mã để giữ tham chiếu).</summary>
    Task<RoleDto> UpdateAsync(int id, UpdateRoleRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// PUT /api/v1/admin/roles/{id}/permissions — gán lại toàn bộ quyền của vai trò
    /// (bảng nối khóa chính tổ hợp <c>RoleId + PermissionId</c>: dòng thiếu sẽ thêm, dòng thừa sẽ xóa).
    /// </summary>
    Task<RoleDto> SetPermissionsAsync(
        int id,
        SetRolePermissionsRequest request,
        CancellationToken cancellationToken = default);
}
