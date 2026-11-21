using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Application.Features.Commission;

/// <summary>
/// Chốt chặn phạm vi dữ liệu của module Hoa hồng (Database/README.md mục 8, BRD mục 2.2):
/// nhân viên chỉ xem hoa hồng của mình, Quản lý chi nhánh chỉ xem trong chi nhánh mình, Admin xem toàn hệ thống.
/// </summary>
/// <remarks>
/// <c>usp_Commission_Summary</c> nhận <c>@EmployeeId</c> và <c>@BranchId</c> tùy chọn nhưng <b>không</b>
/// tự kiểm tra quyền: nếu API truyền thẳng giá trị client gửi lên thì nhân viên A xem được hoa hồng của
/// nhân viên B. Vì vậy mọi giá trị lọc phải đi qua lớp này và luôn lấy từ token khi người gọi không phải Admin.
/// </remarks>
public sealed class CommissionAccessGuard
{
    private readonly ICurrentUser _currentUser;

    public CommissionAccessGuard(ICurrentUser currentUser) => _currentUser = currentUser;

    /// <summary>Admin — xem toàn hệ thống, được điều chỉnh và chốt kỳ hoa hồng.</summary>
    public bool IsAdmin => _currentUser.IsInRole(RoleCodes.Admin);

    /// <summary>Quản lý chi nhánh — xem hoa hồng trong chi nhánh của mình.</summary>
    public bool IsManager => _currentUser.IsInRole(RoleCodes.Manager);

    /// <summary>
    /// Quyết định giá trị lọc thực sự truyền vào <c>@EmployeeId</c>/<c>@BranchId</c> của
    /// <c>usp_Commission_Summary</c> theo vai trò người gọi.
    /// </summary>
    /// <param name="requestedEmployeeId">Giá trị client gửi (chỉ được dùng khi người gọi là Admin).</param>
    /// <param name="requestedBranchId">Giá trị client gửi (chỉ được dùng khi người gọi là Admin).</param>
    public CommissionScope ResolveScope(long? requestedEmployeeId, int? requestedBranchId)
    {
        // Admin: lọc tùy chọn theo nhân viên/chi nhánh; không truyền gì = toàn hệ thống.
        if (IsAdmin)
        {
            return new CommissionScope(requestedEmployeeId, requestedBranchId);
        }

        // Quản lý chi nhánh: luôn bị ép @BranchId từ token. Có thể lọc thêm một nhân viên, nhưng proc luôn
        // lọc kèm theo chi nhánh nên không xem được dữ liệu của chi nhánh khác.
        if (IsManager)
        {
            return new CommissionScope(requestedEmployeeId, RequireBranchId());
        }

        // Nhân viên (TN/TV/TK): chỉ xem hoa hồng của chính mình — ép @EmployeeId từ token, bỏ qua query string.
        return new CommissionScope(RequireEmployeeId(), null);
    }

    /// <summary>Nhân viên phải có hồ sơ nhân viên trong token (thiếu → 403) — KHÔNG nhận <c>employeeId</c> từ client.</summary>
    public long RequireEmployeeId()
        => _currentUser.EmployeeId
           ?? throw new ForbiddenException(
               "Tài khoản chưa gắn hồ sơ nhân viên nên không xem được hoa hồng.");

    /// <summary>Chi nhánh của Quản lý — không có trong token → 403 (không xác định được phạm vi xem).</summary>
    public int RequireBranchId()
        => _currentUser.BranchId
           ?? throw new ForbiddenException(
               "Tài khoản chưa được gắn chi nhánh nên không xem được hoa hồng của chi nhánh.");

    /// <summary>Người dùng đang gọi — <c>usp_Commission_Adjust</c>/<c>usp_Commission_ClosePeriod</c> cần <c>@UserId</c>.</summary>
    public long RequireUserId()
        => _currentUser.UserId
           ?? throw new ForbiddenException("Token thiếu định danh người dùng nên không thực hiện được thao tác này.");
}

/// <summary>Phạm vi truy vấn hoa hồng đã được kiểm tra theo vai trò (<c>null</c> = không lọc).</summary>
public readonly record struct CommissionScope(long? EmployeeId, int? BranchId);