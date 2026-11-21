using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Application.Features.Audit;

/// <summary>
/// Chốt chặn phạm vi dữ liệu của module Nhật ký (Database/README.md mục 8, BRD 2.2):
/// <b>Quản lý chi nhánh chỉ thấy nhật ký của chi nhánh mình</b>, Admin thấy toàn hệ thống.
/// </summary>
/// <remarks>
/// <c>usp_Audit_Search</c> nhận <c>@BranchId</c> tùy chọn và KHÔNG tự kiểm tra quyền, nên nếu API truyền
/// thẳng giá trị client gửi lên thì Quản lý chi nhánh A đọc được nhật ký (kèm giá trị cũ/mới, lý do, IP)
/// của chi nhánh B. Vì vậy <c>branchId</c> của Quản lý luôn bị ép từ token.
/// </remarks>
public sealed class AuditAccessGuard
{
    private readonly ICurrentUser _currentUser;

    public AuditAccessGuard(ICurrentUser currentUser) => _currentUser = currentUser;

    /// <summary>Admin — đọc nhật ký toàn hệ thống.</summary>
    public bool IsAdmin => _currentUser.IsInRole(RoleCodes.Admin);

    /// <summary>
    /// Phạm vi chi nhánh của <c>@BranchId</c>: Admin dùng giá trị lọc tùy chọn (<c>null</c> = toàn hệ thống),
    /// mọi vai trò khác bị ép về chi nhánh trong token.
    /// </summary>
    /// <param name="requestedBranchId">Giá trị client gửi (chỉ được dùng khi người gọi là Admin).</param>
    public int? ResolveBranchScope(int? requestedBranchId)
        => IsAdmin ? requestedBranchId : RequireBranchId();

    /// <summary>Chi nhánh trong token — thiếu → 403 kể cả Admin, vì không xác định được phạm vi xem nhật ký.</summary>
    public int RequireBranchId()
        => _currentUser.BranchId
           ?? throw new ForbiddenException(
               "Tài khoản chưa được gắn chi nhánh nên không xem được nhật ký của chi nhánh.");
}
