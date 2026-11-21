using SiliconHorizon.Application.Features.Admin.Dto;

namespace SiliconHorizon.Application.Features.Admin;

/// <summary>
/// Use case gán vai trò cho người dùng — thao tác trên bảng nối <c>dbo.UserRoles</c>
/// (khóa chính tổ hợp <c>UserId + RoleId</c>; Database/README.md mục 10: <c>/api/admin/users/{id}/roles</c>).
/// </summary>
public interface IUserRoleService
{
    /// <summary>GET /api/v1/admin/users/{userId}/roles — danh sách vai trò hiện có của người dùng.</summary>
    Task<IReadOnlyList<UserRoleDto>> GetRolesAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// PUT /api/v1/admin/users/{userId}/roles — gán lại toàn bộ vai trò của người dùng
    /// (thay thế tập <c>UserRoles</c> hiện có; gửi danh sách rỗng để thu hồi hết vai trò).
    /// </summary>
    Task<IReadOnlyList<UserRoleDto>> SetRolesAsync(
        long userId,
        SetUserRolesRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/v1/admin/users/{userId}/roles/{roleId} — gán thêm một vai trò (bỏ qua nếu đã có,
    /// nhờ vậy thao tác là idempotent).
    /// </summary>
    Task<IReadOnlyList<UserRoleDto>> AssignRoleAsync(
        long userId,
        int roleId,
        CancellationToken cancellationToken = default);
}
