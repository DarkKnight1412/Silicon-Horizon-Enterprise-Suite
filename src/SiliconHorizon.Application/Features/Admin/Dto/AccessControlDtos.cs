using SiliconHorizon.Domain.Entities.Common;

namespace SiliconHorizon.Application.Features.Admin.Dto;

/// <summary>
/// Vai trò trả ra API — CRUD bằng EF Core trên <c>dbo.Roles</c>
/// (Database/README.md mục 10: <c>/api/admin/roles</c>). Mã vai trò khớp <c>RoleCodes</c> và seed ở mục 13.
/// </summary>
public class RoleDto
{
    public int Id { get; init; }

    /// <summary>Mã vai trò (varchar(50), duy nhất — <c>UQ_Roles_Code</c>): Customer, Cashier, ..., Admin.</summary>
    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    /// <summary>Số quyền đang gán cho vai trò (<c>RolePermissions</c>).</summary>
    public int PermissionCount { get; init; }

    public static RoleDto FromEntity(Role role, int permissionCount) => new()
    {
        Id = role.Id,
        Code = role.Code,
        Name = role.Name,
        PermissionCount = permissionCount
    };
}

/// <summary>Yêu cầu tạo vai trò — POST /api/v1/admin/roles (mã duy nhất).</summary>
public class CreateRoleRequest
{
    public string? Code { get; set; }

    public string? Name { get; set; }
}

/// <summary>Yêu cầu cập nhật vai trò — PUT /api/v1/admin/roles/{id} (không đổi mã để giữ tham chiếu).</summary>
public class UpdateRoleRequest
{
    public string? Name { get; set; }
}

/// <summary>
/// Gán bộ quyền cho vai trò — PUT /api/v1/admin/roles/{id}/permissions.
/// Thay thế toàn bộ dòng trong bảng nối <c>RolePermissions</c> (khóa chính tổ hợp RoleId + PermissionId).
/// </summary>
public class SetRolePermissionsRequest
{
    /// <summary>Danh sách <c>PermissionId</c> mong muốn (rỗng = bỏ hết quyền của vai trò).</summary>
    public IReadOnlyList<int> PermissionIds { get; set; } = Array.Empty<int>();
}

/// <summary>
/// Quyền trả ra API — CRUD bằng EF Core trên <c>dbo.Permissions</c>
/// (Database/README.md mục 10: <c>/api/admin/permissions</c>). Bảng chỉ có <c>Id</c> và <c>Code</c>.
/// </summary>
public class PermissionDto
{
    public int Id { get; init; }

    /// <summary>Mã quyền (varchar(100), duy nhất), ví dụ <c>order.cancel.approve</c>.</summary>
    public string Code { get; init; } = string.Empty;

    public static PermissionDto FromEntity(Permission permission) => new()
    {
        Id = permission.Id,
        Code = permission.Code
    };
}

/// <summary>Tham số lọc danh sách quyền — GET /api/v1/admin/permissions.</summary>
public class PermissionListQuery
{
    public string? Keyword { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}

/// <summary>Yêu cầu tạo quyền — POST /api/v1/admin/permissions (mã duy nhất).</summary>
public class CreatePermissionRequest
{
    public string? Code { get; set; }
}

/// <summary>Yêu cầu cập nhật quyền — PUT /api/v1/admin/permissions/{id}.</summary>
public class UpdatePermissionRequest
{
    public string? Code { get; set; }
}

/// <summary>Một vai trò của người dùng — trả ra cho <c>GET /api/v1/admin/users/{id}/roles</c>.</summary>
public class UserRoleDto
{
    public long UserId { get; init; }

    public int RoleId { get; init; }

    public string RoleCode { get; init; } = string.Empty;

    public string RoleName { get; init; } = string.Empty;

    public static UserRoleDto FromEntities(UserRole userRole, Role role) => new()
    {
        UserId = userRole.UserId,
        RoleId = role.Id,
        RoleCode = role.Code,
        RoleName = role.Name
    };
}

/// <summary>
/// Gán bộ vai trò cho người dùng — PUT /api/v1/admin/users/{id}/roles.
/// Thay thế toàn bộ dòng trong bảng nối <c>UserRoles</c> (khóa chính tổ hợp UserId + RoleId).
/// </summary>
public class SetUserRolesRequest
{
    /// <summary>Danh sách <c>RoleId</c> mong muốn (rỗng = thu hồi hết vai trò).</summary>
    public IReadOnlyList<int> RoleIds { get; set; } = Array.Empty<int>();
}
