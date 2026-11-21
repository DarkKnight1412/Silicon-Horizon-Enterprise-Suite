using SiliconHorizon.Domain.Enums;

namespace SiliconHorizon.Application.Features.Inventory.Dto;

/// <summary>
/// Hằng số trạng thái + tên hiển thị tiếng Việt dùng chung cho module Kho/Serial/Nhà cung cấp.
/// </summary>
/// <remarks>
/// Hai enum <c>StockMovementType</c> và <c>PurchaseReceiptStatus</c> đã có sẵn ở
/// <c>SiliconHorizon.Domain.Enums</c>. Ba bảng còn lại (<c>StockTransfers</c>, <c>StockTakes</c>,
/// <c>SerialItems</c>) chưa có enum trong tầng Domain nên hằng số được khai báo tại đây, khớp đúng
/// giá trị số và CHECK constraint trong <c>Database/table_and_data.sql</c> (mục 8).
/// </remarks>
public static class InventoryStatusText
{
    // ── StockTransfers.Status ────────────────────────────────────────────────
    /// <summary>Phiếu chuyển mới tạo, chưa xuất kho.</summary>
    public const byte TransferDraft = 0;

    /// <summary>Hàng đã rời chi nhánh đi, chưa nhập chi nhánh đến.</summary>
    public const byte TransferInTransit = 1;

    /// <summary>Chi nhánh đến đã nhận hàng.</summary>
    public const byte TransferReceived = 2;

    /// <summary>Phiếu nháp bị hủy.</summary>
    public const byte TransferCancelled = 3;

    // ── StockTakes.Status ────────────────────────────────────────────────────
    public const byte StockTakeDraft = 0;

    /// <summary>Đang kiểm kê — <c>usp_StockTake_SubmitCounts</c> chỉ nhận số đếm ở trạng thái này.</summary>
    public const byte StockTakeCounting = 1;

    /// <summary>Đã gửi duyệt — chờ Quản lý duyệt/từ chối.</summary>
    public const byte StockTakePendingApproval = 2;

    public const byte StockTakeApproved = 3;

    public const byte StockTakeRejected = 4;

    // ── SerialItems.Status ───────────────────────────────────────────────────
    public const byte SerialInStock = 0;

    public const byte SerialAllocated = 1;

    public const byte SerialInTransfer = 2;

    public const byte SerialSold = 3;

    public const byte SerialRma = 4;

    public const byte SerialReturned = 5;

    public const byte SerialReplaced = 6;

    public const byte SerialDefective = 7;

    public const byte SerialReturnedToSupplier = 8;

    public const byte SerialScrapped = 9;

    /// <summary>
    /// Serial có nằm trong tồn thực tế (Physical) của chi nhánh hay không.
    /// Theo quy ước ở đầu <c>Database/04_sp_inventory_suppliers.sql</c>: Physical = serial <c>InStock</c> + <c>Allocated</c>;
    /// RMA, Returned, Defective, InTransfer, Sold KHÔNG nằm trong Physical.
    /// </summary>
    public static bool IsInPhysicalStock(byte status) => status is SerialInStock or SerialAllocated;

    /// <summary>Tên trạng thái phiếu chuyển kho (StockTransfers.Status).</summary>
    public static string TransferStatusText(byte status) => status switch
    {
        TransferDraft => "Nháp",
        TransferInTransit => "Đang chuyển",
        TransferReceived => "Đã nhận",
        TransferCancelled => "Đã hủy",
        _ => "Không xác định"
    };

    /// <summary>Tên trạng thái đợt kiểm kê (StockTakes.Status).</summary>
    public static string StockTakeStatusText(byte status) => status switch
    {
        StockTakeDraft => "Nháp",
        StockTakeCounting => "Đang kiểm kê",
        StockTakePendingApproval => "Chờ duyệt",
        StockTakeApproved => "Đã duyệt",
        StockTakeRejected => "Đã từ chối",
        _ => "Không xác định"
    };

    /// <summary>Tên trạng thái serial (SerialItems.Status).</summary>
    public static string SerialStatusText(byte status) => status switch
    {
        SerialInStock => "Trong kho",
        SerialAllocated => "Đã giữ cho đơn",
        SerialInTransfer => "Đang chuyển kho",
        SerialSold => "Đã bán",
        SerialRma => "Đang bảo hành",
        SerialReturned => "Khách đã trả",
        SerialReplaced => "Đã đổi mới",
        SerialDefective => "Hàng lỗi",
        SerialReturnedToSupplier => "Đã trả nhà cung cấp",
        SerialScrapped => "Đã hủy",
        _ => "Không xác định"
    };

    /// <summary>Tên loại bút toán kho (StockMovements.MovementType — bảng append-only, chỉ INSERT).</summary>
    public static string MovementTypeText(byte movementType) => (StockMovementType)movementType switch
    {
        StockMovementType.Receipt => "Nhập kho",
        StockMovementType.Reserve => "Giữ hàng cho đơn",
        StockMovementType.Release => "Nhả hàng giữ",
        StockMovementType.Issue => "Xuất bán",
        StockMovementType.TransferOut => "Xuất chuyển kho",
        StockMovementType.TransferIn => "Nhập chuyển kho",
        StockMovementType.StockTakeAdjust => "Điều chỉnh kiểm kê",
        StockMovementType.ManualAdjust => "Điều chỉnh thủ công",
        StockMovementType.CustomerReturn => "Khách trả hàng",
        StockMovementType.SupplierReturn => "Trả nhà cung cấp",
        _ => "Không xác định"
    };

    /// <summary>Tên trạng thái phiếu nhập kho (PurchaseReceipts.Status).</summary>
    public static string ReceiptStatusText(byte status) => (PurchaseReceiptStatus)status switch
    {
        PurchaseReceiptStatus.Draft => "Nháp",
        PurchaseReceiptStatus.Completed => "Hoàn tất",
        PurchaseReceiptStatus.Cancelled => "Đã hủy",
        _ => "Không xác định"
    };

    /// <summary>Tên loại bút toán công nợ nhà cung cấp (SupplierLedger.EntryType).</summary>
    public static string SupplierLedgerEntryTypeText(byte entryType) => entryType switch
    {
        0 => "Ghi tăng nợ phải trả",
        1 => "Thanh toán",
        2 => "Ghi giảm do trả hàng",
        _ => "Không xác định"
    };
}
