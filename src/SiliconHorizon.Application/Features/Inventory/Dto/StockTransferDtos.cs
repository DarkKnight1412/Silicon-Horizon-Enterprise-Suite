namespace SiliconHorizon.Application.Features.Inventory.Dto;

/// <summary>
/// Một dòng hàng của phiếu chuyển kho. Hàng quản lý theo serial phải liệt kê đủ serial đang ở
/// trạng thái <c>InStock</c> tại chi nhánh ĐI, bằng đúng <c>Quantity</c> (INV-06).
/// </summary>
public sealed class StockTransferItemInput
{
    public long ProductId { get; set; }

    public int Quantity { get; set; }

    public IReadOnlyList<string>? Serials { get; set; }
}

/// <summary>Yêu cầu tạo phiếu chuyển kho (nháp) — POST /api/v1/stock-transfers (usp_StockTransfer_Create).</summary>
/// <remarks>
/// <c>@FromBranchId</c> LUÔN ép từ token (thủ kho chỉ xuất hàng của chi nhánh mình); client chỉ được
/// chọn <c>ToBranchId</c>. Chi nhánh đi trùng chi nhánh đến → proc báo 50004.
/// </remarks>
public class CreateStockTransferRequest
{
    public int ToBranchId { get; set; }

    public IReadOnlyList<StockTransferItemInput> Items { get; set; } = Array.Empty<StockTransferItemInput>();
}

/// <summary>Result set của <c>dbo.usp_StockTransfer_Create</c>.</summary>
public sealed class CreatedStockTransferRow
{
    public long TransferId { get; set; }

    public string TransferCode { get; set; } = string.Empty;
}

/// <summary>Kết quả tạo phiếu chuyển kho trả ra API.</summary>
public class CreatedStockTransferDto
{
    public long TransferId { get; init; }

    public string TransferCode { get; init; } = string.Empty;
}

/// <summary>Header phiếu chuyển — result set 1 của <c>dbo.usp_StockTransfer_Get</c> (<c>t.*</c> + tên hai chi nhánh).</summary>
public sealed class StockTransferHeaderRow
{
    public long Id { get; set; }

    public string TransferCode { get; set; } = string.Empty;

    public int FromBranchId { get; set; }

    public int ToBranchId { get; set; }

    /// <summary>0 Nháp, 1 Đang chuyển, 2 Đã nhận, 3 Đã hủy.</summary>
    public byte Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public string FromBranchName { get; set; } = string.Empty;

    public string ToBranchName { get; set; } = string.Empty;
}

/// <summary>Header phiếu chuyển kho trả ra API.</summary>
public class StockTransferHeaderDto
{
    public long Id { get; init; }

    public string TransferCode { get; init; } = string.Empty;

    public int FromBranchId { get; init; }

    public string FromBranchName { get; init; } = string.Empty;

    public int ToBranchId { get; init; }

    public string ToBranchName { get; init; } = string.Empty;

    public byte Status { get; init; }

    public string StatusName { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public long? CreatedBy { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public long? UpdatedBy { get; init; }

    public static StockTransferHeaderDto FromRow(StockTransferHeaderRow row) => new()
    {
        Id = row.Id,
        TransferCode = row.TransferCode,
        FromBranchId = row.FromBranchId,
        FromBranchName = row.FromBranchName,
        ToBranchId = row.ToBranchId,
        ToBranchName = row.ToBranchName,
        Status = row.Status,
        StatusName = InventoryStatusText.TransferStatusText(row.Status),
        CreatedAt = row.CreatedAt,
        CreatedBy = row.CreatedBy,
        UpdatedAt = row.UpdatedAt,
        UpdatedBy = row.UpdatedBy
    };
}

/// <summary>Dòng hàng phiếu chuyển — result set 2 của <c>dbo.usp_StockTransfer_Get</c>.</summary>
public sealed class StockTransferItemRow
{
    public long Id { get; set; }

    public long ProductId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Quantity { get; set; }
}

/// <summary>Dòng hàng phiếu chuyển kho trả ra API.</summary>
public class StockTransferItemDto
{
    public long Id { get; init; }

    public long ProductId { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public static StockTransferItemDto FromRow(StockTransferItemRow row) => new()
    {
        Id = row.Id,
        ProductId = row.ProductId,
        Sku = row.Sku,
        Name = row.Name,
        Quantity = row.Quantity
    };
}

/// <summary>Serial gắn vào phiếu chuyển — result set 3 của <c>dbo.usp_StockTransfer_Get</c>.</summary>
public sealed class StockTransferSerialRow
{
    public long TransferItemId { get; set; }

    public long SerialItemId { get; set; }

    public string SerialNumber { get; set; } = string.Empty;

    public byte Status { get; set; }
}

/// <summary>Serial của phiếu chuyển trả ra API.</summary>
public class StockTransferSerialDto
{
    public long TransferItemId { get; init; }

    public long SerialItemId { get; init; }

    public string SerialNumber { get; init; } = string.Empty;

    public byte Status { get; init; }

    public string StatusName { get; init; } = string.Empty;

    public static StockTransferSerialDto FromRow(StockTransferSerialRow row) => new()
    {
        TransferItemId = row.TransferItemId,
        SerialItemId = row.SerialItemId,
        SerialNumber = row.SerialNumber,
        Status = row.Status,
        StatusName = InventoryStatusText.SerialStatusText(row.Status)
    };
}

/// <summary>Chi tiết phiếu chuyển kho — GET /api/v1/stock-transfers/{id}.</summary>
public class StockTransferDetailDto
{
    public StockTransferHeaderDto Header { get; init; } = new();

    public IReadOnlyList<StockTransferItemDto> Items { get; init; } = Array.Empty<StockTransferItemDto>();

    public IReadOnlyList<StockTransferSerialDto> Serials { get; init; } = Array.Empty<StockTransferSerialDto>();
}
