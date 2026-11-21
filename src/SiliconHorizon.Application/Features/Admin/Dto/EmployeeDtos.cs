using SiliconHorizon.Domain.Entities.Common;
using SiliconHorizon.Domain.Entities.Hr;
using SiliconHorizon.Domain.Enums;

namespace SiliconHorizon.Application.Features.Admin.Dto;

/// <summary>
/// Nhân viên trả ra API — CRUD bằng EF Core trên <c>dbo.Employees</c>
/// (Database/README.md mục 10: <c>/api/admin/employees</c>).
/// Hồ sơ nhân viên luôn gắn 1-1 với <c>dbo.Users</c> và có vai trò trong <c>dbo.UserRoles</c> nên DTO
/// kèm thông tin tài khoản/vai trò để quản trị thấy ngay trạng thái đồng bộ.
/// </summary>
public class EmployeeDto
{
    public long Id { get; init; }

    /// <summary>Mã nhân viên (varchar(20), duy nhất — <c>UQ_Employees_Code</c>).</summary>
    public string EmployeeCode { get; init; } = string.Empty;

    public long UserId { get; init; }

    public string Username { get; init; } = string.Empty;

    public string? Email { get; init; }

    public string FullName { get; init; } = string.Empty;

    public string? Phone { get; init; }

    /// <summary>0 Cashier, 1 TechAdvisor, 2 Storekeeper, 3 Manager.</summary>
    public EmployeePosition Position { get; init; }

    public string PositionName { get; init; } = string.Empty;

    public int BranchId { get; init; }

    public string? BranchName { get; init; }

    /// <summary>0 Active, 1 Terminated.</summary>
    public EmployeeStatus Status { get; init; }

    public string StatusName { get; init; } = string.Empty;

    public DateOnly HireDate { get; init; }

    public DateOnly? TerminatedDate { get; init; }

    /// <summary>Mã vai trò tương ứng chức vụ trong <c>dbo.Roles</c> (Cashier/.../Manager).</summary>
    public string RoleCode { get; init; } = string.Empty;

    /// <summary>Tài khoản đăng nhập (<c>Users.IsActive</c>) — nghỉ việc thì khóa tài khoản này.</summary>
    public bool IsUserActive { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public static EmployeeDto FromEntities(
        Employee employee,
        User user,
        string roleCode,
        string? branchName) => new()
    {
        Id = employee.Id,
        EmployeeCode = employee.EmployeeCode,
        UserId = employee.UserId,
        Username = user.Username,
        Email = user.Email,
        FullName = employee.FullName,
        Phone = employee.Phone,
        Position = employee.Position,
        PositionName = PositionText(employee.Position),
        BranchId = employee.BranchId,
        BranchName = branchName,
        Status = employee.Status,
        StatusName = StatusText(employee.Status),
        HireDate = employee.HireDate,
        TerminatedDate = employee.TerminatedDate,
        RoleCode = roleCode,
        IsUserActive = user.IsActive,
        CreatedAt = employee.CreatedAt,
        UpdatedAt = employee.UpdatedAt
    };

    /// <summary>Tên chức vụ tiếng Việt khớp BRD mục 2.1.</summary>
    public static string PositionText(EmployeePosition position) => position switch
    {
        EmployeePosition.Cashier => "Thu ngân",
        EmployeePosition.TechAdvisor => "Tư vấn kỹ thuật",
        EmployeePosition.Storekeeper => "Thủ kho",
        EmployeePosition.Manager => "Quản lý chi nhánh",
        _ => position.ToString()
    };

    /// <summary>Trạng thái làm việc tiếng Việt.</summary>
    public static string StatusText(EmployeeStatus status) => status switch
    {
        EmployeeStatus.Active => "Đang làm việc",
        EmployeeStatus.Terminated => "Đã nghỉ việc",
        _ => status.ToString()
    };
}

/// <summary>Tham số lọc danh sách nhân viên — GET /api/v1/admin/employees.</summary>
public class EmployeeListQuery
{
    /// <summary>Tìm theo mã nhân viên, tên, tên đăng nhập hoặc số điện thoại.</summary>
    public string? Keyword { get; set; }

    public int? BranchId { get; set; }

    public EmployeePosition? Position { get; set; }

    public EmployeeStatus? Status { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Yêu cầu tạo nhân viên — POST /api/v1/admin/employees.
/// Service tạo <b>đồng thời</b> <c>Users</c> + <c>UserRoles</c> + <c>Employees</c> trong một giao dịch
/// (lưu ý ở cuối mục 10 của Database/README.md).
/// </summary>
public class CreateEmployeeRequest
{
    /// <summary>Mã nhân viên; bỏ trống thì hệ thống tự sinh dạng <c>NV0001</c>.</summary>
    public string? EmployeeCode { get; set; }

    /// <summary>Tên đăng nhập (varchar(50), duy nhất — <c>UQ_Users_Username</c>).</summary>
    public string? Username { get; set; }

    /// <summary>Mật khẩu ban đầu; băm bằng <c>IPasswordHasher</c> (PBKDF2), không lưu bản rõ.</summary>
    public string? Password { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? FullName { get; set; }

    /// <summary>Chức vụ quyết định vai trò được gán trong <c>UserRoles</c>.</summary>
    public EmployeePosition Position { get; set; }

    public int BranchId { get; set; }

    public DateOnly HireDate { get; set; }
}

/// <summary>
/// Yêu cầu cập nhật nhân viên — PUT /api/v1/admin/employees/{id}.
/// Không đổi <c>username</c>/<c>password</c>, nhưng đồng bộ lại tài khoản (<c>Users.Email/Phone/IsActive</c>),
/// chi nhánh và vai trò khi chức vụ thay đổi.
/// </summary>
public class UpdateEmployeeRequest
{
    public string? FullName { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public EmployeePosition Position { get; set; }

    public int BranchId { get; set; }

    public DateOnly HireDate { get; set; }

    public DateOnly? TerminatedDate { get; set; }

    /// <summary>Khóa/mở tài khoản đăng nhập tương ứng.</summary>
    public bool IsUserActive { get; set; } = true;
}

/// <summary>
/// Đổi trạng thái làm việc — PATCH /api/v1/admin/employees/{id}/status.
/// Nghỉ việc (<c>Terminated</c>) ghi <c>terminatedDate</c> và khóa tài khoản;
/// <b>không xóa cứng</b> nhân viên vì đã gắn với đơn hàng, ca làm, hoa hồng.
/// </summary>
public class SetEmployeeStatusRequest
{
    public EmployeeStatus Status { get; set; }

    /// <summary>Ngày nghỉ việc; bắt buộc khi chuyển sang <c>Terminated</c>.</summary>
    public DateOnly? TerminatedDate { get; set; }
}
