namespace SiliconHorizon.Application.Common.Sql;

/// <summary>
/// Tên stored procedure dùng từ tầng Application — tầng Application là nơi "gọi" proc nên cũng là nơi
/// khai báo tên. Danh sách dưới đây chỉ gồm các proc đã có trong 5 file <c>Database/0x_sp_*.sql</c>.
/// </summary>
public static class StoredProcedureNames
{
    // ── Tài khoản (module Auth) ──────────────────────────────────────────────
    public const string AuthGetUserByUsername = "dbo.usp_Auth_GetUserByUsername";

    public const string AuthRegisterCustomer = "dbo.usp_Auth_RegisterCustomer";

    public const string AuthUpdateLastLogin = "dbo.usp_Auth_UpdateLastLogin";

    // ── Dùng chung ───────────────────────────────────────────────────────────
    public const string AuditWrite = "dbo.usp_Audit_Write";

    public const string AuditSearch = "dbo.usp_Audit_Search";

    // ── Nội bộ (chỉ gọi từ proc khác hoặc lớp hạ tầng) ───────────────────────
    public const string InternalStockMove = "dbo.usp_Internal_StockMove";

    public const string InternalSetOrderStatus = "dbo.usp_Internal_SetOrderStatus";

    public const string InternalCancelOrder = "dbo.usp_Internal_CancelOrder";

    public const string InternalLogSerial = "dbo.usp_Internal_LogSerial";

    public const string InternalSaveSpecs = "dbo.usp_Internal_SaveSpecs";

    public const string InternalResolveCart = "dbo.usp_Internal_ResolveCart";

    public const string InternalAccrueCommission = "dbo.usp_Internal_AccrueCommission";

    public const string InternalClawbackCommission = "dbo.usp_Internal_ClawbackCommission";

    public const string InternalGetOpenPeriod = "dbo.usp_Internal_GetOpenPeriod";

    // ── Danh mục sản phẩm, giá ───────────────────────────────────────────────
    public const string ProductSearch = "dbo.usp_Product_Search";

    public const string ProductGetById = "dbo.usp_Product_GetById";

    public const string ProductCreate = "dbo.usp_Product_Create";

    public const string ProductUpdate = "dbo.usp_Product_Update";

    public const string ProductSetStatus = "dbo.usp_Product_SetStatus";

    public const string ProductPriceUpsert = "dbo.usp_ProductPrice_Upsert";

    public const string CompatCheck = "dbo.usp_Compat_Check";

    public const string VoucherValidate = "dbo.usp_Voucher_Validate";

    // ── Giỏ hàng ─────────────────────────────────────────────────────────────
    public const string CartGet = "dbo.usp_Cart_Get";

    public const string CartAddItem = "dbo.usp_Cart_AddItem";

    public const string CartUpdateItem = "dbo.usp_Cart_UpdateItem";

    public const string CartRemoveItem = "dbo.usp_Cart_RemoveItem";

    public const string CartSelectAll = "dbo.usp_Cart_SelectAll";

    public const string CartMerge = "dbo.usp_Cart_Merge";

    /// <summary>Thêm NHIỀU sản phẩm trong một giao dịch — dùng cho tab "Tự build PC".</summary>
    public const string CartAddItems = "dbo.usp_Cart_AddItems";

    // ── PC build sẵn (combo linh kiện) (Database/10_pc_builds.sql) ───────────
    public const string PcBuildSearch = "dbo.usp_PcBuild_Search";

    public const string PcBuildGetById = "dbo.usp_PcBuild_GetById";

    public const string PcBuildAddToCart = "dbo.usp_PcBuild_AddToCart";

    /// <summary>Danh mục linh kiện + thông số cho tab "TỰ BUILD PC" (khách tự chọn từng món).</summary>
    public const string PcBuildBuilderCatalog = "dbo.usp_PcBuild_BuilderCatalog";

    // ── Đơn hàng (Database/03_sp_orders_payments.sql) ───────────────────────
    public const string OrderCreate = "dbo.usp_Order_Create";

    public const string OrderGet = "dbo.usp_Order_Get";

    public const string OrderList = "dbo.usp_Order_List";

    public const string OrderConfirm = "dbo.usp_Order_Confirm";

    public const string OrderStartPacking = "dbo.usp_Order_StartPacking";

    public const string OrderAssignSerial = "dbo.usp_Order_AssignSerial";

    public const string OrderUnassignSerial = "dbo.usp_Order_UnassignSerial";

    public const string OrderMarkReady = "dbo.usp_Order_MarkReady";

    public const string OrderMarkDeliveryFailed = "dbo.usp_Order_MarkDeliveryFailed";

    public const string OrderRetryDelivery = "dbo.usp_Order_RetryDelivery";

    public const string OrderComplete = "dbo.usp_Order_Complete";

    public const string OrderCancel = "dbo.usp_Order_Cancel";

    /// <summary>Job mỗi phút hủy đơn quá hạn giữ hàng/hạn nhận hàng (UC-33) — không có endpoint, gọi từ BackgroundService.</summary>
    public const string OrderExpireOverdue = "dbo.usp_Order_ExpireOverdue";

    // ── Thanh toán (Database/03_sp_orders_payments.sql) ──────────────────────
    public const string PaymentRecord = "dbo.usp_Payment_Record";

    public const string PaymentRefund = "dbo.usp_Payment_Refund";

    // ── Nhân sự, hoa hồng ────────────────────────────────────────────────────
    public const string ScheduleAssign = "dbo.usp_Schedule_Assign";

    public const string ScheduleRemove = "dbo.usp_Schedule_Remove";

    public const string ScheduleList = "dbo.usp_Schedule_List";

    public const string AttendanceCheckIn = "dbo.usp_Attendance_CheckIn";

    public const string AttendanceCheckOut = "dbo.usp_Attendance_CheckOut";

    public const string AttendanceAdjust = "dbo.usp_Attendance_Adjust";

    public const string AttendanceGetMonthly = "dbo.usp_Attendance_GetMonthly";

    public const string CommissionSummary = "dbo.usp_Commission_Summary";

    public const string CommissionAdjust = "dbo.usp_Commission_Adjust";

    public const string CommissionClosePeriod = "dbo.usp_Commission_ClosePeriod";

    // ── Báo cáo ──────────────────────────────────────────────────────────────
    public const string ReportRevenue = "dbo.usp_Report_Revenue";

    public const string ReportInventory = "dbo.usp_Report_Inventory";

    public const string ReportSerialStatus = "dbo.usp_Report_SerialStatus";

    public const string ReportDebt = "dbo.usp_Report_Debt";

    public const string ReportWarranty = "dbo.usp_Report_Warranty";

    public const string CustomerGetLedger = "dbo.usp_Customer_GetLedger";

    // TODO: bổ sung tên proc của module Kho/Bảo hành/Đổi trả khi implement từng module
    //       (usp_Inventory_*, usp_Transfer_*, usp_StockTake_*, usp_Serial_*,
    //        usp_Warranty_*, usp_Return_*, usp_Supplier_*).
    // ── Kho, serial, nhà cung cấp (Database/04_sp_inventory_suppliers.sql) ───
    // Ghi chú: phần KHO + SERIAL + NHÀ CUNG CẤP của TODO trên đã được bổ sung ở đây;
    // các nhóm usp_Warranty_*/usp_Return_* vẫn chờ module tương ứng.

    public const string StockGetLevels = "dbo.usp_Stock_GetLevels";

    public const string StockSetMin = "dbo.usp_Stock_SetMin";

    public const string StockGetMovements = "dbo.usp_Stock_GetMovements";

    public const string PurchaseReceiptCreate = "dbo.usp_PurchaseReceipt_Create";

    public const string PurchaseReceiptGet = "dbo.usp_PurchaseReceipt_Get";

    public const string PurchaseReceiptList = "dbo.usp_PurchaseReceipt_List";

    public const string StockTransferCreate = "dbo.usp_StockTransfer_Create";

    public const string StockTransferGet = "dbo.usp_StockTransfer_Get";

    public const string StockTransferShip = "dbo.usp_StockTransfer_Ship";

    public const string StockTransferReceive = "dbo.usp_StockTransfer_Receive";

    public const string StockTransferCancel = "dbo.usp_StockTransfer_Cancel";

    public const string StockTakeCreate = "dbo.usp_StockTake_Create";

    public const string StockTakeSubmitCounts = "dbo.usp_StockTake_SubmitCounts";

    public const string StockTakeApprove = "dbo.usp_StockTake_Approve";

    public const string StockTakeGet = "dbo.usp_StockTake_Get";

    public const string SerialLookup = "dbo.usp_Serial_Lookup";

    public const string SerialChangeStatus = "dbo.usp_Serial_ChangeStatus";

    public const string SupplierReturnCreate = "dbo.usp_SupplierReturn_Create";

    public const string SupplierPaymentRecord = "dbo.usp_SupplierPayment_Record";

    public const string SupplierGetLedger = "dbo.usp_Supplier_GetLedger";

    // ── Bảo hành (Database/05_sp_warranty_returns_commission.sql) ────────────
    public const string WarrantyReceive = "dbo.usp_Warranty_Receive";

    public const string WarrantyChangeStatus = "dbo.usp_Warranty_ChangeStatus";

    public const string WarrantyAddAttachment = "dbo.usp_Warranty_AddAttachment";

    public const string WarrantyGet = "dbo.usp_Warranty_Get";

    public const string WarrantyList = "dbo.usp_Warranty_List";

    // ── Đổi trả (Database/05_sp_warranty_returns_commission.sql) ─────────────
    public const string ReturnCreate = "dbo.usp_Return_Create";

    public const string ReturnReview = "dbo.usp_Return_Review";

    public const string ReturnInspect = "dbo.usp_Return_Inspect";

    public const string ReturnRefund = "dbo.usp_Return_Refund";

    public const string ReturnGet = "dbo.usp_Return_Get";

    public const string ReturnList = "dbo.usp_Return_List";
}

