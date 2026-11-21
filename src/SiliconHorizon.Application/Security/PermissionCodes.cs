namespace SiliconHorizon.Application.Security;

/// <summary>
/// Mã quyền dùng cho <c>[RequirePermission("...")]</c> — khớp cột <c>dbo.Permissions.Code</c>.
/// <para>
/// ⚠️ Database/table_and_data.sql hiện <b>chưa seed</b> bảng <c>Permissions</c> và <c>RolePermissions</c>,
/// nên các mã dưới đây là <b>đề xuất</b> theo ma trận phân quyền BRD mục 2.2.
/// Các mã đã xuất hiện trong script gốc: <c>order.cancel.approve</c>, <c>stock.adjust.approve</c>.
/// Chỉ dùng <c>[RequirePermission("...")]</c> sau khi đã seed quyền cho vai trò.
/// </para>
/// </summary>
public static class PermissionCodes
{
    // ── Đơn hàng (ORD) ───────────────────────────────────────────────────────
    public const string OrderCreate = "order.create";

    public const string OrderConfirm = "order.confirm";

    public const string OrderPack = "order.pack";

    public const string OrderShip = "order.ship";

    public const string OrderCancelRequest = "order.cancel.request";

    /// <summary>Quản lý chi nhánh duyệt hủy đơn (ORD-07, UC-20).</summary>
    public const string OrderCancelApprove = "order.cancel.approve";

    // ── Kho, serial (INV, SER) ───────────────────────────────────────────────
    public const string StockReceipt = "stock.receipt";

    public const string StockTransfer = "stock.transfer";

    public const string StockTake = "stock.take";

    /// <summary>Quản lý duyệt điều chỉnh tồn kho (INV-04, UC-21).</summary>
    public const string StockAdjustApprove = "stock.adjust.approve";

    public const string SerialManage = "serial.manage";

    // ── Sản phẩm, giá, khuyến mãi (PRD, PRC) ─────────────────────────────────
    public const string CatalogManage = "catalog.manage";

    public const string PriceManage = "price.manage";

    public const string PromotionManage = "promotion.manage";

    // ── Bảo hành, đổi trả (WAR, RET) ─────────────────────────────────────────
    public const string WarrantyReceive = "warranty.receive";

    public const string WarrantyProcess = "warranty.process";

    public const string ReturnRequest = "return.request";

    public const string ReturnReview = "return.review";

    public const string ReturnInspect = "return.inspect";

    public const string ReturnRefund = "return.refund";

    // ── Nhân sự, hoa hồng, nhật ký, cấu hình (HR, KPI, AUD) ──────────────────
    public const string ScheduleManage = "hr.schedule.manage";

    public const string AttendanceAdjust = "hr.attendance.adjust";

    public const string CommissionManage = "commission.manage";

    public const string AuditView = "audit.view";

    public const string SettingsManage = "settings.manage";
}
