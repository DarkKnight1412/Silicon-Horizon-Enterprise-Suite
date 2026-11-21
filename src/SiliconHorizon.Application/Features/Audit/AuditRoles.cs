using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Application.Features.Audit;

/// <summary>
/// Nhóm vai trò dùng cho <c>[Authorize(Roles = ...)]</c> của module Nhật ký, đối chiếu
/// Database/README.md mục 8: <c>GET /api/audit-logs</c> = QL (chỉ chi nhánh mình), AD.
/// </summary>
public static class AuditRoles
{
    /// <summary>QL, AD — tra cứu nhật ký hệ thống (usp_Audit_Search).</summary>
    public const string Viewers = RoleCodes.Approvers;
}
