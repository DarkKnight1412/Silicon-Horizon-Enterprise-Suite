using Microsoft.AspNetCore.Authorization;
using SiliconHorizon.Application.Abstractions.Security;

namespace SiliconHorizon.Api.Security;

/// <summary>
/// Cho phép truy cập khi token chứa mã quyền yêu cầu.
/// <para>
/// Quyền được đọc trực tiếp từ claim của token (không truy vấn database mỗi request) — token do
/// <c>JwtTokenService</c> phát hành, claim lấy từ <c>usp_Auth_GetUserByUsername</c>.
/// Quyền thu hồi chỉ có hiệu lực khi access token hết hạn hoặc khi gọi <c>/api/auth/refresh</c>.
/// </para>
/// <para>Lưu ý: Admin KHÔNG tự động có mọi quyền — phải seed quyền cho vai trò trong <c>RolePermissions</c>.</para>
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        var granted = context.User
            .FindAll(AuthClaimTypes.Permission)
            .Any(claim => string.Equals(claim.Value, requirement.Permission, StringComparison.OrdinalIgnoreCase));

        if (granted)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
