namespace SiliconHorizon.Application.Features.Inventory.Dto;

/// <summary>Result set của <c>dbo.usp_StockTake_Create</c>.</summary>
public sealed class CreatedStockTakeRow
{
    public long StockTakeId { get; set; }

    public string StockTakeCode { get; set; } = string.Empty;
}

/// <summary>Kết quả mở đợt kiểm kê trả ra API.</summary>
public class CreatedStockTakeDto
{
    public long StockTakeId { get; init; }

    public string StockTakeCode { get; init; } = string.Empty;
}

/// <summary>Một dòng số đếm gửi lên (usp_StockTake_SubmitCounts — @CountsJson).</summary>
public sealed class StockTakeCountInput
{
    public long ProductId { get; set; }

    /// <summary>Số lượng đếm thực tế, không âm.</summary>
    public int CountedQty { get; set; }

    /// <summary>Lý do lệch (nvarchar(300)) — nên nhập khi CountedQty khác tồn hệ thống.</summary>
    public string? Reason { get; set; }
}

/// <summary>
/// Yêu cầu nhập số đếm — POST /api/v1/stock-takes/{id}/counts (usp_StockTake_SubmitCounts).
/// </summary>
public class SubmitStockTakeCountsRequest
{
    public IReadOnlyList<StockTakeCountInput> Counts { get; set; } = Array.Empty<StockTakeCountInput>();

    /// <summary><c>true</c> = gửi duyệt (Đang kiểm kê → Chờ duyệt) và khóa việc nhập số đếm tiếp.</summary>
    public bool Finalize { get; set; }
}

/// <summary>Dòng kiểm kê sau khi nhập số đếm (result set của <c>dbo.usp_StockTake_SubmitCounts</c>).</summary>
public sealed class StockTakeCountRow
{
    public long ProductId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int SystemQty { get; set; }

    public int CountedQty { get; set; }

    /// <summary>Cột tính sẵn: CountedQty - SystemQty.</summary>
    public int DiffQty { get; set; }

    public string? Reason { get; set; }
}

/// <summary>Dòng kiểm kê trả ra API.</summary>
public class StockTakeCountDto
{
    public long ProductId { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public int SystemQty { get; init; }

    public int CountedQty { get; init; }

    public int DiffQty { get; init; }

    public string? Reason { get; init; }

    public static StockTakeCountDto FromRow(StockTakeCountRow row) => new()
    {
        ProductId = row.ProductId,
        Sku = row.Sku,
        Name = row.Name,
        SystemQty = row.SystemQty,
        CountedQty = row.CountedQty,
        DiffQty = row.DiffQty,
        Reason = row.Reason
    };
}

/// <summary>Yêu cầu duyệt/từ chối đợt kiểm kê — POST /api/v1/stock-takes/{id}/approve (usp_StockTake_Approve).</summary>
public class ApproveStockTakeRequest
{
    /// <summary><c>true</c> = duyệt và áp chênh lệch vào tồn thực tế; <c>false</c> = từ chối (không đổi tồn).</summary>
    public bool Approve { get; set; }

    /// <summary>Ghi chú duyệt/từ chối (nvarchar(300)) — proc ghi vào nhật ký AUD-01.</summary>
    public string? Note { get; set; }
}

/// <summary>Header đợt kiểm kê — result set 1 của <c>dbo.usp_StockTake_Get</c> (<c>SELECT * FROM StockTakes</c>).</summary>
public sealed class StockTakeHeaderRow
{
    public long Id { get; set; }

    public string StockTakeCode { get; set; } = string.Empty;

    public int BranchId { get; set; }

    /// <summary>0 Nháp, 1 Đang kiểm kê, 2 Chờ duyệt, 3 Đã duyệt, 4 Đã từ chối.</summary>
    public byte Status { get; set; }

    public long? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }
}

/// <summary>Header đợt kiểm kê trả ra API.</summary>
public class StockTakeHeaderDto
{
    public long Id { get; init; }

    public string StockTakeCode { get; init; } = string.Empty;

    public int BranchId { get; init; }

    public byte Status { get; init; }

    public string StatusName { get; init; } = string.Empty;

    public long? ApprovedBy { get; init; }

    public DateTime? ApprovedAt { get; init; }

    public DateTime CreatedAt { get; init; }

    public long? CreatedBy { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public long? UpdatedBy { get; init; }

    public static StockTakeHeaderDto FromRow(StockTakeHeaderRow row) => new()
    {
        Id = row.Id,
        StockTakeCode = row.StockTakeCode,
        BranchId = row.BranchId,
        Status = row.Status,
        StatusName = InventoryStatusText.StockTakeStatusText(row.Status),
        ApprovedBy = row.ApprovedBy,
        ApprovedAt = row.ApprovedAt,
        CreatedAt = row.CreatedAt,
        CreatedBy = row.CreatedBy,
        UpdatedAt = row.UpdatedAt,
        UpdatedBy = row.UpdatedBy
    };
}

/// <summary>Dòng kiểm kê — result set 2 của <c>dbo.usp_StockTake_Get</c>.</summary>
public sealed class StockTakeItemRow
{
    public long ProductId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Hàng quản lý theo serial không được duyệt khi lệch — phải xử lý từng serial trước (INV-04/INV-05).</summary>
    public bool RequiresSerial { get; set; }

    public int SystemQty { get; set; }

    public int CountedQty { get; set; }

    public int DiffQty { get; set; }

    public string? Reason { get; set; }
}

/// <summary>Dòng kiểm kê trả ra API.</summary>
public class StockTakeItemDto
{
    public long ProductId { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public bool RequiresSerial { get; init; }

    public int SystemQty { get; init; }

    public int CountedQty { get; init; }

    public int DiffQty { get; init; }

    public string? Reason { get; init; }

    public static StockTakeItemDto FromRow(StockTakeItemRow row) => new()
    {
        ProductId = row.ProductId,
        Sku = row.Sku,
        Name = row.Name,
        RequiresSerial = row.RequiresSerial,
        SystemQty = row.SystemQty,
        CountedQty = row.CountedQty,
        DiffQty = row.DiffQty,
        Reason = row.Reason
    };
}

/// <summary>Chi tiết đợt kiểm kê — GET /api/v1/stock-takes/{id} (2 result set của usp_StockTake_Get).</summary>
public class StockTakeDetailDto
{
    public StockTakeHeaderDto Header { get; init; } = new();

    public IReadOnlyList<StockTakeItemDto> Items { get; init; } = Array.Empty<StockTakeItemDto>();
}


