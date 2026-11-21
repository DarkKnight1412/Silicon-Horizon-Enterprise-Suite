using SiliconHorizon.Application.Features.Auth.Dto;

namespace SiliconHorizon.Application.Features.Auth;

/// <summary>
/// Use case tài khoản (Database/README.md mục 2):
/// đăng ký khách, đăng nhập, làm mới token, xem thông tin tài khoản hiện tại.
/// </summary>
public interface IAuthService
{
    Task<AuthTokensDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<RegisteredCustomerDto> RegisterCustomerAsync(
        RegisterCustomerRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthTokensDto> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);

    Task<CurrentAccountDto> GetCurrentAccountAsync(CancellationToken cancellationToken = default);
}
