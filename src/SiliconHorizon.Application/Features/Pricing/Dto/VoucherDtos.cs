using SiliconHorizon.Domain.Entities.Pricing;
using SiliconHorizon.Domain.Enums;

namespace SiliconHorizon.Application.Features.Pricing.Dto;

/// <summary>
/// Voucher trả ra API — CRUD bằng EF Core trên <c>dbo.Vouchers</c>
/// (Database/README.md mục 10: <c>/api/admin/vouchers</c>, nghiệp vụ PRC-08).
/// </summary>
public class VoucherDto
{
    public long Id { get; init; }

    /// <summary>Mã giảm giá (varchar(40), duy nhất, không phân biệt hoa thường).</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>0 Percent (%), 1 Amount (VND).</summary>
    public DiscountType DiscountType { get; init; }

    public decimal DiscountValue { get; init; }

    public decimal? MinOrderAmount { get; init; }

    public decimal? MaxDiscountAmount { get; init; }

    public DateTime StartAt { get; init; }

    public DateTime EndAt { get; init; }

    public int? MaxUses { get; init; }

    /// <summary>Số lượt đã dùng — chỉ proc đặt hàng tăng giá trị này, API quản trị không sửa.</summary>
    public int UsedCount { get; init; }

    /// <summary><c>null</c> = áp dụng cho mọi nhóm khách.</summary>
    public int? CustomerGroupId { get; init; }

    /// <summary>Có được dùng cùng sản phẩm đang khuyến mãi hay không.</summary>
    public bool IsStackable { get; init; }

    public bool IsActive { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public static VoucherDto FromEntity(Voucher voucher) => new()
    {
        Id = voucher.Id,
        Code = voucher.Code,
        DiscountType = voucher.DiscountType,
        DiscountValue = voucher.DiscountValue,
        MinOrderAmount = voucher.MinOrderAmount,
        MaxDiscountAmount = voucher.MaxDiscountAmount,
        StartAt = voucher.StartAt,
        EndAt = voucher.EndAt,
        MaxUses = voucher.MaxUses,
        UsedCount = voucher.UsedCount,
        CustomerGroupId = voucher.CustomerGroupId,
        IsStackable = voucher.IsStackable,
        IsActive = voucher.IsActive,
        CreatedAt = voucher.CreatedAt,
        UpdatedAt = voucher.UpdatedAt
    };
}

/// <summary>Tham số lọc danh sách voucher — GET /api/v1/admin/vouchers.</summary>
public class VoucherListQuery
{
    public string? Keyword { get; set; }

    public bool? IsActive { get; set; }

    public int? CustomerGroupId { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Yêu cầu tạo voucher — POST /api/v1/admin/vouchers.
/// Ràng buộc: <c>code</c> duy nhất (<c>UQ_Vouchers_Code</c>) → trùng trả 409;
/// <c>discountValue &gt; 0</c> và <c>endAt &gt; startAt</c> (<c>CK_Vouchers_*</c>) → sai trả 400.
/// </summary>
public class CreateVoucherRequest
{
    public string? Code { get; set; }

    public DiscountType DiscountType { get; set; }

    public decimal DiscountValue { get; set; }

    public decimal? MinOrderAmount { get; set; }

    public decimal? MaxDiscountAmount { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public int? MaxUses { get; set; }

    /// <summary><c>null</c> = mọi nhóm khách; nếu truyền thì nhóm phải tồn tại.</summary>
    public int? CustomerGroupId { get; set; }

    public bool IsStackable { get; set; }
}

/// <summary>
/// Yêu cầu cập nhật voucher — PUT /api/v1/admin/vouchers/{id}.
/// Không đổi <c>code</c> (giữ tham chiếu trong <c>VoucherUsages</c>/<c>Orders</c>) và không sửa <c>usedCount</c>.
/// </summary>
public class UpdateVoucherRequest
{
    public DiscountType DiscountType { get; set; }

    public decimal DiscountValue { get; set; }

    public decimal? MinOrderAmount { get; set; }

    public decimal? MaxDiscountAmount { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public int? MaxUses { get; set; }

    public int? CustomerGroupId { get; set; }

    public bool IsStackable { get; set; }
}

/// <summary>
/// Đổi trạng thái voucher — PATCH /api/v1/admin/vouchers/{id}/status.
/// Khóa/mở mã là cách "xóa" đúng quy ước: <b>không xóa cứng</b> mã đã phát sinh lượt dùng.
/// </summary>
public record SetVoucherStatusRequest(bool IsActive);

/// <summary>
/// Yêu cầu kiểm tra mã trước khi đặt — POST /api/v1/vouchers/validate (Database/README.md mục 3.1).
/// </summary>
public class ValidateVoucherRequest
{
    public string? Code { get; set; }

    /// <summary>Tạm tính của các dòng được chọn (VND).</summary>
    public decimal OrderAmount { get; set; }

    /// <summary>Đơn có sản phẩm đang khuyến mãi hay không — dùng cho điều kiện <c>isStackable</c>.</summary>
    public bool HasPromotion { get; set; }

    /// <summary>
    /// Chỉ dùng cho khách <b>vãng lai</b> đã có hồ sơ khách nhưng chưa đăng nhập.
    /// Khách đã đăng nhập luôn bị ép <c>customerId</c> từ token (không tin giá trị client gửi).
    /// </summary>
    public long? CustomerId { get; set; }
}

/// <summary>Kết quả kiểm tra voucher — result set của <c>dbo.usp_Voucher_Validate</c>.</summary>
public class VoucherValidationDto
{
    /// <summary><c>true</c> = mã dùng được.</summary>
    public bool IsValid { get; init; }

    /// <summary>Lý do không dùng được (thông điệp tiếng Việt của <c>fn_VoucherCheck</c>); <c>null</c> khi hợp lệ.</summary>
    public string? Message { get; init; }

    /// <summary>Số tiền được giảm (0 khi mã không hợp lệ).</summary>
    public decimal DiscountAmount { get; init; }

    public static VoucherValidationDto FromRow(VoucherValidationRow row) => new()
    {
        IsValid = row.IsValid,
        Message = row.ErrorMessage,
        DiscountAmount = row.IsValid ? row.DiscountAmount : 0m
    };
}

/// <summary>Một dòng result set của <c>dbo.usp_Voucher_Validate</c> (Dapper ánh xạ trực tiếp).</summary>
public sealed class VoucherValidationRow
{
    public bool IsValid { get; set; }

    public string? ErrorMessage { get; set; }

    public decimal DiscountAmount { get; set; }
}
