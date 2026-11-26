namespace SiliconHorizon.Application.Features.Auth.Dto;

/// <summary>POST /api/auth/login — đăng nhập bằng tên đăng nhập hoặc email.</summary>
public record LoginRequest(string Username, string Password);

/// <summary>POST /api/auth/register — khách đăng ký (gọi <c>usp_Auth_RegisterCustomer</c>).</summary>
public record RegisterCustomerRequest(string Username, string Password, string Phone, string FullName, string? Email);

/// <summary>POST /api/auth/refresh.</summary>
public record RefreshTokenRequest(string RefreshToken);

/// <summary>Cặp token trả về sau khi đăng nhập / làm mới token.</summary>
public class AuthTokensDto
{
    public string TokenType { get; init; } = "Bearer";

    public string AccessToken { get; init; } = string.Empty;

    public string RefreshToken { get; init; } = string.Empty;

    public DateTime AccessTokenExpiresAtUtc { get; init; }

    public DateTime RefreshTokenExpiresAtUtc { get; init; }
}

/// <summary>GET /api/me — thông tin tài khoản đang đăng nhập (không có PasswordHash).</summary>
public class CurrentAccountDto
{
    public long UserId { get; init; }

    public string Username { get; init; } = string.Empty;

    public string? Email { get; init; }

    public string? Phone { get; init; }

    /// <summary>0 = Khách hàng, 1 = Nhân viên.</summary>
    public byte UserType { get; init; }

    public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();

    public IReadOnlyCollection<string> Permissions { get; init; } = Array.Empty<string>();

    public long? CustomerId { get; init; }

    public long? EmployeeId { get; init; }

    public int? BranchId { get; init; }

    /// <summary>0 Cashier, 1 TechAdvisor, 2 Storekeeper, 3 Manager.</summary>
    public byte? EmployeePosition { get; init; }
}

/// <summary>Kết quả đăng ký khách hàng mới.</summary>
public record RegisteredCustomerDto(long UserId, long CustomerId);

/// <summary>
/// POST /api/auth/google — ID token do Google Identity Services trả về cho trình duyệt
/// (trường <c>credential</c> trong callback). API xác minh rồi tự phát JWT của hệ thống.
/// </summary>
public record GoogleLoginRequest(string IdToken);
