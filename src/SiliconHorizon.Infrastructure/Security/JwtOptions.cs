using System.ComponentModel.DataAnnotations;

namespace SiliconHorizon.Infrastructure.Security;

/// <summary>
/// Cấu hình JWT, bind từ section <c>Jwt</c> trong appsettings.
/// <para>
/// KHÔNG commit <see cref="SigningKey"/> thật: ở môi trường thật đặt qua biến môi trường
/// <c>Jwt__SigningKey</c> (hoặc user-secrets) — xem AUTHENTICATION_AND_AUTHORIZATION.md mục 6.
/// </para>
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Nơi phát hành token (claim <c>iss</c>).</summary>
    [Required(ErrorMessage = "Thiếu Jwt:Issuer.")]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Đối tượng nhận access token (claim <c>aud</c>).</summary>
    [Required(ErrorMessage = "Thiếu Jwt:Audience.")]
    public string Audience { get; set; } = string.Empty;

    /// <summary>Khóa ký HS256, tối thiểu 32 ký tự (256 bit).</summary>
    [Required(ErrorMessage = "Thiếu Jwt:SigningKey.")]
    [MinLength(32, ErrorMessage = "Jwt:SigningKey phải có ít nhất 32 ký tự (HS256).")]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Thời hạn access token (phút).</summary>
    [Range(1, 1440)]
    public int AccessTokenMinutes { get; set; } = 30;

    /// <summary>Thời hạn refresh token (ngày).</summary>
    [Range(1, 90)]
    public int RefreshTokenDays { get; set; } = 14;

    /// <summary>Token dài hạn cũng nhận ít thay đổi hơn nên cho phép lệch giờ lớn hơn.</summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Audience riêng cho refresh token → refresh token không thể dùng thay access token.</summary>
    public string RefreshAudience => Audience + ":refresh";
}
