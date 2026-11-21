using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Api.Constants;

/// <summary>
/// Nhóm vai trò dùng cho <c>[Authorize(Roles = ...)]</c> của module Đơn hàng / Thanh toán.
/// Khớp cột "Quyền" ở Database/README.md mục 5.1–5.3 và bảng gán quyền cho vai trò trong
/// Database/07_seed_admin_user_and_permissions.sql (Admin được cấp mọi quyền, Manager là nhóm duyệt).
/// </summary>
/// <remarks>
/// Khi bảng <c>Permissions</c> đã được seed (file 07), có thể siết thêm bằng mã quyền tương ứng
/// (<c>order.create</c>, <c>order.confirm</c>, <c>order.pack</c>, <c>order.ship</c>,
/// <c>order.cancel.approve</c>). Hiện chưa gắn <c>[RequirePermission]</c> để tránh 403 toàn bộ khi môi trường
/// chưa chạy file seed đó.
/// </remarks>
public static class OrderRoleGroups
{
    /// <summary>KH xem đơn của mình, NV xem đơn chi nhánh mình, AD xem tất cả.</summary>
    public const string OrderViewers = RoleCodes.Customer + "," + RoleCodes.Staff + "," + RoleCodes.Admin;

    /// <summary>TN, TV, QL, AD — tạo đơn tại quầy (order.create), xác nhận đơn (order.confirm), thu tiền.</summary>
    public const string SalesCounter = RoleCodes.SalesDesk + "," + RoleCodes.Approvers;

    /// <summary>TK, AD — bắt đầu đóng gói, quét/gỡ serial, đánh dấu đóng gói xong (order.pack, serial.manage).</summary>
    public const string Warehouse = RoleCodes.Storekeeper + "," + RoleCodes.Admin;

    /// <summary>TN, TV, TK, AD — ghi nhận giao thất bại, giao lại, hoàn tất đơn (order.ship).</summary>
    public const string Delivery = RoleCodes.SalesDesk + "," + RoleCodes.Storekeeper + "," + RoleCodes.Admin;

    /// <summary>QL, AD — duyệt hủy đơn (order.cancel.approve) và hoàn tiền (return.refund).</summary>
    public const string Approval = RoleCodes.Approvers;

    /// <summary>KH xem công nợ của mình, QL và AD xem công nợ khách hàng.</summary>
    public const string LedgerViewers = RoleCodes.Customer + "," + RoleCodes.Approvers;
}
