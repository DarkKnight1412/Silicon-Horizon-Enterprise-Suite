using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Application.Features.Returns;

/// <summary>
/// Nhóm vai trò dùng cho <c>[Authorize(Roles = ...)]</c> của module Đổi trả, đối chiếu cột "Quyền" ở
/// Database/README.md mục 7.2 (KH = khách, NV = mọi nhân viên, QL = quản lý chi nhánh, TK = thủ kho, AD = admin)
/// và ma trận BRD mục 2.2 (Admin có toàn quyền).
/// </summary>
public static class ReturnsRoles
{
    /// <summary>KH — khách tạo yêu cầu đổi trả cho đơn đã hoàn tất của mình (RET-01).</summary>
    public const string Create = RoleCodes.Customer;

    /// <summary>NV, KH, AD — xem danh sách/chi tiết yêu cầu; khách chỉ thấy yêu cầu của mình.</summary>
    public const string Viewers = RoleCodes.Staff + "," + RoleCodes.Customer + "," + RoleCodes.Admin;

    /// <summary>QL, AD — duyệt hoặc từ chối yêu cầu đổi trả (RET-04).</summary>
    public const string Review = RoleCodes.Approvers;

    /// <summary>TK, AD — nhận hàng và kiểm tra từng dòng/serial khi hàng về kho (RET-03).</summary>
    public const string Inspect = RoleCodes.Storekeeper + "," + RoleCodes.Admin;

    /// <summary>QL, AD — hoàn tiền, cập nhật đơn và thu hồi hoa hồng (RET-05).</summary>
    public const string Refund = RoleCodes.Approvers;
}