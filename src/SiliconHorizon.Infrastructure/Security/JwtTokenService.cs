using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SiliconHorizon.Application.Abstractions.Security;

namespace SiliconHorizon.Infrastructure.Security;

/// <summary>
/// Phát hành và đọc token JWT (HS256) theo quy ước claim trong Database/README.md mục 1.1:
/// <c>userId</c>, vai trò, quyền, và <c>customerId</c> (khách) hoặc <c>employeeId</c> + <c>branchId</c> (nhân viên).
/// <para>
/// Access token và refresh token dùng <b>audience khác nhau</b> nên refresh token không thể dùng thay access token.
/// Refresh token không chứa vai trò/quyền — khi làm mới, quyền được đọc lại từ database.
/// </para>
/// </summary>
public sealed class JwtTokenService : IJwtTokenService
{
    /// <summary>Claim phân biệt loại token (dùng chung với tầng Api qua <see cref="AuthClaimTypes"/>).</summary>
    public const string TokenTypeClaim = AuthClaimTypes.TokenType;

    public const string AccessTokenType = "access";

    public const string RefreshTokenType = "refresh";

    private readonly JwtOptions _options;

    /// <summary>
    /// <c>MapInboundClaims = false</c>: giữ nguyên tên claim khi đọc token
    /// (IdentityModel 8 đặt thuộc tính này trên handler thay vì trên TokenValidationParameters).
    /// </summary>
    private readonly JsonWebTokenHandler _handler = new() { MapInboundClaims = false };

    public JwtTokenService(IOptions<JwtOptions> options) => _options = options.Value;

    public IssuedToken CreateAccessToken(TokenSubject subject)
        => Create(
            subject,
            audience: _options.Audience,
            tokenType: AccessTokenType,
            lifetime: TimeSpan.FromMinutes(_options.AccessTokenMinutes),
            includeAuthorizationClaims: true);

    public IssuedToken CreateRefreshToken(TokenSubject subject)
        => Create(
            subject,
            audience: _options.RefreshAudience,
            tokenType: RefreshTokenType,
            lifetime: TimeSpan.FromDays(_options.RefreshTokenDays),
            includeAuthorizationClaims: false);

    public async Task<RefreshTokenPayload?> ReadRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        // IdentityModel 8: chỉ còn ValidateTokenAsync; overload 3 tham số nhận BaseConfiguration (không phải CancellationToken)
        // nên hủy tác vụ được truyền qua WaitAsync.
        var result = await _handler
            .ValidateTokenAsync(refreshToken, CreateValidationParameters(_options.RefreshAudience, validateLifetime: true))
            .WaitAsync(cancellationToken);

        if (!result.IsValid || result.ClaimsIdentity is not { } identity)
        {
            return null;
        }

        if (identity.FindFirst(TokenTypeClaim)?.Value != RefreshTokenType)
        {
            return null;
        }

        var userIdValue = identity.FindFirst(AuthClaimTypes.UserId)?.Value
            ?? identity.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        var username = identity.FindFirst(AuthClaimTypes.Username)?.Value;

        if (!long.TryParse(userIdValue, out var userId) || string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        var expiresAt = result.SecurityToken is JsonWebToken jsonWebToken
            ? jsonWebToken.ValidTo
            : DateTime.UtcNow;

        return new RefreshTokenPayload(userId, username, expiresAt);
    }

    private IssuedToken Create(
        TokenSubject subject,
        string audience,
        string tokenType,
        TimeSpan lifetime,
        bool includeAuthorizationClaims)
    {
        var issuedAt = DateTime.UtcNow;
        var expiresAt = issuedAt.Add(lifetime);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject.UserId.ToString()),
            new(AuthClaimTypes.Username, subject.Username),
            new(AuthClaimTypes.UserId, subject.UserId.ToString()),
            new(AuthClaimTypes.UserType, subject.UserType.ToString()),
            new(AuthClaimTypes.TokenType, tokenType)
        };

        if (includeAuthorizationClaims)
        {
            // ClaimTypes.Role để [Authorize(Roles = "...")] và User.IsInRole hoạt động không phụ thuộc mapping.
            claims.AddRange(subject.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
            claims.AddRange(subject.Permissions.Select(permission => new Claim(AuthClaimTypes.Permission, permission)));

            if (subject.CustomerId is { } customerId)
            {
                claims.Add(new Claim(AuthClaimTypes.CustomerId, customerId.ToString()));
            }

            if (subject.EmployeeId is { } employeeId)
            {
                claims.Add(new Claim(AuthClaimTypes.EmployeeId, employeeId.ToString()));
            }

            if (subject.BranchId is { } branchId)
            {
                claims.Add(new Claim(AuthClaimTypes.BranchId, branchId.ToString()));
            }

            if (subject.EmployeePosition is { } position)
            {
                claims.Add(new Claim(AuthClaimTypes.EmployeePosition, position.ToString()));
            }
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(CreateSigningKey(), SecurityAlgorithms.HmacSha256)
        };

        return new IssuedToken(_handler.CreateToken(descriptor), expiresAt);
    }

    /// <summary>
    /// Tham số kiểm tra token. Việc giữ nguyên tên claim (<c>userId</c>, <c>customerId</c>, <c>permission</c>...)
    /// do <c>MapInboundClaims = false</c> đặt trên handler (IdentityModel 8 bỏ thuộc tính này khỏi TokenValidationParameters).
    /// </summary>
    private TokenValidationParameters CreateValidationParameters(string audience, bool validateLifetime)
        => new()
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = CreateSigningKey(),
            ValidateLifetime = validateLifetime,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ClockSkew = _options.ClockSkew,
            NameClaimType = AuthClaimTypes.Username,
            RoleClaimType = ClaimTypes.Role
        };

    private SymmetricSecurityKey CreateSigningKey()
        => new(Encoding.UTF8.GetBytes(_options.SigningKey));
}
