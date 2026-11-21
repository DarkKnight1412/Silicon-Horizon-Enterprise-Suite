using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Customers.Dto;

namespace SiliconHorizon.Application.Features.Customers;

/// <summary>
/// Use case khách hàng (Admin) — CRUD bằng EF Core trên <c>dbo.Customers</c>
/// (Database/README.md mục 10: <c>/api/customers</c>).
/// </summary>
public interface ICustomerService
{
    /// <summary>GET /api/v1/customers — danh sách khách hàng, có phân trang và lọc.</summary>
    Task<PagedResult<CustomerDto>> GetPagedAsync(
        CustomerListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>GET /api/v1/customers/{id} — chi tiết khách hàng; không tồn tại → 404.</summary>
    Task<CustomerDto> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/v1/customers — tạo hồ sơ khách hàng (có thể gắn tài khoản đăng nhập đã có).
    /// <c>customerGroupId</c> phải tồn tại; <c>userId</c> (nếu truyền) phải tồn tại và chưa gắn hồ sơ khác → trùng 409.
    /// </summary>
    Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default);

    /// <summary>PUT /api/v1/customers/{id} — cập nhật hồ sơ khách (không đổi tài khoản gắn kèm).</summary>
    Task<CustomerDto> UpdateAsync(
        long id,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// PATCH /api/v1/customers/{id}/status — ngừng/cho giao dịch lại.
    /// Không xóa cứng vì khách đã gắn với đơn hàng, công nợ, địa chỉ.
    /// </summary>
    Task<CustomerDto> SetStatusAsync(
        long id,
        SetCustomerStatusRequest request,
        CancellationToken cancellationToken = default);
}
