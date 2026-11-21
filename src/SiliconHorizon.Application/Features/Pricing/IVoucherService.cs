using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Pricing.Dto;

namespace SiliconHorizon.Application.Features.Pricing;

/// <summary>
/// Use case voucher (PRC-08): kiểm tra mã trước khi đặt hàng và CRUD mã giảm giá.
/// <list type="bullet">
/// <item><see cref="ValidateAsync"/> gọi <c>dbo.usp_Voucher_Validate</c> (stored procedure — có nghiệp vụ:
/// thời hạn, lượt dùng, nhóm khách, giá trị đơn tối thiểu, điều kiện dùng cùng khuyến mãi).</item>
/// <item>Các thao tác CRUD dùng EF Core trên bảng cấu hình <c>dbo.Vouchers</c>
/// (Database/README.md mục 10 — <c>/api/admin/vouchers</c>).</item>
/// </list>
/// </summary>
public interface IVoucherService
{
    /// <summary>
    /// POST /api/v1/vouchers/validate — kiểm tra mã trước khi đặt.
    /// Khách đã đăng nhập bị ép <c>@CustomerId</c> từ token; khách vãng lai không truyền thì
    /// proc tự dùng nhóm khách mặc định (<c>CustomerGroups.IsDefault = 1</c>).
    /// </summary>
    Task<VoucherValidationDto> ValidateAsync(
        ValidateVoucherRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>GET /api/v1/admin/vouchers — danh sách voucher có phân trang và lọc.</summary>
    Task<PagedResult<VoucherDto>> GetPagedAsync(
        VoucherListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>GET /api/v1/admin/vouchers/{id} — chi tiết voucher; không tồn tại → 404.</summary>
    Task<VoucherDto> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/v1/admin/vouchers — tạo mã giảm giá.
    /// <c>code</c> duy nhất (<c>UQ_Vouchers_Code</c>) → trùng 409; <c>discountValue &gt; 0</c> và
    /// <c>endAt &gt; startAt</c> → sai 400.
    /// </summary>
    Task<VoucherDto> CreateAsync(
        CreateVoucherRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// PUT /api/v1/admin/vouchers/{id} — cập nhật voucher (không đổi <c>code</c>, không sửa <c>usedCount</c>).
    /// </summary>
    Task<VoucherDto> UpdateAsync(
        long id,
        UpdateVoucherRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// PATCH /api/v1/admin/vouchers/{id}/status — khóa/mở mã.
    /// Thay cho xóa cứng vì mã đã có thể phát sinh lượt dùng (<c>VoucherUsages</c>, <c>Orders.VoucherId</c>).
    /// </summary>
    Task SetStatusAsync(
        long id,
        SetVoucherStatusRequest request,
        CancellationToken cancellationToken = default);
}
