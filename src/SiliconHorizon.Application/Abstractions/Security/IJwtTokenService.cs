namespace SiliconHorizon.Application.Abstractions.Security;

/// <summary>
/// Thông tin đưa vào token — đúng quy ước trong Database/README.md mục 1.1:
/// token chứa <c>userId</c>, <c>roles</c>, và <c>customerId</c> (khách) hoặc <c>employeeId</c> + <c>branchId</c> (nhân viên).
/// </summary>
public sealed record TokenSubject
{
    public required long UserId { get; init; }

    public required string Username { get; init; }

    /// <summary>0 = Customer, 1 = Employee (Users.UserType).</summary>
    public required byte UserType { get; init; }

    public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();

    public IReadOnlyCollection<string> Permissions { get; init; } = Array.Empty<string>();

    public long? CustomerId { get; init; }

    public long? EmployeeId { get; init; }

    /// <summary>Chi nhánh của nhân viên — API ép giá trị này từ token, không tin client gửi lên.</summary>
    public int? BranchId { get; init; }

    /// <summary>0 Cashier, 1 TechAdvisor, 2 Storekeeper, 3 Manager.</summary>
    public byte? EmployeePosition { get; init; }
}

/// <summary>Token đã phát hành.</summary>
public sealed record IssuedToken(string Value, DateTime ExpiresAtUtc);

/// <summary>Nội dung đọc lại từ refresh token (không cần truy vấn database).</summary>
public sealed record RefreshTokenPayload(long UserId, string Username, DateTime ExpiresAtUtc);

/// <summary>Phát hành và đọc token JWT (HS256).</summary>
public interface IJwtTokenService
{
    /// <summary>Access token ngắn hạn dùng cho mọi request.</summary>
    IssuedToken CreateAccessToken(TokenSubject subject);

    /// <summary>
    /// Refresh token dài hạn, KHÔNG chứa quyền và dùng audience riêng
    /// nên không thể dùng thay access token.
    /// </summary>
    IssuedToken CreateRefreshToken(TokenSubject subject);

    /// <summary>Đọc refresh token; trả về null nếu sai chữ ký, hết hạn hoặc không phải refresh token.</summary>
    Task<RefreshTokenPayload?> ReadRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);
}
