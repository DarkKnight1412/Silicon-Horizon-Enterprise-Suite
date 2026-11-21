namespace SiliconHorizon.Application.Abstractions.Security;

/// <summary>
/// Người gọi hiện tại lấy từ JWT (quy ước API trong Database/README.md mục 1.1):
/// token chứa <c>userId</c>, <c>roles</c>, và <c>customerId</c> (khách) hoặc <c>employeeId</c> + <c>branchId</c> (nhân viên).
/// Tầng API ép các giá trị này từ token, KHÔNG tin giá trị client gửi lên.
/// </summary>
public interface ICurrentUser
{
    long? UserId { get; }

    /// <summary>Tên đăng nhập (claim <c>name</c>) — dùng để gọi lại <c>usp_Auth_GetUserByUsername</c> cho /api/me.</summary>
    string? Username { get; }

    long? CustomerId { get; }

    long? EmployeeId { get; }

    /// <summary>Chi nhánh của nhân viên; Admin có thể truyền null để xem toàn hệ thống.</summary>
    int? BranchId { get; }

    IReadOnlyCollection<string> Roles { get; }

    IReadOnlyCollection<string> Permissions { get; }

    bool IsAuthenticated { get; }

    bool IsInRole(string role);

    bool HasPermission(string permission);

    /// <summary>IP dùng cho nhật ký AUD-02 (truyền vào sp_set_session_context).</summary>
    string? IpAddress { get; }

    string? UserAgent { get; }
}
