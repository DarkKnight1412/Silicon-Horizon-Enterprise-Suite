using System.Security.Claims;
using SiliconHorizon.Application.Abstractions.Security;

namespace SiliconHorizon.Api.Security;

/// <summary>
/// Hiện thực <see cref="ICurrentUser"/> từ claim của JWT (quy ước Database/README.md mục 1.1):
/// <c>userId</c>, <c>roles</c>, và <c>customerId</c> (khách) hoặc <c>employeeId</c> + <c>branchId</c> (nhân viên).
/// <para>
/// Xác thực JWT (JwtBearer) sẽ được bật ở bước tiếp theo; khi đó chỉ cần phát claim đúng tên là lớp này hoạt động.
/// Hiện tại mọi request là ẩn danh nên các giá trị đều null.
/// </para>
/// </summary>
public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public long? UserId => GetLongClaim(AuthClaimTypes.UserId);

    public string? Username => Principal?.FindFirst(AuthClaimTypes.Username)?.Value;

    public long? CustomerId => GetLongClaim(AuthClaimTypes.CustomerId);

    public long? EmployeeId => GetLongClaim(AuthClaimTypes.EmployeeId);

    public int? BranchId => GetIntClaim(AuthClaimTypes.BranchId);

    public IReadOnlyCollection<string> Roles
        => Principal?.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray()
           ?? Array.Empty<string>();

    public IReadOnlyCollection<string> Permissions
        => Principal?.FindAll(AuthClaimTypes.Permission).Select(claim => claim.Value).ToArray()
           ?? Array.Empty<string>();

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;

    public bool HasPermission(string permission)
        => Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    public string? IpAddress => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString();

    private long? GetLongClaim(string claimType)
        => long.TryParse(Principal?.FindFirst(claimType)?.Value, out var value) ? value : null;

    private int? GetIntClaim(string claimType)
        => int.TryParse(Principal?.FindFirst(claimType)?.Value, out var value) ? value : null;
}
