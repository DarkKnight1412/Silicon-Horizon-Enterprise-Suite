using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Application.Features.Inventory;

/// <summary>
/// Nhóm vai trò dùng cho <c>[Authorize(Roles = ...)]</c> của module kho, đối chiếu bảng quyền
/// Database/README.md mục 6 (TK = thủ kho, QL = quản lý chi nhánh, AD = admin, NV = mọi nhân viên, KH = khách hàng).
/// </summary>
/// <remarks>
/// ASP.NET Core ghép nhiều <c>[Authorize]</c> theo kiểu AND nên controller có thể đặt quyền rộng ở cấp class
/// rồi siết hẹp lại ở từng action (ví dụ <see cref="StockDesk"/> ở class, <see cref="Approvers"/> ở action duyệt).
/// </remarks>
public static class InventoryRoles
{
    /// <summary>TK (thủ kho) + AD — nhập kho, tạo/xuất/nhận/hủy phiếu chuyển, mở &amp; nhập đợt kiểm kê.</summary>
    public const string StockOperations = RoleCodes.Storekeeper + "," + RoleCodes.Admin;

    /// <summary>TK, QL, AD — xem tồn kho, thẻ kho, phiếu nhập, phiếu chuyển, kiểm kê, danh mục nhà cung cấp.</summary>
    public const string StockDesk = RoleCodes.Storekeeper + "," + RoleCodes.Manager + "," + RoleCodes.Admin;

    /// <summary>QL, AD — duyệt kiểm kê, mức tồn tối thiểu, thanh toán &amp; công nợ nhà cung cấp, đổi trạng thái serial.</summary>
    public const string Approvers = RoleCodes.Manager + "," + RoleCodes.Admin;

    /// <summary>NV, KH, AD — tra cứu serial; khách chỉ tra được hàng thuộc đơn của mình (proc ép CustomerId).</summary>
    public const string SerialLookup = RoleCodes.Staff + "," + RoleCodes.Customer + "," + RoleCodes.Admin;
}
