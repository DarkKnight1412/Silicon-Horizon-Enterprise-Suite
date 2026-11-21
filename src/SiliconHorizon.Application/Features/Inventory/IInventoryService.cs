using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Inventory.Dto;

namespace SiliconHorizon.Application.Features.Inventory;

/// <summary>
/// Use case kho — tồn kho, nhập kho, chuyển kho nội bộ, kiểm kê, serial.
/// Mọi phương thức chỉ gọi stored procedure trong <c>Database/04_sp_inventory_suppliers.sql</c>.
/// </summary>
public interface IInventoryService
{
    // ── Tồn kho (INV-02, INV-03) ──────────────────────────────────────────────

    /// <summary>Tồn theo chi nhánh/sản phẩm — GET /api/v1/stock (usp_Stock_GetLevels). Chi nhánh ép từ token.</summary>
    Task<PagedResult<StockLevelDto>> GetStockLevelsAsync(
        StockLevelQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Đặt mức tồn tối thiểu theo chi nhánh + sản phẩm — PUT /api/v1/stock/min (usp_Stock_SetMin).
    /// Dùng để bật cảnh báo tồn thấp (INV-02/UC-34); <c>MinStock = null</c> = trả về mặc định của sản phẩm.
    /// </summary>
    Task SetMinStockAsync(SetMinStockRequest request, CancellationToken cancellationToken = default);

    /// <summary>Thẻ kho (lịch sử biến động, chỉ đọc) — GET /api/v1/stock/movements (usp_Stock_GetMovements).</summary>
    Task<PagedResult<StockMovementDto>> GetStockMovementsAsync(
        StockMovementQuery query,
        CancellationToken cancellationToken = default);

    // ── Nhập kho (INV-07..09, UC-13) ──────────────────────────────────────────

    /// <summary>
    /// Nhập kho + sinh serial trong MỘT giao dịch của proc — POST /api/v1/purchase-receipts
    /// (usp_PurchaseReceipt_Create). Chi nhánh nhận hàng ép từ token.
    /// </summary>
    Task<CreatedPurchaseReceiptDto> CreatePurchaseReceiptAsync(
        CreatePurchaseReceiptRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Danh sách phiếu nhập — GET /api/v1/purchase-receipts (usp_PurchaseReceipt_List).</summary>
    Task<PagedResult<PurchaseReceiptListItemDto>> ListPurchaseReceiptsAsync(
        PurchaseReceiptListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Chi tiết phiếu nhập kèm serial — GET /api/v1/purchase-receipts/{id} (usp_PurchaseReceipt_Get).</summary>
    Task<PurchaseReceiptDetailDto> GetPurchaseReceiptAsync(
        long receiptId,
        CancellationToken cancellationToken = default);

    // ── Chuyển kho nội bộ (INV-06, UC-14) ────────────────────────────────────

    /// <summary>
    /// Tạo phiếu chuyển kho ở trạng thái nháp — POST /api/v1/stock-transfers (usp_StockTransfer_Create).
    /// Chi nhánh ĐI ép từ token; chưa trừ tồn cho tới khi gọi <c>ship</c>.
    /// </summary>
    Task<CreatedStockTransferDto> CreateStockTransferAsync(
        CreateStockTransferRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Chi tiết phiếu chuyển — GET /api/v1/stock-transfers/{id} (usp_StockTransfer_Get).</summary>
    Task<StockTransferDetailDto> GetStockTransferAsync(
        long transferId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xuất đi — POST /api/v1/stock-transfers/{id}/ship (usp_StockTransfer_Ship): trừ tồn thực tế chi nhánh đi,
    /// serial InStock → InTransfer. Chỉ chi nhánh đi thực hiện được.
    /// </summary>
    Task ShipStockTransferAsync(long transferId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nhận hàng — POST /api/v1/stock-transfers/{id}/receive (usp_StockTransfer_Receive): cộng tồn chi nhánh đến,
    /// serial InTransfer → InStock và đổi <c>BranchId</c> của serial. Chỉ chi nhánh đến thực hiện được.
    /// </summary>
    Task ReceiveStockTransferAsync(long transferId, CancellationToken cancellationToken = default);

    /// <summary>Hủy phiếu nháp — POST /api/v1/stock-transfers/{id}/cancel (usp_StockTransfer_Cancel).</summary>
    Task CancelStockTransferAsync(long transferId, CancellationToken cancellationToken = default);

    // ── Kiểm kê (INV-04, INV-05, UC-15, UC-21) ───────────────────────────────

    /// <summary>Mở đợt kiểm kê cho chi nhánh của người gọi — POST /api/v1/stock-takes (usp_StockTake_Create).</summary>
    Task<CreatedStockTakeDto> CreateStockTakeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Nhập số đếm (và gửi duyệt nếu <c>Finalize = true</c>) — POST /api/v1/stock-takes/{id}/counts
    /// (usp_StockTake_SubmitCounts). Trả về danh sách dòng kèm chênh lệch.
    /// </summary>
    Task<IReadOnlyList<StockTakeCountDto>> SubmitStockTakeCountsAsync(
        long stockTakeId,
        SubmitStockTakeCountsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Duyệt/từ chối đợt kiểm kê — POST /api/v1/stock-takes/{id}/approve (usp_StockTake_Approve).
    /// Proc từ chối duyệt khi hàng quản lý theo serial bị lệch (phải xử lý từng serial rồi kiểm kê lại).
    /// </summary>
    Task ApproveStockTakeAsync(
        long stockTakeId,
        ApproveStockTakeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Chi tiết đợt kiểm kê — GET /api/v1/stock-takes/{id} (usp_StockTake_Get).</summary>
    Task<StockTakeDetailDto> GetStockTakeAsync(long stockTakeId, CancellationToken cancellationToken = default);

    // ── Serial (SER-01..04) ──────────────────────────────────────────────────

    /// <summary>
    /// Tra cứu serial theo số serial — GET /api/v1/serials/{serialNumber} (usp_Serial_Lookup): thông tin bảo hành,
    /// đơn mua, lịch sử trạng thái, phiếu bảo hành. Khách chỉ tra được hàng thuộc đơn của mình.
    /// </summary>
    Task<SerialLookupDto> LookupSerialAsync(
        string serialNumber,
        long? productId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Đổi trạng thái serial theo <c>SerialItemId</c> — POST /api/v1/serials/{id}/status (usp_Serial_ChangeStatus).
    /// Chỉ cho phép InStock→Defective, Defective→Scrapped, Defective→InStock; rời/vào InStock thì tồn thực tế ±1.
    /// </summary>
    Task ChangeSerialStatusAsync(
        long serialItemId,
        ChangeSerialStatusRequest request,
        CancellationToken cancellationToken = default);
}

