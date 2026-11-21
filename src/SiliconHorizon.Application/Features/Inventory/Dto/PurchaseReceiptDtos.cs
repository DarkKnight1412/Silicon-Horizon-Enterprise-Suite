namespace SiliconHorizon.Application.Features.Inventory.Dto;

/// <summary>
/// Một dòng hàng của phiếu nhập. <c>Serials</c> chỉ dùng cho sản phẩm <c>RequiresSerial = 1</c>
/// và PHẢI có đúng <c>Quantity</c> phần tử, không trùng (INV-07, INV-08).
/// </summary>
public sealed class PurchaseReceiptItemInput
{
    public long ProductId { get; set; }

    /// <summary>Phải &gt; 0.</summary>
    public int Quantity { get; set; }

    /// <summary>Đơn giá nhập, VND (decimal(18,0)), không âm.</summary>
    public decimal UnitCost { get; set; }

    /// <summary>Danh sách số serial (mỗi số tối đa 100 ký tự) — bắt buộc với hàng quản lý theo serial.</summary>
    public IReadOnlyList<string>? Serials { get; set; }
}

/// <summary>Yêu cầu nhập kho — POST /api/v1/purchase-receipts (usp_PurchaseReceipt_Create).</summary>
/// <remarks>
/// Không có <c>branchId</c> trong request: API ép <c>@BranchId</c> từ token (kho nhập về chi nhánh của thủ kho).
/// </remarks>
public class CreatePurchaseReceiptRequest
{
    public long SupplierId { get; set; }

    /// <summary>Số hóa đơn của nhà cung cấp (varchar(50)).</summary>
    public string? SupplierInvoiceNo { get; set; }

    public IReadOnlyList<PurchaseReceiptItemInput> Items { get; set; } = Array.Empty<PurchaseReceiptItemInput>();
}

/// <summary>Result set của <c>dbo.usp_PurchaseReceipt_Create</c>.</summary>
public sealed class CreatedPurchaseReceiptRow
{
    public long ReceiptId { get; set; }

    public string ReceiptCode { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }
}

/// <summary>Kết quả nhập kho trả ra API.</summary>
public class CreatedPurchaseReceiptDto
{
    public long ReceiptId { get; init; }

    public string ReceiptCode { get; init; } = string.Empty;

    public decimal TotalAmount { get; init; }
}

/// <summary>Một dòng danh sách phiếu nhập — result set của <c>dbo.usp_PurchaseReceipt_List</c>.</summary>
public sealed class PurchaseReceiptListItemRow
{
    public long Id { get; set; }

    public string ReceiptCode { get; set; } = string.Empty;

    public long SupplierId { get; set; }

    public string SupplierName { get; set; } = string.Empty;

    public int BranchId { get; set; }

    public byte Status { get; set; }

    public decimal TotalAmount { get; set; }

    public DateTime? ReceivedAt { get; set; }

    public int TotalCount { get; set; }
}

/// <summary>Phiếu nhập trong danh sách — GET /api/v1/purchase-receipts.</summary>
public class PurchaseReceiptListItemDto
{
    public long Id { get; init; }

    public string ReceiptCode { get; init; } = string.Empty;

    public long SupplierId { get; init; }

    public string SupplierName { get; init; } = string.Empty;

    public int BranchId { get; init; }

    public byte Status { get; init; }

    public string StatusName { get; init; } = string.Empty;

    public decimal TotalAmount { get; init; }

    public DateTime? ReceivedAt { get; init; }

    public static PurchaseReceiptListItemDto FromRow(PurchaseReceiptListItemRow row) => new()
    {
        Id = row.Id,
        ReceiptCode = row.ReceiptCode,
        SupplierId = row.SupplierId,
        SupplierName = row.SupplierName,
        BranchId = row.BranchId,
        Status = row.Status,
        StatusName = InventoryStatusText.ReceiptStatusText(row.Status),
        TotalAmount = row.TotalAmount,
        ReceivedAt = row.ReceivedAt
    };
}

/// <summary>Tham số lọc danh sách phiếu nhập; chi nhánh ép từ token (Admin không gắn chi nhánh thì xem tất cả).</summary>
public class PurchaseReceiptListQuery
{
    public long? SupplierId { get; set; }

    /// <summary>UTC, chặn dưới theo <c>ReceivedAt</c>.</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>UTC, chặn trên KHÔNG bao gồm (&lt;).</summary>
    public DateTime? ToDate { get; set; }

    public int PageNumber { get; set; } = 1;

    /// <summary>Proc tự cắt tối đa 100.</summary>
    public int PageSize { get; set; } = 20;
}

/// <summary>Header phiếu nhập — result set 1 của <c>dbo.usp_PurchaseReceipt_Get</c> (<c>r.*</c> + tên NCC/chi nhánh).</summary>
public sealed class PurchaseReceiptHeaderRow
{
    public long Id { get; set; }

    public string ReceiptCode { get; set; } = string.Empty;

    public long SupplierId { get; set; }

    public int BranchId { get; set; }

    public string? SupplierInvoiceNo { get; set; }

    public byte Status { get; set; }

    public decimal TotalAmount { get; set; }

    public DateTime? ReceivedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public string SupplierName { get; set; } = string.Empty;

    public string BranchName { get; set; } = string.Empty;
}

/// <summary>Header phiếu nhập trả ra API.</summary>
public class PurchaseReceiptHeaderDto
{
    public long Id { get; init; }

    public string ReceiptCode { get; init; } = string.Empty;

    public long SupplierId { get; init; }

    public string SupplierName { get; init; } = string.Empty;

    public int BranchId { get; init; }

    public string BranchName { get; init; } = string.Empty;

    public string? SupplierInvoiceNo { get; init; }

    public byte Status { get; init; }

    public string StatusName { get; init; } = string.Empty;

    public decimal TotalAmount { get; init; }

    public DateTime? ReceivedAt { get; init; }

    public DateTime CreatedAt { get; init; }

    public long? CreatedBy { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public long? UpdatedBy { get; init; }

    public static PurchaseReceiptHeaderDto FromRow(PurchaseReceiptHeaderRow row) => new()
    {
        Id = row.Id,
        ReceiptCode = row.ReceiptCode,
        SupplierId = row.SupplierId,
        SupplierName = row.SupplierName,
        BranchId = row.BranchId,
        BranchName = row.BranchName,
        SupplierInvoiceNo = row.SupplierInvoiceNo,
        Status = row.Status,
        StatusName = InventoryStatusText.ReceiptStatusText(row.Status),
        TotalAmount = row.TotalAmount,
        ReceivedAt = row.ReceivedAt,
        CreatedAt = row.CreatedAt,
        CreatedBy = row.CreatedBy,
        UpdatedAt = row.UpdatedAt,
        UpdatedBy = row.UpdatedBy
    };
}

/// <summary>Dòng hàng phiếu nhập — result set 2 của <c>dbo.usp_PurchaseReceipt_Get</c>.</summary>
public sealed class PurchaseReceiptItemRow
{
    public long Id { get; set; }

    public long ProductId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitCost { get; set; }

    public decimal LineTotal { get; set; }
}

/// <summary>Dòng hàng phiếu nhập trả ra API.</summary>
public class PurchaseReceiptItemDto
{
    public long Id { get; init; }

    public long ProductId { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public decimal UnitCost { get; init; }

    public decimal LineTotal { get; init; }

    public static PurchaseReceiptItemDto FromRow(PurchaseReceiptItemRow row) => new()
    {
        Id = row.Id,
        ProductId = row.ProductId,
        Sku = row.Sku,
        Name = row.Name,
        Quantity = row.Quantity,
        UnitCost = row.UnitCost,
        LineTotal = row.LineTotal
    };
}

/// <summary>Serial sinh ra từ phiếu nhập — result set 3 của <c>dbo.usp_PurchaseReceipt_Get</c>.</summary>
public sealed class PurchaseReceiptSerialRow
{
    public long SerialItemId { get; set; }

    public long ProductId { get; set; }

    public string SerialNumber { get; set; } = string.Empty;

    public byte Status { get; set; }
}

/// <summary>Serial của phiếu nhập trả ra API.</summary>
public class PurchaseReceiptSerialDto
{
    public long SerialItemId { get; init; }

    public long ProductId { get; init; }

    public string SerialNumber { get; init; } = string.Empty;

    public byte Status { get; init; }

    public string StatusName { get; init; } = string.Empty;

    public static PurchaseReceiptSerialDto FromRow(PurchaseReceiptSerialRow row) => new()
    {
        SerialItemId = row.SerialItemId,
        ProductId = row.ProductId,
        SerialNumber = row.SerialNumber,
        Status = row.Status,
        StatusName = InventoryStatusText.SerialStatusText(row.Status)
    };
}

/// <summary>Chi tiết phiếu nhập — GET /api/v1/purchase-receipts/{id} (3 result set của usp_PurchaseReceipt_Get).</summary>
public class PurchaseReceiptDetailDto
{
    public PurchaseReceiptHeaderDto Header { get; init; } = new();

    public IReadOnlyList<PurchaseReceiptItemDto> Items { get; init; } = Array.Empty<PurchaseReceiptItemDto>();

    public IReadOnlyList<PurchaseReceiptSerialDto> Serials { get; init; } = Array.Empty<PurchaseReceiptSerialDto>();
}


