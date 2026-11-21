using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Application.Features.Reports;

/// <summary>
/// Nhóm vai trò dùng cho <c>[Authorize(Roles = ...)]</c> của module Báo cáo, đối chiếu cột "Quyền" ở
/// Database/README.md mục 9 (QL = quản lý chi nhánh, AD = admin, TK = thủ kho, NV = mọi nhân viên).
/// </summary>
public static class ReportRoles
{
    /// <summary>QL (chi nhánh mình), AD — báo cáo doanh thu và báo cáo bảo hành.</summary>
    public const string Approvers = RoleCodes.Approvers;

    /// <summary>TK, QL, AD — báo cáo giá trị tồn kho và số serial theo trạng thái.</summary>
    public const string StockReports = RoleCodes.Storekeeper + "," + RoleCodes.Manager + "," + RoleCodes.Admin;

    /// <summary>AD — báo cáo công nợ phải thu/phải trả toàn hệ thống (proc không lọc theo chi nhánh).</summary>
    public const string AdminOnly = RoleCodes.Admin;
}
