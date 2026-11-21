using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace SiliconHorizon.Api.Security;

/// <summary>
/// Yêu cầu một mã quyền cụ thể — khớp cột <c>dbo.Permissions.Code</c> (xem ma trận BRD 2.2
/// và danh sách mã trong <c>Application.Security.PermissionCodes</c>).
/// </summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permission) => Permission = permission;

    public string Permission { get; }
}

/// <summary>
/// Gắn lên controller/action: <c>[RequirePermission(PermissionCodes.PriceManage)]</c>.
/// Sinh policy <c>permission:&lt;mã&gt;</c> được <see cref="PermissionPolicyProvider"/> tạo tự động.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "permission:";

    public RequirePermissionAttribute(string permission) => Policy = PolicyPrefix + permission;
}

/// <summary>
/// Tạo policy "theo yêu cầu" cho mọi mã quyền: danh sách quyền nằm trong database (bảng Permissions),
/// không khai báo tĩnh trong code, nên không thể đăng ký trước từng policy.
/// </summary>
public sealed class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        : base(options)
    {
    }

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var existingPolicy = await base.GetPolicyAsync(policyName);

        if (existingPolicy is not null
            || !policyName.StartsWith(RequirePermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
        {
            return existingPolicy;
        }

        var permission = policyName[RequirePermissionAttribute.PolicyPrefix.Length..];

        if (string.IsNullOrWhiteSpace(permission))
        {
            return null;
        }

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permission))
            .Build();
    }
}
