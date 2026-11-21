using System.Security.Claims;

namespace SiliconHorizon.Application.Abstractions.Security;

/// <summary>
/// Tên các claim trong JWT — khai báo một chỗ để nơi phát hành token (Infrastructure) và
/// nơi đọc token (Api: <c>CurrentUser</c>, kiểm tra quyền) không bị lệch nhau.
/// </summary>
public static class AuthClaimTypes
{
    public const string UserId = "userId";

    public const string UserType = "userType";

    public const string CustomerId = "customerId";

    public const string EmployeeId = "employeeId";

    public const string BranchId = "branchId";

    public const string EmployeePosition = "employeePosition";

    /// <summary>Quyền hiệu lực (nhiều claim cùng tên) — khớp cột <c>dbo.Permissions.Code</c>.</summary>
    public const string Permission = "permission";

    /// <summary>Phân biệt access token / refresh token.</summary>
    public const string TokenType = "token_type";

    /// <summary>Tên đăng nhập — dùng URI chuẩn của .NET để <c>User.Identity.Name</c> và <c>ICurrentUser.Username</c> hoạt động.</summary>
    public const string Username = ClaimTypes.Name;
}
