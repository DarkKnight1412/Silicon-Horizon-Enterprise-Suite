using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Pricing.Dto;

namespace SiliconHorizon.Application.Features.Pricing;

/// <summary>
/// Use case nhóm khách hàng — CRUD bằng EF Core trên bảng cấu hình <c>dbo.CustomerGroups</c>
/// (Database/README.md mục 10: <c>/api/admin/customer-groups</c>). Nhóm khách quyết định bảng giá và voucher.
/// </summary>
public interface ICustomerGroupService
{
    /// <summary>GET /api/v1/admin/customer-groups — danh sách nhóm khách, có phân trang và lọc.</summary>
    Task<PagedResult<CustomerGroupDto>> GetPagedAsync(
        CustomerGroupListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>GET /api/v1/admin/customer-groups/{id} — chi tiết nhóm khách; không tồn tại → 404.</summary>
    Task<CustomerGroupDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/v1/admin/customer-groups — tạo nhóm khách. <c>code</c> duy nhất (<c>UQ_CustomerGroups_Code</c>)
    /// → trùng trả 409; đặt <c>isDefault = true</c> sẽ bỏ cờ mặc định của nhóm khác.
    /// </summary>
    Task<CustomerGroupDto> CreateAsync(
        CreateCustomerGroupRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// PUT /api/v1/admin/customer-groups/{id} — cập nhật tên và cờ mặc định.
    /// <c>code</c> giữ nguyên để không phá tham chiếu từ khách hàng / bảng giá / voucher / giá bậc thang.
    /// </summary>
    Task<CustomerGroupDto> UpdateAsync(
        int id,
        UpdateCustomerGroupRequest request,
        CancellationToken cancellationToken = default);
}
