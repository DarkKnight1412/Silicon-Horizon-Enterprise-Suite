namespace SiliconHorizon.Application.Abstractions.Security;

/// <summary>Thông tin cần thiết lấy từ ID token Google (chỉ các trường dùng cho đăng nhập).</summary>
public sealed record GoogleUserInfo(string Subject, string Email, string? Name);

/// <summary>
/// Xác minh ID token do Google Identity Services phát cho trình duyệt (audience = client id của web).
/// <para>
/// Abstraction đặt ở Application để use case auth không phụ thuộc mạng/thư viện; hiện thực thật nằm ở
/// Infrastructure (<c>GoogleIdTokenValidator</c>) — cùng cách với <c>IJwtTokenService</c>.
/// </para>
/// </summary>
public interface IGoogleTokenValidator
{
    /// <summary>
    /// Xác minh <paramref name="idToken"/> (chữ ký bằng khoá công khai của Google, đúng audience, còn hạn).
    /// Trả <c>null</c> khi token không hợp lệ, sai audience hoặc email CHƯA được Google xác minh
    /// (<c>email_verified = false</c>).
    /// </summary>
    Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}
