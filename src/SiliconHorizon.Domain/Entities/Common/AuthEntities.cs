using SiliconHorizon.Domain.Common;

namespace SiliconHorizon.Domain.Entities.Common;

/// <summary>dbo.Users — tài khoản đăng nhập cho cả khách hàng và nhân viên.</summary>
public class User : IAuditableEntity
{
    public long Id { get; set; }

    public string Username { get; set; } = null!;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    /// <summary>Hash mật khẩu (BCrypt/PBKDF2). Không bao giờ trả ra API.</summary>
    public string PasswordHash { get; set; } = null!;

    /// <summary>0 = Customer, 1 = Employee (Users.UserType).</summary>
    public byte UserType { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? LastLoginAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

/// <summary>dbo.Roles — Customer, Cashier, TechAdvisor, Storekeeper, Manager, Admin.</summary>
public class Role
{
    public int Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

/// <summary>dbo.Permissions — ví dụ: order.cancel.approve, stock.adjust.approve.</summary>
public class Permission
{
    public int Id { get; set; }

    public string Code { get; set; } = null!;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

/// <summary>dbo.UserRoles — bảng nối nhiều-nhiều (khóa chính tổ hợp).</summary>
public class UserRole
{
    public long UserId { get; set; }

    public int RoleId { get; set; }

    public User User { get; set; } = null!;

    public Role Role { get; set; } = null!;
}

/// <summary>dbo.RolePermissions — bảng nối nhiều-nhiều (khóa chính tổ hợp).</summary>
public class RolePermission
{
    public int RoleId { get; set; }

    public int PermissionId { get; set; }

    public Role Role { get; set; } = null!;

    public Permission Permission { get; set; } = null!;
}

/// <summary>dbo.Branches — chi nhánh; nhân viên chỉ thao tác trong chi nhánh của mình.</summary>
public class Branch : IAuditableEntity
{
    public int Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Address { get; set; } = null!;

    public string? Phone { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }
}

/// <summary>
/// dbo.SystemSettings — cấu hình hệ thống (thời hạn giữ hàng, hạn đổi trả...).
/// Bảng này KHÔNG có CreatedAt nên không kế thừa <see cref="IAuditableEntity"/>.
/// </summary>
public class SystemSetting
{
    public string Key { get; set; } = null!;

    public string Value { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }
}
