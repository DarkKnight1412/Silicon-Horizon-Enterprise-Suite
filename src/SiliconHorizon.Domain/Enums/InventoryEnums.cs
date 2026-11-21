namespace SiliconHorizon.Domain.Enums;

/// <summary>
/// StockMovements.MovementType — loại bút toán kho (INV-03).
/// Bảng StockMovements là bất biến: chỉ INSERT, không sửa/xóa.
/// </summary>
public enum StockMovementType : byte
{
    Receipt = 0,
    Reserve = 1,
    Release = 2,
    Issue = 3,
    TransferOut = 4,
    TransferIn = 5,
    StockTakeAdjust = 6,
    ManualAdjust = 7,
    CustomerReturn = 8,
    SupplierReturn = 9
}

/// <summary>PurchaseReceipts.Status — phiếu nhập kho (INV-07..09).</summary>
public enum PurchaseReceiptStatus : byte
{
    Draft = 0,
    Completed = 1,
    Cancelled = 2
}
