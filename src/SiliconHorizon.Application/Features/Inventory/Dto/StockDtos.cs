namespace SiliconHorizon.Application.Features.Inventory.Dto;

/// <summary>
/// Một dòng tồn kho — khớp CHÍNH XÁC các cột của result set <c>dbo.usp_Stock_GetLevels</c>.
/// </summary>
public sealed class StockLevelRow
{
    public int BranchId { get; set; }

    public string BranchName { get; set; } = string.Empty;

    public long ProductId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public int CategoryId { get; set; }

    /// <summary>Số đơn vị đang có ở kho (serial: InStock + Allocated).</summary>
    public int PhysicalQty { get; set; }

    /// <summary>Số đơn vị đang giữ cho đơn hàng chưa hoàn tất.</summary>
    public int ReservedQty { get; set; }

    /// <summary>Cột tính sẵn: AvailableQty = PhysicalQty - ReservedQty.</summary>
    public int AvailableQty { get; set; }

    /// <summary>Mức tồn tối thiểu hiệu lực: <c>StockLevels.MinStock</c>, NULL thì lấy <c>Products.MinStockDefault</c>.</summary>
    public int? MinStock { get; set; }

    /// <summary>1 khi AvailableQty &lt; MinStock (UC-34, INV-02).</summary>
    public bool IsBelowMin { get; set; }

    public int TotalCount { get; set; }
}

/// <summary>Dữ liệu tồn kho trả ra API — GET /api/v1/stock.</summary>
public class StockLevelDto
{
    public int BranchId { get; init; }

    public string BranchName { get; init; } = string.Empty;

    public long ProductId { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public int CategoryId { get; init; }

    public int PhysicalQty { get; init; }

    public int ReservedQty { get; init; }

    public int AvailableQty { get; init; }

    public int? MinStock { get; init; }

    public bool IsBelowMin { get; init; }

    public static StockLevelDto FromRow(StockLevelRow row) => new()
    {
        BranchId = row.BranchId,
        BranchName = row.BranchName,
        ProductId = row.ProductId,
        Sku = row.Sku,
        ProductName = row.ProductName,
        CategoryId = row.CategoryId,
        PhysicalQty = row.PhysicalQty,
        ReservedQty = row.ReservedQty,
        AvailableQty = row.AvailableQty,
        MinStock = row.MinStock,
        IsBelowMin = row.IsBelowMin
    };
}

/// <summary>
/// Tham số lọc tồn kho của GET /api/v1/stock. KHÔNG có <c>branchId</c>:
/// API ép phạm vi chi nhánh từ token (BRD 2.2) — Admin không gắn chi nhánh thì xem toàn hệ thống.
/// </summary>
public class StockLevelQuery
{
    public long? ProductId { get; set; }

    public int? CategoryId { get; set; }

    /// <summary>Từ khóa theo tên sản phẩm (LIKE) hoặc đầu mã SKU — tối đa 100 ký tự (@Keyword nvarchar(100)).</summary>
    public string? Keyword { get; set; }

    /// <summary><c>true</c> = chỉ trả hàng có AvailableQty dưới mức tồn tối thiểu (dùng cho màn hình cảnh báo INV-02/UC-34).</summary>
    public bool BelowMinOnly { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}

/// <summary>Yêu cầu đặt mức tồn tối thiểu — PUT /api/v1/stock/min (usp_Stock_SetMin).</summary>
public class SetMinStockRequest
{
    public long ProductId { get; set; }

    /// <summary><c>null</c> = dùng mặc định <c>Products.MinStockDefault</c>. Giá trị âm bị proc từ chối (50004).</summary>
    public int? MinStock { get; set; }
}

/// <summary>
/// Một dòng thẻ kho — khớp CHÍNH XÁC các cột của result set <c>dbo.usp_Stock_GetMovements</c>.
/// Bảng <c>StockMovements</c> là append-only: không có API sửa/xóa bút toán.
/// </summary>
public sealed class StockMovementRow
{
    public long Id { get; set; }

    /// <summary>Xem <c>StockMovementType</c> (0 Receipt … 9 SupplierReturn).</summary>
    public byte MovementType { get; set; }

    public int PhysicalDelta { get; set; }

    public int ReservedDelta { get; set; }

    /// <summary>Order, PurchaseReceipt, StockTransfer, StockTake, ReturnRequest, SerialItem…</summary>
    public string RefType { get; set; } = string.Empty;

    public long RefId { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public int TotalCount { get; set; }
}

/// <summary>Bút toán kho trả ra API — GET /api/v1/stock/movements.</summary>
public class StockMovementDto
{
    public long Id { get; init; }

    public byte MovementType { get; init; }

    public string MovementTypeName { get; init; } = string.Empty;

    public int PhysicalDelta { get; init; }

    public int ReservedDelta { get; init; }

    public string RefType { get; init; } = string.Empty;

    public long RefId { get; init; }

    public string? Note { get; init; }

    public DateTime CreatedAt { get; init; }

    public long? CreatedBy { get; init; }

    public static StockMovementDto FromRow(StockMovementRow row) => new()
    {
        Id = row.Id,
        MovementType = row.MovementType,
        MovementTypeName = InventoryStatusText.MovementTypeText(row.MovementType),
        PhysicalDelta = row.PhysicalDelta,
        ReservedDelta = row.ReservedDelta,
        RefType = row.RefType,
        RefId = row.RefId,
        Note = row.Note,
        CreatedAt = row.CreatedAt,
        CreatedBy = row.CreatedBy
    };
}

/// <summary>
/// Tham số thẻ kho của GET /api/v1/stock/movements. Proc yêu cầu <c>@BranchId</c> và <c>@ProductId</c>
/// nên cả hai đều bắt buộc (chi nhánh ép từ token).
/// </summary>
public class StockMovementQuery
{
    public long ProductId { get; set; }

    /// <summary>UTC, chặn dưới (>=).</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>UTC, chặn trên KHÔNG bao gồm (&lt;) — truyền đầu ngày kế tiếp.</summary>
    public DateTime? ToDate { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}
