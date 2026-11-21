using SiliconHorizon.Domain.Entities.Inventory;

namespace SiliconHorizon.Application.Features.Inventory.Dto;

/// <summary>Nhà cung cấp trả ra API (CRUD bằng EF Core trên bảng cấu hình <c>dbo.Suppliers</c>).</summary>
public class SupplierDto
{
    public long Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? TaxCode { get; init; }

    public string? Phone { get; init; }

    public string? Address { get; init; }

    /// <summary>Số ngày được nợ — proc nhập kho dùng để tính <c>SupplierLedger.DueDate</c>.</summary>
    public int PaymentTermDays { get; init; }

    public bool IsActive { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public static SupplierDto FromEntity(Supplier supplier) => new()
    {
        Id = supplier.Id,
        Code = supplier.Code,
        Name = supplier.Name,
        TaxCode = supplier.TaxCode,
        Phone = supplier.Phone,
        Address = supplier.Address,
        PaymentTermDays = supplier.PaymentTermDays,
        IsActive = supplier.IsActive,
        CreatedAt = supplier.CreatedAt,
        UpdatedAt = supplier.UpdatedAt
    };
}

/// <summary>Tham số lọc danh sách nhà cung cấp — GET /api/v1/suppliers.</summary>
public class SupplierListQuery
{
    /// <summary>Tìm theo tên hoặc mã nhà cung cấp.</summary>
    public string? Keyword { get; set; }

    /// <summary><c>null</c> = lấy cả nhà cung cấp đã ngừng hoạt động.</summary>
    public bool? IsActive { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

/// <summary>Yêu cầu tạo nhà cung cấp — POST /api/v1/suppliers (SUP-01).</summary>
public class CreateSupplierRequest
{
    /// <summary>Mã nhà cung cấp (varchar(30), duy nhất).</summary>
    public string? Code { get; set; }

    public string? Name { get; set; }

    public string? TaxCode { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public int PaymentTermDays { get; set; }
}

/// <summary>Yêu cầu cập nhật nhà cung cấp — PUT /api/v1/suppliers/{id} (không đổi mã để giữ toàn vẹn tham chiếu).</summary>
public class UpdateSupplierRequest
{
    public string? Name { get; set; }

    public string? TaxCode { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public int PaymentTermDays { get; set; }

    /// <summary>Ngừng hợp tác = <c>false</c> (không xóa cứng bản ghi đã phát sinh phiếu nhập/công nợ).</summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Một dòng hàng trả nhà cung cấp. Serial phải đang ở trạng thái <c>Defective</c> (7) tại chi nhánh
/// và phải liệt kê đủ bằng số lượng (SUP-02).
/// </summary>
public sealed class SupplierReturnItemInput
{
    public long ProductId { get; set; }

    public int Quantity { get; set; }

    public IReadOnlyList<string>? Serials { get; set; }
}

/// <summary>Yêu cầu trả hàng nhà cung cấp — POST /api/v1/supplier-returns (usp_SupplierReturn_Create).</summary>
public class CreateSupplierReturnRequest
{
    public long SupplierId { get; set; }

    public IReadOnlyList<SupplierReturnItemInput> Items { get; set; } = Array.Empty<SupplierReturnItemInput>();

    /// <summary>Số tiền ghi giảm công nợ phải trả (>= 0); 0 = không ghi giảm.</summary>
    public decimal CreditAmount { get; set; }
}

/// <summary>Result set của <c>dbo.usp_SupplierReturn_Create</c>.</summary>
public sealed class CreatedSupplierReturnRow
{
    public long SupplierReturnId { get; set; }

    public string ReturnCode { get; set; } = string.Empty;
}

/// <summary>Kết quả trả hàng nhà cung cấp trả ra API.</summary>
public class CreatedSupplierReturnDto
{
    public long SupplierReturnId { get; init; }

    public string ReturnCode { get; init; } = string.Empty;
}

/// <summary>
/// Yêu cầu ghi nhận thanh toán nhà cung cấp — POST /api/v1/suppliers/{id}/payments (usp_SupplierPayment_Record).
/// Proc chặn thanh toán vượt công nợ hiện tại (50006 → 409).
/// </summary>
public class RecordSupplierPaymentRequest
{
    /// <summary>Số tiền thanh toán, VND, phải &gt; 0.</summary>
    public decimal Amount { get; set; }

    /// <summary>Phiếu nhập được thanh toán (tùy chọn) — nếu truyền, proc kiểm tra phiếu thuộc đúng nhà cung cấp.</summary>
    public long? PurchaseReceiptId { get; set; }
}

/// <summary>Kết quả ghi nhận thanh toán — result set của <c>dbo.usp_SupplierPayment_Record</c>.</summary>
public class SupplierPaymentResultDto
{
    /// <summary>Công nợ phải trả còn lại sau khi thanh toán.</summary>
    public decimal BalanceAfter { get; init; }
}

/// <summary>Dòng đầu tiên của <c>dbo.usp_Supplier_GetLedger</c> — công nợ hiện tại.</summary>
public sealed class SupplierBalanceRow
{
    public decimal Balance { get; set; }
}

/// <summary>Một dòng sao kê công nợ — result set 2 của <c>dbo.usp_Supplier_GetLedger</c>.</summary>
public sealed class SupplierLedgerEntryRow
{
    public long Id { get; set; }

    /// <summary>0 Payable (tăng nợ), 1 Payment (giảm nợ), 2 ReturnCredit (giảm nợ do trả hàng).</summary>
    public byte EntryType { get; set; }

    /// <summary>Dương = tăng nợ phải trả; âm = giảm nợ (thanh toán / trả hàng).</summary>
    public decimal Amount { get; set; }

    public DateTime? DueDate { get; set; }

    public long? PurchaseReceiptId { get; set; }

    public long? SupplierReturnId { get; set; }

    public DateTime CreatedAt { get; set; }

    public int TotalCount { get; set; }
}

/// <summary>Dòng sao kê công nợ trả ra API.</summary>
public class SupplierLedgerEntryDto
{
    public long Id { get; init; }

    public byte EntryType { get; init; }

    public string EntryTypeName { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public DateTime? DueDate { get; init; }

    public long? PurchaseReceiptId { get; init; }

    public long? SupplierReturnId { get; init; }

    public DateTime CreatedAt { get; init; }

    public static SupplierLedgerEntryDto FromRow(SupplierLedgerEntryRow row) => new()
    {
        Id = row.Id,
        EntryType = row.EntryType,
        EntryTypeName = InventoryStatusText.SupplierLedgerEntryTypeText(row.EntryType),
        Amount = row.Amount,
        DueDate = row.DueDate,
        PurchaseReceiptId = row.PurchaseReceiptId,
        SupplierReturnId = row.SupplierReturnId,
        CreatedAt = row.CreatedAt
    };
}

/// <summary>Tham số phân trang sao kê công nợ (proc tự cắt tối đa 200).</summary>
public class SupplierLedgerQuery
{
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}

/// <summary>Sao kê công nợ phải trả — GET /api/v1/suppliers/{id}/ledger.</summary>
public class SupplierLedgerDto
{
    public long SupplierId { get; init; }

    /// <summary>Tổng công nợ phải trả hiện tại (dương = còn nợ nhà cung cấp).</summary>
    public decimal Balance { get; init; }

    public IReadOnlyList<SupplierLedgerEntryDto> Entries { get; init; } = Array.Empty<SupplierLedgerEntryDto>();

    public int PageNumber { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }
}

