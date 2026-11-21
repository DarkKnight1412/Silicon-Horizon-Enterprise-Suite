namespace SiliconHorizon.Application.Features.Auth.Dto;

/// <summary>
/// Các lớp ánh xạ result set của stored procedure. Tên property PHẢI khớp cột proc trả về
/// (xem Database/02_sp_foundation_catalog_cart.sql).
/// </summary>
/// <remarks>Result set 1 của <c>usp_Auth_GetUserByUsername</c>.</remarks>
public class AuthUserRow
{
    public long Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    /// <summary>Chỉ dùng nội bộ để so khớp mật khẩu — không trả ra API.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public byte UserType { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>Result set 2 — vai trò của người dùng.</summary>
public class AuthRoleRow
{
    public string RoleCode { get; set; } = string.Empty;
}

/// <summary>Result set 3 — quyền hiệu lực (đã gộp từ các vai trò).</summary>
public class AuthPermissionRow
{
    public string PermissionCode { get; set; } = string.Empty;
}

/// <summary>Result set 4 — hồ sơ nhân viên (nếu là nhân viên).</summary>
public class AuthEmployeeRow
{
    public long EmployeeId { get; set; }

    public int BranchId { get; set; }

    public byte Position { get; set; }

    public byte Status { get; set; }
}

/// <summary>Result set 5 — hồ sơ khách hàng (nếu là khách).</summary>
public class AuthCustomerRow
{
    public long CustomerId { get; set; }

    public int CustomerGroupId { get; set; }
}

/// <summary>Kết quả của <c>usp_Auth_RegisterCustomer</c>.</summary>
public class RegisteredCustomerRow
{
    public long UserId { get; set; }

    public long CustomerId { get; set; }
}
