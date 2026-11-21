using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Application.Features.Commission;

/// <summary>
/// Nhóm vai trò dùng cho <c>[Authorize(Roles = ...)]</c> của module Hoa hồng, đối chiếu cột "Quyền" ở
/// Database/README.md mục 8: <c>GET /api/commissions</c> = NV (của mình), QL, AD;
/// điều chỉnh và chốt kỳ hoa hồng = AD.
/// </summary>
public static class CommissionRoles
{
    /// <summary>NV (mọi nhân viên), AD — xem tổng hợp hoa hồng; nhân viên chỉ xem của mình (ép EmployeeId từ token).</summary>
    public const string Viewers = RoleCodes.Staff + "," + RoleCodes.Admin;

    /// <summary>AD — điều chỉnh hoa hồng thủ công và chốt kỳ hoa hồng.</summary>
    public const string Admin = RoleCodes.Admin;
}