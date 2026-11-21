using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiliconHorizon.Application.Features.Auth;
using SiliconHorizon.Application.Features.Auth.Dto;

namespace SiliconHorizon.Api.Controllers;

/// <summary>
/// Tài khoản — Database/README.md mục 2.
/// <para>
/// Public: <c>/api/auth/register</c>, <c>/api/auth/login</c>.
/// Cần đăng nhập: <c>/api/me</c>.
/// <c>/api/auth/refresh</c> để AllowAnonymous vì khi access token hết hạn thì client chỉ còn refresh token
/// (bảo vệ bằng chính chữ ký + audience riêng của refresh token, xem AUTHENTICATION_AND_AUTHORIZATION.md mục 5).
/// </para>
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    /// <summary>POST /api/auth/register — khách đăng ký tài khoản.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RegisteredCustomerDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegisteredCustomerDto>> Register(
        [FromBody] RegisterCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var registered = await _authService.RegisterCustomerAsync(request, cancellationToken);

        return Created($"/api/me", registered);
    }

    /// <summary>POST /api/auth/login — đăng nhập, nhận access token + refresh token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthTokensDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthTokensDto>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
        => Ok(await _authService.LoginAsync(request, cancellationToken));

    /// <summary>POST /api/auth/refresh — đổi refresh token lấy cặp token mới.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthTokensDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthTokensDto>> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
        => Ok(await _authService.RefreshAsync(request, cancellationToken));

    /// <summary>GET /api/me — thông tin tài khoản đang đăng nhập (vai trò, quyền, phạm vi chi nhánh).</summary>
    [HttpGet("/api/me")]
    [Authorize]
    [ProducesResponseType(typeof(CurrentAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentAccountDto>> GetCurrentAccount(CancellationToken cancellationToken)
        => Ok(await _authService.GetCurrentAccountAsync(cancellationToken));
}
