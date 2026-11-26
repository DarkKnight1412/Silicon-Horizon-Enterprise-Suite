using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SiliconHorizon.Application.Abstractions.Security;

namespace SiliconHorizon.Infrastructure.Security;

/// <summary>
/// Xác minh ID token của Google bằng KHOÁ CÔNG KHAI (JWKS) — KHÔNG cần Client Secret.
/// <para>
/// Quy tắc kiểm tra:
/// • Chữ ký phải khớp khoá công khai tại <c>https://www.googleapis.com/oauth2/v3/certs</c> (JWKS được cache 1 giờ).
/// • <c>aud</c> phải bằng <c>Google:ClientId</c> trong cấu hình (client id của web khách hàng).
/// • <c>iss</c> phải là <c>accounts.google.com</c> (có hoặc không có tiền tố https).
/// • Bắt buộc <c>email_verified = true</c> — nếu không, kẻ khác có thể tạo tài khoản Google bằng email của
///   người khác rồi chiếm tài khoản trong hệ thống.
/// </para>
/// </summary>
public sealed class GoogleIdTokenValidator : IGoogleTokenValidator
{
    private const string JwksUrl = "https://www.googleapis.com/oauth2/v3/certs";

    private static readonly string[] ValidIssuers = ["accounts.google.com", "https://accounts.google.com"];

    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

    private static readonly JsonWebTokenHandler TokenHandler = new();

    private static readonly SemaphoreSlim JwksLock = new(1, 1);

    private static JsonWebKeySet? cachedKeys;
    private static DateTime cachedUntilUtc = DateTime.MinValue;

    private readonly string _clientId;

    public GoogleIdTokenValidator(IConfiguration configuration)
        => _clientId = configuration["Google:ClientId"] ?? string.Empty;

    public async Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_clientId) || string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        var signingKeys = await GetSigningKeysAsync(cancellationToken);

        if (signingKeys.Count == 0)
        {
            return null;
        }

        var parameters = new TokenValidationParameters
        {
            ValidAudience = _clientId,
            ValidIssuers = ValidIssuers,
            IssuerSigningKeys = signingKeys,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        try
        {
            var result = await TokenHandler.ValidateTokenAsync(idToken, parameters);

            if (!result.IsValid)
            {
                return null;
            }

            var subject = ReadString(result.Claims, "sub");
            var email = ReadString(result.Claims, "email");

            if (subject is null || email is null || !ReadBool(result.Claims, "email_verified"))
            {
                return null;
            }

            return new GoogleUserInfo(subject, email, ReadString(result.Claims, "name"));
        }
        catch
        {
            // Token sai định dạng/chữ ký/hết hạn ⇒ coi như không hợp lệ; không lộ chi tiết ra ngoài.
            return null;
        }
    }

    /// <summary>Lấy khoá công khai của Google (cache 1 giờ) để xác minh chữ ký.</summary>
    private static async Task<IList<SecurityKey>> GetSigningKeysAsync(CancellationToken cancellationToken)
    {
        if (cachedKeys is not null && DateTime.UtcNow < cachedUntilUtc)
        {
            return cachedKeys.GetSigningKeys();
        }

        await JwksLock.WaitAsync(cancellationToken);

        try
        {
            if (cachedKeys is not null && DateTime.UtcNow < cachedUntilUtc)
            {
                return cachedKeys.GetSigningKeys();
            }

            var json = await HttpClient.GetStringAsync(JwksUrl, cancellationToken);

            cachedKeys = new JsonWebKeySet(json);
            cachedUntilUtc = DateTime.UtcNow.AddHours(1);

            return cachedKeys.GetSigningKeys();
        }
        catch
        {
            // Mất mạng/Google lỗi: trả rỗng ⇒ AuthService báo đăng nhập Google thất bại (không crash).
            return Array.Empty<SecurityKey>();
        }
        finally
        {
            JwksLock.Release();
        }
    }

    private static string? ReadString(IDictionary<string, object> claims, string name)
        => claims.TryGetValue(name, out var value) && value is string text ? text : null;

    private static bool ReadBool(IDictionary<string, object> claims, string name)
        => claims.TryGetValue(name, out var value) && value is bool flag && flag;
}
