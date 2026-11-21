using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Application.Features.Reports;

/// <summary>
/// Chốt chặn phạm vi chi nhánh của module Báo cáo (Database/README.md mục 9, BRD 2.2):
/// Quản lý chi nhánh chỉ xem số liệu của chi nhánh mình, Admin xem toàn hệ thống.
/// </summary>
/// <remarks>
/// Các proc báo cáo nhận <c>@BranchId int = NULL</c> nghĩa là "toàn hệ thống" nhưng KHÔNG tự kiểm tra quyền,
/// nên Quản lý chi nhánh nếu không bị ép <c>@BranchId</c> sẽ xem được doanh thu của chi nhánh khác.
/// <para>
/// <b>Ngoại lệ:</b> <c>usp_Report_Debt</c> không có tham số chi nhánh nên chỉ Admin được gọi (chặn ở
/// <see cref="ReportRoles.AdminOnly"/>); Quản lý chi nhánh không xem được báo cáo này.
/// </para>
/// </remarks>
public sealed class ReportAccessGuard
{
    private readonly ICurrentUser _currentUser;

    public ReportAccessGuard(ICurrentUser currentUser) => _currentUser = currentUser;

    /// <summary>Admin — xem số liệu toàn hệ thống, không bị giới hạn chi nhánh.</summary>
    public bool IsAdmin => _currentUser.IsInRole(RoleCodes.Admin);

    /// <summary>
    /// Giá trị thực sự truyền vào <c>@BranchId</c>: Admin dùng giá trị lọc tùy chọn (<c>null</c> = toàn hệ thống),
    /// mọi vai trò khác bị ép về chi nhánh trong token.
    /// </summary>
    /// <param name="requestedBranchId">Giá trị client gửi (chỉ được dùng khi người gọi là Admin).</param>
    public int? ResolveBranchScope(int? requestedBranchId)
        => IsAdmin ? requestedBranchId : RequireBranchId();

    /// <summary>Chi nhánh trong token — thiếu → 403 vì không xác định được phạm vi số liệu.</summary>
    public int RequireBranchId()
        => _currentUser.BranchId
           ?? throw new ForbiddenException(
               "Tài khoản chưa được gắn chi nhánh nên không xem được báo cáo của chi nhánh.");
}
