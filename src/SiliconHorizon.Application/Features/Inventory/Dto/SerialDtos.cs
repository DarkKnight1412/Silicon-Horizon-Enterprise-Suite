namespace SiliconHorizon.Application.Features.Inventory.Dto;

/// <summary>
/// Thông tin serial — result set 1 của <c>dbo.usp_Serial_Lookup</c> (tra cứu theo số serial).
/// </summary>
public sealed class SerialInfoRow
{
    public long SerialItemId { get; set; }

    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>0 InStock … 9 Scrapped (xem <see cref="InventoryStatusText"/>).</summary>
    public byte Status { get; set; }

    public int BranchId { get; set; }

    public long ProductId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public DateTime? SoldAt { get; set; }

    public DateTime? WarrantyStartDate { get; set; }

    public DateTime? WarrantyEndDate { get; set; }

    public int? WarrantyDaysLeft { get; set; }

    public bool InWarranty { get; set; }

    public long? OrderId { get; set; }

    public string? OrderCode { get; set; }

    public DateTime? PurchaseDate { get; set; }

    public long? CustomerId { get; set; }

    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }

    /// <summary>Serial thay thế (khi bảo hành đổi mới).</summary>
    public long? ReplacedBySerialId { get; set; }
}

/// <summary>Thông tin serial trả ra API.</summary>
public class SerialInfoDto
{
    public long SerialItemId { get; init; }

    public string SerialNumber { get; init; } = string.Empty;

    public byte Status { get; init; }

    public string StatusName { get; init; } = string.Empty;

    public int BranchId { get; init; }

    public long ProductId { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public DateTime? SoldAt { get; init; }

    public DateTime? WarrantyStartDate { get; init; }

    public DateTime? WarrantyEndDate { get; init; }

    /// <summary>Số ngày còn lại của bảo hành (âm = đã hết hạn).</summary>
    public int? WarrantyDaysLeft { get; init; }

    public bool InWarranty { get; init; }

    public long? OrderId { get; init; }

    public string? OrderCode { get; init; }

    public DateTime? PurchaseDate { get; init; }

    public long? CustomerId { get; init; }

    public string? CustomerName { get; init; }

    public string? CustomerPhone { get; init; }

    public long? ReplacedBySerialId { get; init; }

    public static SerialInfoDto FromRow(SerialInfoRow row) => new()
    {
        SerialItemId = row.SerialItemId,
        SerialNumber = row.SerialNumber,
        Status = row.Status,
        StatusName = InventoryStatusText.SerialStatusText(row.Status),
        BranchId = row.BranchId,
        ProductId = row.ProductId,
        Sku = row.Sku,
        ProductName = row.ProductName,
        SoldAt = row.SoldAt,
        WarrantyStartDate = row.WarrantyStartDate,
        WarrantyEndDate = row.WarrantyEndDate,
        WarrantyDaysLeft = row.WarrantyDaysLeft,
        InWarranty = row.InWarranty,
        OrderId = row.OrderId,
        OrderCode = row.OrderCode,
        PurchaseDate = row.PurchaseDate,
        CustomerId = row.CustomerId,
        CustomerName = row.CustomerName,
        CustomerPhone = row.CustomerPhone,
        ReplacedBySerialId = row.ReplacedBySerialId
    };
}

/// <summary>Một lần đổi trạng thái serial — result set 2 của <c>dbo.usp_Serial_Lookup</c>.</summary>
public sealed class SerialStatusHistoryRow
{
    /// <summary><c>NULL</c> khi là lần ghi nhận đầu tiên (nhập kho) — proc ghi FromStatus = NULL.</summary>
    public byte? FromStatus { get; set; }

    public byte ToStatus { get; set; }

    /// <summary>PurchaseReceipt, Order, StockTransfer, SupplierReturn, Manual…</summary>
    public string? RefType { get; set; }

    public long? RefId { get; set; }

    public string? Note { get; set; }

    public DateTime ChangedAt { get; set; }
}

/// <summary>Lịch sử trạng thái serial trả ra API (SerialStatusHistory là bảng append-only).</summary>
public class SerialStatusHistoryDto
{
    public byte? FromStatus { get; init; }

    public string? FromStatusName { get; init; }

    public byte ToStatus { get; init; }

    public string ToStatusName { get; init; } = string.Empty;

    public string? RefType { get; init; }

    public long? RefId { get; init; }

    public string? Note { get; init; }

    public DateTime ChangedAt { get; init; }

    public static SerialStatusHistoryDto FromRow(SerialStatusHistoryRow row) => new()
    {
        FromStatus = row.FromStatus,
        FromStatusName = row.FromStatus is { } from ? InventoryStatusText.SerialStatusText(from) : null,
        ToStatus = row.ToStatus,
        ToStatusName = InventoryStatusText.SerialStatusText(row.ToStatus),
        RefType = row.RefType,
        RefId = row.RefId,
        Note = row.Note,
        ChangedAt = row.ChangedAt
    };
}

/// <summary>Phiếu bảo hành gắn với serial — result set 3 của <c>dbo.usp_Serial_Lookup</c>.</summary>
public sealed class SerialWarrantyTicketRow
{
    public long Id { get; set; }

    public string TicketCode { get; set; } = string.Empty;

    public byte Status { get; set; }

    public string? IssueDescription { get; set; }

    public DateTime? ReceivedAt { get; set; }

    public DateTime? ReturnedAt { get; set; }
}

/// <summary>Phiếu bảo hành của serial trả ra API.</summary>
public class SerialWarrantyTicketDto
{
    public long Id { get; init; }

    public string TicketCode { get; init; } = string.Empty;

    /// <summary>Trạng thái phiếu bảo hành (WarrantyTickets.Status — chi tiết ở module Bảo hành).</summary>
    public byte Status { get; init; }

    public string? IssueDescription { get; init; }

    public DateTime? ReceivedAt { get; init; }

    public DateTime? ReturnedAt { get; init; }

    public static SerialWarrantyTicketDto FromRow(SerialWarrantyTicketRow row) => new()
    {
        Id = row.Id,
        TicketCode = row.TicketCode,
        Status = row.Status,
        IssueDescription = row.IssueDescription,
        ReceivedAt = row.ReceivedAt,
        ReturnedAt = row.ReturnedAt
    };
}

/// <summary>Kết quả tra cứu serial — GET /api/v1/serials/{serialNumber} (3 result set của usp_Serial_Lookup).</summary>
public class SerialLookupDto
{
    public SerialInfoDto Serial { get; init; } = new();

    public IReadOnlyList<SerialStatusHistoryDto> StatusHistory { get; init; } = Array.Empty<SerialStatusHistoryDto>();

    public IReadOnlyList<SerialWarrantyTicketDto> WarrantyTickets { get; init; } = Array.Empty<SerialWarrantyTicketDto>();
}

/// <summary>
/// Yêu cầu đổi trạng thái serial — POST /api/v1/serials/{id}/status (usp_Serial_ChangeStatus).
/// </summary>
public class ChangeSerialStatusRequest
{
    /// <summary>Chỉ nhận 0 (Trong kho), 7 (Hàng lỗi), 9 (Đã hủy) — proc giới hạn các chuyển đổi được phép.</summary>
    public byte ToStatus { get; set; }

    /// <summary>Lý do (bắt buộc, tối đa 300 ký tự) — proc ghi nhật ký AUD-01 cùng lý do này.</summary>
    public string? Reason { get; set; }
}

